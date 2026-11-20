# OrderFulfillment v2 — generated behaviour specification

70 scenario(s). Kill/Abort is accepted in every non-terminal state and is not repeated per scenario.

## Scenario 1: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 2: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 3: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 4: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 5: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentCaptured` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 6: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 7: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Failed("TimeoutBeforePayment")`

## Scenario 8: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 9: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 10: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 11: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 12: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Failed("TimeoutBeforePayment")`

## Scenario 13: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 14: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 15: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 16: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.T)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 17: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try1]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.T)`, `Failed("TimeoutBeforePayment")`

## Scenario 18: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentCaptured` is received
  - **Then** move to `InvPay[Inv:wait, Pay:got]` with no effects
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 19: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentCaptured` is received
  - **Then** move to `InvPay[Inv:wait, Pay:got]` with no effects
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 20: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentCaptured` is received
  - **Then** move to `InvPay[Inv:wait, Pay:got]` with no effects
- **When** `InventoryReserved` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 21: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 22: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Failed("TimeoutBeforePayment")`

## Scenario 23: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 24: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Failed("TimeoutBeforePayment")`

## Scenario 25: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 26: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 27: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 28: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 29: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Failed("TimeoutBeforePayment")`

## Scenario 30: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 31: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 32: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 33: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.T)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 34: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff1]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.T)`, `Failed("TimeoutBeforePayment")`

## Scenario 35: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 36: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 37: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try2]` with no effects
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 38: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try2]` with no effects
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 39: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try2]` with no effects
- **When** `PaymentCaptured` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 40: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try2]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 41: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try2]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Failed("TimeoutBeforePayment")`

## Scenario 42: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try2]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 43: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try2]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 44: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try2]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 45: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try2]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.T)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 46: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try2]` with no effects
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.T)`, `Failed("TimeoutBeforePayment")`

## Scenario 47: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** move to `InvPay[Inv:wait, Pay:got]` with no effects
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 48: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** move to `InvPay[Inv:wait, Pay:got]` with no effects
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 49: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** move to `InvPay[Inv:wait, Pay:got]` with no effects
- **When** `InventoryReserved` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 50: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 51: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Failed("TimeoutBeforePayment")`

## Scenario 52: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff2]` with no effects
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 53: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff2]` with no effects
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.Pay.B)`, `Failed("TimeoutBeforePayment")`

## Scenario 54: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff2]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 55: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff2]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 56: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff2]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 57: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff2]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.T)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 58: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:backoff2]` with no effects
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.T)`, `Failed("TimeoutBeforePayment")`

## Scenario 59: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 60: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 61: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try3]` with no effects
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 62: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try3]` with no effects
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 63: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try3]` with no effects
- **When** `PaymentCaptured` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 64: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try3]` with no effects
- **When** `PaymentDeclined` is received and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.T)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 65: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `InventoryReserved` is received
  - **Then** move to `InvPay[Inv:got, Pay:try3]` with no effects
- **When** `PaymentDeclined` is received and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.T)`, `Failed("TimeoutBeforePayment")`

## Scenario 66: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** move to `InvPay[Inv:wait, Pay:got]` with no effects
- **When** timer `InvPay.T` fires and `HasPayment && !HasInventory` holds
  - **Then** `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 67: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** move to `InvPay[Inv:wait, Pay:got]` with no effects
- **When** timer `InvPay.T` fires and `!(HasPayment && !HasInventory)` holds
  - **Then** `Failed("TimeoutBeforePayment")`

## Scenario 68: ends Completed "Fulfilled"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentCaptured` is received
  - **Then** move to `InvPay[Inv:wait, Pay:got]` with no effects
- **When** `InventoryReserved` is received
  - **Then** `CancelTimer(InvPay.T)`, `Completed("Fulfilled")`

## Scenario 69: ends Failed "TimeoutAfterPayment_Refunded"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received and `HasPayment && !HasInventory` holds
  - **Then** `CancelTimer(InvPay.T)`, `Publish RefundPayment`, `Failed("TimeoutAfterPayment_Refunded")`

## Scenario 70: ends Failed "TimeoutBeforePayment"

- **Given** no OrderFulfillment instance exists for the correlation id
- **When** `OrderSubmitted` is received
  - **Then** `Publish ReserveInventory, ChargePayment`, `ScheduleTimer(InvPay.T, 6m)`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received
  - **Then** `ScheduleTimer(InvPay.Pay.B, 30s)`
- **When** timer `InvPay.Pay.B` fires
  - **Then** `Publish ChargePayment`
- **When** `PaymentDeclined` is received and `!(HasPayment && !HasInventory)` holds
  - **Then** `CancelTimer(InvPay.T)`, `Failed("TimeoutBeforePayment")`
