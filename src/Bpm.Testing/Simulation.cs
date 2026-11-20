using System.Globalization;
using Bpm.Runtime;

namespace Bpm.Testing;

/// <summary>
/// Faults the simulated infrastructure injects (paper §7.4, §8.3). Defaults are a well-behaved network.
/// </summary>
public sealed record FaultProfile
{
    public TimeSpan MinNetworkDelay { get; init; } = TimeSpan.FromMilliseconds(5);
    public TimeSpan MaxNetworkDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    public double DuplicateProbability { get; init; }
    public double DropProbability { get; init; }
    /// <summary>Actual timer lateness; the declared assumption δ must be &gt;= this for the bound to hold.</summary>
    public TimeSpan MaxTimerSkew { get; init; } = TimeSpan.Zero;
    public double AppendFailureProbability { get; init; }
    public int MaxConsecutiveAppendFailures { get; init; } = 3;
    public TimeSpan RedeliveryDelay { get; init; } = TimeSpan.FromMilliseconds(100);
    public double DispatcherCrashProbability { get; init; }
    public double HostRestartProbability { get; init; }
    public TimeSpan DispatchDelay { get; init; } = TimeSpan.FromMilliseconds(1);

    public static FaultProfile None { get; } = new();

    public static FaultProfile Chaos { get; } = new()
    {
        MinNetworkDelay = TimeSpan.FromMilliseconds(1),
        MaxNetworkDelay = TimeSpan.FromSeconds(90),
        DuplicateProbability = 0.25,
        DropProbability = 0.1,
        MaxTimerSkew = TimeSpan.FromMilliseconds(900),
        AppendFailureProbability = 0.1,
        MaxConsecutiveAppendFailures = 2,
        RedeliveryDelay = TimeSpan.FromMilliseconds(100),
        DispatcherCrashProbability = 0.1,
        HostRestartProbability = 0.05,
    };
}

/// <summary>What a fake domain service sees when handling a command.</summary>
public sealed class ServiceContext(Simulation sim, Envelope command, List<(object Reply, TimeSpan Delay)> replies)
{
    public Envelope Command { get; } = command;
    public Random Random => sim.Random;
    public DateTimeOffset Now => sim.Clock.Now;

    public void Reply(object message, TimeSpan? after = null) => replies.Add((message, after ?? TimeSpan.Zero));
}

/// <summary>
/// A deterministic discrete-event simulation of the whole local deployment: event store, process host,
/// outbox dispatcher, timer service, network and fake domain services, on a virtual clock.
/// Identical seeds give identical runs.
/// </summary>
public sealed class Simulation : IMessageBus
{
    private readonly PriorityQueue<(string What, Action Action), (DateTimeOffset, long)> _agenda = new();
    private long _seq;
    private readonly Dictionary<Type, Action<object, ServiceContext>> _services = [];
    private readonly Dictionary<string, List<Envelope>> _serviceReplies = [];
    private readonly Dictionary<string, int> _appendFailures = [];
    private readonly ProcessRegistry _registry;
    private readonly IEventStore _durable;
    private bool _pumpScheduled;

    public Simulation(ProcessRegistry registry, FaultProfile? faults = null, int seed = 1, IEventStore? store = null,
        DateTimeOffset? start = null)
    {
        _registry = registry;
        Faults = faults ?? FaultProfile.None;
        Random = new Random(seed);
        Clock = new VirtualClock(start ?? new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));
        _durable = store ?? new InMemoryEventStore();
        Store = new FaultyEventStore(_durable, ShouldFailAppend);
        Timers = new TimerService();
        Timers.Scheduled += OnTimerScheduled;
        Host = new ProcessHost(registry, Store, Clock);
        Outbox = new OutboxDispatcher(Store, this, Timers)
        {
            BeforeCheckpoint = e =>
            {
                if (Chance(Faults.DispatcherCrashProbability))
                    throw new SimulatedCrashException($"dispatcher crashed before checkpointing position {e.Position}");
            },
        };
    }

    public FaultProfile Faults { get; }
    public Random Random { get; }
    public VirtualClock Clock { get; }
    public FaultyEventStore Store { get; }
    public TimerService Timers { get; }
    public ProcessHost Host { get; private set; }
    public OutboxDispatcher Outbox { get; }
    public List<string> Trace { get; } = [];
    public List<ProcessEnded> Ended { get; } = [];

    /// <summary>
    /// Raised once per instance when it reaches a terminal outcome. The place to wire integrations
    /// between bounded contexts (e.g. a fulfilled order starts a shipment) without coupling their PMs.
    /// </summary>
    public event Action<ProcessEnded, Envelope>? ProcessEnded;
    /// <summary>First (non-duplicate) execution of each command by a domain service: the external side effects.</summary>
    public List<Envelope> ExternalEffects { get; } = [];
    public int HostRestarts { get; private set; }
    public int DuplicatesInjected { get; private set; }
    public int DropsInjected { get; private set; }
    public int DispatcherCrashes { get; private set; }
    public bool Verbose { get; set; }

    /// <summary>Registers a fake domain service for a command type.</summary>
    public Simulation Service<TCommand>(Action<TCommand, ServiceContext> handler)
    {
        _services[typeof(TCommand)] = (c, ctx) => handler((TCommand)c, ctx);
        return this;
    }

    /// <summary>Injects an external input (e.g. a start message or a Kill) at a virtual time.</summary>
    public void Inject(object body, string correlationId, TimeSpan at, string? messageId = null)
    {
        var env = new Envelope(messageId ?? DeterministicId.For($"external:{correlationId}:{_seq}:{body.GetType().Name}"), correlationId, body);
        At(Clock.Now + at, $"inject {env}", () => DeliverToHost(env));
    }

    public void Run(TimeSpan? limit = null)
    {
        var end = Clock.Now + (limit ?? TimeSpan.FromDays(365));
        while (_agenda.TryPeek(out _, out var key) && key.Item1 <= end)
        {
            var item = _agenda.Dequeue();
            Clock.AdvanceTo(key.Item1);
            item.Action();
        }
    }

    // ---- IMessageBus: called by the outbox --------------------------------------------------

    public void Send(Envelope envelope)
    {
        if (envelope.Body is ProcessEnded ended)
        {
            Log($"END     {ended.StreamId}: {ended.Terminal}(\"{ended.Outcome}\")");
            if (Ended.Any(e => e.StreamId == ended.StreamId)) return; // outbox redelivery
            Ended.Add(ended);
            ProcessEnded?.Invoke(ended, envelope);
            return;
        }
        Network(envelope, () => DeliverToService(envelope));
    }

    // ---- infrastructure behaviour ------------------------------------------------------------

    private void Network(Envelope env, Action deliver)
    {
        if (Chance(Faults.DropProbability))
        {
            DropsInjected++;
            Log($"DROP    {env}");
            return;
        }
        At(Clock.Now + NetworkDelay(), $"deliver {env}", deliver);
        if (Chance(Faults.DuplicateProbability))
        {
            DuplicatesInjected++;
            At(Clock.Now + NetworkDelay(), $"deliver dup {env}", deliver);
        }
    }

    private void DeliverToService(Envelope command)
    {
        if (!_services.TryGetValue(command.Body.GetType(), out var service))
        {
            Log($"NO-SVC  {command}");
            return;
        }
        // Idempotent consumer: a duplicate command re-sends the original replies (same ids).
        if (!_serviceReplies.TryGetValue(command.MessageId, out var replies))
        {
            var produced = new List<(object, TimeSpan)>();
            service(command.Body, new ServiceContext(this, command, produced));
            replies = produced.Select((r, i) => new Envelope(
                DeterministicId.For($"{command.MessageId}:reply:{i}"), command.CorrelationId, r.Item1, command.MessageId)).ToList();
            _serviceReplies[command.MessageId] = replies;
            ExternalEffects.Add(command);
            Log($"SVC     {command} -> {(replies.Count == 0 ? "(no reply)" : string.Join(", ", replies))}");
            for (var i = 0; i < replies.Count; i++)
            {
                var reply = replies[i];
                var delay = produced[i].Item2;
                At(Clock.Now + delay, $"reply {reply}", () => Network(reply, () => DeliverToHost(reply)));
            }
        }
        else
        {
            Log($"SVC-DUP {command} (replaying {replies.Count} replies)");
            foreach (var reply in replies) Network(reply, () => DeliverToHost(reply));
        }
    }

    private void DeliverToHost(Envelope env)
    {
        if (Chance(Faults.HostRestartProbability)) RestartHost();
        try
        {
            foreach (var r in Host.Handle(env))
                Log($"HOST    {env.TypeName,-18} {r}");
            SchedulePump();
        }
        catch (AppendFailedException e)
        {
            // At-least-once transport: the message is redelivered.
            Log($"FAIL    {env}: {e.Message}; redelivering");
            At(Clock.Now + Faults.RedeliveryDelay, $"redeliver {env}", () => DeliverToHost(env));
        }
    }

    private void SchedulePump()
    {
        if (_pumpScheduled) return;
        _pumpScheduled = true;
        At(Clock.Now + Faults.DispatchDelay, "outbox pump", () =>
        {
            _pumpScheduled = false;
            try
            {
                Outbox.Pump();
            }
            catch (SimulatedCrashException e)
            {
                DispatcherCrashes++;
                Log($"CRASH   {e.Message}");
                SchedulePump();
            }
        });
    }

    private void OnTimerScheduled(TimerFired fire)
    {
        var skew = TimeSpan.FromTicks((long)(Random.NextDouble() * Faults.MaxTimerSkew.Ticks));
        var at = fire.Deadline + skew;
        if (at < Clock.Now) at = Clock.Now;
        At(at, $"timer {fire.TimerId}", () =>
        {
            // Each timer is delivered at its own (skewed) time; cancelled/superseded timers are gone.
            if (Timers.Take(fire.StreamId, fire.TimerId, fire.Generation) is { } env)
            {
                Log($"TIMER   {env}");
                DeliverToHost(env);
                if (Chance(Faults.DuplicateProbability))
                {
                    DuplicatesInjected++;
                    At(Clock.Now + NetworkDelay(), $"timer dup {env}", () => DeliverToHost(env));
                }
            }
        });
    }

    private void RestartHost()
    {
        HostRestarts++;
        Host = new ProcessHost(_registry, Store, Clock);
        Timers.Rebuild(Host.Instances());
        Log("RESTART host restarted; timer schedule rebuilt from instance streams");
    }

    private bool ShouldFailAppend(string streamId)
    {
        var n = _appendFailures.GetValueOrDefault(streamId);
        if (n < Faults.MaxConsecutiveAppendFailures && Chance(Faults.AppendFailureProbability))
        {
            _appendFailures[streamId] = n + 1;
            return true;
        }
        _appendFailures[streamId] = 0;
        return false;
    }

    private void At(DateTimeOffset t, string what, Action action) => _agenda.Enqueue((what, action), (t, _seq++));

    private bool Chance(double p) => p > 0 && Random.NextDouble() < p;

    private TimeSpan NetworkDelay()
        => Faults.MinNetworkDelay + TimeSpan.FromTicks((long)(Random.NextDouble() * (Faults.MaxNetworkDelay - Faults.MinNetworkDelay).Ticks));

    private void Log(string line)
    {
        var entry = $"{Clock.Now.ToString("MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} {line}";
        Trace.Add(entry);
        if (Verbose) Console.WriteLine(entry);
    }
}
