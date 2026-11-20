using Bpm.Compiler;
using Bpm.Runtime;

namespace Bpm.Testing;

public sealed record InstanceReport(string StreamId, string Definition, int Version, string Outcome, TimeSpan? Lifetime, TimeSpan Bound);

/// <summary>
/// Post-run checks for the runtime half of the paper's properties (§6, §7.4):
/// outcome totality, bounded-time liveness, deterministic replay, absorbing terminals and effect
/// identity uniqueness.
/// </summary>
public static class InvariantChecker
{
    public static (IReadOnlyList<string> Violations, IReadOnlyList<InstanceReport> Instances) Check(
        IEventStore store, ProcessRegistry registry, bool requireAllTerminal = true)
    {
        var violations = new List<string>();
        var reports = new List<InstanceReport>();
        var effectIds = new HashSet<string>();

        foreach (var streamId in store.Streams.OrderBy(s => s, StringComparer.Ordinal))
        {
            var events = store.Read(streamId);
            var started = (InstanceStarted)events[0];
            var process = registry.Pinned(started);
            var state = Engine.Fold(process, streamId, events)!;

            var lifetime = state.EndedAt - state.StartedAt;
            reports.Add(new InstanceReport(streamId, state.Definition, state.Version,
                state.IsTerminal ? $"{state.Terminal}({state.Outcome})" : $"ACTIVE in {state.Control}", lifetime, state.LifetimeBound));

            if (requireAllTerminal && !state.IsTerminal)
                violations.Add($"{streamId}: not terminal (stuck in {state.Control}) — outcome totality violated");
            if (lifetime > state.LifetimeBound)
                violations.Add($"{streamId}: active for {lifetime} > compiler bound {state.LifetimeBound} — bounded-time liveness violated");

            foreach (var p in Engine.VerifyReplay(process, streamId, events))
                violations.Add($"{streamId}: {p}");

            var terminalSeen = false;
            foreach (var e in events)
            {
                if (e is not InputAccepted a) continue;
                if (terminalSeen) violations.Add($"{streamId}: input {a.MessageType} accepted after terminal outcome — instance reopened");
                foreach (var eff in a.Effects)
                {
                    if (!effectIds.Add(eff.EffectId)) violations.Add($"{streamId}: effect id {eff.EffectId} is not unique");
                    if (eff.Kind == EffectKind.Outcome) terminalSeen = true;
                }
            }
        }
        return (violations, reports);
    }
}
