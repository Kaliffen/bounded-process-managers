namespace Bpm.Runtime;

public sealed record StoredEvent(long Position, string StreamId, long Version, ProcessEvent Event);

public sealed class ConcurrencyException(string stream, long expected, long actual)
    : Exception($"stream {stream}: expected version {expected} but was {actual}");

public sealed class AppendFailedException(string message) : Exception(message);

/// <summary>
/// Minimal event store: per-stream optimistic concurrency plus a global, ordered log that the outbox
/// dispatcher tails.
/// </summary>
public interface IEventStore
{
    IReadOnlyList<ProcessEvent> Read(string streamId);
    /// <summary>Atomically appends; returns the new stream version.</summary>
    long Append(string streamId, long expectedVersion, IReadOnlyList<ProcessEvent> events);
    IReadOnlyList<StoredEvent> ReadAll(long fromPosition, int max = int.MaxValue);
    IReadOnlyCollection<string> Streams { get; }
}

public class InMemoryEventStore : IEventStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<ProcessEvent>> _streams = [];
    private readonly List<StoredEvent> _all = [];

    public IReadOnlyList<ProcessEvent> Read(string streamId)
    {
        lock (_gate) return _streams.TryGetValue(streamId, out var s) ? [.. s] : [];
    }

    public virtual long Append(string streamId, long expectedVersion, IReadOnlyList<ProcessEvent> events)
    {
        lock (_gate)
        {
            if (!_streams.TryGetValue(streamId, out var s)) _streams[streamId] = s = [];
            if (s.Count != expectedVersion) throw new ConcurrencyException(streamId, expectedVersion, s.Count);
            foreach (var e in events)
            {
                s.Add(e);
                _all.Add(new StoredEvent(_all.Count, streamId, s.Count, e));
            }
            Persist(streamId, _all.Skip(_all.Count - events.Count).ToList());
            return s.Count;
        }
    }

    protected virtual void Persist(string streamId, IReadOnlyList<StoredEvent> appended) { }

    protected void Load(StoredEvent e)
    {
        if (!_streams.TryGetValue(e.StreamId, out var s)) _streams[e.StreamId] = s = [];
        s.Add(e.Event);
        _all.Add(e with { Position = _all.Count, Version = s.Count });
    }

    public IReadOnlyList<StoredEvent> ReadAll(long fromPosition, int max = int.MaxValue)
    {
        lock (_gate) return _all.Skip((int)fromPosition).Take(max).ToList();
    }

    public IReadOnlyCollection<string> Streams
    {
        get { lock (_gate) return [.. _streams.Keys]; }
    }
}

/// <summary>
/// Local durable store: one append-only JSON-lines file (the global log). Streams are rebuilt in
/// memory on open. Each append is written and flushed before the call returns.
/// </summary>
public sealed class FileEventStore : InMemoryEventStore
{
    private readonly string _path;

    public FileEventStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "events.jsonl");
        if (!File.Exists(_path)) return;
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var tab = line.IndexOf('\t');
            Load(new StoredEvent(0, line[..tab], 0, Json.DeserializeEvent(line[(tab + 1)..])));
        }
    }

    protected override void Persist(string streamId, IReadOnlyList<StoredEvent> appended)
    {
        using var f = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var w = new StreamWriter(f);
        foreach (var e in appended) w.WriteLine($"{e.StreamId}\t{Json.SerializeEvent(e.Event)}");
        w.Flush();
        f.Flush(flushToDisk: true);
    }
}
