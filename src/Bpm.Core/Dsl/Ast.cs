namespace Bpm.Dsl;

/// <summary>
/// Source location captured from the builder API (CallerFilePath/CallerLineNumber) so that
/// compiler diagnostics can point back at the workflow definition.
/// </summary>
public sealed record SourceLocation(string File, int Line)
{
    public override string ToString() => $"{File}({Line})";
}

/// <summary>
/// The abstract syntax tree of the restricted process DSL.
///
/// The AST is deliberately public and mutable: the type-state builders in <see cref="Flow{TState}"/>
/// make malformed programs hard to write, but the compiler's validation pass over this AST is the
/// actual safety boundary (paper §3.1). Tests construct malformed ASTs directly to prove that.
/// </summary>
public abstract class Step
{
    /// <summary>The state type the step's lambdas were written against.</summary>
    public Type? StateType { get; set; }
    public SourceLocation? Location { get; set; }
    public abstract string Kind { get; }
}

/// <summary>Publish(command): describe commands for delivery to a domain service, then continue.</summary>
public sealed class PublishStep : Step
{
    public required Func<object, object[]> Commands { get; set; }
    public required string Label { get; set; }
    public Step? Next { get; set; }
    public override string Kind => "Publish";
}

/// <summary>Branch(predicate, then, else): a finite, deterministic choice over the folded state.</summary>
public sealed class BranchStep : Step
{
    public required Func<object, bool> Predicate { get; set; }
    public required string Label { get; set; }
    public Step? Then { get; set; }
    public Step? Else { get; set; }
    public override string Kind => "Branch";
}

public enum JoinKind
{
    /// <summary>Conjunctive join A &amp; B: enabled once every arm is satisfied.</summary>
    All,
    /// <summary>Disjunctive join A | B: the first matching alternative (in stream append order) wins.</summary>
    Any,
    /// <summary>A join with no arms: a pure bounded wait (Delay).</summary>
    Delay,
}

/// <summary>
/// Join(pattern).WithTimeout(duration): a correlation-scoped wait. <see cref="Timeout"/> is nullable
/// in the AST only so that the validator can reject unbounded joins built by bypassing the builder.
/// </summary>
public sealed class JoinStep : Step
{
    public JoinKind JoinKind { get; set; }
    public string? Name { get; set; }
    public List<JoinArm> Arms { get; set; } = [];
    public TimeSpan? Timeout { get; set; }
    /// <summary>Continuation once an <see cref="JoinKind.All"/> join is fully satisfied.</summary>
    public Step? OnMatched { get; set; }
    /// <summary>
    /// Continuation when the join horizon expires, or when an arm becomes unsatisfiable
    /// (e.g. a bounded retry arm exhausts its attempts). For <see cref="JoinKind.Delay"/> this is the
    /// continuation after the delay.
    /// </summary>
    public Step? OnTimeout { get; set; }
    public override string Kind => JoinKind == JoinKind.Delay ? "Delay" : "Join";
}

public abstract class JoinArm
{
    public string? Name { get; set; }
    public abstract IEnumerable<Type> MessageTypes { get; }
}

/// <summary>On&lt;T&gt;(): satisfied by one message of type T scoped to this instance.</summary>
public sealed class MessageArm : JoinArm
{
    public required Type MessageType { get; set; }
    /// <summary>Only used by <see cref="JoinKind.Any"/> joins: the continuation if this case wins.</summary>
    public Step? Then { get; set; }
    public override IEnumerable<Type> MessageTypes => [MessageType];
}

/// <summary>
/// Retry(n, step, onAttemptFailed) used as a join arm: wait for <see cref="SuccessType"/>; on
/// <see cref="FailureType"/> wait <see cref="Backoff"/>, re-issue the command, and try again, at most
/// <see cref="MaxAttempts"/> attempts in total. The whole retry runs inside the enclosing join horizon.
/// </summary>
public sealed class RetryArm : JoinArm
{
    public int MaxAttempts { get; set; }
    public TimeSpan Backoff { get; set; }
    public required Type SuccessType { get; set; }
    public required Type FailureType { get; set; }
    public required Func<object, object[]> Reissue { get; set; }
    public required string ReissueLabel { get; set; }
    public override IEnumerable<Type> MessageTypes => [SuccessType, FailureType];
}

/// <summary>
/// Retry(n, attempt).OnExhausted(...): repeat an attempt sub-workflow at most n times. Inside the
/// attempt, <see cref="AttemptFailedStep"/> means "this attempt failed"; it continues with the next
/// attempt (after the backoff) or, after the last attempt, with <see cref="OnExhausted"/>.
/// The compiler unrolls this into n explicit copies.
/// </summary>
public sealed class RetryStep : Step
{
    public string? Name { get; set; }
    public int MaxAttempts { get; set; }
    public TimeSpan Backoff { get; set; }
    public Step? Attempt { get; set; }
    public Step? OnExhausted { get; set; }
    public override string Kind => "Retry";
}

/// <summary>Marks the failure of the innermost enclosing <see cref="RetryStep"/> attempt.</summary>
public sealed class AttemptFailedStep : Step
{
    public override string Kind => "AttemptFailed";
}

public enum TerminalKind { Completed, Failed, Aborted }

/// <summary>Complete(outcome) / Fail(reason): declared terminal outcomes.</summary>
public sealed class TerminalStep : Step
{
    public TerminalKind TerminalKind { get; set; }
    public required string Outcome { get; set; }
    public override string Kind => TerminalKind.ToString();
}
