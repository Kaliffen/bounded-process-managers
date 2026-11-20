# DeliveryTracking v1 — generated behaviour specification

8 scenario(s). Kill/Abort is accepted in every non-terminal state and is not repeated per scenario.

## Scenario 1: ends Failed "CarrierUnresponsive"

- **Given** no DeliveryTracking instance exists for the correlation id
- **When** `ShipmentDispatched` is received
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#1.T, 2d)`
- **When** timer `Attempt#1.T` fires
  - **Then** `Publish OpenCarrierClaim`, `Failed("CarrierUnresponsive")`

## Scenario 2: ends Completed "Delivered"

- **Given** no DeliveryTracking instance exists for the correlation id
- **When** `ShipmentDispatched` is received
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#1.T, 2d)`
- **When** `Delivered` is received
  - **Then** `CancelTimer(Attempt#1.T)`, `Publish NotifyCustomerDelivered`, `Completed("Delivered")`

## Scenario 3: ends Failed "CarrierUnresponsive"

- **Given** no DeliveryTracking instance exists for the correlation id
- **When** `ShipmentDispatched` is received
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#1.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#1.T)`, `ScheduleTimer(Delivery.backoff1.T, 1d)`
- **When** timer `Delivery.backoff1.T` fires
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#2.T, 2d)`
- **When** timer `Attempt#2.T` fires
  - **Then** `Publish OpenCarrierClaim`, `Failed("CarrierUnresponsive")`

## Scenario 4: ends Completed "Delivered"

- **Given** no DeliveryTracking instance exists for the correlation id
- **When** `ShipmentDispatched` is received
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#1.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#1.T)`, `ScheduleTimer(Delivery.backoff1.T, 1d)`
- **When** timer `Delivery.backoff1.T` fires
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#2.T, 2d)`
- **When** `Delivered` is received
  - **Then** `CancelTimer(Attempt#2.T)`, `Publish NotifyCustomerDelivered`, `Completed("Delivered")`

## Scenario 5: ends Failed "CarrierUnresponsive"

- **Given** no DeliveryTracking instance exists for the correlation id
- **When** `ShipmentDispatched` is received
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#1.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#1.T)`, `ScheduleTimer(Delivery.backoff1.T, 1d)`
- **When** timer `Delivery.backoff1.T` fires
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#2.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#2.T)`, `ScheduleTimer(Delivery.backoff2.T, 1d)`
- **When** timer `Delivery.backoff2.T` fires
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#3.T, 2d)`
- **When** timer `Attempt#3.T` fires
  - **Then** `Publish OpenCarrierClaim`, `Failed("CarrierUnresponsive")`

## Scenario 6: ends Completed "Delivered"

- **Given** no DeliveryTracking instance exists for the correlation id
- **When** `ShipmentDispatched` is received
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#1.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#1.T)`, `ScheduleTimer(Delivery.backoff1.T, 1d)`
- **When** timer `Delivery.backoff1.T` fires
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#2.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#2.T)`, `ScheduleTimer(Delivery.backoff2.T, 1d)`
- **When** timer `Delivery.backoff2.T` fires
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#3.T, 2d)`
- **When** `Delivered` is received
  - **Then** `CancelTimer(Attempt#3.T)`, `Publish NotifyCustomerDelivered`, `Completed("Delivered")`

## Scenario 7: ends Failed "LostInTransit"

- **Given** no DeliveryTracking instance exists for the correlation id
- **When** `ShipmentDispatched` is received
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#1.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#1.T)`, `ScheduleTimer(Delivery.backoff1.T, 1d)`
- **When** timer `Delivery.backoff1.T` fires
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#2.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#2.T)`, `ScheduleTimer(Delivery.backoff2.T, 1d)`
- **When** timer `Delivery.backoff2.T` fires
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#3.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#3.T)`, `Publish ReturnToSender`, `ScheduleTimer(Return.T, 7d)`
- **When** timer `Return.T` fires
  - **Then** `Publish OpenCarrierClaim`, `Failed("LostInTransit")`

## Scenario 8: ends Completed "ReturnedToSender"

- **Given** no DeliveryTracking instance exists for the correlation id
- **When** `ShipmentDispatched` is received
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#1.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#1.T)`, `ScheduleTimer(Delivery.backoff1.T, 1d)`
- **When** timer `Delivery.backoff1.T` fires
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#2.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#2.T)`, `ScheduleTimer(Delivery.backoff2.T, 1d)`
- **When** timer `Delivery.backoff2.T` fires
  - **Then** `Publish ScheduleDelivery`, `ScheduleTimer(Attempt#3.T, 2d)`
- **When** `DeliveryAttemptFailed` is received
  - **Then** `CancelTimer(Attempt#3.T)`, `Publish ReturnToSender`, `ScheduleTimer(Return.T, 7d)`
- **When** `ReturnedToSender` is received
  - **Then** `CancelTimer(Return.T)`, `Completed("ReturnedToSender")`
