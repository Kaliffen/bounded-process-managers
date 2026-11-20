using Bpm.Dsl;
using static Bpm.Dsl.Flow<Shipping.DeliveryState>;

namespace Shipping;

/// <summary>
/// Track a dispatched shipment to the door. Up to three delivery attempts a day apart; a silent
/// carrier triggers a claim; after three failed attempts the parcel goes back to the sender, and if
/// that does not complete within a week it is declared lost.
///
///   T_DAG = 3 attempts x 2d + 2 backoffs x 1d + 7d return window = 15d (budget 21d).
/// </summary>
public static class DeliveryTrackingProcess
{
    [BoundedProcess]
    public static ProcessDefinition Definition { get; } =
        ProcessManager.Define<DeliveryState>("DeliveryTracking", maxBudget: 21.Days())
            .StartedBy<ShipmentDispatched>(m => new DeliveryState(m.ShipmentId, m.Carrier, m.TrackingNumber))
            .Evolve<DeliveryAttemptFailed>((s, _) => s with { FailedAttempts = s.FailedAttempts + 1 })
            .Evolve<Delivered>((s, m) => s with { SignedBy = m.SignedBy })
            .Then(
                Retry(maxAttempts: 3, backoff: 1.Days(), name: "Delivery",
                        attempt: Publish(s => new ScheduleDelivery(s.ShipmentId, s.TrackingNumber, s.FailedAttempts + 1))
                            .Then(Join.Any(
                                    Case<Delivered>(
                                        Publish(s => new NotifyCustomerDelivered(s.ShipmentId, s.SignedBy!))
                                            .Then(Complete("Delivered"))),
                                    Case<DeliveryAttemptFailed>(AttemptFailed()))
                                .Named("Attempt")
                                .WithTimeout(2.Days())
                                .OnTimeout(
                                    Publish(s => new OpenCarrierClaim(s.ShipmentId, s.TrackingNumber, "no delivery report"))
                                        .Then(Fail("CarrierUnresponsive")))))
                    .OnExhausted(
                        Publish(s => new ReturnToSender(s.ShipmentId, s.TrackingNumber))
                            .Then(Join.Any(Case<ReturnedToSender>(Complete("ReturnedToSender")))
                                .Named("Return")
                                .WithTimeout(7.Days())
                                .OnTimeout(
                                    Publish(s => new OpenCarrierClaim(s.ShipmentId, s.TrackingNumber, "lost in return"))
                                        .Then(Fail("LostInTransit"))))));
}
