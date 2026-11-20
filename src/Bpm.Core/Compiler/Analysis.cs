using Bpm.Dsl;

namespace Bpm.Compiler;

/// <summary>
/// Stage 4 (paper §5): static lifetime analysis over the lowered machine.
///
/// The machine is a DAG. Edge weights: an edge leaving a wait scope (to another scope or a terminal)
/// costs that scope's horizon; edges between partial-join states of the same scope cost zero,
/// because the scope's single horizon timer is never restarted (§3.3) and bounds the whole stay.
///
///   T_DAG = max over Init-to-terminal paths of the sum of edge weights   (topological, O(|V|+|E|))
///   k     = max number of timer deliveries on any path
///   r     = max number of processed inputs on any path
///   T_max <= T_DAG + k·δ + r·ε                                            (§5.2)
///
/// Taking the three maxima independently over-approximates the max of the sum, so T_max is conservative.
/// </summary>
public sealed record LifetimeAnalysis(
    TimeSpan TDag,
    int MaxTimerDeliveries,
    int MaxProcessingSteps,
    RuntimeAssumptions Assumptions,
    TimeSpan TMax,
    TimeSpan? Budget,
    IReadOnlyList<string> CriticalPath,
    int StateCount,
    int TransitionCount,
    int TerminalCount)
{
    public string Formula =>
        $"T_max <= T_DAG + k*delta + r*epsilon = {TDag.Pretty()} + {MaxTimerDeliveries}*{Assumptions.TimerSkew.Pretty()} + {MaxProcessingSteps}*{Assumptions.ProcessingDelay.Pretty()} = {TMax.Pretty()}";
}

internal static class Analyzer
{
    public static LifetimeAnalysis Analyze(
        ProcessDefinition def,
        IReadOnlyDictionary<string, FsmState> states,
        IReadOnlyList<Transition> transitions)
    {
        var order = TopologicalOrder(states, transitions)
                    ?? throw new InvalidOperationException("machine is cyclic; checked earlier");
        // Kill edges are dominated: every wait state also has a horizon edge with the same weight, so
        // dropping them changes no maximum, but keeps the reported critical path meaningful.
        var outgoing = transitions.Where(t => t.Input.Kind != InputKind.Kill).ToLookup(t => t.From);

        TimeSpan Weight(Transition t)
        {
            var from = states[t.From];
            var to = states[t.To];
            return from.Kind == StateKind.Wait && from.Scope != to.Scope ? from.Horizon : TimeSpan.Zero;
        }

        var (tDag, path) = Longest(order, outgoing, t => Weight(t).Ticks);
        var (k, _) = Longest(order, outgoing, t => t.Input.Kind == InputKind.Timer ? 1 : 0);
        var (r, _) = Longest(order, outgoing, _ => 1);

        var tdag = TimeSpan.FromTicks(tDag);
        var a = def.Assumptions;
        var tmax = tdag + a.TimerSkew * k + a.ProcessingDelay * r;
        return new LifetimeAnalysis(
            tdag, (int)k, (int)r, a, tmax, def.MaxBudget, path,
            states.Count, transitions.Count, states.Values.Count(s => s.IsTerminal));
    }

    /// <summary>Longest path from Init over a DAG given in topological order.</summary>
    private static (long Value, List<string> Path) Longest(
        IReadOnlyList<string> order, ILookup<string, Transition> outgoing, Func<Transition, long> weight)
    {
        var best = new Dictionary<string, long> { [Determinizer.InitId] = 0 };
        var pred = new Dictionary<string, string>();
        foreach (var s in order)
        {
            if (!best.TryGetValue(s, out var d)) continue;
            foreach (var t in outgoing[s])
            {
                var nd = d + weight(t);
                if (!best.TryGetValue(t.To, out var cur) || nd > cur)
                {
                    best[t.To] = nd;
                    pred[t.To] = s;
                }
            }
        }
        var (end, value) = best.MaxBy(kv => kv.Value);
        var path = new List<string> { end };
        while (pred.TryGetValue(path[^1], out var p)) path.Add(p);
        path.Reverse();
        return (value, path);
    }

    /// <summary>Kahn's algorithm; returns null if a cycle exists.</summary>
    public static List<string>? TopologicalOrder(IReadOnlyDictionary<string, FsmState> states, IReadOnlyList<Transition> transitions)
    {
        var indeg = states.Keys.ToDictionary(k => k, _ => 0);
        // Collapse parallel edges: indegree counts distinct transitions, which is fine for Kahn.
        foreach (var t in transitions) indeg[t.To]++;
        var outgoing = transitions.ToLookup(t => t.From);
        var queue = new Queue<string>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key).OrderBy(x => x, StringComparer.Ordinal));
        var order = new List<string>();
        while (queue.TryDequeue(out var s))
        {
            order.Add(s);
            foreach (var t in outgoing[s])
                if (--indeg[t.To] == 0) queue.Enqueue(t.To);
        }
        return order.Count == states.Count ? order : null;
    }
}

/// <summary>
/// Checks that the lowered machine satisfies the properties the source restrictions are supposed to
/// guarantee. A failure here is a compiler bug (BPM099), not a user error: the source-level checks
/// should already make these impossible.
/// </summary>
internal static class Invariants
{
    public static IEnumerable<string> Check(IReadOnlyDictionary<string, FsmState> states, IReadOnlyList<Transition> transitions)
    {
        if (Analyzer.TopologicalOrder(states, transitions) is null)
            yield return "lowered machine contains a reachable non-terminal cycle";

        var bySource = transitions.ToLookup(t => t.From);

        foreach (var s in states.Values)
        {
            var rows = bySource[s.Id].ToList();
            if (s.IsTerminal)
            {
                if (rows.Count > 0) yield return $"terminal state {s.Id} has outgoing transitions (must be absorbing)";
                continue;
            }
            if (rows.Count == 0) yield return $"non-terminal state {s.Id} has no outgoing transitions";
            if (s.Kind == StateKind.Wait && !rows.Any(t => t.Input == InputSymbol.Timer($"{s.Scope}.T")))
                yield return $"wait state {s.Id} cannot be left by its horizon timer";

            // Determinism: for each input, the guards of the candidate rows must partition the
            // predicate space: pairwise exclusive and jointly exhaustive.
            foreach (var group in rows.GroupBy(t => t.Input))
            {
                var g = group.ToList();
                for (var i = 0; i < g.Count; i++)
                for (var j = i + 1; j < g.Count; j++)
                    if (!Exclusive(g[i].Guards, g[j].Guards))
                        yield return $"nondeterminism: {s.Id} on {group.Key.Display} has overlapping rows #{g[i].Index} and #{g[j].Index}";
                var coverage = g.Sum(t => Math.Pow(2, -t.Guards.Select(x => x.BranchId).Distinct().Count()));
                if (Math.Abs(coverage - 1.0) > 1e-9)
                    yield return $"incomplete guards: {s.Id} on {group.Key.Display} covers {coverage:P0} of cases";
            }

            foreach (var t in rows)
            foreach (var e in t.Effects.Where(e => e.Kind == EffectKind.CancelTimer && t.Input.Kind == InputKind.Timer && e.Ref == t.Input.Name))
                yield return $"row #{t.Index} cancels the timer it consumes ({e.Ref})";
        }

        var reachable = new HashSet<string> { Determinizer.InitId };
        var stack = new Stack<string>([Determinizer.InitId]);
        while (stack.TryPop(out var s))
            foreach (var t in bySource[s])
                if (reachable.Add(t.To)) stack.Push(t.To);
        foreach (var s in states.Keys.Where(s => !reachable.Contains(s)))
            yield return $"state {s} is unreachable";
    }

    private static bool Exclusive(IReadOnlyList<Guard> a, IReadOnlyList<Guard> b)
        => a.Any(x => b.Any(y => y.BranchId == x.BranchId && y.Value != x.Value));
}
