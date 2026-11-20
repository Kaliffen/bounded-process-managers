using Bpm.Dsl;
using static Bpm.Dsl.Flow<Bpm.Tests.KState>;

namespace Bpm.Tests;

public sealed record KStart(string Id);
public sealed record Ack(string Id);
public sealed record Nack(string Id);
public sealed record Ping(string Id);
public sealed record Pong(string Id);
public sealed record Done(string Id);
public sealed record Cmd(string What);

public sealed record KState(int Acks = 0, int Nacks = 0, int Pings = 0);

/// <summary>
/// A definition that nests every construct: standalone Retry around a three-arm conjunctive join that
/// itself contains a zero-backoff retry arm, branches in both continuations, AttemptFailed from a
/// branch, a Delay, and a disjunctive join after exhaustion.
/// </summary>
public static class KitchenSink
{
    public static ProcessDefinition Create(TimeSpan? budget = null) =>
        ProcessManager.Define<KState>("KitchenSink", maxBudget: budget ?? 1.Days())
            .StartedBy<KStart>(_ => new KState())
            .Evolve<Ack>((s, _) => s with { Acks = s.Acks + 1 })
            .Evolve<Nack>((s, _) => s with { Nacks = s.Nacks + 1 })
            .Evolve<Ping>((s, _) => s with { Pings = s.Pings + 1 })
            .StartWith(_ => [new Cmd("go")])
            .Then(
                Retry(maxAttempts: 2, backoff: 10.Seconds(), name: "Outer",
                        attempt: Join.All(
                                On<Ack>(),
                                Retry(maxAttempts: 3, backoff: TimeSpan.Zero,
                                    step: On<Pong>(), onAttemptFailed: On<Nack>(),
                                    reissue: s => new Cmd($"re{s.Nacks}"), name: "PongRetry"),
                                On<Ping>())
                            .Named("Triple")
                            .WithTimeout(1.Minutes())
                            .OnMatched(Branch(s => s.Nacks >= 2,
                                Delay(5.Seconds()).Then(Complete("Persistent")),
                                Complete("Easy")))
                            .OnTimeout(Branch(s => s.Acks > 0,
                                AttemptFailed(),
                                Publish(_ => new Cmd("noack")).Then(Fail("NoAck")))))
                    .OnExhausted(
                        Join.Any(
                                Case<Done>(Complete("LateDone")),
                                Case<Ping>(Fail("PingAfterExhaust")))
                            .Named("Final")
                            .WithTimeout(30.Seconds())
                            .OnTimeout(Fail("GaveUp"))));
}
