# OrderFulfillment v1 — transition table

Table hash `408fc1a731e9f925` · 12 states · 38 transitions · T_DAG = **5m**

| # | Current state | Accepted input | Guard | Next state | Emitted effects |
|---|---|---|---|---|---|
| 0 | `Init` | OrderSubmitted | -- | `InvPay[Inv:wait, Pay:try1]` | Publish ReserveInventory, ChargePayment; ScheduleTimer(InvPay.T, 5m) |
| 1 | `InvPay[Inv:wait, Pay:try1]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 2 | `InvPay[Inv:wait, Pay:try1]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | Failed("TimeoutBeforePayment") |
| 3 | `InvPay[Inv:wait, Pay:try1]` | InventoryReserved | -- | `InvPay[Inv:got, Pay:try1]` | (none — horizon keeps ticking) |
| 4 | `InvPay[Inv:wait, Pay:try1]` | PaymentCaptured | -- | `InvPay[Inv:wait, Pay:got]` | (none — horizon keeps ticking) |
| 5 | `InvPay[Inv:wait, Pay:try1]` | PaymentDeclined | -- | `InvPay[Inv:wait, Pay:backoff1]` | ScheduleTimer(InvPay.Pay.B, 30s) |
| 6 | `InvPay[Inv:wait, Pay:try1]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); Aborted("Killed") |
| 7 | `InvPay[Inv:got, Pay:try1]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 8 | `InvPay[Inv:got, Pay:try1]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | Failed("TimeoutBeforePayment") |
| 9 | `InvPay[Inv:got, Pay:try1]` | PaymentCaptured | -- | `Completed(Fulfilled)` | CancelTimer(InvPay.T); Completed("Fulfilled") |
| 10 | `InvPay[Inv:got, Pay:try1]` | PaymentDeclined | -- | `InvPay[Inv:got, Pay:backoff1]` | ScheduleTimer(InvPay.Pay.B, 30s) |
| 11 | `InvPay[Inv:got, Pay:try1]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); Aborted("Killed") |
| 12 | `InvPay[Inv:wait, Pay:got]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 13 | `InvPay[Inv:wait, Pay:got]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | Failed("TimeoutBeforePayment") |
| 14 | `InvPay[Inv:wait, Pay:got]` | InventoryReserved | -- | `Completed(Fulfilled)` | CancelTimer(InvPay.T); Completed("Fulfilled") |
| 15 | `InvPay[Inv:wait, Pay:got]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); Aborted("Killed") |
| 16 | `InvPay[Inv:wait, Pay:backoff1]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | CancelTimer(InvPay.Pay.B); Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 17 | `InvPay[Inv:wait, Pay:backoff1]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | CancelTimer(InvPay.Pay.B); Failed("TimeoutBeforePayment") |
| 18 | `InvPay[Inv:wait, Pay:backoff1]` | InventoryReserved | -- | `InvPay[Inv:got, Pay:backoff1]` | (none — horizon keeps ticking) |
| 19 | `InvPay[Inv:wait, Pay:backoff1]` | TimerFired(InvPay.Pay.B) | -- | `InvPay[Inv:wait, Pay:try2]` | Publish ChargePayment |
| 20 | `InvPay[Inv:wait, Pay:backoff1]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); CancelTimer(InvPay.Pay.B); Aborted("Killed") |
| 21 | `InvPay[Inv:got, Pay:backoff1]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | CancelTimer(InvPay.Pay.B); Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 22 | `InvPay[Inv:got, Pay:backoff1]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | CancelTimer(InvPay.Pay.B); Failed("TimeoutBeforePayment") |
| 23 | `InvPay[Inv:got, Pay:backoff1]` | TimerFired(InvPay.Pay.B) | -- | `InvPay[Inv:got, Pay:try2]` | Publish ChargePayment |
| 24 | `InvPay[Inv:got, Pay:backoff1]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); CancelTimer(InvPay.Pay.B); Aborted("Killed") |
| 25 | `InvPay[Inv:wait, Pay:try2]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 26 | `InvPay[Inv:wait, Pay:try2]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | Failed("TimeoutBeforePayment") |
| 27 | `InvPay[Inv:wait, Pay:try2]` | InventoryReserved | -- | `InvPay[Inv:got, Pay:try2]` | (none — horizon keeps ticking) |
| 28 | `InvPay[Inv:wait, Pay:try2]` | PaymentCaptured | -- | `InvPay[Inv:wait, Pay:got]` | (none — horizon keeps ticking) |
| 29 | `InvPay[Inv:wait, Pay:try2]` | PaymentDeclined | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | CancelTimer(InvPay.T); Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 30 | `InvPay[Inv:wait, Pay:try2]` | PaymentDeclined | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | CancelTimer(InvPay.T); Failed("TimeoutBeforePayment") |
| 31 | `InvPay[Inv:wait, Pay:try2]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); Aborted("Killed") |
| 32 | `InvPay[Inv:got, Pay:try2]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 33 | `InvPay[Inv:got, Pay:try2]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | Failed("TimeoutBeforePayment") |
| 34 | `InvPay[Inv:got, Pay:try2]` | PaymentCaptured | -- | `Completed(Fulfilled)` | CancelTimer(InvPay.T); Completed("Fulfilled") |
| 35 | `InvPay[Inv:got, Pay:try2]` | PaymentDeclined | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | CancelTimer(InvPay.T); Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 36 | `InvPay[Inv:got, Pay:try2]` | PaymentDeclined | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | CancelTimer(InvPay.T); Failed("TimeoutBeforePayment") |
| 37 | `InvPay[Inv:got, Pay:try2]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); Aborted("Killed") |
| * | any terminal | any input | -- | unchanged | (ignored, recorded as InputIgnored for audit) |
| * | any wait | TimerFired with gen != active gen | -- | unchanged | (stale timer, recorded as InputIgnored) |
| * | any | already-consumed message id | -- | unchanged | (duplicate, dropped by persisted dedup set) |
