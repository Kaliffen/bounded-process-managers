using Bpm.Dsl;
using static Bpm.Dsl.Flow<OrderFulfillment.OrderState>;

namespace OrderFulfillment;

/// <summary>
/// Version 2 of OrderFulfillment: three payment attempts and a 6 minute horizon. Instances started
/// under v1 keep running the v1 table they pinned; new instances start on v2 (paper §7.1).
/// </summary>
public static class OrderProcessV2
{
    [BoundedProcess]
    public static ProcessDefinition Definition { get; } =
        ProcessManager.Define<OrderState>("OrderFulfillment", maxBudget: 10.Minutes(), version: 2)
            .StartedBy<OrderSubmitted>(m => new OrderState(m.OrderId, m.Items, m.Total))
            .Evolve<InventoryReserved>((s, _) => s with { HasInventory = true })
            .Evolve<PaymentCaptured>((s, m) => s with { HasPayment = true, PaymentId = m.PaymentId })
            .Evolve<PaymentDeclined>((s, _) => s with { Declines = s.Declines + 1 })
            .StartWith(ctx => [
                new ReserveInventory(ctx.OrderId, ctx.Items),
                new ChargePayment(ctx.OrderId, ctx.Total),
            ])
            .Then(
                Join.All(
                        On<InventoryReserved>("Inv"),
                        Retry(maxAttempts: 3, backoff: 30.Seconds(),
                            step: On<PaymentCaptured>(), onAttemptFailed: On<PaymentDeclined>(),
                            reissue: s => new ChargePayment(s.OrderId, s.Total), name: "Pay"))
                    .Named("InvPay")
                    .WithTimeout(6.Minutes())
                    .OnMatched(Complete("Fulfilled"))
                    .OnTimeout(
                        Branch(
                            predicate: s => s.HasPayment && !s.HasInventory,
                            then: Publish(s => new RefundPayment(s.OrderId, s.PaymentId!))
                                .Then(Fail("TimeoutAfterPayment_Refunded")),
                            @else: Fail("TimeoutBeforePayment"))));
}
