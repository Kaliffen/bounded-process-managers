using Bpm.Runtime;
using Bpm.Testing;
using OrderFulfillment;
using Shipping;

namespace Demo;

/// <summary>How the simulated external world behaves. Probabilities are per command.</summary>
public sealed record WorldBehaviour
{
    public double InventoryNeverReplies { get; init; }
    public TimeSpan InventoryMaxDelay { get; init; } = TimeSpan.FromSeconds(20);
    public double PaymentDeclines { get; init; }
    public double PaymentNeverReplies { get; init; }
    public TimeSpan PaymentMaxDelay { get; init; } = TimeSpan.FromSeconds(20);
    public double CarrierRefuses { get; init; }
    public double CarrierSilent { get; init; }
    public TimeSpan CarrierMaxDelay { get; init; } = TimeSpan.FromMinutes(5);
    public double DeliveryFails { get; init; }
    public TimeSpan DeliveryMaxDelay { get; init; } = TimeSpan.FromHours(20);

    public static WorldBehaviour Reliable { get; } = new();
}

/// <summary>Stand-ins for the independently owned inventory, payment and carrier systems.</summary>
public static class FakeServices
{
    public static Simulation WithFakeWorld(this Simulation sim, WorldBehaviour? behaviour = null)
    {
        var b = behaviour ?? WorldBehaviour.Reliable;

        // ---- order context's neighbours
        sim.Service<ReserveInventory>((cmd, ctx) =>
        {
            if (Roll(ctx, b.InventoryNeverReplies)) return;
            ctx.Reply(new InventoryReserved(cmd.OrderId), Delay(ctx, b.InventoryMaxDelay));
        });
        sim.Service<ChargePayment>((cmd, ctx) =>
        {
            if (Roll(ctx, b.PaymentNeverReplies)) return;
            ctx.Reply(Roll(ctx, b.PaymentDeclines)
                ? new PaymentDeclined(cmd.OrderId, "insufficient funds")
                : new PaymentCaptured(cmd.OrderId, $"pay-{ctx.Command.MessageId[..8]}"), Delay(ctx, b.PaymentMaxDelay));
        });
        sim.Service<RefundPayment>((_, _) => { });

        // ---- shipping context's neighbours
        sim.Service<RequestCarrier>((cmd, ctx) =>
        {
            if (Roll(ctx, b.CarrierSilent)) return;
            ctx.Reply(Roll(ctx, b.CarrierRefuses)
                ? new CarrierUnavailable(cmd.ShipmentId)
                : new CarrierBooked(cmd.ShipmentId, "ACME Freight"), Delay(ctx, b.CarrierMaxDelay));
        });
        sim.Service<NotifyPickup>((cmd, ctx) =>
            ctx.Reply(new ShipmentDispatched(cmd.ShipmentId, cmd.Carrier, $"TRK-{ctx.Command.MessageId[..8]}"), TimeSpan.FromMinutes(10)));
        sim.Service<ScheduleDelivery>((cmd, ctx) =>
            ctx.Reply(Roll(ctx, b.DeliveryFails)
                ? new DeliveryAttemptFailed(cmd.ShipmentId, "nobody home")
                : new Delivered(cmd.ShipmentId, "J. Doe"), Delay(ctx, b.DeliveryMaxDelay)));
        sim.Service<ReturnToSender>((cmd, ctx) => ctx.Reply(new ReturnedToSender(cmd.ShipmentId), TimeSpan.FromDays(3)));
        sim.Service<EscalateToOps>((_, _) => { });
        sim.Service<NotifyCustomerDelivered>((_, _) => { });
        sim.Service<OpenCarrierClaim>((_, _) => { });
        return sim;
    }

    /// <summary>
    /// Context integration lives in the host, not in either process manager: a fulfilled order
    /// requests a shipment. Neither domain references the other.
    /// </summary>
    public static Simulation WithOrderToShippingIntegration(this Simulation sim)
    {
        sim.ProcessEnded += (ended, envelope) =>
        {
            if (ended is { Definition: "OrderFulfillment", Outcome: "Fulfilled" })
            {
                var orderId = envelope.CorrelationId;
                sim.Inject(new ShipmentRequested($"ship-{orderId}", "customer address"), $"ship-{orderId}", TimeSpan.Zero,
                    messageId: DeterministicId.For($"integration:{envelope.MessageId}"));
            }
        };
        return sim;
    }

    private static bool Roll(ServiceContext ctx, double p) => ctx.Random.NextDouble() < p;
    private static TimeSpan Delay(ServiceContext ctx, TimeSpan max) => TimeSpan.FromTicks((long)(ctx.Random.NextDouble() * max.Ticks));
}
