using System.Text.Json;
using System.Text.Json.Serialization;
using Bpm.Compiler;
using Bpm.Dsl;

namespace Bpm.Runtime;

/// <summary>Recorded facts in a process instance's single authoritative stream.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$event")]
[JsonDerivedType(typeof(InstanceStarted), "started")]
[JsonDerivedType(typeof(InputAccepted), "accepted")]
[JsonDerivedType(typeof(InputIgnored), "ignored")]
public abstract record ProcessEvent
{
    public required DateTimeOffset RecordedAt { get; init; }
}

/// <summary>First event of every stream: pins the definition version and table hash (paper §7.1).</summary>
public sealed record InstanceStarted : ProcessEvent
{
    public required string Definition { get; init; }
    public required int Version { get; init; }
    public required string TableHash { get; init; }
    public required string CorrelationId { get; init; }
    /// <summary>The compiler's wall-clock lifetime bound for this version, recorded for drain planning.</summary>
    public required TimeSpan LifetimeBound { get; init; }
}

/// <summary>
/// An input was consumed. Holds the fact (the input) and the decision (row, next state, materialized
/// effects). Effects live in the same atomic append as the state change: the stream is the outbox.
/// </summary>
public sealed record InputAccepted : ProcessEvent
{
    public required string MessageId { get; init; }
    public required InputKind Kind { get; init; }
    public required string MessageType { get; init; }
    public required string Payload { get; init; }
    public required string From { get; init; }
    public required string To { get; init; }
    public required int Row { get; init; }
    public required IReadOnlyList<Effect> Effects { get; init; }
}

/// <summary>An input was observed but not accepted (late reply, stale timer, unexpected). Kept for audit.</summary>
public sealed record InputIgnored : ProcessEvent
{
    public required string MessageId { get; init; }
    public required string MessageType { get; init; }
    public required string Reason { get; init; }
}

/// <summary>A materialized effect with a deterministic identity.</summary>
public sealed record Effect
{
    public required string EffectId { get; init; }
    public required EffectKind Kind { get; init; }
    public string? Label { get; init; }
    public string? CommandType { get; init; }
    public string? Payload { get; init; }
    public string? TimerId { get; init; }
    public long? Generation { get; init; }
    public DateTimeOffset? Deadline { get; init; }
    public TerminalKind? Terminal { get; init; }
    public string? Outcome { get; init; }

    public override string ToString() => Kind switch
    {
        EffectKind.Publish => $"Publish {CommandType?.Split(',')[0].Split('.', '+')[^1]}",
        EffectKind.ScheduleTimer => $"ScheduleTimer({TimerId}, gen={Generation}, at {Deadline:MM-dd HH:mm:ss})",
        EffectKind.CancelTimer => $"CancelTimer({TimerId}, gen={Generation})",
        _ => $"{Terminal}(\"{Outcome}\")",
    };
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value, value.GetType(), Options);
    public static object Deserialize(string json, Type type) => JsonSerializer.Deserialize(json, type, Options)!;

    public static string SerializeEvent(ProcessEvent e) => JsonSerializer.Serialize(e, Options);
    public static ProcessEvent DeserializeEvent(string json) => JsonSerializer.Deserialize<ProcessEvent>(json, Options)!;

    /// <summary>Assembly-qualified short name, resolvable with <see cref="Type.GetType(string)"/>.</summary>
    public static string TypeName(Type t) => $"{t.FullName}, {t.Assembly.GetName().Name}";

    public static Type ResolveType(string name)
        => Type.GetType(name)
           ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name.Split(',')[0])).FirstOrDefault(t => t is not null)
           ?? throw new InvalidOperationException($"cannot resolve message type '{name}'");
}
