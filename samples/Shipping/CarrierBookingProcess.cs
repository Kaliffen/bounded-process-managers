using Bpm.Dsl;
using static Bpm.Dsl.Flow<Shipping.ShipmentState>;

namespace Shipping;

/// <summary>
/// Book a carrier: ask, and if the carrier refuses or stays silent, retry (bounded). Once booked,
/// wait out a cooling-off period, then confirm the pickup and wait for the carrier's
/// <see cref="ShipmentDispatched"/>. That same message starts a separate, independently bounded
/// <see cref="DeliveryTrackingProcess"/> instance (paper §9: chained bounded instances).
///
/// The handoff is an explicit, bounded wait: if dispatch is never observed (lost command, lost
/// reply, carrier no-show), this instance escalates instead of silently dropping the shipment.
///
///   T_DAG = 3 attempts x 15m + 2 backoffs x 1m + 30m cooling-off + 1h pickup window = 2h17m.
/// </summary>
public static class CarrierBookingProcess
{
    [BoundedProcess]
    public static ProcessDefinition Definition { get; } =
        ProcessManager.Define<ShipmentState>("CarrierBooking", maxBudget: 3.Hours())
            .StartedBy<ShipmentRequested>(m => new ShipmentState(m.ShipmentId, m.Destination))
            .Evolve<CarrierBooked>((s, m) => s with { Carrier = m.Carrier })
            .Evolve<CarrierUnavailable>((s, _) => s with { Refusals = s.Refusals + 1 })
            .Then(
                Retry(maxAttempts: 3, backoff: 1.Minutes(), name: "Book",
                        attempt: Publish(s => new RequestCarrier(s.ShipmentId, s.Destination))
                            .Then(Join.Any(
                                    Case<CarrierBooked>(
                                        Delay(30.Minutes(), name: "CoolingOff")
                                            .Then(Publish(s => new NotifyPickup(s.ShipmentId, s.Carrier!))
                                                .Then(Join.Any(Case<ShipmentDispatched>(Complete("Dispatched")))
                                                    .Named("Pickup")
                                                    .WithTimeout(1.Hours())
                                                    .OnTimeout(
                                                        Publish(s => new EscalateToOps(s.ShipmentId, s.Refusals))
                                                            .Then(Fail("PickupNotConfirmed")))))),
                                    Case<CarrierUnavailable>(AttemptFailed()))
                                .Named("Quote")
                                .WithTimeout(15.Minutes())
                                .OnTimeout(AttemptFailed())))
                    .OnExhausted(
                        Publish(s => new EscalateToOps(s.ShipmentId, s.Refusals))
                            .Then(Fail("NoCarrierAvailable"))));
}
