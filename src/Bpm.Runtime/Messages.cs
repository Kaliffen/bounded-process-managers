using System.Security.Cryptography;
using System.Text;

namespace Bpm.Runtime;

/// <summary>
/// Transport envelope. <see cref="MessageId"/> is the stable identity used for deduplication;
/// <see cref="CorrelationId"/> selects the process instance.
/// </summary>
public sealed record Envelope(string MessageId, string CorrelationId, object Body, string? CausationId = null)
{
    public string TypeName => Body.GetType().Name;
    public override string ToString() => $"{TypeName}#{MessageId[..Math.Min(8, MessageId.Length)]} corr={CorrelationId}";
}

/// <summary>
/// Time enters a process as a message (paper §3.3). The generation token distinguishes the current
/// wait from stale timers.
/// </summary>
public sealed record TimerFired(string StreamId, string TimerId, long Generation, DateTimeOffset Deadline)
{
    public static string IdFor(string streamId, string timerId, long generation) => $"timer:{streamId}:{timerId}:{generation}";
}

/// <summary>Explicit control message: records terminal cancellation (paper §3.2 Kill/Abort).</summary>
public sealed record KillProcess(string Reason);

/// <summary>Emitted by the outbox when an instance reaches a terminal outcome.</summary>
public sealed record ProcessEnded(string StreamId, string Definition, int Version, string Terminal, string Outcome);

/// <summary>RFC 4122 version-5 (SHA-1, name-based) UUIDs: stable identities for decided effects.</summary>
public static class DeterministicId
{
    private static readonly Guid Namespace = new("6f1c7a52-5d0e-4c1e-9d55-0b6f6d1f2a10");

    public static string For(string name)
    {
        var ns = Namespace.ToByteArray(bigEndian: true);
        var bytes = SHA1.HashData([.. ns, .. Encoding.UTF8.GetBytes(name)]);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes.AsSpan(0, 16), bigEndian: true).ToString();
    }
}
