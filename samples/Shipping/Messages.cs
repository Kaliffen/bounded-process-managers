namespace Shipping;

// Shipping bounded context. Correlation id = shipment id (carried by the envelope).

// ---- Carrier booking -----------------------------------------------------------------------------
public sealed record ShipmentRequested(string ShipmentId, string Destination);
public sealed record RequestCarrier(string ShipmentId, string Destination);
public sealed record CarrierBooked(string ShipmentId, string Carrier);
public sealed record CarrierUnavailable(string ShipmentId);
public sealed record NotifyPickup(string ShipmentId, string Carrier);
public sealed record EscalateToOps(string ShipmentId, int Refusals);

// ---- Delivery tracking ---------------------------------------------------------------------------
/// <summary>Published by the carrier after pickup; starts a DeliveryTracking instance.</summary>
public sealed record ShipmentDispatched(string ShipmentId, string Carrier, string TrackingNumber);
public sealed record ScheduleDelivery(string ShipmentId, string TrackingNumber, int Attempt);
public sealed record Delivered(string ShipmentId, string SignedBy);
public sealed record DeliveryAttemptFailed(string ShipmentId, string Reason);
public sealed record NotifyCustomerDelivered(string ShipmentId, string SignedBy);
public sealed record ReturnToSender(string ShipmentId, string TrackingNumber);
public sealed record ReturnedToSender(string ShipmentId);
public sealed record OpenCarrierClaim(string ShipmentId, string TrackingNumber, string Reason);

// ---- Folded state --------------------------------------------------------------------------------
public sealed record ShipmentState(string ShipmentId, string Destination, string? Carrier = null, int Refusals = 0);

public sealed record DeliveryState(
    string ShipmentId,
    string Carrier,
    string TrackingNumber,
    int FailedAttempts = 0,
    string? SignedBy = null);
