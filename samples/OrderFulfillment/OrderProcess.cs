using Bpm.Dsl;
using static Bpm.Dsl.Flow<OrderFulfillment.OrderState>;

namespace OrderFulfillment;

// ---- Messages (correlation is carried by the envelope; OrderId is the correlation id) ----------

public sealed record OrderItem(string Sku, int Quantity);

public sealed record OrderSubmitted(string OrderId, OrderItem[] Items, decimal Total);
public sealed record ReserveInventory(string OrderId, OrderItem[] Items);
public sealed record ChargePayment(string OrderId, decimal Total);
public sealed record RefundPayment(string OrderId, string PaymentId);

public sealed record InventoryReserved(string OrderId);
public sealed record PaymentCaptured(string OrderId, string PaymentId);
public sealed record PaymentDeclined(string OrderId, string Reason);

// ---- Folded state -----------------------------------------------------------------------------

public sealed record OrderState(
    string OrderId,
    OrderItem[] Items,
    decimal Total,
    bool HasInventory = false,
    bool HasPayment = false,
    string? PaymentId = null,
    int Declines = 0);

public static class OrderProcess
{
    /// <summary>
    /// The paper's §3.4 example. Differences from the paper's listing, all required to make it
    /// statically analysable and executable:
    ///  * StartedBy/Evolve declare how the state the predicates read is folded from messages.
    ///  * Continuations are Steps, not lambdas returning Steps (a lambda could pick a step from
    ///    runtime data, which would defeat static analysis).
    ///  * The retry arm says which command to re-issue (`reissue:`) — the paper's table re-sends
    ///    ChargePayment but the listing never says where that command comes from.
    /// </summary>
    [BoundedProcess]
    public static ProcessDefinition Definition { get; } =
        ProcessManager.Define<OrderState>("OrderFulfillment", maxBudget: 10.Minutes())
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
                        Retry(maxAttempts: 2, backoff: 30.Seconds(),
                            step: On<PaymentCaptured>(), onAttemptFailed: On<PaymentDeclined>(),
                            reissue: s => new ChargePayment(s.OrderId, s.Total), name: "Pay"))
                    .Named("InvPay")
                    .WithTimeout(5.Minutes()) // Enforced by type-state builder; verified by AST pass
                    .OnMatched(Complete("Fulfilled"))
                    .OnTimeout(
                        Branch(
                            predicate: s => s.HasPayment && !s.HasInventory,
                            then: Publish(s => new RefundPayment(s.OrderId, s.PaymentId!))
                                .Then(Fail("TimeoutAfterPayment_Refunded")),
                            @else: Fail("TimeoutBeforePayment"))));
}
