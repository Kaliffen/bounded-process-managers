using System.Runtime.CompilerServices;

namespace Bpm.Dsl;

/// <summary>
/// Declared infrastructure assumptions used by the wall-clock bound (paper §5.2):
/// every timer is delivered no later than <see cref="TimerSkew"/> (δ) after its deadline, and each
/// processing step (load, decide, append) adds at most <see cref="ProcessingDelay"/> (ε).
/// </summary>
public sealed record RuntimeAssumptions(TimeSpan TimerSkew, TimeSpan ProcessingDelay)
{
    public static RuntimeAssumptions Default { get; } = new(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
}

/// <summary>
/// A workflow definition: the source DSL program. It is inert data; <c>ProcessCompiler</c> turns it
/// into a <c>CompiledProcess</c> (a finite, deterministic transition table) or rejects it.
/// </summary>
public sealed class ProcessDefinition
{
    public required string Name { get; init; }
    public int Version { get; init; } = 1;
    public TimeSpan? MaxBudget { get; init; }
    public required Type StateType { get; init; }
    public RuntimeAssumptions Assumptions { get; set; } = RuntimeAssumptions.Default;
    public SourceLocation? Location { get; init; }

    /// <summary>The message type that creates an instance (the "Init" transition).</summary>
    public Type? StartMessageType { get; set; }
    /// <summary>Builds the initial folded state from the start message.</summary>
    public Func<object, object>? Init { get; set; }
    /// <summary>evolve: State x Input -> State, per accepted message type. Must be pure.</summary>
    public Dictionary<Type, Func<object, object, object>> Evolvers { get; } = [];

    /// <summary>The workflow body (normally a Publish produced by StartWith).</summary>
    public Step? Body { get; set; }
}

public static class ProcessManager
{
    public static DefinitionBuilder<TState> Define<TState>(
        string name,
        TimeSpan? maxBudget = null,
        int version = 1,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => new(new ProcessDefinition
        {
            Name = name,
            Version = version,
            MaxBudget = maxBudget,
            StateType = typeof(TState),
            Location = new SourceLocation(file, line),
        });
}

/// <summary>Type-state: a definition must first say which message starts it.</summary>
public sealed class DefinitionBuilder<TState>(ProcessDefinition def)
{
    public StartedDefinitionBuilder<TState> StartedBy<TMsg>(Func<TMsg, TState> init)
    {
        def.StartMessageType = typeof(TMsg);
        def.Init = m => init((TMsg)m)!;
        return new StartedDefinitionBuilder<TState>(def);
    }
}

public sealed class StartedDefinitionBuilder<TState>(ProcessDefinition def)
{
    /// <summary>Registers the pure evolve function applied when a message of type TMsg is accepted.</summary>
    public StartedDefinitionBuilder<TState> Evolve<TMsg>(Func<TState, TMsg, TState> evolve)
    {
        def.Evolvers[typeof(TMsg)] = (s, m) => evolve((TState)s, (TMsg)m)!;
        return this;
    }

    public StartedDefinitionBuilder<TState> Assuming(TimeSpan timerSkew, TimeSpan processingDelay)
    {
        def.Assumptions = new RuntimeAssumptions(timerSkew, processingDelay);
        return this;
    }

    /// <summary>Commands published by the Init transition, before the body runs.</summary>
    public PendingStart<TState> StartWith(
        Func<TState, object[]> commands,
        [CallerArgumentExpression(nameof(commands))] string label = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => new(def, new PublishStep
        {
            Commands = s => commands((TState)s),
            Label = Flow<TState>.CompactLabel(label),
            StateType = typeof(TState),
            Location = new SourceLocation(file, line),
        });

    public ProcessDefinition Then(Step body)
    {
        def.Body = body;
        return def;
    }
}

public sealed class PendingStart<TState>(ProcessDefinition def, PublishStep start)
{
    public ProcessDefinition Then(Step next)
    {
        start.Next = next;
        def.Body = start;
        return def;
    }
}

/// <summary>Marks a static field/property returning a <see cref="ProcessDefinition"/> for the bpm compiler CLI.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method)]
public sealed class BoundedProcessAttribute : Attribute;

public static class Durations
{
    public static TimeSpan Milliseconds(this int n) => TimeSpan.FromMilliseconds(n);
    public static TimeSpan Seconds(this int n) => TimeSpan.FromSeconds(n);
    public static TimeSpan Minutes(this int n) => TimeSpan.FromMinutes(n);
    public static TimeSpan Hours(this int n) => TimeSpan.FromHours(n);
    public static TimeSpan Days(this int n) => TimeSpan.FromDays(n);

    public static string Pretty(this TimeSpan t)
    {
        if (t == TimeSpan.Zero) return "0s";
        var parts = new List<string>();
        if (t.Days > 0) parts.Add($"{t.Days}d");
        if (t.Hours > 0) parts.Add($"{t.Hours}h");
        if (t.Minutes > 0) parts.Add($"{t.Minutes}m");
        if (t.Seconds > 0) parts.Add($"{t.Seconds}s");
        if (t.Milliseconds > 0) parts.Add($"{t.Milliseconds}ms");
        return string.Join("", parts);
    }
}
