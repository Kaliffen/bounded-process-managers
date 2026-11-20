using Bpm.Compiler;
using Bpm.Dsl;

namespace Bpm.Runtime;

public interface IClock { DateTimeOffset Now { get; } }

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
}

public sealed class VirtualClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset Now { get; private set; } = start;

    public void AdvanceTo(DateTimeOffset t)
    {
        if (t < Now) throw new InvalidOperationException("virtual time cannot go backwards");
        Now = t;
    }
}

/// <summary>
/// Holds every deployed version of every definition. New instances start on the latest version;
/// existing instances keep running the version (and table hash) they pinned at start (paper §7.1).
/// </summary>
public sealed class ProcessRegistry
{
    private readonly Dictionary<(string, int), CompiledProcess> _versions = [];

    public ProcessRegistry Register(CompiledProcess process)
    {
        _versions[(process.Name, process.Version)] = process;
        return this;
    }

    public ProcessRegistry Register(ProcessDefinition definition) => Register(ProcessCompiler.CompileOrThrow(definition));

    public CompiledProcess Latest(string name) => _versions.Values.Where(p => p.Name == name).MaxBy(p => p.Version)
                                                  ?? throw new KeyNotFoundException(name);

    public CompiledProcess? Get(string name, int version) => _versions.GetValueOrDefault((name, version));

    public IEnumerable<string> Names => _versions.Keys.Select(k => k.Item1).Distinct();

    public IEnumerable<CompiledProcess> All => _versions.Values;

    /// <summary>Resolve the compiled table an existing stream is pinned to; refuses silent drift.</summary>
    public CompiledProcess Pinned(InstanceStarted started)
    {
        var p = Get(started.Definition, started.Version)
                ?? throw new InvalidOperationException($"{started.Definition} v{started.Version} is no longer deployed; instance needs explicit handling");
        if (p.TableHash != started.TableHash)
            throw new InvalidOperationException(
                $"{started.Definition} v{started.Version} changed its transition table ({started.TableHash} -> {p.TableHash}) without a version bump");
        return p;
    }
}

public sealed record HandleResult(string? StreamId, DecisionKind Kind, string? From, string? To, string? Reason, IReadOnlyList<Effect> Effects)
{
    public override string ToString() => Kind switch
    {
        DecisionKind.Accepted => $"{StreamId}: {From} -> {To}{(Effects.Count > 0 ? " / " + string.Join(", ", Effects) : "")}",
        _ => $"{StreamId ?? "-"}: {Kind} ({Reason})",
    };
}

/// <summary>
/// Message handler for process instances: route by correlation, fold the stream, decide, append.
/// Holds no in-memory instance state, so a crash/restart is just "construct a new host".
/// </summary>
public sealed class ProcessHost(ProcessRegistry registry, IEventStore store, IClock clock)
{
    public int MaxConcurrencyRetries { get; init; } = 5;
    public List<HandleResult> Orphans { get; } = [];

    public IReadOnlyList<HandleResult> Handle(Envelope envelope)
    {
        var results = new List<HandleResult>();
        if (envelope.Body is TimerFired tf)
        {
            results.Add(HandleOn(tf.StreamId, null, envelope));
            return results;
        }

        var typeName = envelope.Body.GetType().FullName!;
        foreach (var name in registry.Names)
        {
            // Route only to definitions that react to this message type (any deployed version), so
            // unrelated traffic sharing a correlation id does not pollute other instances' streams.
            if (envelope.Body is not KillProcess && !registry.All.Any(p => p.Name == name && p.MessageTypes.ContainsKey(typeName)))
                continue;
            var streamId = Engine.StreamIdFor(name, envelope.CorrelationId);
            var latest = registry.Latest(name);
            var exists = store.Read(streamId).Count > 0;
            if (exists || envelope.Body.GetType() == latest.StartMessageType)
                results.Add(HandleOn(streamId, latest, envelope));
        }
        if (results.Count == 0)
        {
            var orphan = new HandleResult(null, DecisionKind.NotForThisProcess, null, null, $"no instance for {envelope}", []);
            Orphans.Add(orphan);
            results.Add(orphan);
        }
        return results;
    }

    private HandleResult HandleOn(string streamId, CompiledProcess? forNew, Envelope envelope)
    {
        for (var attempt = 0; ; attempt++)
        {
            var events = store.Read(streamId);
            CompiledProcess process;
            InstanceState? state = null;
            if (events.Count == 0)
            {
                if (forNew is null) return new HandleResult(streamId, DecisionKind.NotForThisProcess, null, null, "unknown stream", []);
                process = forNew;
            }
            else
            {
                process = registry.Pinned((InstanceStarted)events[0]);
                state = Engine.Fold(process, streamId, events);
            }

            var decision = Engine.Decide(process, state, envelope, clock.Now);
            if (decision.Events.Count == 0)
                return new HandleResult(streamId, decision.Kind, state?.Control, state?.Control, decision.Reason, []);
            try
            {
                store.Append(streamId, events.Count, decision.Events);
            }
            catch (ConcurrencyException) when (attempt < MaxConcurrencyRetries)
            {
                continue;
            }
            var accepted = decision.Events.OfType<InputAccepted>().SingleOrDefault();
            return new HandleResult(streamId, decision.Kind, accepted?.From ?? state?.Control, accepted?.To ?? state?.Control,
                decision.Reason, accepted?.Effects ?? []);
        }
    }

    public InstanceState? Load(string streamId)
    {
        var events = store.Read(streamId);
        return events.Count == 0 ? null : Engine.Fold(registry.Pinned((InstanceStarted)events[0]), streamId, events);
    }

    public IEnumerable<InstanceState> Instances()
        => store.Streams.Select(Load).Where(s => s is not null)!;
}

/// <summary>
/// Timer service. Its schedule is derived state: it can always be rebuilt from the active timer
/// sets (Θ) of non-terminal instances, so it needs no durable storage of its own.
/// </summary>
public sealed class TimerService
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Stream, string Timer), (TimerFired Fire, string Correlation)> _pending = [];

    /// <summary>Raised (outside the lock) when a timer is newly scheduled. Used by schedulers that wake per timer.</summary>
    public event Action<TimerFired>? Scheduled;

    public void Schedule(string streamId, string correlationId, string timerId, long generation, DateTimeOffset deadline)
    {
        var fire = new TimerFired(streamId, timerId, generation, deadline);
        lock (_gate)
        {
            var key = (streamId, timerId);
            if (_pending.TryGetValue(key, out var existing) && existing.Fire.Generation >= generation) return; // idempotent
            _pending[key] = (fire, correlationId);
        }
        Scheduled?.Invoke(fire);
    }

    public void Cancel(string streamId, string timerId, long generation)
    {
        lock (_gate)
        {
            if (_pending.TryGetValue((streamId, timerId), out var p) && p.Fire.Generation <= generation)
                _pending.Remove((streamId, timerId));
        }
    }

    /// <summary>Removes and returns timers due at <paramref name="now"/> as messages.</summary>
    public IReadOnlyList<Envelope> TakeDue(DateTimeOffset now)
    {
        lock (_gate)
        {
            var due = _pending.Where(kv => kv.Value.Fire.Deadline <= now).OrderBy(kv => kv.Value.Fire.Deadline).ToList();
            foreach (var kv in due) _pending.Remove(kv.Key);
            return due.Select(kv => ToEnvelope(kv.Value.Fire, kv.Value.Correlation)).ToList();
        }
    }

    /// <summary>Removes and returns one specific timer if it is still pending.</summary>
    public Envelope? Take(string streamId, string timerId, long generation)
    {
        lock (_gate)
        {
            if (!_pending.TryGetValue((streamId, timerId), out var p) || p.Fire.Generation != generation) return null;
            _pending.Remove((streamId, timerId));
            return ToEnvelope(p.Fire, p.Correlation);
        }
    }

    public DateTimeOffset? NextDeadline
    {
        get { lock (_gate) return _pending.Count == 0 ? null : _pending.Values.Min(p => p.Fire.Deadline); }
    }

    public int Count
    {
        get { lock (_gate) return _pending.Count; }
    }

    public void Rebuild(IEnumerable<InstanceState> instances)
    {
        lock (_gate) _pending.Clear();
        foreach (var i in instances.Where(i => !i.IsTerminal))
        foreach (var (timerId, t) in i.Timers)
            Schedule(i.StreamId, i.CorrelationId, timerId, t.Generation, t.Deadline);
    }

    private static Envelope ToEnvelope(TimerFired fire, string correlation)
        => new(TimerFired.IdFor(fire.StreamId, fire.TimerId, fire.Generation), correlation, fire);
}

public interface IMessageBus
{
    void Send(Envelope envelope);
}

/// <summary>
/// Tails the global log and dispatches newly decided effects. Replay never dispatches: only this
/// component does, and it is idempotent because every effect has a deterministic id that downstream
/// consumers deduplicate on. A crash between send and checkpoint causes redelivery, never loss.
/// </summary>
public sealed class OutboxDispatcher(IEventStore store, IMessageBus bus, TimerService timers)
{
    private readonly Dictionary<string, InstanceStarted> _started = [];

    public long Checkpoint { get; private set; }
    public int Dispatched { get; private set; }

    private InstanceStarted Started(string streamId)
        => _started.TryGetValue(streamId, out var s) ? s : _started[streamId] = (InstanceStarted)store.Read(streamId)[0];

    /// <summary>
    /// Invoked after an event's effects were sent and before the checkpoint advances. Throwing here
    /// models a crash at the worst moment: the effects are re-sent on the next pump.
    /// </summary>
    public Action<StoredEvent>? BeforeCheckpoint { get; set; }

    /// <summary>Resume position, e.g. loaded from a persisted checkpoint at startup.</summary>
    public void ResumeFrom(long position) => Checkpoint = position;

    public int Pump()
    {
        var n = 0;
        foreach (var stored in store.ReadAll(Checkpoint))
        {
            if (stored.Event is InputAccepted a)
            {
                var started = Started(stored.StreamId);
                var corr = started.CorrelationId;
                foreach (var e in a.Effects)
                {
                    switch (e.Kind)
                    {
                        case EffectKind.Publish:
                            bus.Send(new Envelope(e.EffectId, corr, Json.Deserialize(e.Payload!, Json.ResolveType(e.CommandType!)), a.MessageId));
                            break;
                        case EffectKind.ScheduleTimer:
                            timers.Schedule(stored.StreamId, corr, e.TimerId!, e.Generation!.Value, e.Deadline!.Value);
                            break;
                        case EffectKind.CancelTimer:
                            timers.Cancel(stored.StreamId, e.TimerId!, e.Generation!.Value);
                            break;
                        case EffectKind.Outcome:
                            bus.Send(new Envelope(e.EffectId, corr,
                                new ProcessEnded(stored.StreamId, started.Definition, started.Version, e.Terminal.ToString()!, e.Outcome!), a.MessageId));
                            break;
                    }
                    Dispatched++;
                    n++;
                }
                BeforeCheckpoint?.Invoke(stored);
            }
            Checkpoint = stored.Position + 1;
        }
        return n;
    }
}

/// <summary>Operational use of the bound (paper §7.1): when can an old version be retired?</summary>
public static class DrainPlanner
{
    public sealed record VersionDrain(string Definition, int Version, int ActiveInstances, DateTimeOffset? DrainedBy);

    public static IReadOnlyList<VersionDrain> Plan(ProcessHost host, ProcessRegistry registry)
        => host.Instances()
            .GroupBy(i => (i.Definition, i.Version))
            .Select(g =>
            {
                var active = g.Where(i => !i.IsTerminal).ToList();
                return new VersionDrain(g.Key.Definition, g.Key.Version, active.Count,
                    active.Count == 0 ? null : active.Max(i => i.StartedAt + i.LifetimeBound));
            })
            .OrderBy(d => d.Definition).ThenBy(d => d.Version)
            .ToList();
}
