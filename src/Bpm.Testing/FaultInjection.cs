using Bpm.Runtime;

namespace Bpm.Testing;

/// <summary>Fault-injection decorator: fails appends according to a policy (paper §7.4 "failed appends").</summary>
public sealed class FaultyEventStore(IEventStore inner, Func<string, bool> shouldFail) : IEventStore
{
    public int FailedAppends { get; private set; }
    public IReadOnlyList<ProcessEvent> Read(string streamId) => inner.Read(streamId);

    public long Append(string streamId, long expectedVersion, IReadOnlyList<ProcessEvent> events)
    {
        if (shouldFail(streamId))
        {
            FailedAppends++;
            throw new AppendFailedException($"injected append failure on {streamId}");
        }
        return inner.Append(streamId, expectedVersion, events);
    }

    public IReadOnlyList<StoredEvent> ReadAll(long fromPosition, int max = int.MaxValue) => inner.ReadAll(fromPosition, max);
    public IReadOnlyCollection<string> Streams => inner.Streams;
}

/// <summary>Thrown by fault-injection hooks to model a process crash.</summary>
public sealed class SimulatedCrashException(string message) : Exception(message);
