using Bpm.Dsl;

namespace Bpm.Compiler;

/// <summary>
/// Stage 3 (paper §4): determinization. Builds the reachable product of the arm automata of each
/// wait scope and flattens the instant continuation chains (publish, branch) between waits into
/// guarded transitions. The result maps every reachable (state, input, guard) to exactly one next
/// state and a finite effect description.
/// </summary>
internal sealed class Determinizer(
    ProcessDefinition def,
    LNode body,
    IReadOnlyDictionary<string, Publisher> publishers,
    IReadOnlyDictionary<string, BranchPredicate> branches,
    CompilerOptions options)
{
    public const string InitId = "Init";

    private readonly Dictionary<string, FsmState> _states = [];
    private readonly List<Transition> _transitions = [];
    private readonly Queue<(string Id, LWait Wait, string[] Arms)> _work = new();

    public IReadOnlyDictionary<string, FsmState> States => _states;
    public IReadOnlyList<Transition> Transitions => _transitions;

    private sealed record Outcome(IReadOnlyList<Guard> Guards, IReadOnlyList<EffectTemplate> Effects, string Target);

    public void Run()
    {
        _states[InitId] = new FsmState { Id = InitId, Kind = StateKind.Init };
        AddRows(InitId, InputSymbol.Message(def.StartMessageType!), [], body);

        while (_work.TryDequeue(out var item))
        {
            var (id, wait, arms) = item;
            var active = _states[id].ActiveTimers;

            // Horizon: the one non-restarting deadline of this scope.
            AddRows(id, InputSymbol.Timer(wait.HorizonTimer),
                CancelAll(active, except: wait.HorizonTimer),
                wait.OnTimeout);

            switch (wait.Kind)
            {
                case JoinKind.Any:
                    for (var k = 0; k < wait.Arms.Count; k++)
                    {
                        var input = wait.Arms[k].Rules.Single().Input;
                        AddRows(id, input, CancelAll(active), wait.Cases[k]);
                    }
                    break;

                case JoinKind.All:
                    for (var k = 0; k < wait.Arms.Count; k++)
                    {
                        var arm = wait.Arms[k];
                        foreach (var rule in arm.Rules.Where(r => r.From == arms[k]))
                        {
                            var next = (string[])arms.Clone();
                            next[k] = rule.To;
                            var consumed = rule.Input.Kind == InputKind.Timer ? rule.Input.Name : null;

                            if (arm.Dead.Contains(rule.To))
                            {
                                // Arm unsatisfiable (retry exhausted): the join resolves early via OnTimeout.
                                AddRows(id, rule.Input, [.. rule.Effects, .. CancelAll(active, consumed)], wait.OnTimeout);
                            }
                            else if (wait.Arms.Select((a, i) => a.Accepting.Contains(next[i])).All(x => x))
                            {
                                AddRows(id, rule.Input, [.. rule.Effects, .. CancelAll(active, consumed)], wait.OnMatched!);
                            }
                            else
                            {
                                // Partial arrival: move between partial-join states; the horizon keeps ticking.
                                var to = EnsureWaitState(wait, next);
                                Add(id, rule.Input, [], to, rule.Effects);
                            }
                        }
                    }
                    break;
            }

            // Kill/Abort is accepted in every non-terminal state.
            var aborted = EnsureTerminal(TerminalKind.Aborted, "Killed");
            Add(id, InputSymbol.Kill, [], aborted, [.. CancelAll(active), EffectTemplate.Terminate(TerminalKind.Aborted, "Killed")]);

            if (_states.Count > options.MaxStates)
                throw new LoweringLimitException($"lowered machine exceeds {options.MaxStates} states");
        }
    }

    private static List<EffectTemplate> CancelAll(IReadOnlyList<string> active, string? except = null)
        => active.Where(t => t != except).Select(EffectTemplate.Cancel).ToList();

    private void AddRows(string from, InputSymbol input, IReadOnlyList<EffectTemplate> prefix, LNode continuation)
    {
        foreach (var o in Explore(continuation, [], prefix))
            Add(from, input, o.Guards, o.Target, o.Effects);
    }

    private void Add(string from, InputSymbol input, IReadOnlyList<Guard> guards, string to, IReadOnlyList<EffectTemplate> effects)
        => _transitions.Add(new Transition
        {
            Index = _transitions.Count,
            From = from,
            Input = input,
            Guards = guards,
            To = to,
            Effects = effects,
        });

    /// <summary>Run an instant chain symbolically: branches fork into guarded alternatives.</summary>
    private IEnumerable<Outcome> Explore(LNode node, IReadOnlyList<Guard> guards, IReadOnlyList<EffectTemplate> effects)
    {
        switch (node)
        {
            case LPublish p:
                foreach (var o in Explore(p.Next, guards, [.. effects, EffectTemplate.Publish(p.Id, publishers[p.Id].Label)]))
                    yield return o;
                break;
            case LBranch b:
                var label = branches[b.Id].Label;
                foreach (var o in Explore(b.Then, [.. guards, new Guard(b.Id, label, true)], effects)) yield return o;
                foreach (var o in Explore(b.Else, [.. guards, new Guard(b.Id, label, false)], effects)) yield return o;
                break;
            case LTerminal t:
                yield return new Outcome(guards, [.. effects, EffectTemplate.Terminate(t.Kind, t.Outcome)], EnsureTerminal(t.Kind, t.Outcome));
                break;
            case LWait w:
                var id = EnsureWaitState(w, w.Arms.Select(a => a.Initial).ToArray());
                yield return new Outcome(guards, [.. effects, EffectTemplate.Schedule(w.HorizonTimer, w.Timeout)], id);
                break;
        }
    }

    private string EnsureWaitState(LWait w, string[] arms)
    {
        var id = w.Kind == JoinKind.All
            ? $"{w.Scope}[{string.Join(", ", w.Arms.Select((a, i) => $"{a.Name}:{arms[i]}"))}]"
            : w.Scope;
        if (_states.ContainsKey(id)) return id;

        var timers = new List<string> { w.HorizonTimer };
        for (var i = 0; i < w.Arms.Count; i++)
            if (w.Arms[i].TimerIn.TryGetValue(arms[i], out var t)) timers.Add(t);

        _states[id] = new FsmState
        {
            Id = id,
            Kind = StateKind.Wait,
            Scope = w.Scope,
            Horizon = w.Timeout,
            ActiveTimers = timers,
        };
        _work.Enqueue((id, w, arms));
        return id;
    }

    private string EnsureTerminal(TerminalKind kind, string outcome)
    {
        var id = $"{kind}({outcome})";
        _states.TryAdd(id, new FsmState { Id = id, Kind = StateKind.Terminal, Terminal = kind, Outcome = outcome });
        return id;
    }
}
