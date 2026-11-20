using Bpm.Testing;

namespace Shipping.Tests;

/// <summary>How the simulated carrier behaves. Probabilities are per command.</summary>
public sealed record CarrierBehaviour
{
    public double Refuses { get; init; }
    public double SilentOnBooking { get; init; }
    public TimeSpan BookingMaxDelay { get; init; } = TimeSpan.FromMinutes(5);
    public double DeliveryFails { get; init; }
    public double SilentOnDelivery { get; init; }
    public TimeSpan DeliveryMaxDelay { get; init; } = TimeSpan.FromHours(20);
    public double ReturnLost { get; init; }

    public static CarrierBehaviour Reliable { get; } = new();
}

/// <summary>The external world of the shipping context: the carrier and its couriers.</summary>
public static class ShippingFakes
{
    public static Simulation WithCarrier(this Simulation sim, CarrierBehaviour? behaviour = null)
    {
        var b = behaviour ?? CarrierBehaviour.Reliable;

        sim.Service<RequestCarrier>((cmd, ctx) =>
        {
            if (ctx.Random.NextDouble() < b.SilentOnBooking) return;
            ctx.Reply(ctx.Random.NextDouble() < b.Refuses
                ? new CarrierUnavailable(cmd.ShipmentId)
                : new CarrierBooked(cmd.ShipmentId, "ACME Freight"), Delay(ctx, b.BookingMaxDelay));
        });

        // Pickup confirmed: the carrier dispatches, which starts DeliveryTracking.
        sim.Service<NotifyPickup>((cmd, ctx) =>
            ctx.Reply(new ShipmentDispatched(cmd.ShipmentId, cmd.Carrier, $"TRK-{ctx.Command.MessageId[..8]}"), TimeSpan.FromMinutes(10)));

        sim.Service<ScheduleDelivery>((cmd, ctx) =>
        {
            if (ctx.Random.NextDouble() < b.SilentOnDelivery) return;
            ctx.Reply(ctx.Random.NextDouble() < b.DeliveryFails
                ? new DeliveryAttemptFailed(cmd.ShipmentId, "nobody home")
                : new Delivered(cmd.ShipmentId, "J. Doe"), Delay(ctx, b.DeliveryMaxDelay));
        });

        sim.Service<ReturnToSender>((cmd, ctx) =>
        {
            if (ctx.Random.NextDouble() < b.ReturnLost) return;
            ctx.Reply(new ReturnedToSender(cmd.ShipmentId), TimeSpan.FromDays(3));
        });

        sim.Service<EscalateToOps>((_, _) => { });
        sim.Service<NotifyCustomerDelivered>((_, _) => { });
        sim.Service<OpenCarrierClaim>((_, _) => { });
        return sim;
    }

    private static TimeSpan Delay(ServiceContext ctx, TimeSpan max) => TimeSpan.FromTicks((long)(ctx.Random.NextDouble() * max.Ticks));
}
