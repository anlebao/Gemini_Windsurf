# Task Card: Phase 4 — ShopERP OrderSyncSubscriber Routing Key Update

> **Master plan:** `gateway_router_multi_vps_master_plan.md`
> **Workflow:** `newfeaturebuild.md`
> **Phase:** 4 of 7 (SIMPLIFIED — minor update, not a new endpoint)
> **Depends on:** Phase 3 contract (NATS subject `vanan.cloud.order.created.{shopInstanceId}`)
> **Unlocks:** End-to-end multi-VPS order delivery (with Phase 5)

---

## 1. Use Case & Business Design

**Problem:** Current `OrderSyncSubscriber` (ShopERP) subscribes to wildcard subject `vanan.cloud.order.created` — ALL ShopERP instances receive ALL orders. In multi-VPS deployment, each ShopERP would write all tenants' orders to its SQLite (wasteful + wrong — ShopERP-A shouldn't have tenant-B's orders).

Phase 3 changes Gateway to publish with routing key: `vanan.cloud.order.created.{shopInstanceId}`. Phase 4 updates ShopERP to subscribe only to its own ShopInstanceId.

**Existing infrastructure (verified 2026-07-18):**
`5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs` already:
- Subscribes to `vanan.cloud.order.created` (line 60)
- Parses order payload (OrderId, TenantId, Items with ProductName + VatRate)
- Auto-creates product stubs in SQLite if missing (lines 130-144)
- Creates order in SQLite via `Order.Create` + `OrderItem.Create` (idempotent — checks `exists` first)

**Goal:**
1. ShopERP reads its `ShopInstanceId` from env var `SHOP_INSTANCE_ID` (or config).
2. `OrderSyncSubscriber` subscribes to `vanan.cloud.order.created.{myShopInstanceId}` instead of wildcard.
3. Validate env var present on startup — fail fast if missing (with clear error message).
4. Update auto-create-product-stub logic to use `UnitPrice` and `VatRate` from the order payload (provided by client snapshot from QR) instead of `0m`. This avoids price-validation failures when a customer scans a legacy QR and the ShopERP product does not yet exist.

**Out of scope:** Gateway router (Phase 3), KhachLink UI (Phase 5), Admin UI (Phase 6).

---

## 2. Open Question Q5 Resolution

**Q5: How does ShopERP know its own ShopInstanceId?**

**Decision (per master plan §6 recommendation):** Env var `SHOP_INSTANCE_ID` set at deploy.
- Local dev: `SHOP_INSTANCE_ID={deterministic-guid-for-local}` in `docker-compose.yml` or launchSettings.
- VPS production: `SHOP_INSTANCE_ID` in docker container env (set in CD pipeline or docker-compose).
- Multi-VPS: each VPS has its own `SHOP_INSTANCE_ID` matching a row in Gateway PG `ShopInstances` table.

**Validation on startup:** `OrderSyncSubscriber.ExecuteAsync` checks env var. If missing → log ERROR + throw `InvalidOperationException` (fail fast). Don't silently subscribe to wildcard (would cause cross-VPS data leak).

**Backward compat:** If `SHOP_INSTANCE_ID` not set AND a `ShopInstance` with `BaseUrl = "http://shoperp:5003"` exists in Gateway PG (default local), use that as fallback. **Actually NO — fail fast is safer.** Document migration: existing VPS must set env var before deploying Phase 4.

---

## 2. Reverse Impact Analysis

### Configuration (`5_WebApps/ShopERP/`)
- **`appsettings.json`** — ADD default `ShopInstance:Id` (nullable, env var override preferred):
  ```json
  "ShopInstance": {
    "Id": null
  }
  ```
- **`appsettings.Development.json`** — ADD local dev ShopInstanceId:
  ```json
  "ShopInstance": {
    "Id": "00000000-0000-0000-0000-000000000001"
  }
  ```
  (matches the default ShopInstance seeded by Phase 1 migration for local dev)
- **`docker-compose.yml`** (if exists for local dev) — ADD env var:
  ```yaml
  shoperp:
    environment:
      - SHOP_INSTANCE_ID=00000000-0000-0000-0000-000000000001
  ```
- **VPS docker-compose / CD pipeline** — ADD env var (Phase 7 deploy handles this).

### Service Layer (`5_WebApps/ShopERP/Services/`)
- **`OrderSyncSubscriber.cs`** — UPDATE `ExecuteAsync`:
  ```csharp
  // Read ShopInstanceId from config (env var SHOP_INSTANCE_ID → config ShopInstance:Id)
  string? shopInstanceIdStr = _configuration.GetValue<string>("ShopInstance:Id")
      ?? Environment.GetEnvironmentVariable("SHOP_INSTANCE_ID");
  if (!Guid.TryParse(shopInstanceIdStr, out Guid shopInstanceId) || shopInstanceId == Guid.Empty)
  {
      _logger.LogError("OrderSyncSubscriber: SHOP_INSTANCE_ID not configured. Set env var SHOP_INSTANCE_ID or config ShopInstance:Id. Aborting subscriber.");
      throw new InvalidOperationException("SHOP_INSTANCE_ID not configured — cannot route NATS subscription.");
  }

  string subject = $"vanan.cloud.order.created.{shopInstanceId}";
  _ = _subscriptionConnection.SubscribeAsync(subject, async (sender, args) =>
  {
      await SyncOrderCreatedAsync(args.Message.Data, stoppingToken);
  });

  // Also subscribe to status changes (routed similarly)
  string statusSubject = $"vanan.cloud.order.status.changed.{shopInstanceId}";
  _ = _subscriptionConnection.SubscribeAsync(statusSubject, async (sender, args) =>
  {
      await SyncOrderStatusChangedAsync(args.Message.Data, stoppingToken);
  });

  _logger.LogInformation("OrderSyncSubscriber subscribed to {Subject} + {StatusSubject} (ShopInstanceId={ShopInstanceId})",
      subject, statusSubject, shopInstanceId);
  ```
- **Update `SyncOrderCreatedAsync`** product stub creation: use `UnitPrice` + `VatRate` from the order item payload instead of hardcoded `0m`. The payload now includes these fields (client snapshot from QR). Keep idempotent insert and fallback to existing product if already present.
- **Keep `SyncOrderStatusChangedAsync`** logic unchanged.

### DI Registration (`5_WebApps/ShopERP/Program.cs`)
- No new DI registrations. `OrderSyncSubscriber` already registered as `HostedService`.

### Tests
- **NEW: `6_Tests/VanAn.Integration.Tests/OrderSyncSubscriberRoutingTests.cs`**:
  - `ExecuteAsync_WithShopInstanceIdConfigured_SubscribesToRoutedSubject`
  - `ExecuteAsync_WithoutShopInstanceId_ThrowsInvalidOperationException`
  - `SyncOrderCreatedAsync_WithRoutedEvent_CreatesOrderInSQLite` (existing behavior, just verify still works)
- **Manual verification (Phase 4 gate):** start ShopERP with `SHOP_INSTANCE_ID` set → check logs show `subscribed to vanan.cloud.order.created.{id}`.

### TDD Plan
1. Write failing test: `ExecuteAsync_WithoutShopInstanceId_ThrowsInvalidOperationException`.
2. Update `OrderSyncSubscriber.ExecuteAsync` to read + validate ShopInstanceId.
3. Run test → pass.
4. Write failing test: `ExecuteAsync_WithShopInstanceIdConfigured_SubscribesToRoutedSubject` (mock NATS connection, verify subject string).
5. Update subscription to use routed subject.
6. Run test → pass.
7. Add config to `appsettings.json` + `appsettings.Development.json`.
8. Manual smoke: start ShopERP locally → verify logs.
9. Full regression.

---

## 3. Detailed Coding Plan

### Namespace Strategy
- `VanAn.ShopERP.Services` (OrderSyncSubscriber update)
- `VanAn.Integration.Tests` (new tests)
- No new namespaces.

### Implementation Steps
**Step 1 — Config files (2 modified files):**
- `appsettings.json`: add `ShopInstance:Id` (null default).
- `appsettings.Development.json`: add `ShopInstance:Id` = local dev Guid (matches Phase 1 seed).
- Build → 0 errors.

**Step 2 — Tests (1 new file):**
- `OrderSyncSubscriberRoutingTests.cs` with mocked NATS `IConnection` (verify `SubscribeAsync` called with correct subject string).
- Run → all fail (routing not implemented).

**Step 3 — Update `OrderSyncSubscriber` (1 file):**
- In `ExecuteAsync`:
  - Read `ShopInstance:Id` from config (env var override).
  - Validate non-empty Guid.
  - Build routed subject strings (`order.created.{shopInstanceId}` and `order.status.changed.{shopInstanceId}`).
  - Subscribe to routed subjects.
- In `SyncOrderCreatedAsync`:
  - When auto-creating product stub, use `UnitPrice` and `VatRate` from the order item payload (not `0m`).
  - Code currently: `new Product(tenantIdObj, productName, "Synced from Gateway", 0m, "Synced", true, null, vatRate, 0m);`
  - Change to: `new Product(tenantIdObj, productName, "Synced from Gateway", unitPrice, "Synced", true, null, vatRate, 0m);`
  - Parse `UnitPrice` from payload same way `VatRate` is parsed.
- Run tests → pass.

**Step 4 — Manual smoke (local):**
- Start ShopERP container with `SHOP_INSTANCE_ID=00000000-0000-0000-0000-000000000001`.
- Verify logs: `OrderSyncSubscriber subscribed to vanan.cloud.order.created.00000000-...-001 + vanan.cloud.order.status.changed.00000000-...-001`.
- Trigger a test order via Gateway (after Phase 3) → verify order appears in ShopERP SQLite.

**Step 5 — Full regression:**
- `dotnet build VanAn.sln` — 0 errors.
- `dotnet test` — all pass.
- `guard-check.ps1` PASS.

### Active Skills
- `outbox-pattern-implementation` (NATS routing key alignment)
- `domain-integrity-validation` (no Domain change — verify)

---

## 4. Validation Gates

| Gate | Command | Expected |
|---|---|---|
| Build | `dotnet build VanAn.sln` | 0 errors |
| Unit tests | `dotnet test 6_Tests/VanAn.Integration.Tests --filter OrderSyncSubscriberRouting` | All pass |
| Manual smoke | Start ShopERP with SHOP_INSTANCE_ID | Logs show routed subscription |
| Existing tests | `dotnet test 6_Tests/VanAn.Core.Tests` | No regressions |
| Guard check | `./guard-check.ps1` | PASS |

---

## 5. Deliverables

- Modified: `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs` (routed subject + validation + stub price fix)
- Modified: `5_WebApps/ShopERP/appsettings.json` (ShopInstance:Id null default)
- Modified: `5_WebApps/ShopERP/appsettings.Development.json` (local dev ShopInstanceId)
- New: `6_Tests/VanAn.Integration.Tests/OrderSyncSubscriberRoutingTests.cs`

**Note:** VPS docker-compose env var update happens in Phase 7 deploy, not Phase 4.

---

## 6. Approval Gate

No domain modification. Standard IMPLEMENT approval.

**Note:** Phase 4 is now MINOR (~100 LOC) compared to original plan (~200 LOC + new endpoint). Existing OrderSyncSubscriber infrastructure reused.

---

## 7. COMPLETION SUMMARY

**Phase 4 COMPLETE** — commit `<HASH>` on `main`.

### Files created
| File | Purpose |
|------|---------|
| _TBD_ | _TBD_ |

### Files modified
| File | Change |
|------|--------|
| _TBD_ | _TBD_ |

### Issues fixed during implementation
- _TBD_

### Verification

#### Static Verification (compile-time)
- **Build:** _TBD_
- **Unit tests:** _TBD_
- **guard-check.ps1:** _TBD_

#### Live Runtime Verification (boot + HTTP + UI)
> **Lesson learned (Wave 0):** Build + Architecture Tests + guard-check PASS ≠ runtime works.
> Live runtime verification is MANDATORY for all phases.

| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | _TBD_ | _TBD_ | _TBD_ |
