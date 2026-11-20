using Bpm.Dsl;

namespace Bpm.Compiler;

public enum Severity { Warning, Error }

public sealed record Diagnostic(string Code, Severity Severity, string Message, string Path, SourceLocation? Location)
{
    /// <summary>MSBuild/VS canonical error format so build hosts surface it as a real compile error.</summary>
    public override string ToString()
    {
        var where = Location is { File.Length: > 0 } l ? $"{l.File}({l.Line}): " : "";
        return $"{where}{Severity.ToString().ToLowerInvariant()} {Code}: {Message} [at {Path}]";
    }
}

/// <summary>Diagnostic catalogue. Every code is a static check from paper §5.3 or §3.</summary>
public static class Codes
{
    public const string UnboundedJoin = "BPM001";          // join without a finite timeout
    public const string InvalidTimeout = "BPM002";         // timeout <= 0 or beyond MaxHorizon
    public const string InvalidRetryBound = "BPM003";      // retry attempts not in [1, MaxRetryAttempts], or negative backoff
    public const string Cycle = "BPM004";                  // reachable cycle (unbounded loop / recursion)
    public const string MissingContinuation = "BPM005";    // a path does not reach a declared terminal outcome
    public const string DuplicateName = "BPM006";          // join/retry names must be unique
    public const string BudgetExceeded = "BPM007";         // T_DAG > declared maxBudget
    public const string StateLimit = "BPM008";             // lowered machine exceeds the state budget (join blow-up)
    public const string RetryUnreachable = "BPM009";       // warning: retries cannot fit in the join horizon
    public const string EmptyJoin = "BPM010";              // join with no arms
    public const string MissingStart = "BPM011";           // no StartedBy / Init
    public const string AmbiguousArm = "BPM012";           // same message type in two arms of one join
    public const string RetryInAny = "BPM013";             // retry arms only allowed in conjunctive joins
    public const string AttemptFailedOutsideRetry = "BPM014";
    public const string StateTypeMismatch = "BPM015";
    public const string WallClockOverBudget = "BPM016";    // warning: T_max (with δ, ε) exceeds budget
    public const string ArmContinuation = "BPM017";        // All-join arm with its own continuation / Any-case without one
    public const string InternalInvariant = "BPM099";      // lowered machine violates an invariant the compiler must guarantee
}

public sealed class CompilationException(string definition, IReadOnlyList<Diagnostic> diagnostics)
    : Exception($"Process definition '{definition}' was rejected:{Environment.NewLine}" +
                string.Join(Environment.NewLine, diagnostics.Where(d => d.Severity == Severity.Error).Select(d => "  " + d)))
{
    public IReadOnlyList<Diagnostic> Diagnostics { get; } = diagnostics;
}
