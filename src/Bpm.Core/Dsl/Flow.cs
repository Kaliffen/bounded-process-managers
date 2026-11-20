using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Bpm.Dsl;

/// <summary>
/// The embedded DSL surface, typed against the folded state <typeparamref name="TState"/>.
/// Intended to be imported with <c>using static Bpm.Dsl.Flow&lt;MyState&gt;;</c>.
///
/// Every builder that would suspend or continue is "pending" until its continuation (or timeout) is
/// supplied, so incomplete programs do not type-check. In particular a join only becomes a
/// <see cref="Step"/> after <c>WithTimeout</c> (paper §3.1: UnboundedJoin --WithTimeout--> BoundedJoin).
/// </summary>
public static partial class Flow<TState>
{
    public static PendingPublish Publish(
        Func<TState, object> command,
        [CallerArgumentExpression(nameof(command))] string label = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => new(new PublishStep
        {
            Commands = s => [command((TState)s)],
            Label = CompactLabel(label),
            StateType = typeof(TState),
            Location = new SourceLocation(file, line),
        });

    public static PendingPublish Publish(
        Func<TState, object[]> commands,
        [CallerArgumentExpression(nameof(commands))] string label = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => new(new PublishStep
        {
            Commands = s => commands((TState)s),
            Label = CompactLabel(label),
            StateType = typeof(TState),
            Location = new SourceLocation(file, line),
        });

    public static Step Branch(
        Func<TState, bool> predicate,
        Step then,
        Step @else,
        [CallerArgumentExpression(nameof(predicate))] string label = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => new BranchStep
        {
            Predicate = s => predicate((TState)s),
            Label = CompactLabel(label),
            Then = then,
            Else = @else,
            StateType = typeof(TState),
            Location = new SourceLocation(file, line),
        };

    public static Step Complete(string outcome, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        => new TerminalStep { TerminalKind = TerminalKind.Completed, Outcome = outcome, Location = new SourceLocation(file, line) };

    public static Step Fail(string reason, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        => new TerminalStep { TerminalKind = TerminalKind.Failed, Outcome = reason, Location = new SourceLocation(file, line) };

    /// <summary>Join arm: one message of type TMsg.</summary>
    public static MessageArm On<TMsg>(string? name = null) => new() { MessageType = typeof(TMsg), Name = name ?? typeof(TMsg).Name };

    /// <summary>Disjunctive-join case: if TMsg arrives first, continue with <paramref name="then"/>.</summary>
    public static MessageArm Case<TMsg>(Step then, string? name = null)
        => new() { MessageType = typeof(TMsg), Then = then, Name = name ?? typeof(TMsg).Name };

    /// <summary>
    /// Bounded retry used as a join arm (paper §3.4). Waits for <paramref name="step"/>; on
    /// <paramref name="onAttemptFailed"/> waits <paramref name="backoff"/>, publishes
    /// <paramref name="reissue"/>, and waits again, for at most <paramref name="maxAttempts"/> attempts.
    /// The first attempt's command is expected to have been published before the join.
    /// </summary>
    public static RetryArm Retry(
        int maxAttempts,
        TimeSpan backoff,
        MessageArm step,
        MessageArm onAttemptFailed,
        Func<TState, object> reissue,
        string? name = null,
        [CallerArgumentExpression(nameof(reissue))] string reissueLabel = "")
        => new()
        {
            MaxAttempts = maxAttempts,
            Backoff = backoff,
            SuccessType = step.MessageType,
            FailureType = onAttemptFailed.MessageType,
            Reissue = s => [reissue((TState)s)],
            ReissueLabel = CompactLabel(reissueLabel),
            Name = name ?? step.MessageType.Name,
        };

    /// <summary>
    /// Bounded retry of a sub-workflow. Inside <paramref name="attempt"/>, use <see cref="AttemptFailed"/>
    /// to fail the attempt. Must be finished with <c>.OnExhausted(...)</c>.
    /// </summary>
    public static PendingRetry Retry(
        int maxAttempts,
        TimeSpan backoff,
        Step attempt,
        string? name = null,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0)
        => new(new RetryStep
        {
            MaxAttempts = maxAttempts,
            Backoff = backoff,
            Attempt = attempt,
            Name = name,
            Location = new SourceLocation(file, line),
        });

    public static Step AttemptFailed([CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        => new AttemptFailedStep { Location = new SourceLocation(file, line) };

    /// <summary>A pure bounded wait. Time enters the process as a timer message, never as a sleep.</summary>
    public static PendingDelay Delay(TimeSpan duration, string? name = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        => new(new JoinStep
        {
            JoinKind = JoinKind.Delay,
            Timeout = duration,
            Name = name,
            Location = new SourceLocation(file, line),
        });

    public static class Join
    {
        /// <summary>Conjunctive join: every arm must be satisfied.</summary>
        public static UnboundedJoin All(params JoinArm[] arms) => new(new JoinStep { JoinKind = JoinKind.All, Arms = [.. arms] });

        /// <summary>Disjunctive join: the first matching case wins.</summary>
        public static UnboundedAnyJoin Any(params MessageArm[] cases) => new(new JoinStep { JoinKind = JoinKind.Any, Arms = [.. cases] });
    }

    internal static string CompactLabel(string expr) => Flow.CompactLabel(expr);
}

internal static partial class Flow
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^\(?\s*(?<p>\w+)\s*\)?\s*=>\s*")]
    private static partial Regex LambdaHead();

    [GeneratedRegex(@"\bnew\s+(?<t>[A-Z]\w*)\s*\(")]
    private static partial Regex NewExpression();

    /// <summary>
    /// Turns the caller's lambda source text into a readable label for generated documentation:
    /// <c>s => s.HasPayment &amp;&amp; !s.HasInventory</c> becomes <c>HasPayment &amp;&amp; !HasInventory</c> and
    /// <c>ctx => [new A(..), new B(..)]</c> becomes <c>A, B</c>.
    /// </summary>
    internal static string CompactLabel(string expr)
    {
        var text = Whitespace().Replace(expr, " ").Trim();
        var head = LambdaHead().Match(text);
        if (!head.Success) return text;
        var param = head.Groups["p"].Value;
        var body = text[head.Length..];
        var created = NewExpression().Matches(body).Select(m => m.Groups["t"].Value).ToList();
        if (created.Count > 0) return string.Join(", ", created);
        return Regex.Replace(body, $@"\b{Regex.Escape(param)}\.", "");
    }
}

public sealed class PendingPublish(PublishStep step)
{
    public Step Then(Step next)
    {
        step.Next = next;
        return step;
    }
}

public sealed class PendingDelay(JoinStep step)
{
    public Step Then(Step next)
    {
        step.OnTimeout = next;
        return step;
    }
}

public sealed class PendingRetry(RetryStep step)
{
    public Step OnExhausted(Step next)
    {
        step.OnExhausted = next;
        return step;
    }
}

/// <summary>A join without a timeout. It is not a <see cref="Step"/>, so it cannot be used as one.</summary>
public sealed class UnboundedJoin(JoinStep step)
{
    public UnboundedJoin Named(string name)
    {
        step.Name = name;
        return this;
    }

    public BoundedJoin WithTimeout(TimeSpan timeout, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        step.Timeout = timeout;
        step.Location = new SourceLocation(file, line);
        return new BoundedJoin(step);
    }
}

public sealed class BoundedJoin(JoinStep step)
{
    public MatchedJoin OnMatched(Step next)
    {
        step.OnMatched = next;
        return new MatchedJoin(step);
    }
}

public sealed class MatchedJoin(JoinStep step)
{
    /// <summary>Taken when the horizon expires or an arm becomes unsatisfiable (retry exhausted).</summary>
    public Step OnTimeout(Step next)
    {
        step.OnTimeout = next;
        return step;
    }
}

public sealed class UnboundedAnyJoin(JoinStep step)
{
    public UnboundedAnyJoin Named(string name)
    {
        step.Name = name;
        return this;
    }

    public BoundedAnyJoin WithTimeout(TimeSpan timeout, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        step.Timeout = timeout;
        step.Location = new SourceLocation(file, line);
        return new BoundedAnyJoin(step);
    }
}

public sealed class BoundedAnyJoin(JoinStep step)
{
    public Step OnTimeout(Step next)
    {
        step.OnTimeout = next;
        return step;
    }
}
