using Bpm.Compiler;
using Bpm.Dsl;
using Bpm.Runtime;
using Bpm.Testing;
using Bpm.Tests.Fixtures;
using static Bpm.Dsl.Flow<Bpm.Tests.Fixtures.OrderState>;

namespace Bpm.Tests;

public class EngineTests
{
    private static readonly CompiledProcess P = ProcessCompiler.CompileOrThrow(PaperOrder.V1);
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string Corr = "o1";
    private static readonly string Stream = Engine.StreamIdFor(P.Name, Corr);

    /// <summary>Tiny in-memory harness around the pure engine.</summary>
    private sealed class Instance
    {
        public List<ProcessEvent> Events { get; } = [];
        public InstanceState? State => Engine.Fold(P, Stream, Events);

        public Decision Send(object body, string id, DateTimeOffset at)
        {
            var d = Engine.Decide(P, State, new Envelope(id, Corr, body), at);
            Events.AddRange(d.Events);
            return d;
        }

        public Decision Fire(string timerId, DateTimeOffset at, long? gen = null)
        {
            var t = State!.Timers.GetValueOrDefault(timerId);
            var g = gen ?? t!.Generation;
            return Send(new TimerFired(Stream, timerId, g, t?.Deadline ?? at), TimerFired.IdFor(Stream, timerId, g), at);
        }
    }

    private static Instance Started()
    {
        var i = new Instance();
        i.Send(new OrderSubmitted(Corr, [new OrderItem("a", 1)], 10m), "m0", T0);
        return i;
    }

    [Fact]
    public void Start_pins_version_and_hash_and_schedules_horizon_relative_to_decision_time()
    {
        var i = Started();
        var started = Assert.IsType<InstanceStarted>(i.Events[0]);
        Assert.Equal(P.TableHash, started.TableHash);
        Assert.Equal(P.Analysis.TMax, started.LifetimeBound);
        Assert.Equal(T0 + TimeSpan.FromMinutes(5), i.State!.Timers["InvPay.T"].Deadline);
        var accepted = Assert.IsType<InputAccepted>(i.Events[1]);
        Assert.Equal(["ReserveInventory", "ChargePayment"], accepted.Effects.Where(e => e.Kind == EffectKind.Publish).Select(e => e.CommandType!.Split(',')[0].Split('.')[^1]));
    }

    [Fact]
    public void Duplicate_message_does_not_change_state_or_repeat_effects()
    {
        var i = Started();
        i.Send(new PaymentDeclined(Corr, "no"), "m1", T0.AddSeconds(1));
        var before = i.Events.Count;
        var dup = i.Send(new PaymentDeclined(Corr, "no"), "m1", T0.AddSeconds(2));
        Assert.Equal(DecisionKind.Duplicate, dup.Kind);
        Assert.Equal(before, i.Events.Count);
        Assert.Equal(1, ((OrderState)i.State!.Data!).Declines);
    }

    [Fact]
    public void Consumed_duplicate_cannot_satisfy_a_join_twice()
    {
        var i = Started();
        i.Send(new InventoryReserved(Corr), "inv", T0.AddSeconds(1));
        i.Send(new InventoryReserved(Corr), "inv", T0.AddSeconds(2));
        // A *different* second InventoryReserved is not expected any more: ignored, not double-counted.
        var other = i.Send(new InventoryReserved(Corr), "inv-2", T0.AddSeconds(3));
        Assert.Equal(DecisionKind.Ignored, other.Kind);
        Assert.Equal("InvPay[Inv:got, Pay:try1]", i.State!.Control);
    }

    [Fact]
    public void Stale_timer_generation_is_rejected()
    {
        var i = Started();
        var d = i.Fire("InvPay.T", T0.AddMinutes(5), gen: 99);
        Assert.Equal(DecisionKind.Ignored, d.Kind);
        Assert.Contains("stale timer", d.Reason);
        Assert.False(i.State!.IsTerminal);
    }

    [Fact]
    public void Cancelled_backoff_timer_is_stale_after_the_join_resolves()
    {
        var i = Started();
        i.Send(new PaymentDeclined(Corr, "no"), "d1", T0.AddSeconds(1));
        var backoffGen = i.State!.Timers["InvPay.Pay.B"].Generation;
        i.Fire("InvPay.T", T0.AddMinutes(5)); // horizon wins, cancels backoff
        Assert.True(i.State!.IsTerminal);
        var late = i.Send(new TimerFired(Stream, "InvPay.Pay.B", backoffGen, T0), TimerFired.IdFor(Stream, "InvPay.Pay.B", backoffGen), T0.AddMinutes(6));
        Assert.Equal(DecisionKind.Ignored, late.Kind);
    }

    [Fact]
    public void Late_reply_after_terminal_is_audited_and_does_not_reopen()
    {
        var i = Started();
        i.Fire("InvPay.T", T0.AddMinutes(5));
        var outcome = i.State!.Outcome;
        var late = i.Send(new PaymentCaptured(Corr, "p"), "late", T0.AddMinutes(7));
        Assert.Equal(DecisionKind.Ignored, late.Kind);
        Assert.IsType<InputIgnored>(i.Events[^1]);
        Assert.Equal(outcome, i.State!.Outcome);
        Assert.False(((OrderState)i.State!.Data!).HasPayment);
    }

    [Fact]
    public void Kill_records_terminal_cancellation_and_cancels_timers()
    {
        var i = Started();
        i.Send(new KillProcess("ops"), "kill", T0.AddMinutes(1));
        var s = i.State!;
        Assert.Equal(TerminalKind.Aborted, s.Terminal);
        Assert.Empty(s.Timers);
        Assert.Contains(((InputAccepted)i.Events[^1]).Effects, e => e is { Kind: EffectKind.CancelTimer, TimerId: "InvPay.T" });
    }

    [Fact]
    public void Concurrent_kill_and_completion_first_append_wins()
    {
        var i = Started();
        i.Send(new InventoryReserved(Corr), "inv", T0.AddSeconds(1));
        i.Send(new PaymentCaptured(Corr, "p"), "pay", T0.AddSeconds(2));
        var kill = i.Send(new KillProcess("ops"), "kill", T0.AddSeconds(2));
        Assert.Equal(DecisionKind.Ignored, kill.Kind);
        Assert.Equal("Fulfilled", i.State!.Outcome);
    }

    [Fact]
    public void Replay_reproduces_every_decision()
    {
        var i = Started();
        i.Send(new PaymentDeclined(Corr, "no"), "d1", T0.AddSeconds(1));
        i.Fire("InvPay.Pay.B", T0.AddSeconds(31));
        i.Send(new PaymentCaptured(Corr, "p"), "p1", T0.AddSeconds(40));
        i.Send(new InventoryReserved(Corr), "inv", T0.AddSeconds(50));
        Assert.Equal("Fulfilled", i.State!.Outcome);
        Assert.Empty(Engine.VerifyReplay(P, Stream, i.Events));
        Assert.Equal(Engine.Fold(P, Stream, i.Events), Engine.Fold(P, Stream, i.Events), new StateComparer());
    }

    [Fact]
    public void Replay_detects_hidden_nondeterminism_in_callbacks()
    {
        var flips = 0;
        var def = ProcessManager.Define<OrderState>("Flaky")
            .StartedBy<OrderSubmitted>(m => new OrderState(m.OrderId, m.Items, m.Total))
            .Then(Branch(_ => flips++ % 2 == 0, Complete("heads"), Complete("tails")));
        var p = ProcessCompiler.CompileOrThrow(def);
        var stream = Engine.StreamIdFor("Flaky", Corr);
        var d = Engine.Decide(p, null, new Envelope("m0", Corr, new OrderSubmitted(Corr, [], 1m)), T0);
        Assert.NotEmpty(Engine.VerifyReplay(p, stream, d.Events));
    }

    [Fact]
    public void Effect_ids_are_deterministic()
    {
        var a = Started();
        var b = Started();
        Assert.Equal(
            ((InputAccepted)a.Events[1]).Effects.Select(e => e.EffectId),
            ((InputAccepted)b.Events[1]).Effects.Select(e => e.EffectId));
    }

    private sealed class StateComparer : IEqualityComparer<InstanceState?>
    {
        public bool Equals(InstanceState? x, InstanceState? y)
            => x!.Control == y!.Control && Json.Serialize(x.Data!) == Json.Serialize(y.Data!) && x.Timers.Count == y.Timers.Count
               && x.Consumed.SetEquals(y.Consumed) && x.Outcome == y.Outcome && x.StreamVersion == y.StreamVersion;

        public int GetHashCode(InstanceState? obj) => 0;
    }
}

public class VersioningTests
{
    [Fact]
    public void Instances_keep_their_pinned_version_after_a_new_version_is_deployed()
    {
        var registry = new ProcessRegistry().Register(PaperOrder.V1);
        var sim = new Simulation(registry).WithFakeServices(ServiceBehaviour.Reliable with { InventoryNeverReplies = 1 });
        sim.Inject(new OrderSubmitted("old", [], 1m), "old", TimeSpan.Zero);
        sim.Run(TimeSpan.FromMinutes(1));
        registry.Register(PaperOrder.V2);
        sim.Inject(new OrderSubmitted("new", [], 1m), "new", TimeSpan.Zero);
        sim.Run();

        var old = sim.Host.Load("OrderFulfillment-old")!;
        var @new = sim.Host.Load("OrderFulfillment-new")!;
        Assert.Equal(1, old.Version);
        Assert.Equal(2, @new.Version);
        Assert.Equal(TimeSpan.FromMinutes(5), old.EndedAt - old.StartedAt);  // v1 horizon
        Assert.Equal(TimeSpan.FromMinutes(6), @new.EndedAt - @new.StartedAt); // v2 horizon
        Assert.Empty(InvariantChecker.Check(sim.Store, registry).Violations);
    }

    [Fact]
    public void Changing_a_table_without_bumping_the_version_is_refused()
    {
        var registry = new ProcessRegistry().Register(PaperOrder.V1);
        var sim = new Simulation(registry).WithFakeServices(ServiceBehaviour.Reliable with { InventoryNeverReplies = 1 });
        sim.Inject(new OrderSubmitted("x", [], 1m), "x", TimeSpan.Zero);
        sim.Run(TimeSpan.FromSeconds(30));

        // Same name and version, different behaviour.
        var tampered = ProcessManager.Define<OrderState>("OrderFulfillment", version: 1)
            .StartedBy<OrderSubmitted>(m => new OrderState(m.OrderId, m.Items, m.Total))
            .Then(Complete("different"));
        var registry2 = new ProcessRegistry().Register(tampered);
        var host = new ProcessHost(registry2, sim.Store, sim.Clock);
        var e = Assert.Throws<InvalidOperationException>(() => host.Load("OrderFulfillment-x"));
        Assert.Contains("without a version bump", e.Message);
    }

    [Fact]
    public void Drain_horizon_is_start_plus_bound()
    {
        var registry = new ProcessRegistry().Register(PaperOrder.V1);
        var sim = new Simulation(registry).WithFakeServices(ServiceBehaviour.Reliable with { InventoryNeverReplies = 1 });
        var start = sim.Clock.Now;
        sim.Inject(new OrderSubmitted("a", [], 1m), "a", TimeSpan.Zero);
        sim.Inject(new OrderSubmitted("b", [], 1m), "b", TimeSpan.FromSeconds(30));
        sim.Run(TimeSpan.FromSeconds(45));
        var plan = Assert.Single(DrainPlanner.Plan(sim.Host, registry));
        Assert.Equal(2, plan.ActiveInstances);
        var bound = ProcessCompiler.CompileOrThrow(PaperOrder.V1).Analysis.TMax;
        Assert.Equal(start + TimeSpan.FromSeconds(30) + bound, plan.DrainedBy);
    }
}

public class InfrastructureTests
{
    [Fact]
    public void File_store_survives_restart_and_replays()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bpm-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var registry = new ProcessRegistry().Register(PaperOrder.V1);
            var sim = new Simulation(registry, store: new FileEventStore(dir)).WithFakeServices();
            sim.Inject(new OrderSubmitted("f1", [new OrderItem("x", 1)], 5m), "f1", TimeSpan.Zero);
            sim.Run();

            var reopened = new FileEventStore(dir);
            var events = reopened.Read("OrderFulfillment-f1");
            Assert.Equal(sim.Store.Read("OrderFulfillment-f1").Count, events.Count);
            var p = registry.Latest("OrderFulfillment");
            Assert.Equal("Fulfilled", Engine.Fold(p, "OrderFulfillment-f1", events)!.Outcome);
            Assert.Empty(Engine.VerifyReplay(p, "OrderFulfillment-f1", events));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Timer_schedule_is_rebuilt_from_instance_streams()
    {
        var registry = new ProcessRegistry().Register(PaperOrder.V1);
        var sim = new Simulation(registry).WithFakeServices(ServiceBehaviour.Reliable with { InventoryNeverReplies = 1, PaymentNeverReplies = 1 });
        var start = sim.Clock.Now;
        sim.Inject(new OrderSubmitted("t1", [], 1m), "t1", TimeSpan.Zero);
        sim.Run(TimeSpan.FromSeconds(10));

        var fresh = new TimerService();
        fresh.Rebuild(sim.Host.Instances());
        Assert.Equal(1, fresh.Count);
        Assert.Equal(start + TimeSpan.FromMinutes(5), fresh.NextDeadline);
    }

    [Fact]
    public void Outbox_redelivers_after_a_crash_and_consumers_deduplicate()
    {
        var registry = new ProcessRegistry().Register(PaperOrder.V1);
        var store = new InMemoryEventStore();
        var host = new ProcessHost(registry, store, new VirtualClock(DateTimeOffset.UnixEpoch));
        var sent = new List<Envelope>();
        var bus = new ListBus(sent);
        var outbox = new OutboxDispatcher(store, bus, new TimerService());
        host.Handle(new Envelope("m0", "c1", new OrderSubmitted("c1", [], 1m)));

        outbox.BeforeCheckpoint = e => throw new SimulatedCrashException($"crash at {e.Position}");
        Assert.Throws<SimulatedCrashException>(() => outbox.Pump());
        outbox.BeforeCheckpoint = null;
        outbox.Pump();

        Assert.Equal(4, sent.Count); // both commands sent twice
        Assert.Equal(2, sent.Select(e => e.MessageId).Distinct().Count()); // ... with identical ids
    }

    private sealed class ListBus(List<Envelope> sent) : IMessageBus
    {
        public void Send(Envelope envelope) => sent.Add(envelope);
    }
}
