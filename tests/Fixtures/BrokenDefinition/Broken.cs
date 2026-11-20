using Bpm.Dsl;
using static Bpm.Dsl.Flow<Broken.State>;

namespace Broken;

public sealed record Start(string Id);
public sealed record Reply(string Id);
public sealed record Ask(string Id);
public sealed record State(string Id);

public static class Definitions
{
    // Two sequential 6-minute waits cannot fit a 10-minute budget: BPM007.
    [BoundedProcess]
    public static ProcessDefinition OverBudget { get; } =
        ProcessManager.Define<State>("OverBudget", maxBudget: 10.Minutes())
            .StartedBy<Start>(m => new State(m.Id))
            .StartWith(s => [new Ask(s.Id)])
            .Then(
                Join.All(On<Reply>()).WithTimeout(6.Minutes())
                    .OnMatched(Publish(s => new Ask(s.Id)).Then(
                        Join.All(On<Reply>()).Named("Second").WithTimeout(6.Minutes())
                            .OnMatched(Complete("Done"))
                            .OnTimeout(Fail("SecondTimeout"))))
                    .OnTimeout(Fail("FirstTimeout")));

    // Built without the type-state builder: a join with no timeout. BPM001.
    [BoundedProcess]
    public static ProcessDefinition Unbounded { get; } = new()
    {
        Name = "Unbounded",
        StateType = typeof(State),
        StartMessageType = typeof(Start),
        Init = m => new State(((Start)m).Id),
        Body = new JoinStep
        {
            JoinKind = JoinKind.All,
            Arms = [On<Reply>()],
            OnMatched = Complete("Done"),
            OnTimeout = Fail("never"),
        },
    };
}
