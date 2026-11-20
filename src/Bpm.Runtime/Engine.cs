using System.Collections.Immutable;
using Bpm.Compiler;
using Bpm.Dsl;

namespace Bpm.Runtime;

public sealed record ActiveTimer(long Generation, DateTimeOffset Deadline);

/// <summary>
/// The folded configuration of one instance: control state, domain state S, active generation-tagged
/// timers Θ, and the persisted consumption (dedup) set. Everything is derived from the stream.
/// </summary>
public sealed record InstanceState
{
    public required string StreamId { get; init; }
    public required string Definition { get; init; }
    public required int Version { get; init; }
    public required string TableHash { get; init; }
    public required string CorrelationId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required TimeSpan LifetimeBound { get; init; }
    public long StreamVersion { get; init; }
    public string Control { get; init; } = "Init";
    public object? Data { get; init; }
    public ImmutableDictionary<string, ActiveTimer> Timers { get; init; } = ImmutableDictionary<string, ActiveTimer>.Empty;
    public long NextGeneration { get; init; } = 1;
    public ImmutableHashSet<string> Consumed { get; init; } = [];
    public TerminalKind? Terminal { get; init; }
    public string? Outcome { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public bool IsTerminal => Terminal is not null;
}

public enum DecisionKind { Accepted, Ignored, Duplicate, NotForThisProcess }

public sealed record Decision(DecisionKind Kind, IReadOnlyList<ProcessEvent> Events, string? Reason = null)
{
    public static Decision Duplicate { get; } = new(DecisionKind.Duplicate, [], "already consumed");
}

/// <summary>
/// The pure core: <c>decide : (State, Input) -> Events</c> and <c>evolve : (State, Event) -> State</c>,
/// both driven by the compiled transition table. No I/O happens here; time is passed in, effects are
/// only described. Replaying a stream through <see cref="Fold"/> never re-executes effects.
/// </summary>
public static class Engine
{
    public static string StreamIdFor(string definition, string correlationId) => $"{definition}-{correlationId}";

    public static InputSymbol? SymbolFor(object body) => body switch
    {
        TimerFired t => InputSymbol.Timer(t.TimerId),
        KillProcess => InputSymbol.Kill,
        _ => InputSymbol.Message(body.GetType()),
    };

    public static Decision Decide(CompiledProcess process, InstanceState? state, Envelope input, DateTimeOffset now)
    {
        var events = new List<ProcessEvent>();
        if (state is null)
        {
            if (input.Body.GetType() != process.StartMessageType)
                return new Decision(DecisionKind.NotForThisProcess, [], "no instance and not a start message");
            var started = new InstanceStarted
            {
                RecordedAt = now,
                Definition = process.Name,
                Version = process.Version,
                TableHash = process.TableHash,
                CorrelationId = input.CorrelationId,
                LifetimeBound = process.Analysis.TMax,
            };
            events.Add(started);
            state = Apply(process, null, started, StreamIdFor(process.Name, input.CorrelationId));
        }

        if (state.Consumed.Contains(input.MessageId))
            return Decision.Duplicate;

        Decision Ignore(string reason) => new(DecisionKind.Ignored,
        [
            .. events,
            new InputIgnored { RecordedAt = now, MessageId = input.MessageId, MessageType = input.TypeName, Reason = reason },
        ], reason);

        if (state.IsTerminal)
            return Ignore($"instance is terminal ({state.Terminal}: {state.Outcome}); late input routed to audit");

        var symbol = SymbolFor(input.Body)!;
        if (input.Body is TimerFired tf)
        {
            if (!state.Timers.TryGetValue(tf.TimerId, out var active) || active.Generation != tf.Generation)
                return Ignore($"stale timer {tf.TimerId} gen={tf.Generation} (active gen={(state.Timers.TryGetValue(tf.TimerId, out var a) ? a.Generation : 0)})");
        }

        var rows = process.RowsFor(state.Control, symbol).ToList();
        if (rows.Count == 0)
            return Ignore($"{symbol.Display} is not accepted in state {state.Control}");

        // evolve first: guards and command factories see the state including this input.
        var data = Evolve(process, state.Data, input.Body, state.Control == process.InitState);
        var row = SelectRow(process, rows, data);

        // `state` already includes InstanceStarted when it was created above, so this is the
        // version the InputAccepted event will have in the stream.
        var version = state.StreamVersion + 1;
        var effects = new List<Effect>();
        var nextGen = state.NextGeneration;
        var timers = state.Timers;
        for (var i = 0; i < row.Effects.Count; i++)
        {
            var e = row.Effects[i];
            var id = DeterministicId.For($"{state.StreamId}:{version}:{i}");
            switch (e.Kind)
            {
                case EffectKind.Publish:
                    foreach (var (cmd, j) in process.Publishers[e.Ref!].Factory(data!).Select((c, j) => (c, j)))
                        effects.Add(new Effect
                        {
                            EffectId = j == 0 ? id : DeterministicId.For($"{state.StreamId}:{version}:{i}:{j}"),
                            Kind = EffectKind.Publish,
                            Label = e.Label,
                            CommandType = Json.TypeName(cmd.GetType()),
                            Payload = Json.Serialize(cmd),
                        });
                    break;
                case EffectKind.ScheduleTimer:
                    var gen = nextGen++;
                    // Deadline is relative to the recorded decision time, not to dispatch time.
                    effects.Add(new Effect { EffectId = id, Kind = e.Kind, TimerId = e.Ref, Generation = gen, Deadline = now + e.Duration!.Value });
                    break;
                case EffectKind.CancelTimer:
                    timers.TryGetValue(e.Ref!, out var t);
                    effects.Add(new Effect { EffectId = id, Kind = e.Kind, TimerId = e.Ref, Generation = t?.Generation ?? 0 });
                    break;
                case EffectKind.Outcome:
                    effects.Add(new Effect { EffectId = id, Kind = e.Kind, Terminal = e.Terminal, Outcome = e.Outcome });
                    break;
            }
        }

        events.Add(new InputAccepted
        {
            RecordedAt = now,
            MessageId = input.MessageId,
            Kind = symbol.Kind,
            MessageType = Json.TypeName(input.Body.GetType()),
            Payload = Json.Serialize(input.Body),
            From = state.Control,
            To = row.To,
            Row = row.Index,
            Effects = effects,
        });
        return new Decision(DecisionKind.Accepted, events);
    }

    private static object? Evolve(CompiledProcess process, object? data, object body, bool isStart)
    {
        if (isStart) return process.Definition.Init!(body);
        return process.Definition.Evolvers.TryGetValue(body.GetType(), out var evolve) ? evolve(data!, body) : data;
    }

    private static Transition SelectRow(CompiledProcess process, List<Transition> rows, object? data)
    {
        var values = new Dictionary<string, bool>();
        bool Eval(string branchId) => values.TryGetValue(branchId, out var v) ? v : values[branchId] = process.Branches[branchId].Predicate(data!);
        var matching = rows.Where(r => r.Guards.All(g => Eval(g.BranchId) == g.Value)).ToList();
        return matching.Count == 1
            ? matching[0]
            : throw new InvalidOperationException($"determinism violated: {matching.Count} rows match in {rows[0].From} on {rows[0].Input}");
    }

    /// <summary>evolve over recorded events. Pure; never dispatches effects.</summary>
    public static InstanceState Apply(CompiledProcess process, InstanceState? state, ProcessEvent e, string streamId)
    {
        switch (e)
        {
            case InstanceStarted s:
                return new InstanceState
                {
                    StreamId = streamId,
                    Definition = s.Definition,
                    Version = s.Version,
                    TableHash = s.TableHash,
                    CorrelationId = s.CorrelationId,
                    StartedAt = s.RecordedAt,
                    LifetimeBound = s.LifetimeBound,
                    StreamVersion = 1,
                };
            case InputIgnored ig:
                return state! with { StreamVersion = state.StreamVersion + 1, Consumed = state.Consumed.Add(ig.MessageId) };
            case InputAccepted a:
            {
                var st = state!;
                var data = st.Data;
                if (a.Kind == InputKind.Message)
                {
                    var body = Json.Deserialize(a.Payload, Json.ResolveType(a.MessageType));
                    data = Evolve(process, data, body, a.From == process.InitState);
                }
                var timers = st.Timers;
                if (a.Kind == InputKind.Timer)
                {
                    var fired = (TimerFired)Json.Deserialize(a.Payload, typeof(TimerFired));
                    timers = timers.Remove(fired.TimerId);
                }
                var nextGen = st.NextGeneration;
                TerminalKind? terminal = null;
                string? outcome = null;
                foreach (var eff in a.Effects)
                {
                    switch (eff.Kind)
                    {
                        case EffectKind.ScheduleTimer:
                            timers = timers.SetItem(eff.TimerId!, new ActiveTimer(eff.Generation!.Value, eff.Deadline!.Value));
                            nextGen = Math.Max(nextGen, eff.Generation!.Value + 1);
                            break;
                        case EffectKind.CancelTimer:
                            timers = timers.Remove(eff.TimerId!);
                            break;
                        case EffectKind.Outcome:
                            terminal = eff.Terminal;
                            outcome = eff.Outcome;
                            break;
                    }
                }
                return st with
                {
                    StreamVersion = st.StreamVersion + 1,
                    Control = a.To,
                    Data = data,
                    Timers = terminal is null ? timers : ImmutableDictionary<string, ActiveTimer>.Empty,
                    NextGeneration = nextGen,
                    Consumed = st.Consumed.Add(a.MessageId),
                    Terminal = terminal ?? st.Terminal,
                    Outcome = outcome ?? st.Outcome,
                    EndedAt = terminal is null ? st.EndedAt : a.RecordedAt,
                };
            }
            default:
                throw new InvalidOperationException($"unknown event {e.GetType().Name}");
        }
    }

    public static InstanceState? Fold(CompiledProcess process, string streamId, IEnumerable<ProcessEvent> events)
        => events.Aggregate((InstanceState?)null, (s, e) => Apply(process, s, e, streamId));

    /// <summary>
    /// Target property 3 check: re-derive every recorded decision from (state, input) and require it to
    /// be identical to what was recorded. Detects hidden nondeterminism in evolve/predicates/factories
    /// and transition-table drift under a pinned version.
    /// </summary>
    public static IReadOnlyList<string> VerifyReplay(CompiledProcess process, string streamId, IReadOnlyList<ProcessEvent> events)
    {
        var problems = new List<string>();
        InstanceState? state = null;
        foreach (var e in events)
        {
            if (e is InputAccepted a && state is not null)
            {
                var body = Json.Deserialize(a.Payload, Json.ResolveType(a.MessageType));
                var replayed = Decide(process, state, new Envelope(a.MessageId, state.CorrelationId, body), a.RecordedAt);
                var again = replayed.Events.OfType<InputAccepted>().SingleOrDefault();
                if (again is null)
                    problems.Add($"v{state.StreamVersion + 1}: recorded accept of {a.MessageType} but replay decided {replayed.Kind} ({replayed.Reason})");
                else if (again.To != a.To || again.Row != a.Row || !again.Effects.SequenceEqual(a.Effects))
                    problems.Add($"v{state.StreamVersion + 1}: replay diverged: recorded row #{a.Row} -> {a.To}, replay row #{again.Row} -> {again.To}");
            }
            else if (e is InstanceStarted s && s.TableHash != process.TableHash)
                problems.Add($"stream pinned table {s.TableHash} but replaying with {process.TableHash}");
            state = Apply(process, state, e, streamId);
        }
        return problems;
    }
}
