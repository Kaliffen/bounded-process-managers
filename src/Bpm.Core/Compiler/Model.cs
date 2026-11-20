using Bpm.Dsl;

namespace Bpm.Compiler;

public enum InputKind { Message, Timer, Kill }

/// <summary>An element of the input alphabet: a message type, a (static) timer identity, or Kill.</summary>
public sealed record InputSymbol(InputKind Kind, string Name)
{
    public static readonly InputSymbol Kill = new(InputKind.Kill, "Kill");
    public static InputSymbol Message(Type t) => new(InputKind.Message, t.FullName!);
    public static InputSymbol Timer(string timerId) => new(InputKind.Timer, timerId);

    public string Display => Kind switch
    {
        InputKind.Message => Name[(Name.LastIndexOfAny(['.', '+']) + 1)..],
        InputKind.Timer => $"TimerFired({Name})",
        _ => "Kill",
    };

    public override string ToString() => Display;
}

public enum EffectKind { Publish, ScheduleTimer, CancelTimer, Outcome }

/// <summary>
/// A finite effect description attached to a transition. Concrete values (command payloads, timer
/// generations, deadlines) are materialized at runtime from the folded state.
/// </summary>
public sealed record EffectTemplate(
    EffectKind Kind,
    string? Ref = null,
    string? Label = null,
    TimeSpan? Duration = null,
    TerminalKind? Terminal = null,
    string? Outcome = null)
{
    public static EffectTemplate Publish(string publisherId, string label) => new(EffectKind.Publish, publisherId, label);
    public static EffectTemplate Schedule(string timerId, TimeSpan d) => new(EffectKind.ScheduleTimer, timerId, Duration: d);
    public static EffectTemplate Cancel(string timerId) => new(EffectKind.CancelTimer, timerId);
    public static EffectTemplate Terminate(TerminalKind k, string outcome) => new(EffectKind.Outcome, Terminal: k, Outcome: outcome);

    public string Display => Kind switch
    {
        EffectKind.Publish => $"Publish {Label}",
        EffectKind.ScheduleTimer => $"ScheduleTimer({Ref}, {Duration!.Value.Pretty()})",
        EffectKind.CancelTimer => $"CancelTimer({Ref})",
        _ => $"{Terminal}(\"{Outcome}\")",
    };

    public override string ToString() => Display;
}

public enum StateKind { Init, Wait, Terminal }

public sealed class FsmState
{
    public required string Id { get; init; }
    public StateKind Kind { get; init; }
    /// <summary>The join scope (one non-restarting horizon timer) this wait state belongs to.</summary>
    public string? Scope { get; init; }
    public TimeSpan Horizon { get; init; }
    public IReadOnlyList<string> ActiveTimers { get; init; } = [];
    public TerminalKind? Terminal { get; init; }
    public string? Outcome { get; init; }
    public bool IsTerminal => Kind == StateKind.Terminal;
    public override string ToString() => Id;
}

/// <summary>A guard is the value a branch predicate must have on the (post-evolve) folded state.</summary>
public sealed record Guard(string BranchId, string Label, bool Value)
{
    public string Display => Value ? Label : $"!({Label})";
}

/// <summary>(StateId, Input, Guard) -> (NextStateId, EffectsDescription) — paper §4.</summary>
public sealed class Transition
{
    public required int Index { get; init; }
    public required string From { get; init; }
    public required InputSymbol Input { get; init; }
    public required IReadOnlyList<Guard> Guards { get; init; }
    public required string To { get; init; }
    public required IReadOnlyList<EffectTemplate> Effects { get; init; }

    public string GuardDisplay => Guards.Count == 0 ? "--" : string.Join(" && ", Guards.Select(g => g.Display));

    public override string ToString() =>
        $"{From} --{Input.Display}{(Guards.Count > 0 ? $" [{GuardDisplay}]" : "")}--> {To} / {string.Join(", ", Effects.Select(e => e.Display))}";
}

public sealed record Publisher(string Id, string Label, Func<object, object[]> Factory);
public sealed record BranchPredicate(string Id, string Label, Func<object, bool> Predicate);
