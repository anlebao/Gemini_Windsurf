# Task Card #2: NATS Sync Debug + Fix — Orders Not Appearing in ShopERP

> **Status:** ✅ CODE COMPLETE (session 2026-09-15) — root cause found + fixed, pending deploy + RV
> **Priority:** P2 — owner cannot confirm orders via ShopERP UI
> **Created:** 2026-09-15
> **Master plan:** `docs/AI/tasks/community_commerce_fixes/master_plan.md`
> **Prerequisite:** Issue #1 (OrderType) fixed — need DELIVERY orders to test full flow
> **Effort:** 1 day (root cause = code bug, not infra)

## Root Cause (found 2026-09-15)

**NATS sync WAS working.** Debug evidence:
- NATS reachable from ShopERP → Gateway (10.148.0.2:4222) ✅
- NATS subscription `vanan.cloud.order.created.9e94f876-...` shows `msgs: 2` (2 messages delivered) ✅
- ShopERP logs: `OrderSyncSubscriber connected to NATS` + `synced order 01a0a426/01a0a427 → SQLite` ✅
- SQLite has 21 orders (with WAL) including the 2 DELIVERY orders ✅

**Actual root cause:** `OrderSyncSubscriber.SyncOrderCreatedAsync` did NOT read `OrderType` from the NATS event payload. It called `Order.Create()` which defaults to DINEIN. So DELIVERY orders from PG were stored as DINEIN in SQLite → `CommunityOrderService` filter `OrderType == "DELIVERY"` returned 0 → shipper never saw orders.

**"Empty logs" red herring:** `appsettings.Production.json` has `"Default": "Warning"` which filters `LogInformation` (success logs like "connected" and "synced"). Only warnings/errors visible. RV operator saw empty logs and wrongly concluded subscriber wasn't running.

**Secondary issue:** `OrderService.cs` (publisher) did not include `DeliveryAddress`, `DeliveryLat`, `DeliveryLng`, `ShippingFee` in the Outbox event data — only `OrderType` and `CustomerInfo.Address`. Fixed by adding delivery fields to the event payload.

## Problem

Orders created via Gateway checkout (PostgreSQL) do not appear in ShopERP SQLite order list. Owner cannot see pending orders to confirm them.

**Evidence (RV 2026-09-14):**

```
PostgreSQL OutboxMessages:
  EventType=OrderCreated, Status=2 (processed), RoutingKey=9e94f876-... ✓
  → NatsSyncWorker published to NATS subject vanan.cloud.order.created.9e94f876-...

ShopERP (vanan-shop-a):
  SHOP_INSTANCE_ID=9e94f876-... ✓ (matches RoutingKey)
  OrderSyncSubscriber registered in Program.cs:427 ✓
  OrderSyncSubscriber logs: TRỐNG — no "connected to NATS", no "subscribed", no error
  NatsSyncWorker logs: polling OutboxMessages every 1s (SQLite→PG direction, expected)
```

**Possible root causes (ranked by likelihood):**

1. **NATS network unreachable** — ShopERP (vanan-shop-a, 10.148.0.3) → NATS (vanan-gateway, 10.148.0.2:4222) — VPC firewall or routing issue
2. **OrderSyncSubscriber crash silent** — `ResolveShopInstanceId()` or NATS connect throws, but exception swallowed
3. **NATS subject mismatch** — publisher subject doesn't match subscriber subject (unlikely — both use same ShopInstanceId)
4. **ShopERP SQLite has orders but UI filter hides them** — default time/status filter too restrictive

## Solution

Debug-first approach — không fix code mù. Verify từng layer:

### Step 1: Check ShopERP SQLite (is data there?)

```bash
docker exec vanan-shoperp-1 sqlite3 /app/keys/vanan_shoperp.db \
  "SELECT COUNT(*) FROM Orders; SELECT Id, OrderType, Status, OrderDate FROM Orders ORDER BY OrderDate DESC LIMIT 5;"
```

- If orders exist → **Issue is UI filter** (ShopERP Orders/Index.razor) → fix default filter
- If empty → continue to Step 2

### Step 2: Check NATS connectivity

```bash
# From ShopERP container
docker exec vanan-shoperp-1 sh -c "echo 'PING' | nc -w 3 10.148.0.2 4222"

# Check NATS server logs
docker logs vanan-nats-1 --since=1h 2>&1 | grep -i connection
```

- If unreachable → **Issue is network/firewall** → fix VPC routing
- If reachable → continue to Step 3

### Step 3: Check OrderSyncSubscriber startup

```bash
# Full logs from startup
docker logs vanan-shoperp-1 2>&1 | grep -iE "OrderSync|NATS|subscribed|SHOP_INSTANCE|connected|error|exception" | head -30
```

- If no OrderSyncSubscriber logs at all → subscriber not starting (DI issue or crash before logging)
- If error/exception → fix root cause
- If "connected to NATS" log exists but no order sync → subject mismatch or message handling issue

### Step 4: Add structured logging + restart

If Steps 1-3 inconclusive:
- Add `LogInformation` at key points in `OrderSyncSubscriber.ExecuteAsync()` (before NATS connect, after subscribe, on message receive)
- Rebuild + deploy + check logs

### Step 5: Verify NATS message delivery

```bash
# Subscribe to same subject from Gateway VPS
docker exec vanan-nats-1 nats sub "vanan.cloud.order.created.9e94f876-27bd-4a16-a85b-b5f42620bc6e"
# Then create a test order via checkout → check if message arrives
```

## Scope Checklist

### Phase A — Debug (Day 1)
- [ ] A1: Check ShopERP SQLite Orders count
- [ ] A2: Check NATS connectivity from ShopERP → Gateway
- [ ] A3: Check OrderSyncSubscriber startup logs
- [ ] A4: Document findings — identify root cause

### Phase B — Fix (Day 1-2, depends on root cause)
- [ ] B1: If UI filter issue → fix ShopERP Orders/Index.razor default filter
- [ ] B2: If network issue → fix VPC firewall/routing
- [ ] B3: If code issue → fix OrderSyncSubscriber (add logging, fix crash)
- [ ] B4: If subject mismatch → fix subject format (publisher or subscriber)

### Phase C — Verify (Day 2-3)
- [ ] C1: Create order via KhachLink checkout
- [ ] C2: Order appears in ShopERP SQLite within 5s
- [ ] C3: Order appears in ShopERP Orders UI (with correct filter)
- [ ] C4: Owner can confirm order via ShopERP UI
- [ ] C5: Order status syncs back to Gateway PostgreSQL

## Prerequisites

- Issue #1 (OrderType) fixed — need DELIVERY orders for full flow test
- SSH access to both VPS (vanan-gateway, vanan-shop-a)
- NATS client tools available in containers (or install)

## Verification

1. **SQLite check:** `docker exec vanan-shoperp-1 sqlite3 /app/keys/vanan_shoperp.db "SELECT COUNT(*) FROM Orders WHERE OrderDate > datetime('now', '-1 hour')"` → >0 after checkout
2. **UI check:** ShopERP Orders page → filter "Tất cả" → new order visible
3. **Owner confirm:** Click "✓ Xác nhận đơn hàng" → status changes to "confirmed"
4. **Status sync:** Gateway PostgreSQL `Orders.Status` updates to "confirmed" (SQLite→PG sync)
5. **Shipper sees:** After owner confirm → shipper nearby-orders API returns the order

## Files potentially modified

| File | Change | When |
|---|---|---|
| `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs` | Add structured logging | If code issue |
| `5_WebApps/ShopERP/Components/Pages/Orders/Index.razor` | Fix default filter | If UI filter issue |
| `docker-compose.shoperp.yml` | Network config | If network issue |
| VPC firewall rules | NATS port 4222 | If firewall issue |

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Root cause is network/firewall — needs infra access | Coordinate with infra team / GCP console |
| R2 | OrderSyncSubscriber has silent crash | Add try-catch + logging at every step |
| R3 | NATS subject format mismatch | Compare publisher (`NatsSyncWorker.BuildSubject`) vs subscriber subject string |
| R4 | Fix breaks existing order sync | Test with existing orders — regression check |

## Related

- Master plan: `docs/AI/tasks/community_commerce_fixes/master_plan.md`
- Task card #1: `docs/AI/tasks/community_commerce_fixes/task_card_01_order_type.md`
- Source: `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs`
- Source: `3_CoreHub/Services/NatsSyncWorker.cs`
- Source: `2_Gateway/Controllers/PublicOrdersController.cs` (routing key lookup)
- Infra: vanan-gateway (10.148.0.2), vanan-shop-a (10.148.0.3), NATS port 4222
