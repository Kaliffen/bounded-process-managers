# CarrierBooking v1 — transition table

Table hash `cb54ca44e5d34a2e` · 32 states · 76 transitions · T_DAG = **2h17m**

| # | Current state | Accepted input | Guard | Next state | Emitted effects |
|---|---|---|---|---|---|
| 0 | `Init` | ShipmentRequested | -- | `Quote#1` | Publish RequestCarrier; ScheduleTimer(Quote#1.T, 15m) |
| 1 | `Quote#1` | TimerFired(Quote#1.T) | -- | `Book.backoff1~2` | ScheduleTimer(Book.backoff1~2.T, 1m) |
| 2 | `Quote#1` | CarrierBooked | -- | `CoolingOff#1` | CancelTimer(Quote#1.T); ScheduleTimer(CoolingOff#1.T, 30m) |
| 3 | `Quote#1` | CarrierUnavailable | -- | `Book.backoff1` | CancelTimer(Quote#1.T); ScheduleTimer(Book.backoff1.T, 1m) |
| 4 | `Quote#1` | Kill | -- | `Aborted(Killed)` | CancelTimer(Quote#1.T); Aborted("Killed") |
| 5 | `Book.backoff1~2` | TimerFired(Book.backoff1~2.T) | -- | `Quote#2~2` | Publish RequestCarrier; ScheduleTimer(Quote#2~2.T, 15m) |
| 6 | `Book.backoff1~2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Book.backoff1~2.T); Aborted("Killed") |
| 7 | `CoolingOff#1` | TimerFired(CoolingOff#1.T) | -- | `Pickup#1` | Publish NotifyPickup; ScheduleTimer(Pickup#1.T, 1h) |
| 8 | `CoolingOff#1` | Kill | -- | `Aborted(Killed)` | CancelTimer(CoolingOff#1.T); Aborted("Killed") |
| 9 | `Book.backoff1` | TimerFired(Book.backoff1.T) | -- | `Quote#2` | Publish RequestCarrier; ScheduleTimer(Quote#2.T, 15m) |
| 10 | `Book.backoff1` | Kill | -- | `Aborted(Killed)` | CancelTimer(Book.backoff1.T); Aborted("Killed") |
| 11 | `Quote#2~2` | TimerFired(Quote#2~2.T) | -- | `Book.backoff2~4` | ScheduleTimer(Book.backoff2~4.T, 1m) |
| 12 | `Quote#2~2` | CarrierBooked | -- | `CoolingOff#2~2` | CancelTimer(Quote#2~2.T); ScheduleTimer(CoolingOff#2~2.T, 30m) |
| 13 | `Quote#2~2` | CarrierUnavailable | -- | `Book.backoff2~3` | CancelTimer(Quote#2~2.T); ScheduleTimer(Book.backoff2~3.T, 1m) |
| 14 | `Quote#2~2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Quote#2~2.T); Aborted("Killed") |
| 15 | `Pickup#1` | TimerFired(Pickup#1.T) | -- | `Failed(PickupNotConfirmed)` | Publish EscalateToOps; Failed("PickupNotConfirmed") |
| 16 | `Pickup#1` | ShipmentDispatched | -- | `Completed(Dispatched)` | CancelTimer(Pickup#1.T); Completed("Dispatched") |
| 17 | `Pickup#1` | Kill | -- | `Aborted(Killed)` | CancelTimer(Pickup#1.T); Aborted("Killed") |
| 18 | `Quote#2` | TimerFired(Quote#2.T) | -- | `Book.backoff2~2` | ScheduleTimer(Book.backoff2~2.T, 1m) |
| 19 | `Quote#2` | CarrierBooked | -- | `CoolingOff#2` | CancelTimer(Quote#2.T); ScheduleTimer(CoolingOff#2.T, 30m) |
| 20 | `Quote#2` | CarrierUnavailable | -- | `Book.backoff2` | CancelTimer(Quote#2.T); ScheduleTimer(Book.backoff2.T, 1m) |
| 21 | `Quote#2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Quote#2.T); Aborted("Killed") |
| 22 | `Book.backoff2~4` | TimerFired(Book.backoff2~4.T) | -- | `Quote#3~4` | Publish RequestCarrier; ScheduleTimer(Quote#3~4.T, 15m) |
| 23 | `Book.backoff2~4` | Kill | -- | `Aborted(Killed)` | CancelTimer(Book.backoff2~4.T); Aborted("Killed") |
| 24 | `CoolingOff#2~2` | TimerFired(CoolingOff#2~2.T) | -- | `Pickup#2~2` | Publish NotifyPickup; ScheduleTimer(Pickup#2~2.T, 1h) |
| 25 | `CoolingOff#2~2` | Kill | -- | `Aborted(Killed)` | CancelTimer(CoolingOff#2~2.T); Aborted("Killed") |
| 26 | `Book.backoff2~3` | TimerFired(Book.backoff2~3.T) | -- | `Quote#3~3` | Publish RequestCarrier; ScheduleTimer(Quote#3~3.T, 15m) |
| 27 | `Book.backoff2~3` | Kill | -- | `Aborted(Killed)` | CancelTimer(Book.backoff2~3.T); Aborted("Killed") |
| 28 | `Book.backoff2~2` | TimerFired(Book.backoff2~2.T) | -- | `Quote#3~2` | Publish RequestCarrier; ScheduleTimer(Quote#3~2.T, 15m) |
| 29 | `Book.backoff2~2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Book.backoff2~2.T); Aborted("Killed") |
| 30 | `CoolingOff#2` | TimerFired(CoolingOff#2.T) | -- | `Pickup#2` | Publish NotifyPickup; ScheduleTimer(Pickup#2.T, 1h) |
| 31 | `CoolingOff#2` | Kill | -- | `Aborted(Killed)` | CancelTimer(CoolingOff#2.T); Aborted("Killed") |
| 32 | `Book.backoff2` | TimerFired(Book.backoff2.T) | -- | `Quote#3` | Publish RequestCarrier; ScheduleTimer(Quote#3.T, 15m) |
| 33 | `Book.backoff2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Book.backoff2.T); Aborted("Killed") |
| 34 | `Quote#3~4` | TimerFired(Quote#3~4.T) | -- | `Failed(NoCarrierAvailable)` | Publish EscalateToOps; Failed("NoCarrierAvailable") |
| 35 | `Quote#3~4` | CarrierBooked | -- | `CoolingOff#3~4` | CancelTimer(Quote#3~4.T); ScheduleTimer(CoolingOff#3~4.T, 30m) |
| 36 | `Quote#3~4` | CarrierUnavailable | -- | `Failed(NoCarrierAvailable)` | CancelTimer(Quote#3~4.T); Publish EscalateToOps; Failed("NoCarrierAvailable") |
| 37 | `Quote#3~4` | Kill | -- | `Aborted(Killed)` | CancelTimer(Quote#3~4.T); Aborted("Killed") |
| 38 | `Pickup#2~2` | TimerFired(Pickup#2~2.T) | -- | `Failed(PickupNotConfirmed)` | Publish EscalateToOps; Failed("PickupNotConfirmed") |
| 39 | `Pickup#2~2` | ShipmentDispatched | -- | `Completed(Dispatched)` | CancelTimer(Pickup#2~2.T); Completed("Dispatched") |
| 40 | `Pickup#2~2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Pickup#2~2.T); Aborted("Killed") |
| 41 | `Quote#3~3` | TimerFired(Quote#3~3.T) | -- | `Failed(NoCarrierAvailable)` | Publish EscalateToOps; Failed("NoCarrierAvailable") |
| 42 | `Quote#3~3` | CarrierBooked | -- | `CoolingOff#3~3` | CancelTimer(Quote#3~3.T); ScheduleTimer(CoolingOff#3~3.T, 30m) |
| 43 | `Quote#3~3` | CarrierUnavailable | -- | `Failed(NoCarrierAvailable)` | CancelTimer(Quote#3~3.T); Publish EscalateToOps; Failed("NoCarrierAvailable") |
| 44 | `Quote#3~3` | Kill | -- | `Aborted(Killed)` | CancelTimer(Quote#3~3.T); Aborted("Killed") |
| 45 | `Quote#3~2` | TimerFired(Quote#3~2.T) | -- | `Failed(NoCarrierAvailable)` | Publish EscalateToOps; Failed("NoCarrierAvailable") |
| 46 | `Quote#3~2` | CarrierBooked | -- | `CoolingOff#3~2` | CancelTimer(Quote#3~2.T); ScheduleTimer(CoolingOff#3~2.T, 30m) |
| 47 | `Quote#3~2` | CarrierUnavailable | -- | `Failed(NoCarrierAvailable)` | CancelTimer(Quote#3~2.T); Publish EscalateToOps; Failed("NoCarrierAvailable") |
| 48 | `Quote#3~2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Quote#3~2.T); Aborted("Killed") |
| 49 | `Pickup#2` | TimerFired(Pickup#2.T) | -- | `Failed(PickupNotConfirmed)` | Publish EscalateToOps; Failed("PickupNotConfirmed") |
| 50 | `Pickup#2` | ShipmentDispatched | -- | `Completed(Dispatched)` | CancelTimer(Pickup#2.T); Completed("Dispatched") |
| 51 | `Pickup#2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Pickup#2.T); Aborted("Killed") |
| 52 | `Quote#3` | TimerFired(Quote#3.T) | -- | `Failed(NoCarrierAvailable)` | Publish EscalateToOps; Failed("NoCarrierAvailable") |
| 53 | `Quote#3` | CarrierBooked | -- | `CoolingOff#3` | CancelTimer(Quote#3.T); ScheduleTimer(CoolingOff#3.T, 30m) |
| 54 | `Quote#3` | CarrierUnavailable | -- | `Failed(NoCarrierAvailable)` | CancelTimer(Quote#3.T); Publish EscalateToOps; Failed("NoCarrierAvailable") |
| 55 | `Quote#3` | Kill | -- | `Aborted(Killed)` | CancelTimer(Quote#3.T); Aborted("Killed") |
| 56 | `CoolingOff#3~4` | TimerFired(CoolingOff#3~4.T) | -- | `Pickup#3~4` | Publish NotifyPickup; ScheduleTimer(Pickup#3~4.T, 1h) |
| 57 | `CoolingOff#3~4` | Kill | -- | `Aborted(Killed)` | CancelTimer(CoolingOff#3~4.T); Aborted("Killed") |
| 58 | `CoolingOff#3~3` | TimerFired(CoolingOff#3~3.T) | -- | `Pickup#3~3` | Publish NotifyPickup; ScheduleTimer(Pickup#3~3.T, 1h) |
| 59 | `CoolingOff#3~3` | Kill | -- | `Aborted(Killed)` | CancelTimer(CoolingOff#3~3.T); Aborted("Killed") |
| 60 | `CoolingOff#3~2` | TimerFired(CoolingOff#3~2.T) | -- | `Pickup#3~2` | Publish NotifyPickup; ScheduleTimer(Pickup#3~2.T, 1h) |
| 61 | `CoolingOff#3~2` | Kill | -- | `Aborted(Killed)` | CancelTimer(CoolingOff#3~2.T); Aborted("Killed") |
| 62 | `CoolingOff#3` | TimerFired(CoolingOff#3.T) | -- | `Pickup#3` | Publish NotifyPickup; ScheduleTimer(Pickup#3.T, 1h) |
| 63 | `CoolingOff#3` | Kill | -- | `Aborted(Killed)` | CancelTimer(CoolingOff#3.T); Aborted("Killed") |
| 64 | `Pickup#3~4` | TimerFired(Pickup#3~4.T) | -- | `Failed(PickupNotConfirmed)` | Publish EscalateToOps; Failed("PickupNotConfirmed") |
| 65 | `Pickup#3~4` | ShipmentDispatched | -- | `Completed(Dispatched)` | CancelTimer(Pickup#3~4.T); Completed("Dispatched") |
| 66 | `Pickup#3~4` | Kill | -- | `Aborted(Killed)` | CancelTimer(Pickup#3~4.T); Aborted("Killed") |
| 67 | `Pickup#3~3` | TimerFired(Pickup#3~3.T) | -- | `Failed(PickupNotConfirmed)` | Publish EscalateToOps; Failed("PickupNotConfirmed") |
| 68 | `Pickup#3~3` | ShipmentDispatched | -- | `Completed(Dispatched)` | CancelTimer(Pickup#3~3.T); Completed("Dispatched") |
| 69 | `Pickup#3~3` | Kill | -- | `Aborted(Killed)` | CancelTimer(Pickup#3~3.T); Aborted("Killed") |
| 70 | `Pickup#3~2` | TimerFired(Pickup#3~2.T) | -- | `Failed(PickupNotConfirmed)` | Publish EscalateToOps; Failed("PickupNotConfirmed") |
| 71 | `Pickup#3~2` | ShipmentDispatched | -- | `Completed(Dispatched)` | CancelTimer(Pickup#3~2.T); Completed("Dispatched") |
| 72 | `Pickup#3~2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Pickup#3~2.T); Aborted("Killed") |
| 73 | `Pickup#3` | TimerFired(Pickup#3.T) | -- | `Failed(PickupNotConfirmed)` | Publish EscalateToOps; Failed("PickupNotConfirmed") |
| 74 | `Pickup#3` | ShipmentDispatched | -- | `Completed(Dispatched)` | CancelTimer(Pickup#3.T); Completed("Dispatched") |
| 75 | `Pickup#3` | Kill | -- | `Aborted(Killed)` | CancelTimer(Pickup#3.T); Aborted("Killed") |
| * | any terminal | any input | -- | unchanged | (ignored, recorded as InputIgnored for audit) |
| * | any wait | TimerFired with gen != active gen | -- | unchanged | (stale timer, recorded as InputIgnored) |
| * | any | already-consumed message id | -- | unchanged | (duplicate, dropped by persisted dedup set) |
