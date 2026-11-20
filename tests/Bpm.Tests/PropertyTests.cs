using Bpm.Dsl;
using Bpm.Runtime;
using Bpm.Testing;
using Bpm.Tests.Fixtures;

namespace Bpm.Tests;

/// <summary>
/// Paper §7.4 / §8.3: fault injection across many seeds. Every run must end with every instance in a
/// terminal outcome within its compiler-derived bound, replay-verified, with no reopened terminals and
/// no repeated external effects.
/// </summary>
public class FaultInjectionTests
{
    public static TheoryData<int> Seeds() => [.. Enumerable.Range(1, 25)];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Chaos_run_preserves_all_invariants(int seed)
    {
        var registry = new ProcessRegistry().Register(PaperOrder.V1).Register(KitchenSink.Create());
        var sim = new Simulation(registry, FaultProfile.Chaos, seed).WithFakeServices(new ServiceBehaviour
        {
            InventoryNeverReplies = 0.15,
            PaymentDeclines = 0.35,
            PaymentNeverReplies = 0.05,
            InventoryMaxDelay = TimeSpan.FromMinutes(4),
            PaymentMaxDelay = TimeSpan.FromMinutes(2),
        });
        // KitchenSink: a noisy external participant sends every message type at random times.
        sim.Service<Cmd>((_, ctx) =>
        {
            object[] noise = [new Ack("k"), new Nack("k"), new Ping("k"), new Pong("k"), new Done("k")];
            for (var n = ctx.Random.Next(0, 4); n > 0; n--)
                ctx.Reply(noise[ctx.Random.Next(noise.Length)], TimeSpan.FromSeconds(ctx.Random.Next(0, 90)));
        });

        var rnd = new Random(seed);
        for (var i = 0; i < 40; i++)
        {
            var at = TimeSpan.FromSeconds(rnd.Next(0, 1800));
            sim.Inject(new OrderSubmitted($"o{i}", [], i), $"o{i}", at);
            if (rnd.NextDouble() < 0.15) sim.Inject(new KillProcess("ops"), $"o{i}", at + TimeSpan.FromSeconds(rnd.Next(0, 400)));
            if (rnd.NextDouble() < 0.1) sim.Inject(new OrderSubmitted($"o{i}", [], i), $"o{i}", at + TimeSpan.FromSeconds(rnd.Next(1, 400)));
        }
        for (var i = 0; i < 20; i++)
            sim.Inject(new KStart($"k{i}"), $"k{i}", TimeSpan.FromSeconds(rnd.Next(0, 1800)));

        sim.Run();

        var (violations, reports) = InvariantChecker.Check(sim.Store, registry);
        Assert.Empty(violations);
        Assert.Equal(60, reports.Count);

        foreach (var g in sim.ExternalEffects.GroupBy(e => e.CorrelationId))
        {
            Assert.True(g.Count(e => e.Body is ChargePayment) <= 2, $"{g.Key}: charged more than the retry bound");
            Assert.True(g.Count(e => e.Body is RefundPayment) <= 1, $"{g.Key}: refunded twice");
        }
        foreach (var refund in sim.ExternalEffects.Where(e => e.Body is RefundPayment))
            Assert.Equal("TimeoutAfterPayment_Refunded", sim.Host.Load($"OrderFulfillment-{refund.CorrelationId}")!.Outcome);
    }

    [Fact]
    public void Bound_is_violated_when_infrastructure_breaks_its_declared_assumptions()
    {
        // Timers up to a minute late, but the definition only assumes δ = 1s: the checker must notice.
        var registry = new ProcessRegistry().Register(PaperOrder.V1);
        var sim = new Simulation(registry, FaultProfile.None with { MaxTimerSkew = TimeSpan.FromMinutes(1) }, seed: 3)
            .WithFakeServices(ServiceBehaviour.Reliable with { InventoryNeverReplies = 1 });
        for (var i = 0; i < 10; i++) sim.Inject(new OrderSubmitted($"o{i}", [], 1m), $"o{i}", TimeSpan.Zero);
        sim.Run();
        Assert.Contains(InvariantChecker.Check(sim.Store, registry).Violations, v => v.Contains("bounded-time liveness violated"));
    }
}

/// <summary>Target property 4 via <see cref="DifferentialTester"/> on the engine fixtures.</summary>
public class DifferentialTests
{
    public static TheoryData<string, int> Cases()
    {
        var data = new TheoryData<string, int>();
        foreach (var name in new[] { "order-v1", "order-v2", "kitchen" })
        foreach (var seed in Enumerable.Range(1, 5))
            data.Add(name, seed);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Compiled_machine_matches_source_semantics(string subject, int seed)
    {
        Func<Random, object>[] orderVocabulary =
        [
            _ => new InventoryReserved("c"),
            r => new PaymentCaptured("c", $"p{r.Next(3)}"),
            _ => new PaymentDeclined("c", "no"),
            _ => new OrderSubmitted("c", [], 1m),
            _ => new Ack("unrelated"),
        ];
        var result = subject switch
        {
            "order-v1" => DifferentialTester.Run(PaperOrder.V1, new OrderSubmitted("c", [new OrderItem("x", 1)], 3m), orderVocabulary, seed),
            "order-v2" => DifferentialTester.Run(PaperOrder.V2, new OrderSubmitted("c", [new OrderItem("x", 1)], 3m), orderVocabulary, seed),
            _ => DifferentialTester.Run(KitchenSink.Create(), new KStart("c"),
                [_ => new Ack("c"), _ => new Nack("c"), _ => new Ping("c"), _ => new Pong("c"), _ => new Done("c")], seed),
        };
        Assert.True(result.Passed, result.ToString());
    }
}
