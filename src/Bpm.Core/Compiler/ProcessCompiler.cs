using System.Security.Cryptography;
using System.Text;
using Bpm.Dsl;

namespace Bpm.Compiler;

/// <summary>The output of compilation: a finite, deterministic, analysed transition table.</summary>
public sealed class CompiledProcess
{
    private readonly ILookup<(string, InputSymbol), Transition> _index;

    internal CompiledProcess(
        ProcessDefinition definition,
        IReadOnlyDictionary<string, FsmState> states,
        IReadOnlyList<Transition> transitions,
        IReadOnlyDictionary<string, Publisher> publishers,
        IReadOnlyDictionary<string, BranchPredicate> branches,
        IReadOnlySet<Type> messageTypes,
        LifetimeAnalysis analysis,
        IReadOnlyList<Diagnostic> warnings)
    {
        Definition = definition;
        States = states;
        Transitions = transitions;
        Publishers = publishers;
        Branches = branches;
        Analysis = analysis;
        Warnings = warnings;
        var types = new HashSet<Type>(messageTypes) { definition.StartMessageType! };
        foreach (var t in definition.Evolvers.Keys) types.Add(t);
        MessageTypes = types.ToDictionary(t => t.FullName!);
        _index = transitions.ToLookup(t => (t.From, t.Input));
        TableHash = ComputeHash();
    }

    public ProcessDefinition Definition { get; }
    public string Name => Definition.Name;
    public int Version => Definition.Version;
    public IReadOnlyDictionary<string, FsmState> States { get; }
    public IReadOnlyList<Transition> Transitions { get; }
    public IReadOnlyDictionary<string, Publisher> Publishers { get; }
    public IReadOnlyDictionary<string, BranchPredicate> Branches { get; }
    /// <summary>Every message type the machine reacts to, keyed by full name.</summary>
    public IReadOnlyDictionary<string, Type> MessageTypes { get; }
    public LifetimeAnalysis Analysis { get; }
    public IReadOnlyList<Diagnostic> Warnings { get; }
    /// <summary>Hash of the canonical transition table; pinned by every instance (paper §7.1).</summary>
    public string TableHash { get; }
    public string InitState => Determinizer.InitId;
    public Type StartMessageType => Definition.StartMessageType!;

    public IEnumerable<Transition> RowsFor(string state, InputSymbol input) => _index[(state, input)];

    private string ComputeHash()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{Name}@{Version}");
        foreach (var s in States.Values.OrderBy(s => s.Id, StringComparer.Ordinal))
            sb.AppendLine($"S {s.Id} {s.Kind} {s.Horizon.Ticks} {string.Join(",", s.ActiveTimers)}");
        foreach (var t in Transitions) sb.AppendLine($"T {t}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16].ToLowerInvariant();
    }
}

public sealed record CompilationResult(ProcessDefinition Definition, CompiledProcess? Process, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Process is not null;
    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == Severity.Error);
}

/// <summary>
/// The four-stage pipeline of paper §4: AST validation -> join lowering -> determinization -> analysis.
/// If any stage cannot establish the termination criteria, compilation fails.
/// </summary>
public static class ProcessCompiler
{
    public static CompilationResult Compile(ProcessDefinition definition, CompilerOptions? options = null)
    {
        options ??= new CompilerOptions();
        var diagnostics = new List<Diagnostic>();
        var root = definition.Name;

        // 1. AST validation — the safety boundary.
        diagnostics.AddRange(new Validator(definition, options).Run());
        if (diagnostics.Any(d => d.Severity == Severity.Error))
            return new CompilationResult(definition, null, diagnostics);

        try
        {
            // 2. Join lowering.
            var expander = new Expander(definition, options);
            var lowered = expander.Expand();

            // 3. Determinization.
            var det = new Determinizer(definition, lowered, expander.Publishers, expander.Branches, options);
            det.Run();

            foreach (var violation in Invariants.Check(det.States, det.Transitions))
                diagnostics.Add(new Diagnostic(Codes.InternalInvariant, Severity.Error, violation, root, definition.Location));
            if (diagnostics.Any(d => d.Severity == Severity.Error))
                return new CompilationResult(definition, null, diagnostics);

            // 4. Analysis.
            var analysis = Analyzer.Analyze(definition, det.States, det.Transitions);
            if (definition.MaxBudget is { } budget)
            {
                if (analysis.TDag > budget)
                    diagnostics.Add(new Diagnostic(Codes.BudgetExceeded, Severity.Error,
                        $"worst-case waiting bound T_DAG = {analysis.TDag.Pretty()} exceeds the declared budget {budget.Pretty()} (critical path: {string.Join(" -> ", analysis.CriticalPath)})",
                        root, definition.Location));
                else if (analysis.TMax > budget)
                    diagnostics.Add(new Diagnostic(Codes.WallClockOverBudget, Severity.Warning,
                        $"T_DAG fits the budget but the wall-clock bound does not: {analysis.Formula} > {budget.Pretty()}",
                        root, definition.Location));
            }
            if (diagnostics.Any(d => d.Severity == Severity.Error))
                return new CompilationResult(definition, null, diagnostics);

            var process = new CompiledProcess(definition, det.States, det.Transitions, expander.Publishers,
                expander.Branches, expander.MessageTypes, analysis, diagnostics);
            return new CompilationResult(definition, process, diagnostics);
        }
        catch (LoweringLimitException e)
        {
            diagnostics.Add(new Diagnostic(Codes.StateLimit, Severity.Error, e.Message, root, definition.Location));
            return new CompilationResult(definition, null, diagnostics);
        }
    }

    public static CompiledProcess CompileOrThrow(ProcessDefinition definition, CompilerOptions? options = null)
    {
        var result = Compile(definition, options);
        return result.Process ?? throw new CompilationException(definition.Name, result.Diagnostics);
    }
}
