using Bpm.Dsl;

namespace Bpm.Compiler;

public sealed record CompilerOptions
{
    /// <summary>Upper limit for any single timeout. Finite by construction; configurable per deployment.</summary>
    public TimeSpan MaxHorizon { get; init; } = TimeSpan.FromDays(366);
    public int MaxRetryAttempts { get; init; } = 100;
    /// <summary>Guard against join/retry state blow-up (paper §9 threats to validity).</summary>
    public int MaxStates { get; init; } = 20_000;
    public int MaxAstNodes { get; init; } = 100_000;
}

/// <summary>
/// Stage 1 (paper §4): AST validation. This is the safety boundary: it runs on the raw AST, so
/// programs assembled without the type-state builders are still checked.
/// </summary>
internal sealed class Validator(ProcessDefinition def, CompilerOptions options)
{
    private readonly List<Diagnostic> _diagnostics = [];
    private readonly HashSet<Step> _onStack = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<(Step, bool)> _done = [];
    private readonly Dictionary<string, object> _names = [];
    private int _nodes;

    public IReadOnlyList<Diagnostic> Run()
    {
        var root = def.Name;
        if (def.StartMessageType is null || def.Init is null)
            Error(Codes.MissingStart, "the definition has no StartedBy<TMessage>(...) start message", root, def.Location);
        if (def.Body is null)
            Error(Codes.MissingContinuation, "the definition has no body; it can never reach a terminal outcome", root, def.Location);
        else
            Visit(def.Body, root, retryDepth: 0);
        return _diagnostics;
    }

    private void Visit(Step? step, string path, int retryDepth, SourceLocation? parentLocation = null)
    {
        if (step is null)
        {
            Error(Codes.MissingContinuation, "missing continuation: this path does not reach a declared terminal outcome", path, parentLocation);
            return;
        }
        if (_onStack.Contains(step))
        {
            Error(Codes.Cycle, $"cycle detected: {step.Kind} step is reachable from itself (unbounded loops and recursion are not representable; use Retry(n, ...))", path, step.Location);
            return;
        }
        // Shared sub-trees (DAG sharing) are fine; validate each (node, retry-context) once.
        if (!_done.Add((step, retryDepth > 0))) return;
        if (++_nodes > options.MaxAstNodes)
        {
            Error(Codes.StateLimit, $"AST exceeds {options.MaxAstNodes} nodes", path, step.Location);
            return;
        }

        _onStack.Add(step);
        try
        {
            CheckStateType(step, path);
            switch (step)
            {
                case PublishStep p:
                    Visit(p.Next, $"{path}/Publish", retryDepth, p.Location);
                    break;
                case BranchStep b:
                    Visit(b.Then, $"{path}/Branch({b.Label}).Then", retryDepth, b.Location);
                    Visit(b.Else, $"{path}/Branch({b.Label}).Else", retryDepth, b.Location);
                    break;
                case JoinStep j:
                    VisitJoin(j, path, retryDepth);
                    break;
                case RetryStep r:
                    var rp = $"{path}/Retry[{r.Name ?? "?"}]";
                    CheckName(r.Name, r, rp, r.Location);
                    CheckRetryBounds(r.MaxAttempts, r.Backoff, rp, r.Location);
                    Visit(r.Attempt, $"{rp}.Attempt", retryDepth + 1, r.Location);
                    Visit(r.OnExhausted, $"{rp}.OnExhausted", retryDepth, r.Location);
                    break;
                case AttemptFailedStep a:
                    if (retryDepth == 0)
                        Error(Codes.AttemptFailedOutsideRetry, "AttemptFailed() used outside of a Retry(...) attempt", path, a.Location);
                    break;
                case TerminalStep t:
                    if (string.IsNullOrWhiteSpace(t.Outcome))
                        Error(Codes.MissingContinuation, "terminal outcome must be named", path, t.Location);
                    break;
                default:
                    Error(Codes.InternalInvariant, $"unknown step type {step.GetType().Name}", path, step.Location);
                    break;
            }
        }
        finally
        {
            _onStack.Remove(step);
        }
    }

    private void VisitJoin(JoinStep j, string path, int retryDepth)
    {
        var jp = $"{path}/{j.Kind}[{j.Name ?? "?"}]";
        CheckName(j.Name, j, jp, j.Location);

        if (j.Timeout is not { } timeout)
            Error(Codes.UnboundedJoin, "join has no timeout; every suspension must declare a finite horizon (.WithTimeout(...))", jp, j.Location);
        else if (timeout <= TimeSpan.Zero || timeout > options.MaxHorizon)
            Error(Codes.InvalidTimeout, $"timeout {timeout} must be > 0 and <= {options.MaxHorizon.Pretty()}", jp, j.Location);

        var seen = new Dictionary<Type, string>();
        foreach (var arm in j.Arms)
        foreach (var t in arm.MessageTypes)
        {
            if (!seen.TryAdd(t, arm.Name ?? "?"))
                Error(Codes.AmbiguousArm, $"message type {t.Name} is matched by more than one arm of the same join ('{seen[t]}' and '{arm.Name}')", jp, j.Location);
        }

        switch (j.JoinKind)
        {
            case JoinKind.Delay:
                if (j.Arms.Count > 0) Error(Codes.EmptyJoin, "a Delay has no arms", jp, j.Location);
                Visit(j.OnTimeout, $"{jp}.Then", retryDepth, j.Location);
                break;

            case JoinKind.All:
                if (j.Arms.Count == 0) Error(Codes.EmptyJoin, "Join.All needs at least one arm (use Delay for a pure wait)", jp, j.Location);
                foreach (var arm in j.Arms)
                {
                    switch (arm)
                    {
                        case MessageArm { Then: not null }:
                            Error(Codes.ArmContinuation, $"arm '{arm.Name}' of a conjunctive join cannot have its own continuation; use OnMatched", jp, j.Location);
                            break;
                        case RetryArm r:
                            CheckRetryBounds(r.MaxAttempts, r.Backoff, $"{jp}.{r.Name}", j.Location);
                            if (j.Timeout is { } t && r.MaxAttempts > 1 && r.Backoff * (r.MaxAttempts - 1) >= t)
                                Warn(Codes.RetryUnreachable,
                                    $"retry arm '{r.Name}': {r.MaxAttempts - 1} backoff(s) of {r.Backoff.Pretty()} do not fit inside the {t.Pretty()} join horizon; later attempts can never run",
                                    jp, j.Location);
                            break;
                    }
                }
                Visit(j.OnMatched, $"{jp}.OnMatched", retryDepth, j.Location);
                Visit(j.OnTimeout, $"{jp}.OnTimeout", retryDepth, j.Location);
                break;

            case JoinKind.Any:
                if (j.Arms.Count == 0) Error(Codes.EmptyJoin, "Join.Any needs at least one case", jp, j.Location);
                if (j.OnMatched is not null)
                    Error(Codes.ArmContinuation, "Join.Any uses per-case continuations, not OnMatched", jp, j.Location);
                foreach (var arm in j.Arms)
                {
                    if (arm is RetryArm)
                        Error(Codes.RetryInAny, $"retry arm '{arm.Name}' is only allowed in a conjunctive join", jp, j.Location);
                    else if (arm is MessageArm m)
                        Visit(m.Then, $"{jp}.Case[{m.Name}]", retryDepth, j.Location);
                }
                Visit(j.OnTimeout, $"{jp}.OnTimeout", retryDepth, j.Location);
                break;
        }
    }

    private void CheckRetryBounds(int attempts, TimeSpan backoff, string path, SourceLocation? location)
    {
        if (attempts < 1 || attempts > options.MaxRetryAttempts)
            Error(Codes.InvalidRetryBound, $"retry attempts must be a static bound in [1, {options.MaxRetryAttempts}] (was {attempts})", path, location);
        if (backoff < TimeSpan.Zero || backoff > options.MaxHorizon)
            Error(Codes.InvalidRetryBound, $"retry backoff must be in [0, {options.MaxHorizon.Pretty()}] (was {backoff})", path, location);
    }

    private void CheckName(string? name, object owner, string path, SourceLocation? location)
    {
        if (name is null) return;
        if (_names.TryGetValue(name, out var existing) && !ReferenceEquals(existing, owner))
            Error(Codes.DuplicateName, $"name '{name}' is used by more than one join/retry", path, location);
        else
            _names[name] = owner;
    }

    private void CheckStateType(Step step, string path)
    {
        if (step.StateType is { } t && !t.IsAssignableFrom(def.StateType))
            Error(Codes.StateTypeMismatch, $"{step.Kind} was written against state {t.Name} but the definition folds {def.StateType.Name}", path, step.Location);
    }

    private void Error(string code, string message, string path, SourceLocation? location)
        => _diagnostics.Add(new Diagnostic(code, Severity.Error, message, path, location ?? def.Location));

    private void Warn(string code, string message, string path, SourceLocation? location)
        => _diagnostics.Add(new Diagnostic(code, Severity.Warning, message, path, location ?? def.Location));
}
