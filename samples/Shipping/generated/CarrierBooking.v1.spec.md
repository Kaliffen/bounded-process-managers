# CarrierBooking v1 — generated behaviour specification

22 scenario(s). Kill/Abort is accepted in every non-terminal state and is not repeated per scenario.

## Scenario 1: ends Failed "NoCarrierAvailable"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** timer `Quote#1.T` fires
  - **Then** `ScheduleTimer(Book.backoff1~2.T, 1m)`
- **When** timer `Book.backoff1~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2~2.T, 15m)`
- **When** timer `Quote#2~2.T` fires
  - **Then** `ScheduleTimer(Book.backoff2~4.T, 1m)`
- **When** timer `Book.backoff2~4.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~4.T, 15m)`
- **When** timer `Quote#3~4.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("NoCarrierAvailable")`

## Scenario 2: ends Failed "PickupNotConfirmed"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** timer `Quote#1.T` fires
  - **Then** `ScheduleTimer(Book.backoff1~2.T, 1m)`
- **When** timer `Book.backoff1~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2~2.T, 15m)`
- **When** timer `Quote#2~2.T` fires
  - **Then** `ScheduleTimer(Book.backoff2~4.T, 1m)`
- **When** timer `Book.backoff2~4.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~4.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#3~4.T)`, `ScheduleTimer(CoolingOff#3~4.T, 30m)`
- **When** timer `CoolingOff#3~4.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#3~4.T, 1h)`
- **When** timer `Pickup#3~4.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("PickupNotConfirmed")`

## Scenario 3: ends Completed "Dispatched"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** timer `Quote#1.T` fires
  - **Then** `ScheduleTimer(Book.backoff1~2.T, 1m)`
- **When** timer `Book.backoff1~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2~2.T, 15m)`
- **When** timer `Quote#2~2.T` fires
  - **Then** `ScheduleTimer(Book.backoff2~4.T, 1m)`
- **When** timer `Book.backoff2~4.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~4.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#3~4.T)`, `ScheduleTimer(CoolingOff#3~4.T, 30m)`
- **When** timer `CoolingOff#3~4.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#3~4.T, 1h)`
- **When** `ShipmentDispatched` is received
  - **Then** `CancelTimer(Pickup#3~4.T)`, `Completed("Dispatched")`

## Scenario 4: ends Failed "NoCarrierAvailable"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** timer `Quote#1.T` fires
  - **Then** `ScheduleTimer(Book.backoff1~2.T, 1m)`
- **When** timer `Book.backoff1~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2~2.T, 15m)`
- **When** timer `Quote#2~2.T` fires
  - **Then** `ScheduleTimer(Book.backoff2~4.T, 1m)`
- **When** timer `Book.backoff2~4.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~4.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#3~4.T)`, `Publish EscalateToOps`, `Failed("NoCarrierAvailable")`

## Scenario 5: ends Failed "PickupNotConfirmed"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** timer `Quote#1.T` fires
  - **Then** `ScheduleTimer(Book.backoff1~2.T, 1m)`
- **When** timer `Book.backoff1~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2~2.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#2~2.T)`, `ScheduleTimer(CoolingOff#2~2.T, 30m)`
- **When** timer `CoolingOff#2~2.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#2~2.T, 1h)`
- **When** timer `Pickup#2~2.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("PickupNotConfirmed")`

## Scenario 6: ends Completed "Dispatched"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** timer `Quote#1.T` fires
  - **Then** `ScheduleTimer(Book.backoff1~2.T, 1m)`
- **When** timer `Book.backoff1~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2~2.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#2~2.T)`, `ScheduleTimer(CoolingOff#2~2.T, 30m)`
- **When** timer `CoolingOff#2~2.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#2~2.T, 1h)`
- **When** `ShipmentDispatched` is received
  - **Then** `CancelTimer(Pickup#2~2.T)`, `Completed("Dispatched")`

## Scenario 7: ends Failed "NoCarrierAvailable"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** timer `Quote#1.T` fires
  - **Then** `ScheduleTimer(Book.backoff1~2.T, 1m)`
- **When** timer `Book.backoff1~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2~2.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#2~2.T)`, `ScheduleTimer(Book.backoff2~3.T, 1m)`
- **When** timer `Book.backoff2~3.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~3.T, 15m)`
- **When** timer `Quote#3~3.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("NoCarrierAvailable")`

## Scenario 8: ends Failed "PickupNotConfirmed"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** timer `Quote#1.T` fires
  - **Then** `ScheduleTimer(Book.backoff1~2.T, 1m)`
- **When** timer `Book.backoff1~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2~2.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#2~2.T)`, `ScheduleTimer(Book.backoff2~3.T, 1m)`
- **When** timer `Book.backoff2~3.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~3.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#3~3.T)`, `ScheduleTimer(CoolingOff#3~3.T, 30m)`
- **When** timer `CoolingOff#3~3.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#3~3.T, 1h)`
- **When** timer `Pickup#3~3.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("PickupNotConfirmed")`

## Scenario 9: ends Completed "Dispatched"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** timer `Quote#1.T` fires
  - **Then** `ScheduleTimer(Book.backoff1~2.T, 1m)`
- **When** timer `Book.backoff1~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2~2.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#2~2.T)`, `ScheduleTimer(Book.backoff2~3.T, 1m)`
- **When** timer `Book.backoff2~3.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~3.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#3~3.T)`, `ScheduleTimer(CoolingOff#3~3.T, 30m)`
- **When** timer `CoolingOff#3~3.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#3~3.T, 1h)`
- **When** `ShipmentDispatched` is received
  - **Then** `CancelTimer(Pickup#3~3.T)`, `Completed("Dispatched")`

## Scenario 10: ends Failed "NoCarrierAvailable"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** timer `Quote#1.T` fires
  - **Then** `ScheduleTimer(Book.backoff1~2.T, 1m)`
- **When** timer `Book.backoff1~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2~2.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#2~2.T)`, `ScheduleTimer(Book.backoff2~3.T, 1m)`
- **When** timer `Book.backoff2~3.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~3.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#3~3.T)`, `Publish EscalateToOps`, `Failed("NoCarrierAvailable")`

## Scenario 11: ends Failed "PickupNotConfirmed"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(CoolingOff#1.T, 30m)`
- **When** timer `CoolingOff#1.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#1.T, 1h)`
- **When** timer `Pickup#1.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("PickupNotConfirmed")`

## Scenario 12: ends Completed "Dispatched"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(CoolingOff#1.T, 30m)`
- **When** timer `CoolingOff#1.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#1.T, 1h)`
- **When** `ShipmentDispatched` is received
  - **Then** `CancelTimer(Pickup#1.T)`, `Completed("Dispatched")`

## Scenario 13: ends Failed "NoCarrierAvailable"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(Book.backoff1.T, 1m)`
- **When** timer `Book.backoff1.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2.T, 15m)`
- **When** timer `Quote#2.T` fires
  - **Then** `ScheduleTimer(Book.backoff2~2.T, 1m)`
- **When** timer `Book.backoff2~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~2.T, 15m)`
- **When** timer `Quote#3~2.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("NoCarrierAvailable")`

## Scenario 14: ends Failed "PickupNotConfirmed"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(Book.backoff1.T, 1m)`
- **When** timer `Book.backoff1.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2.T, 15m)`
- **When** timer `Quote#2.T` fires
  - **Then** `ScheduleTimer(Book.backoff2~2.T, 1m)`
- **When** timer `Book.backoff2~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~2.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#3~2.T)`, `ScheduleTimer(CoolingOff#3~2.T, 30m)`
- **When** timer `CoolingOff#3~2.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#3~2.T, 1h)`
- **When** timer `Pickup#3~2.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("PickupNotConfirmed")`

## Scenario 15: ends Completed "Dispatched"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(Book.backoff1.T, 1m)`
- **When** timer `Book.backoff1.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2.T, 15m)`
- **When** timer `Quote#2.T` fires
  - **Then** `ScheduleTimer(Book.backoff2~2.T, 1m)`
- **When** timer `Book.backoff2~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~2.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#3~2.T)`, `ScheduleTimer(CoolingOff#3~2.T, 30m)`
- **When** timer `CoolingOff#3~2.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#3~2.T, 1h)`
- **When** `ShipmentDispatched` is received
  - **Then** `CancelTimer(Pickup#3~2.T)`, `Completed("Dispatched")`

## Scenario 16: ends Failed "NoCarrierAvailable"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(Book.backoff1.T, 1m)`
- **When** timer `Book.backoff1.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2.T, 15m)`
- **When** timer `Quote#2.T` fires
  - **Then** `ScheduleTimer(Book.backoff2~2.T, 1m)`
- **When** timer `Book.backoff2~2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3~2.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#3~2.T)`, `Publish EscalateToOps`, `Failed("NoCarrierAvailable")`

## Scenario 17: ends Failed "PickupNotConfirmed"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(Book.backoff1.T, 1m)`
- **When** timer `Book.backoff1.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#2.T)`, `ScheduleTimer(CoolingOff#2.T, 30m)`
- **When** timer `CoolingOff#2.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#2.T, 1h)`
- **When** timer `Pickup#2.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("PickupNotConfirmed")`

## Scenario 18: ends Completed "Dispatched"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(Book.backoff1.T, 1m)`
- **When** timer `Book.backoff1.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#2.T)`, `ScheduleTimer(CoolingOff#2.T, 30m)`
- **When** timer `CoolingOff#2.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#2.T, 1h)`
- **When** `ShipmentDispatched` is received
  - **Then** `CancelTimer(Pickup#2.T)`, `Completed("Dispatched")`

## Scenario 19: ends Failed "NoCarrierAvailable"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(Book.backoff1.T, 1m)`
- **When** timer `Book.backoff1.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#2.T)`, `ScheduleTimer(Book.backoff2.T, 1m)`
- **When** timer `Book.backoff2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3.T, 15m)`
- **When** timer `Quote#3.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("NoCarrierAvailable")`

## Scenario 20: ends Failed "PickupNotConfirmed"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(Book.backoff1.T, 1m)`
- **When** timer `Book.backoff1.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#2.T)`, `ScheduleTimer(Book.backoff2.T, 1m)`
- **When** timer `Book.backoff2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#3.T)`, `ScheduleTimer(CoolingOff#3.T, 30m)`
- **When** timer `CoolingOff#3.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#3.T, 1h)`
- **When** timer `Pickup#3.T` fires
  - **Then** `Publish EscalateToOps`, `Failed("PickupNotConfirmed")`

## Scenario 21: ends Completed "Dispatched"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(Book.backoff1.T, 1m)`
- **When** timer `Book.backoff1.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#2.T)`, `ScheduleTimer(Book.backoff2.T, 1m)`
- **When** timer `Book.backoff2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3.T, 15m)`
- **When** `CarrierBooked` is received
  - **Then** `CancelTimer(Quote#3.T)`, `ScheduleTimer(CoolingOff#3.T, 30m)`
- **When** timer `CoolingOff#3.T` fires
  - **Then** `Publish NotifyPickup`, `ScheduleTimer(Pickup#3.T, 1h)`
- **When** `ShipmentDispatched` is received
  - **Then** `CancelTimer(Pickup#3.T)`, `Completed("Dispatched")`

## Scenario 22: ends Failed "NoCarrierAvailable"

- **Given** no CarrierBooking instance exists for the correlation id
- **When** `ShipmentRequested` is received
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#1.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#1.T)`, `ScheduleTimer(Book.backoff1.T, 1m)`
- **When** timer `Book.backoff1.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#2.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#2.T)`, `ScheduleTimer(Book.backoff2.T, 1m)`
- **When** timer `Book.backoff2.T` fires
  - **Then** `Publish RequestCarrier`, `ScheduleTimer(Quote#3.T, 15m)`
- **When** `CarrierUnavailable` is received
  - **Then** `CancelTimer(Quote#3.T)`, `Publish EscalateToOps`, `Failed("NoCarrierAvailable")`
