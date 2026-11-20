using System.Collections.Immutable;
using Bpm.Dsl;

namespace Bpm.Testing;

/// <summary>
/// Source-level operational semantics: interprets the DSL AST directly (no unrolling, no product
/// construction, no transition table). Used to test target property 4 — that the compiled machine
/// preserves the observable behaviour of the source program — by differential testing.
///
/// Observables are commands published, timers scheduled (by duration) and terminal outcomes.
/// Timers are identified by opaque handles allocated here; cancellations are reported separately
/// so a harness can track which timers remain live, but are not part of the compared trace.
/// </summary>
public sealed class ReferenceInterpreter(ProcessDefinition def)
{
    private sealed record Frame(RetryStep Retry, int Attempt);

    private sealed class ArmProgress
    {
        public bool Got;
        public int Attempt = 1;
        public int? BackoffTimer;
    }

    private sealed class Wait
    {
        public required JoinStep? Join { get; init; }
        public required int Horizon { get; init; }
        public Step? DelayContinuation { get; init; }
        public required ImmutableStack<Frame> Frames { get; init; }
        public List<ArmProgress> Arms { get; } = [];
    }

    private Wait? _wait;
    private int _nextHandle = 1;
    private readonly List<string> _out = [];

    public object? Data { get; private set; }
    public bool Started { get; private set; }
    public bool Terminal { get; private set; }
    public HashSet<int> CancelledTimers { get; } = [];
    /// <summary>Timers scheduled by the last step, as (handle, duration).</summary>
    public List<(int Handle, TimeSpan Duration)> NewTimers { get; } = [];

    public IReadOnlyList<string>? Start(object message)
    {
        Begin();
        if (Started || message.GetType() != def.StartMessageType) return null;
        Begin();
        Started = true;
        Data = def.Init!(message);
        Run(def.Body!, ImmutableStack<Frame>.Empty);
        return [.. _out];
    }

    public IReadOnlyList<string>? Offer(object message)
    {
        Begin();
        if (!Started) return Start(message);
        if (Terminal || _wait is null) return null;
        var type = message.GetType();
        var w = _wait;
        var join = w.Join;
        if (join is null) return null; // Delay: only time moves it on

        if (join.JoinKind == JoinKind.Any)
        {
            var c = join.Arms.OfType<MessageArm>().FirstOrDefault(a => a.MessageType == type);
            if (c is null) return null;
            Data = Evolve(message);
            CancelAll(w);
            Run(c.Then!, w.Frames);
            return [.. _out];
        }

        for (var i = 0; i < join.Arms.Count; i++)
        {
            var arm = join.Arms[i];
            var p = w.Arms[i];
            if (p.Got) continue;
            switch (arm)
            {
                case MessageArm m when m.MessageType == type:
                    Data = Evolve(message);
                    p.Got = true;
                    return AfterArmChange(w);

                case RetryArm r when p.BackoffTimer is null && r.SuccessType == type:
                    Data = Evolve(message);
                    p.Got = true;
                    return AfterArmChange(w);

                case RetryArm r when p.BackoffTimer is null && r.FailureType == type:
                    Data = Evolve(message);
                    if (p.Attempt >= r.MaxAttempts)
                    {
                        CancelAll(w);
                        Run(join.OnTimeout!, w.Frames);
                        return [.. _out];
                    }
                    if (r.Backoff > TimeSpan.Zero)
                        p.BackoffTimer = Schedule(r.Backoff);
                    else
                    {
                        p.Attempt++;
                        Emit(r.Reissue(Data!));
                    }
                    return [.. _out];
            }
        }
        return null;
    }

    public IReadOnlyList<string>? FireTimer(int handle)
    {
        Begin();
        if (Terminal || _wait is null || CancelledTimers.Contains(handle)) return null;
        var w = _wait;
        if (handle == w.Horizon)
        {
            CancelAll(w, except: handle);
            Run(w.Join is null ? w.DelayContinuation! : w.Join.OnTimeout!, w.Frames);
            return [.. _out];
        }
        for (var i = 0; i < w.Arms.Count; i++)
        {
            if (w.Arms[i].BackoffTimer != handle) continue;
            var r = (RetryArm)w.Join!.Arms[i];
            w.Arms[i].BackoffTimer = null;
            w.Arms[i].Attempt++;
            Emit(r.Reissue(Data!));
            return [.. _out];
        }
        return null;
    }

    public IReadOnlyList<string>? Kill()
    {
        Begin();
        if (!Started || Terminal || _wait is null) return null;
        CancelAll(_wait);
        Finish(TerminalKind.Aborted, "Killed");
        return [.. _out];
    }

    private IReadOnlyList<string> AfterArmChange(Wait w)
    {
        if (w.Arms.All(a => a.Got))
        {
            CancelAll(w);
            Run(w.Join!.OnMatched!, w.Frames);
        }
        return [.. _out];
    }

    private void Run(Step step, ImmutableStack<Frame> frames)
    {
        _wait = null;
        while (true)
        {
            switch (step)
            {
                case PublishStep p:
                    Emit(p.Commands(Data!));
                    step = p.Next!;
                    break;
                case BranchStep b:
                    step = b.Predicate(Data!) ? b.Then! : b.Else!;
                    break;
                case RetryStep r:
                    frames = frames.Push(new Frame(r, 1));
                    step = r.Attempt!;
                    break;
                case AttemptFailedStep:
                {
                    var f = frames.Peek();
                    frames = frames.Pop();
                    if (f.Attempt >= f.Retry.MaxAttempts)
                    {
                        step = f.Retry.OnExhausted!;
                        break;
                    }
                    frames = frames.Push(f with { Attempt = f.Attempt + 1 });
                    if (f.Retry.Backoff <= TimeSpan.Zero)
                    {
                        step = f.Retry.Attempt!;
                        break;
                    }
                    _wait = new Wait { Join = null, Horizon = Schedule(f.Retry.Backoff), DelayContinuation = f.Retry.Attempt, Frames = frames };
                    return;
                }
                case JoinStep { JoinKind: JoinKind.Delay } d:
                    _wait = new Wait { Join = null, Horizon = Schedule(d.Timeout!.Value), DelayContinuation = d.OnTimeout, Frames = frames };
                    return;
                case JoinStep j:
                    var w = new Wait { Join = j, Horizon = Schedule(j.Timeout!.Value), Frames = frames };
                    foreach (var _ in j.Arms) w.Arms.Add(new ArmProgress());
                    _wait = w;
                    return;
                case TerminalStep t:
                    Finish(t.TerminalKind, t.Outcome);
                    return;
                default:
                    throw new InvalidOperationException(step.GetType().Name);
            }
        }
    }

    private void Finish(TerminalKind kind, string outcome)
    {
        _out.Add($"outcome {kind}({outcome})");
        Terminal = true;
        _wait = null;
    }

    private object? Evolve(object message)
        => def.Evolvers.TryGetValue(message.GetType(), out var e) ? e(Data!, message) : Data;

    private int Schedule(TimeSpan d)
    {
        var h = _nextHandle++;
        _out.Add($"schedule {d}");
        NewTimers.Add((h, d));
        return h;
    }

    private void CancelAll(Wait w, int? except = null)
    {
        if (w.Horizon != except) CancelledTimers.Add(w.Horizon);
        foreach (var a in w.Arms)
            if (a.BackoffTimer is { } b && b != except) CancelledTimers.Add(b);
    }

    private void Emit(object[] commands)
    {
        foreach (var c in commands) _out.Add($"publish {c}");
    }

    private void Begin()
    {
        _out.Clear();
        NewTimers.Clear();
    }
}
