# DeliveryTracking v1 — transition table

Table hash `eef65e33508ba55d` · 12 states · 20 transitions · T_DAG = **15d**

| # | Current state | Accepted input | Guard | Next state | Emitted effects |
|---|---|---|---|---|---|
| 0 | `Init` | ShipmentDispatched | -- | `Attempt#1` | Publish ScheduleDelivery; ScheduleTimer(Attempt#1.T, 2d) |
| 1 | `Attempt#1` | TimerFired(Attempt#1.T) | -- | `Failed(CarrierUnresponsive)` | Publish OpenCarrierClaim; Failed("CarrierUnresponsive") |
| 2 | `Attempt#1` | Delivered | -- | `Completed(Delivered)` | CancelTimer(Attempt#1.T); Publish NotifyCustomerDelivered; Completed("Delivered") |
| 3 | `Attempt#1` | DeliveryAttemptFailed | -- | `Delivery.backoff1` | CancelTimer(Attempt#1.T); ScheduleTimer(Delivery.backoff1.T, 1d) |
| 4 | `Attempt#1` | Kill | -- | `Aborted(Killed)` | CancelTimer(Attempt#1.T); Aborted("Killed") |
| 5 | `Delivery.backoff1` | TimerFired(Delivery.backoff1.T) | -- | `Attempt#2` | Publish ScheduleDelivery; ScheduleTimer(Attempt#2.T, 2d) |
| 6 | `Delivery.backoff1` | Kill | -- | `Aborted(Killed)` | CancelTimer(Delivery.backoff1.T); Aborted("Killed") |
| 7 | `Attempt#2` | TimerFired(Attempt#2.T) | -- | `Failed(CarrierUnresponsive)` | Publish OpenCarrierClaim; Failed("CarrierUnresponsive") |
| 8 | `Attempt#2` | Delivered | -- | `Completed(Delivered)` | CancelTimer(Attempt#2.T); Publish NotifyCustomerDelivered; Completed("Delivered") |
| 9 | `Attempt#2` | DeliveryAttemptFailed | -- | `Delivery.backoff2` | CancelTimer(Attempt#2.T); ScheduleTimer(Delivery.backoff2.T, 1d) |
| 10 | `Attempt#2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Attempt#2.T); Aborted("Killed") |
| 11 | `Delivery.backoff2` | TimerFired(Delivery.backoff2.T) | -- | `Attempt#3` | Publish ScheduleDelivery; ScheduleTimer(Attempt#3.T, 2d) |
| 12 | `Delivery.backoff2` | Kill | -- | `Aborted(Killed)` | CancelTimer(Delivery.backoff2.T); Aborted("Killed") |
| 13 | `Attempt#3` | TimerFired(Attempt#3.T) | -- | `Failed(CarrierUnresponsive)` | Publish OpenCarrierClaim; Failed("CarrierUnresponsive") |
| 14 | `Attempt#3` | Delivered | -- | `Completed(Delivered)` | CancelTimer(Attempt#3.T); Publish NotifyCustomerDelivered; Completed("Delivered") |
| 15 | `Attempt#3` | DeliveryAttemptFailed | -- | `Return` | CancelTimer(Attempt#3.T); Publish ReturnToSender; ScheduleTimer(Return.T, 7d) |
| 16 | `Attempt#3` | Kill | -- | `Aborted(Killed)` | CancelTimer(Attempt#3.T); Aborted("Killed") |
| 17 | `Return` | TimerFired(Return.T) | -- | `Failed(LostInTransit)` | Publish OpenCarrierClaim; Failed("LostInTransit") |
| 18 | `Return` | ReturnedToSender | -- | `Completed(ReturnedToSender)` | CancelTimer(Return.T); Completed("ReturnedToSender") |
| 19 | `Return` | Kill | -- | `Aborted(Killed)` | CancelTimer(Return.T); Aborted("Killed") |
| * | any terminal | any input | -- | unchanged | (ignored, recorded as InputIgnored for audit) |
| * | any wait | TimerFired with gen != active gen | -- | unchanged | (stale timer, recorded as InputIgnored) |
| * | any | already-consumed message id | -- | unchanged | (duplicate, dropped by persisted dedup set) |
