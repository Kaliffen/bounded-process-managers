# OrderFulfillment v2 — transition table

Table hash `c40793e32c42e4ae` · 16 states · 58 transitions · T_DAG = **6m**

| # | Current state | Accepted input | Guard | Next state | Emitted effects |
|---|---|---|---|---|---|
| 0 | `Init` | OrderSubmitted | -- | `InvPay[Inv:wait, Pay:try1]` | Publish ReserveInventory, ChargePayment; ScheduleTimer(InvPay.T, 6m) |
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
| 29 | `InvPay[Inv:wait, Pay:try2]` | PaymentDeclined | -- | `InvPay[Inv:wait, Pay:backoff2]` | ScheduleTimer(InvPay.Pay.B, 30s) |
| 30 | `InvPay[Inv:wait, Pay:try2]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); Aborted("Killed") |
| 31 | `InvPay[Inv:got, Pay:try2]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 32 | `InvPay[Inv:got, Pay:try2]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | Failed("TimeoutBeforePayment") |
| 33 | `InvPay[Inv:got, Pay:try2]` | PaymentCaptured | -- | `Completed(Fulfilled)` | CancelTimer(InvPay.T); Completed("Fulfilled") |
| 34 | `InvPay[Inv:got, Pay:try2]` | PaymentDeclined | -- | `InvPay[Inv:got, Pay:backoff2]` | ScheduleTimer(InvPay.Pay.B, 30s) |
| 35 | `InvPay[Inv:got, Pay:try2]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); Aborted("Killed") |
| 36 | `InvPay[Inv:wait, Pay:backoff2]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | CancelTimer(InvPay.Pay.B); Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 37 | `InvPay[Inv:wait, Pay:backoff2]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | CancelTimer(InvPay.Pay.B); Failed("TimeoutBeforePayment") |
| 38 | `InvPay[Inv:wait, Pay:backoff2]` | InventoryReserved | -- | `InvPay[Inv:got, Pay:backoff2]` | (none — horizon keeps ticking) |
| 39 | `InvPay[Inv:wait, Pay:backoff2]` | TimerFired(InvPay.Pay.B) | -- | `InvPay[Inv:wait, Pay:try3]` | Publish ChargePayment |
| 40 | `InvPay[Inv:wait, Pay:backoff2]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); CancelTimer(InvPay.Pay.B); Aborted("Killed") |
| 41 | `InvPay[Inv:got, Pay:backoff2]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | CancelTimer(InvPay.Pay.B); Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 42 | `InvPay[Inv:got, Pay:backoff2]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | CancelTimer(InvPay.Pay.B); Failed("TimeoutBeforePayment") |
| 43 | `InvPay[Inv:got, Pay:backoff2]` | TimerFired(InvPay.Pay.B) | -- | `InvPay[Inv:got, Pay:try3]` | Publish ChargePayment |
| 44 | `InvPay[Inv:got, Pay:backoff2]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); CancelTimer(InvPay.Pay.B); Aborted("Killed") |
| 45 | `InvPay[Inv:wait, Pay:try3]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 46 | `InvPay[Inv:wait, Pay:try3]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | Failed("TimeoutBeforePayment") |
| 47 | `InvPay[Inv:wait, Pay:try3]` | InventoryReserved | -- | `InvPay[Inv:got, Pay:try3]` | (none — horizon keeps ticking) |
| 48 | `InvPay[Inv:wait, Pay:try3]` | PaymentCaptured | -- | `InvPay[Inv:wait, Pay:got]` | (none — horizon keeps ticking) |
| 49 | `InvPay[Inv:wait, Pay:try3]` | PaymentDeclined | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | CancelTimer(InvPay.T); Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 50 | `InvPay[Inv:wait, Pay:try3]` | PaymentDeclined | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | CancelTimer(InvPay.T); Failed("TimeoutBeforePayment") |
| 51 | `InvPay[Inv:wait, Pay:try3]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); Aborted("Killed") |
| 52 | `InvPay[Inv:got, Pay:try3]` | TimerFired(InvPay.T) | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 53 | `InvPay[Inv:got, Pay:try3]` | TimerFired(InvPay.T) | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | Failed("TimeoutBeforePayment") |
| 54 | `InvPay[Inv:got, Pay:try3]` | PaymentCaptured | -- | `Completed(Fulfilled)` | CancelTimer(InvPay.T); Completed("Fulfilled") |
| 55 | `InvPay[Inv:got, Pay:try3]` | PaymentDeclined | HasPayment && !HasInventory | `Failed(TimeoutAfterPayment_Refunded)` | CancelTimer(InvPay.T); Publish RefundPayment; Failed("TimeoutAfterPayment_Refunded") |
| 56 | `InvPay[Inv:got, Pay:try3]` | PaymentDeclined | !(HasPayment && !HasInventory) | `Failed(TimeoutBeforePayment)` | CancelTimer(InvPay.T); Failed("TimeoutBeforePayment") |
| 57 | `InvPay[Inv:got, Pay:try3]` | Kill | -- | `Aborted(Killed)` | CancelTimer(InvPay.T); Aborted("Killed") |
| * | any terminal | any input | -- | unchanged | (ignored, recorded as InputIgnored for audit) |
| * | any wait | TimerFired with gen != active gen | -- | unchanged | (stale timer, recorded as InputIgnored) |
| * | any | already-consumed message id | -- | unchanged | (duplicate, dropped by persisted dedup set) |
