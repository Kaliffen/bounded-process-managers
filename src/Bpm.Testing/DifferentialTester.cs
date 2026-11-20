using Bpm.Compiler;
using Bpm.Dsl;
using Bpm.Runtime;

namespace Bpm.Testing;

public sealed record DifferentialOptions
{
    public int Trials { get; init; } = 300;
    public int MaxStepsPerTrial { get; init; } = 12;
    public double TimerProbability { get; init; } = 0.25;
    public double KillProbability { get; init; } = 0.03;
}

public sealed record DifferentialResult(bool Passed, int Trials, int Inputs, string? Failure)
{
    public override string ToString() => Passed ? $"passed: {Trials} trials, {Inputs} inputs" : Failure!;
}

/// <summary>
/// Checks target property 4 (source/target correspondence) for any definition: random input
/// sequences are fed both to the compiled transition table (through the runtime engine) and to the
/// <see cref="ReferenceInterpreter"/> (the AST interpreted directly). Observable traces — commands
/// published, timers scheduled, terminal outcomes, and which inputs are accepted at all — must match.
/// </summary>
public static class DifferentialTester
{
    private const string Correlation = "c";

    /// <param name="startMessage">The definition's start message.</param>
    /// <param name="vocabulary">Message generators. Include unrelated and out-of-order messages: they must be ignored identically.</param>
    public static DifferentialResult Run(
        ProcessDefinition definition,
        object startMessage,
        IReadOnlyList<Func<Random, object>> vocabulary,
        int seed,
        DifferentialOptions? options = null)
    {
        options ??= new DifferentialOptions();
        var process = ProcessCompiler.CompileOrThrow(definition);
        var rnd = new Random(seed);
        var inputs = 0;
        for (var trial = 0; trial < options.Trials; trial++)
        {
            var failure = RunTrial(process, definition, startMessage, vocabulary, rnd, options, ref inputs);
            if (failure is not null)
                return new DifferentialResult(false, trial + 1, inputs, $"{definition.Name} seed {seed} trial {trial}: {failure}");
        }
        return new DifferentialResult(true, options.Trials, inputs, null);
    }

    private static string? RunTrial(
        CompiledProcess p, ProcessDefinition def, object start, IReadOnlyList<Func<Random, object>> vocab,
        Random rnd, DifferentialOptions options, ref int inputs)
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var stream = Engine.StreamIdFor(p.Name, Correlation);
        var reference = new ReferenceInterpreter(def);
        var refTimers = new List<(int Handle, DateTimeOffset Deadline)>();
        InstanceState? state = null;
        var msgSeq = 0;
        var trace = new List<string>();

        IReadOnlyList<string>? Target(object body)
        {
            var id = body is TimerFired tf ? TimerFired.IdFor(stream, tf.TimerId, tf.Generation) : $"m{msgSeq++}";
            var d = Engine.Decide(p, state, new Envelope(id, Correlation, body), now);
            foreach (var e in d.Events) state = Engine.Apply(p, state, e, stream);
            var accepted = d.Events.OfType<InputAccepted>().SingleOrDefault();
            return accepted?.Effects.Where(e => e.Kind != EffectKind.CancelTimer).Select(e => e.Kind switch
            {
                EffectKind.Publish => $"publish {Json.Deserialize(e.Payload!, Json.ResolveType(e.CommandType!))}",
                EffectKind.ScheduleTimer => $"schedule {e.Deadline!.Value - now}",
                _ => $"outcome {e.Terminal}({e.Outcome})",
            }).ToList();
        }

        string? Compare(string input, IReadOnlyList<string>? expected, IReadOnlyList<string>? actual)
        {
            trace.Add($"{input}: source={(expected is null ? "ignored" : string.Join("; ", expected))} | compiled={(actual is null ? "ignored" : string.Join("; ", actual))}");
            foreach (var (h, d) in reference.NewTimers) refTimers.Add((h, now + d));
            var ok = expected is null ? actual is null : actual is not null && expected.SequenceEqual(actual);
            return ok ? null : "divergence\n  " + string.Join("\n  ", trace);
        }

        inputs++;
        if (Compare("start", reference.Start(start), Target(start)) is { } f0) return f0;

        for (var step = 0; step < options.MaxStepsPerTrial && !reference.Terminal; step++)
        {
            inputs++;
            var live = refTimers.Where(t => !reference.CancelledTimers.Contains(t.Handle))
                .OrderBy(t => t.Deadline).ThenBy(t => t.Handle).ToList();
            if (live.Count != state!.Timers.Count)
                return $"live timer sets differ (source {live.Count}, compiled {state.Timers.Count})\n  " + string.Join("\n  ", trace);

            var roll = rnd.NextDouble();
            string? failure;
            if (roll < options.TimerProbability && live.Count > 0)
            {
                // Advance to the earliest deadline; both sides break ties by scheduling order.
                var (handle, deadline) = live[0];
                var (timerId, active) = state.Timers.OrderBy(t => t.Value.Deadline).ThenBy(t => t.Value.Generation).First();
                if (active.Deadline != deadline)
                    return $"earliest deadlines differ (source {deadline}, compiled {active.Deadline})\n  " + string.Join("\n  ", trace);
                if (deadline > now) now = deadline;
                refTimers.RemoveAll(t => t.Handle == handle);
                failure = Compare($"fire {timerId}", reference.FireTimer(handle),
                    Target(new TimerFired(stream, timerId, active.Generation, active.Deadline)));
            }
            else if (roll < options.TimerProbability + options.KillProbability)
            {
                failure = Compare("kill", reference.Kill(), Target(new KillProcess("x")));
            }
            else
            {
                now += TimeSpan.FromSeconds(rnd.Next(0, 3));
                var msg = vocab[rnd.Next(vocab.Count)](rnd);
                failure = Compare(msg.GetType().Name, reference.Offer(msg), Target(msg));
            }
            if (failure is not null) return failure;
        }
        return reference.Terminal == state!.IsTerminal ? null : "terminal status differs";
    }
}
