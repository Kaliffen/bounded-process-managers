using Bpm.Runtime;
using Bpm.Testing;

namespace OrderFulfillment.Tests;

public class OrderScenarioTests
{
    private static Simulation Run(OrderServicesBehaviour behaviour, Action<Simulation>? configure = null)
    {
        var registry = new ProcessRegistry().Register(OrderProcess.Definition);
        var sim = new Simulation(registry).WithOrderServices(behaviour);
        configure?.Invoke(sim);
        sim.Inject(new OrderSubmitted("o1", [new OrderItem("sku", 1)], 42m), "o1", TimeSpan.Zero);
        sim.Run();
        Assert.Empty(InvariantChecker.Check(sim.Store, registry).Violations);
        return sim;
    }

    [Fact]
    public void Happy_path_fulfils()
        => Assert.Equal("Fulfilled", Run(OrderServicesBehaviour.Reliable).Host.Load("OrderFulfillment-o1")!.Outcome);

    [Fact]
    public void One_decline_is_retried_after_the_backoff()
    {
        var sim = Run(OrderServicesBehaviour.Reliable, s =>
        {
            var n = 0;
            s.Service<ChargePayment>((cmd, ctx) => ctx.Reply(++n == 1
                ? new PaymentDeclined(cmd.OrderId, "no")
                : new PaymentCaptured(cmd.OrderId, "p"), TimeSpan.FromSeconds(1)));
        });
        Assert.Equal("Fulfilled", sim.Host.Load("OrderFulfillment-o1")!.Outcome);
        Assert.Equal(2, sim.ExternalEffects.Count(e => e.Body is ChargePayment));
    }

    [Fact]
    public void Payment_without_inventory_is_refunded_at_the_horizon()
    {
        var sim = Run(OrderServicesBehaviour.Reliable with { InventoryNeverReplies = 1 });
        var s = sim.Host.Load("OrderFulfillment-o1")!;
        Assert.Equal("TimeoutAfterPayment_Refunded", s.Outcome);
        Assert.Equal(TimeSpan.FromMinutes(5), s.EndedAt - s.StartedAt);
        Assert.Single(sim.ExternalEffects, e => e.Body is RefundPayment);
    }

    [Fact]
    public void Two_declines_exhaust_the_retry_without_waiting_for_the_horizon()
    {
        var sim = Run(OrderServicesBehaviour.Reliable with { PaymentDeclines = 1 });
        var s = sim.Host.Load("OrderFulfillment-o1")!;
        Assert.Equal("TimeoutBeforePayment", s.Outcome);
        Assert.True(s.EndedAt - s.StartedAt < TimeSpan.FromMinutes(2));
        Assert.Empty(sim.ExternalEffects.Where(e => e.Body is RefundPayment));
    }

    [Fact]
    public void V1_instances_finish_on_v1_after_v2_is_deployed()
    {
        var registry = new ProcessRegistry().Register(OrderProcess.Definition);
        var sim = new Simulation(registry).WithOrderServices(OrderServicesBehaviour.Reliable with { InventoryNeverReplies = 1 });
        sim.Inject(new OrderSubmitted("old", [], 1m), "old", TimeSpan.Zero);
        sim.Run(TimeSpan.FromMinutes(1));
        registry.Register(OrderProcessV2.Definition);
        sim.Inject(new OrderSubmitted("new", [], 1m), "new", TimeSpan.Zero);
        sim.Run();

        Assert.Equal(1, sim.Host.Load("OrderFulfillment-old")!.Version);
        Assert.Equal(2, sim.Host.Load("OrderFulfillment-new")!.Version);
        Assert.Empty(InvariantChecker.Check(sim.Store, registry).Violations);
    }
}

public class OrderPropertyTests
{
    public static TheoryData<int> Seeds() => [.. Enumerable.Range(1, 10)];

    private static readonly Func<Random, object>[] Vocabulary =
    [
        _ => new InventoryReserved("c"),
        r => new PaymentCaptured("c", $"p{r.Next(3)}"),
        _ => new PaymentDeclined("c", "no"),
        _ => new OrderSubmitted("c", [], 1m),
    ];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Compiled_machines_match_source_semantics(int seed)
    {
        foreach (var def in new[] { OrderProcess.Definition, OrderProcessV2.Definition })
        {
            var r = DifferentialTester.Run(def, new OrderSubmitted("c", [new OrderItem("x", 1)], 3m), Vocabulary, seed);
            Assert.True(r.Passed, r.ToString());
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Chaos_preserves_invariants(int seed)
    {
        var registry = new ProcessRegistry().Register(OrderProcess.Definition);
        var sim = new Simulation(registry, FaultProfile.Chaos, seed).WithOrderServices(new OrderServicesBehaviour
        {
            InventoryNeverReplies = 0.15, PaymentDeclines = 0.35, PaymentNeverReplies = 0.05,
            InventoryMaxDelay = TimeSpan.FromMinutes(4), PaymentMaxDelay = TimeSpan.FromMinutes(2),
        });
        var rnd = new Random(seed);
        for (var i = 0; i < 50; i++)
        {
            var at = TimeSpan.FromSeconds(rnd.Next(0, 1800));
            sim.Inject(new OrderSubmitted($"o{i}", [], i), $"o{i}", at);
            if (rnd.NextDouble() < 0.1) sim.Inject(new KillProcess("ops"), $"o{i}", at + TimeSpan.FromSeconds(rnd.Next(0, 400)));
        }
        sim.Run();

        Assert.Empty(InvariantChecker.Check(sim.Store, registry).Violations);
        foreach (var g in sim.ExternalEffects.GroupBy(e => e.CorrelationId))
        {
            Assert.True(g.Count(e => e.Body is ChargePayment) <= 2);
            Assert.True(g.Count(e => e.Body is RefundPayment) <= 1);
        }
    }
}
