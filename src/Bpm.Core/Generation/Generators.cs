using System.Text;
using System.Text.Json;
using Bpm.Compiler;
using Bpm.Dsl;

namespace Bpm.Generation;

/// <summary>
/// Generated behavioural documentation (paper §7.3). Everything is derived from the same compiled
/// table the runtime executes, so documentation cannot drift from behaviour. Output is
/// deterministic, so CI can diff it and require review when behaviour changes.
/// </summary>
public static class Generators
{
    public static IReadOnlyDictionary<string, string> All(CompiledProcess p) => new Dictionary<string, string>
    {
        [$"{p.Name}.v{p.Version}.table.md"] = TransitionTable(p),
        [$"{p.Name}.v{p.Version}.states.mmd"] = Mermaid(p),
        [$"{p.Name}.v{p.Version}.spec.md"] = Specifications(p),
        [$"{p.Name}.v{p.Version}.analysis.json"] = AnalysisJson(p),
    };

    public static string TransitionTable(CompiledProcess p)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {p.Name} v{p.Version} — transition table");
        sb.AppendLine();
        sb.AppendLine($"Table hash `{p.TableHash}` · {p.States.Count} states · {p.Transitions.Count} transitions · T_DAG = **{p.Analysis.TDag.Pretty()}**");
        sb.AppendLine();
        sb.AppendLine("| # | Current state | Accepted input | Guard | Next state | Emitted effects |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var t in p.Transitions)
            sb.AppendLine($"| {t.Index} | `{t.From}` | {Esc(t.Input.Display)} | {Esc(t.GuardDisplay)} | `{t.To}` | {Esc(t.Effects.Count == 0 ? "(none — horizon keeps ticking)" : string.Join("; ", t.Effects.Select(e => e.Display)))} |");
        sb.AppendLine("| * | any terminal | any input | -- | unchanged | (ignored, recorded as InputIgnored for audit) |");
        sb.AppendLine("| * | any wait | TimerFired with gen != active gen | -- | unchanged | (stale timer, recorded as InputIgnored) |");
        sb.AppendLine("| * | any | already-consumed message id | -- | unchanged | (duplicate, dropped by persisted dedup set) |");
        return sb.ToString();
    }

    public static string Mermaid(CompiledProcess p)
    {
        var ids = p.States.Keys.Select((k, i) => (k, i)).ToDictionary(x => x.k, x => $"s{x.i}");
        var sb = new StringBuilder();
        sb.AppendLine("stateDiagram-v2");
        foreach (var s in p.States.Values)
            sb.AppendLine($"    state \"{s.Id.Replace("\"", "'")}\" as {ids[s.Id]}");
        sb.AppendLine($"    [*] --> {ids[p.InitState]}");
        foreach (var t in p.Transitions.Where(t => t.Input.Kind != InputKind.Kill))
        {
            var label = t.Input.Display + (t.Guards.Count > 0 ? $" [{t.GuardDisplay}]" : "");
            sb.AppendLine($"    {ids[t.From]} --> {ids[t.To]} : {label.Replace(":", "#58;")}");
        }
        foreach (var s in p.States.Values.Where(s => s.IsTerminal))
            sb.AppendLine($"    {ids[s.Id]} --> [*]");
        sb.AppendLine("    %% Every wait state also accepts Kill --> Aborted(Killed) (omitted for readability)");
        return sb.ToString();
    }

    /// <summary>Given/When/Then scenarios: one per Init-to-terminal path (finite because the machine is a DAG).</summary>
    public static string Specifications(CompiledProcess p, int maxScenarios = 500)
    {
        var outgoing = p.Transitions.Where(t => t.Input.Kind != InputKind.Kill).ToLookup(t => t.From);
        var paths = new List<List<Transition>>();
        void Walk(string state, List<Transition> path)
        {
            if (paths.Count >= maxScenarios) return;
            if (p.States[state].IsTerminal)
            {
                paths.Add([.. path]);
                return;
            }
            foreach (var t in outgoing[state])
            {
                path.Add(t);
                Walk(t.To, path);
                path.RemoveAt(path.Count - 1);
            }
        }
        Walk(p.InitState, []);

        var sb = new StringBuilder();
        sb.AppendLine($"# {p.Name} v{p.Version} — generated behaviour specification");
        sb.AppendLine();
        sb.AppendLine($"{paths.Count} scenario(s){(paths.Count >= maxScenarios ? $" (truncated at {maxScenarios})" : "")}. Kill/Abort is accepted in every non-terminal state and is not repeated per scenario.");
        var n = 0;
        foreach (var path in paths)
        {
            var end = p.States[path[^1].To];
            sb.AppendLine();
            sb.AppendLine($"## Scenario {++n}: ends {end.Terminal} \"{end.Outcome}\"");
            sb.AppendLine();
            sb.AppendLine($"- **Given** no {p.Name} instance exists for the correlation id");
            foreach (var t in path)
            {
                var when = t.Input.Kind == InputKind.Timer ? $"timer `{t.Input.Name}` fires" : $"`{t.Input.Display}` is received";
                var guard = t.Guards.Count > 0 ? $" and `{t.GuardDisplay}` holds" : "";
                sb.AppendLine($"- **When** {when}{guard}");
                var then = t.Effects.Count == 0 ? $"move to `{t.To}` with no effects" : string.Join(", ", t.Effects.Select(e => $"`{e.Display}`"));
                sb.AppendLine($"  - **Then** {then}");
            }
        }
        return sb.ToString();
    }

    public static string AnalysisJson(CompiledProcess p)
    {
        var a = p.Analysis;
        var doc = new
        {
            definition = p.Name,
            version = p.Version,
            tableHash = p.TableHash,
            states = a.StateCount,
            transitions = a.TransitionCount,
            terminalOutcomes = p.States.Values.Where(s => s.IsTerminal).Select(s => s.Id).OrderBy(x => x, StringComparer.Ordinal),
            waitScopes = p.States.Values.Where(s => s.Scope is not null).GroupBy(s => s.Scope!)
                .Select(g => new { scope = g.Key, horizon = g.First().Horizon.Pretty(), partialStates = g.Count() }),
            bound = new
            {
                tDag = a.TDag.Pretty(),
                tDagSeconds = a.TDag.TotalSeconds,
                maxTimerDeliveries_k = a.MaxTimerDeliveries,
                maxProcessingSteps_r = a.MaxProcessingSteps,
                assumedTimerSkew_delta = a.Assumptions.TimerSkew.Pretty(),
                assumedProcessingDelay_epsilon = a.Assumptions.ProcessingDelay.Pretty(),
                tMax = a.TMax.Pretty(),
                tMaxSeconds = a.TMax.TotalSeconds,
                formula = a.Formula,
                budget = a.Budget?.Pretty(),
                criticalPath = a.CriticalPath,
            },
            assumptions = new[]
            {
                "Every scheduled timer is durably delivered within delta of its deadline.",
                "Every processing step (load, decide, append) completes within epsilon, including bounded append retries.",
                "If the timer service or event store is unavailable indefinitely, no wall-clock bound follows.",
                "T_DAG bounds the coordinator's own waiting; it does not force remote participants to reply.",
            },
            warnings = p.Warnings.Select(w => w.ToString()),
        };
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + Environment.NewLine;
    }

    private static string Esc(string s) => s.Replace("|", "\\|");
}
