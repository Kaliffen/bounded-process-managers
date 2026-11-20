using System.Collections.Immutable;
using Bpm.Dsl;

namespace Bpm.Compiler;

// ---------------------------------------------------------------------------------------------
// Stage 2 (paper §4): join lowering.
//   * Retry(n, attempt) sub-workflows are unrolled into n explicit copies (bounded expansion).
//   * Delay and retry backoff become zero-arm wait scopes with their own horizon timer.
//   * Every wait gets a unique scope id; every publish/branch gets a stable id.
//   * Each join arm is lowered to a small local automaton ("join rules") over the input alphabet.
// The determinizer (stage 3) takes the product of the arm automata within a scope.
// ---------------------------------------------------------------------------------------------

internal abstract class LNode;

internal sealed class LPublish(string id, LNode next) : LNode
{
    public string Id { get; } = id;
    public LNode Next { get; } = next;
}

internal sealed class LBranch(string id, LNode then, LNode @else) : LNode
{
    public string Id { get; } = id;
    public LNode Then { get; } = then;
    public LNode Else { get; } = @else;
}

internal sealed class LTerminal(TerminalKind kind, string outcome) : LNode
{
    public TerminalKind Kind { get; } = kind;
    public string Outcome { get; } = outcome;
}

internal sealed class LWait : LNode
{
    public required string Scope { get; init; }
    public required JoinKind Kind { get; init; }
    public required TimeSpan Timeout { get; init; }
    public required IReadOnlyList<ArmAutomaton> Arms { get; init; }
    /// <summary>Any-join: continuation per arm (same order as Arms).</summary>
    public IReadOnlyList<LNode> Cases { get; init; } = [];
    public LNode? OnMatched { get; init; }
    public required LNode OnTimeout { get; init; }
    public string HorizonTimer => $"{Scope}.T";
}

/// <summary>A local join rule: within one arm, <c>From --Input--> To / Effects</c>.</summary>
internal sealed record ArmRule(string From, InputSymbol Input, string To, IReadOnlyList<EffectTemplate> Effects);

/// <summary>The partial-join automaton for a single join arm.</summary>
internal sealed class ArmAutomaton
{
    public required string Name { get; init; }
    public required string Initial { get; init; }
    public required IReadOnlySet<string> Accepting { get; init; }
    public IReadOnlySet<string> Dead { get; init; } = new HashSet<string>();
    public required IReadOnlyList<ArmRule> Rules { get; init; }
    /// <summary>Arm state -> timer that is active while the arm is in that state (retry backoff).</summary>
    public IReadOnlyDictionary<string, string> TimerIn { get; init; } = new Dictionary<string, string>();

    public ArmRule? Step(string state, InputSymbol input) => Rules.FirstOrDefault(r => r.From == state && r.Input == input);

    public static ArmAutomaton ForMessage(string name, Type type) => new()
    {
        Name = name,
        Initial = "wait",
        Accepting = new HashSet<string> { "got" },
        Rules = [new ArmRule("wait", InputSymbol.Message(type), "got", [])],
    };

    /// <summary>
    /// try_i --success--> got
    /// try_i --failure--> backoff_i / ScheduleTimer(B)          (i &lt; n, backoff &gt; 0)
    /// try_i --failure--> try_i+1  / Publish(reissue)           (i &lt; n, backoff = 0)
    /// try_n --failure--> exhausted                             (join becomes unsatisfiable)
    /// backoff_i --TimerFired(B)--> try_i+1 / Publish(reissue)
    /// </summary>
    public static ArmAutomaton ForRetry(string scope, RetryArm arm, string reissueId)
    {
        var name = arm.Name ?? arm.SuccessType.Name;
        var timer = $"{scope}.{name}.B";
        var success = InputSymbol.Message(arm.SuccessType);
        var failure = InputSymbol.Message(arm.FailureType);
        var reissue = EffectTemplate.Publish(reissueId, arm.ReissueLabel);
        var rules = new List<ArmRule>();
        var timerIn = new Dictionary<string, string>();
        for (var i = 1; i <= arm.MaxAttempts; i++)
        {
            rules.Add(new ArmRule($"try{i}", success, "got", []));
            if (i == arm.MaxAttempts)
                rules.Add(new ArmRule($"try{i}", failure, "exhausted", []));
            else if (arm.Backoff > TimeSpan.Zero)
            {
                rules.Add(new ArmRule($"try{i}", failure, $"backoff{i}", [EffectTemplate.Schedule(timer, arm.Backoff)]));
                rules.Add(new ArmRule($"backoff{i}", InputSymbol.Timer(timer), $"try{i + 1}", [reissue]));
                timerIn[$"backoff{i}"] = timer;
            }
            else
                rules.Add(new ArmRule($"try{i}", failure, $"try{i + 1}", [reissue]));
        }
        return new ArmAutomaton
        {
            Name = name,
            Initial = "try1",
            Accepting = new HashSet<string> { "got" },
            Dead = new HashSet<string> { "exhausted" },
            Rules = rules,
            TimerIn = timerIn,
        };
    }
}

internal sealed class Expander(ProcessDefinition def, CompilerOptions options)
{
    private sealed record RetryFrame(RetryStep Step, int Attempt, string Name);

    private readonly HashSet<string> _scopes = [];
    private readonly Dictionary<string, int> _counters = [];
    private readonly Dictionary<RetryStep, string> _retryNames = new(ReferenceEqualityComparer.Instance);
    private int _expanded;

    public Dictionary<string, Publisher> Publishers { get; } = [];
    public Dictionary<string, BranchPredicate> Branches { get; } = [];
    public HashSet<Type> MessageTypes { get; } = [];

    public LNode Expand() => Expand(def.Body!, ImmutableStack<RetryFrame>.Empty);

    private LNode Expand(Step step, ImmutableStack<RetryFrame> frames)
    {
        if (++_expanded > options.MaxAstNodes)
            throw new LoweringLimitException($"retry unrolling exceeded {options.MaxAstNodes} lowered nodes");

        switch (step)
        {
            case PublishStep p:
            {
                var id = NextId("P");
                Publishers[id] = new Publisher(id, p.Label, p.Commands);
                return new LPublish(id, Expand(p.Next!, frames));
            }
            case BranchStep b:
            {
                var id = NextId("B");
                Branches[id] = new BranchPredicate(id, b.Label, b.Predicate);
                return new LBranch(id, Expand(b.Then!, frames), Expand(b.Else!, frames));
            }
            case JoinStep j:
                return ExpandJoin(j, frames);

            case RetryStep r:
            {
                var name = _retryNames.TryGetValue(r, out var n) ? n : _retryNames[r] = r.Name ?? NextId("Retry");
                return Expand(r.Attempt!, frames.Push(new RetryFrame(r, 1, name)));
            }
            case AttemptFailedStep:
            {
                var frame = frames.Peek();
                var outer = frames.Pop();
                var r = frame.Step;
                if (frame.Attempt >= r.MaxAttempts)
                    return Expand(r.OnExhausted!, outer);

                var nextFrames = outer.Push(frame with { Attempt = frame.Attempt + 1 });
                var nextAttempt = Expand(r.Attempt!, nextFrames);
                if (r.Backoff <= TimeSpan.Zero) return nextAttempt;
                return new LWait
                {
                    Scope = UniqueScope($"{frame.Name}.backoff{frame.Attempt}", outer),
                    Kind = JoinKind.Delay,
                    Timeout = r.Backoff,
                    Arms = [],
                    OnTimeout = nextAttempt,
                };
            }
            case TerminalStep t:
                return new LTerminal(t.TerminalKind, t.Outcome);
            default:
                throw new InvalidOperationException($"unexpected step {step.GetType().Name}");
        }
    }

    private LWait ExpandJoin(JoinStep j, ImmutableStack<RetryFrame> frames)
    {
        var scope = UniqueScope(j.Name ?? NextId(j.JoinKind == JoinKind.Delay ? "Delay" : "J"), frames);
        var arms = new List<ArmAutomaton>();
        var cases = new List<LNode>();
        foreach (var arm in j.Arms)
        {
            foreach (var t in arm.MessageTypes) MessageTypes.Add(t);
            switch (arm)
            {
                case RetryArm r:
                {
                    var id = NextId("R");
                    Publishers[id] = new Publisher(id, r.ReissueLabel, r.Reissue);
                    arms.Add(ArmAutomaton.ForRetry(scope, r, id));
                    break;
                }
                case MessageArm m:
                    arms.Add(ArmAutomaton.ForMessage(m.Name ?? m.MessageType.Name, m.MessageType));
                    if (j.JoinKind == JoinKind.Any) cases.Add(Expand(m.Then!, frames));
                    break;
            }
        }
        return new LWait
        {
            Scope = scope,
            Kind = j.JoinKind,
            Timeout = j.Timeout!.Value,
            Arms = arms,
            Cases = cases,
            OnMatched = j.JoinKind == JoinKind.All ? Expand(j.OnMatched!, frames) : null,
            OnTimeout = Expand(j.OnTimeout!, frames),
        };
    }

    private string UniqueScope(string baseName, ImmutableStack<RetryFrame> frames)
    {
        // Copies produced by retry unrolling are tagged with their attempt numbers.
        var tagged = baseName + string.Concat(frames.Reverse().Select(f => $"#{f.Attempt}"));
        var candidate = tagged;
        for (var i = 2; !_scopes.Add(candidate); i++) candidate = $"{tagged}~{i}";
        return candidate;
    }

    private string NextId(string prefix)
    {
        _counters[prefix] = _counters.GetValueOrDefault(prefix) + 1;
        return $"{prefix}{_counters[prefix]}";
    }
}

internal sealed class LoweringLimitException(string message) : Exception(message);
