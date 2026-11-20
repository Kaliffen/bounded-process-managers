using Bpm.Dsl;
using Bpm.Runtime;
using Bpm.Testing;
using static Bpm.Dsl.Flow<Bpm.Tests.Fixtures.OrderState>;

namespace Bpm.Tests.Fixtures;

// Engine tests own their fixtures: they must not depend on the sample domains, which are free to
// change. This mirrors the paper's §3.4 order example.

public sealed record OrderItem(string Sku, int Quantity);
public sealed record OrderSubmitted(string OrderId, OrderItem[] Items, decimal Total);
public sealed record ReserveInventory(string OrderId, OrderItem[] Items);
public sealed record ChargePayment(string OrderId, decimal Total);
public sealed record RefundPayment(string OrderId, string PaymentId);
public sealed record InventoryReserved(string OrderId);
public sealed record PaymentCaptured(string OrderId, string PaymentId);
public sealed record PaymentDeclined(string OrderId, string Reason);

public sealed record OrderState(
    string OrderId, OrderItem[] Items, decimal Total,
    bool HasInventory = false, bool HasPayment = false, string? PaymentId = null, int Declines = 0);

public static class PaperOrder
{
    public static ProcessDefinition V1 { get; } = Create(version: 1, attempts: 2, horizon: TimeSpan.FromMinutes(5));
    public static ProcessDefinition V2 { get; } = Create(version: 2, attempts: 3, horizon: TimeSpan.FromMinutes(6));

    public static ProcessDefinition Create(int version, int attempts, TimeSpan horizon) =>
        ProcessManager.Define<OrderState>("OrderFulfillment", maxBudget: 10.Minutes(), version: version)
            .StartedBy<OrderSubmitted>(m => new OrderState(m.OrderId, m.Items, m.Total))
            .Evolve<InventoryReserved>((s, _) => s with { HasInventory = true })
            .Evolve<PaymentCaptured>((s, m) => s with { HasPayment = true, PaymentId = m.PaymentId })
            .Evolve<PaymentDeclined>((s, _) => s with { Declines = s.Declines + 1 })
            .StartWith(ctx => [new ReserveInventory(ctx.OrderId, ctx.Items), new ChargePayment(ctx.OrderId, ctx.Total)])
            .Then(
                Join.All(
                        On<InventoryReserved>("Inv"),
                        Retry(maxAttempts: attempts, backoff: 30.Seconds(),
                            step: On<PaymentCaptured>(), onAttemptFailed: On<PaymentDeclined>(),
                            reissue: s => new ChargePayment(s.OrderId, s.Total), name: "Pay"))
                    .Named("InvPay")
                    .WithTimeout(horizon)
                    .OnMatched(Complete("Fulfilled"))
                    .OnTimeout(
                        Branch(s => s.HasPayment && !s.HasInventory,
                            then: Publish(s => new RefundPayment(s.OrderId, s.PaymentId!)).Then(Fail("TimeoutAfterPayment_Refunded")),
                            @else: Fail("TimeoutBeforePayment"))));
}

public sealed record ServiceBehaviour
{
    public double InventoryNeverReplies { get; init; }
    public TimeSpan InventoryMaxDelay { get; init; } = TimeSpan.FromSeconds(20);
    public double PaymentDeclines { get; init; }
    public double PaymentNeverReplies { get; init; }
    public TimeSpan PaymentMaxDelay { get; init; } = TimeSpan.FromSeconds(20);
    public static ServiceBehaviour Reliable { get; } = new();
}

public static class FixtureServices
{
    public static Simulation WithFakeServices(this Simulation sim, ServiceBehaviour? behaviour = null)
    {
        var b = behaviour ?? ServiceBehaviour.Reliable;
        sim.Service<ReserveInventory>((cmd, ctx) =>
        {
            if (ctx.Random.NextDouble() < b.InventoryNeverReplies) return;
            ctx.Reply(new InventoryReserved(cmd.OrderId), Delay(ctx, b.InventoryMaxDelay));
        });
        sim.Service<ChargePayment>((cmd, ctx) =>
        {
            if (ctx.Random.NextDouble() < b.PaymentNeverReplies) return;
            ctx.Reply(ctx.Random.NextDouble() < b.PaymentDeclines
                ? new PaymentDeclined(cmd.OrderId, "insufficient funds")
                : new PaymentCaptured(cmd.OrderId, $"pay-{ctx.Command.MessageId[..8]}"), Delay(ctx, b.PaymentMaxDelay));
        });
        sim.Service<RefundPayment>((_, _) => { });
        return sim;
    }

    private static TimeSpan Delay(ServiceContext ctx, TimeSpan max) => TimeSpan.FromTicks((long)(ctx.Random.NextDouble() * max.Ticks));
}
