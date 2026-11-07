# F1 Process Details — Track B (Order Lifecycle, Option C)

> Metadata in `registries/activities.yaml` ACTV-201..212. Captured at `64789992`.

## Verified mechanics

- `PublicOrdersController`: `POST /api/public/orders` (guest) + `POST /api/public/orders/checkout` → `CreateOrderCommand` → `OrderService.CreateOrderFromCommandAsync(command, tenantId, routingKey)`. Routing key resolved at creation — routed subject `vanan.cloud.order.created.{shopInstanceId}`.
- `OrderSyncSubscriber` (ShopERP): subscribes ONLY to routed subjects — wildcard `vanan.cloud.order.created.>` deliberately removed (prevents cross-VPS delivery).
- Back-sync: status via `vanan.cloud.order.status.changed.{si}` + `vanan.shoperp.*` (SQLite→PG direction per `NatsSyncWorker` comments).
- Loyalty accrual: `vanan.cloud.loyalty.changed.{deviceId}` (AllianceWalletService / LoyaltyBalanceSyncPublisher + outbox fallback).
- Payment: `vanan.cloud.order.payment.confirmed.{si}` noted in `WalletService` comments (I).
- Tracking: `GET /api/public/orders/{id}` → `PublicOrderTrackingDto`.

## Execution split

MANUAL: ACTV-201/202/207/208/210 (browse, checkout, confirm, kitchen, track) · AUTOMATED: the rest · HYBRID: none (per decompose-first).

## INFERRED items (upgrade at P3/RV)

- POS/kitchen notification mechanism (SignalR assumed — ACTV-206)
- Order confirmation UI flow in ShopERP (ACTV-207)
- Cancellation/reversal path details (gap candidate)

## Draft gaps → P4

| Candidate | Class |
|---|---|
| Order rejection/cancel reason capture (visible in flows?) | MISSING_AUDIT_APPROVAL? (verify P3) |
| Routing-fail monitoring for missing ShopInstance | MISSING_AUDIT_APPROVAL / OPERATIONAL |
| Duplicate-notification suppression on replica upsert | verify |
