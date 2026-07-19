# Task Card: Phase 3 — Gateway Order Creator (Client Snapshot + Multi-tenant Grouping + Routed Outbox)

> **Master plan:** `gateway_router_multi_vps_master_plan.md`
> **Workflow:** `newfeaturebuild.md`
> **Phase:** 3 of 7 (CRITICAL — fixes the checkout bug)
> **Depends on:** Phase 1 (ShopInstance entity), Phase 2 (ShopInstanceService)
> **Unlocks:** Phase 4 (ShopERP OrderSyncSubscriber routing key), Phase 5 (KhachLink UI contract)

---

## 1. Use Case & Business Design

**Problem (root cause of KhachLink checkout bug):**
`OrderService.CreateOrderFromCommandAsync` calls `LoadProductsForSnapshotAsync` which queries `IVanAnDbContext.Products` (Gateway PG). Gateway PG only has 12 products for tenant `00000000-...-001` (sync broken — 62 products across 8 tenants in ShopERP SQLite never synced). When KhachLink checks out a product from tenant `eb7f9261-...`, lookup fails → `KeyNotFoundException` → checkout 500.

**Goal (per user decisions 2026-07-18):**
1. **Client provides ProductName + VatRate per item** in checkout request. Gateway creates order in PG using this snapshot — NO PG product lookup.
2. **Multi-tenant grouping:** if cart has items from 2 tenants, create 2 separate orders in PG (one per tenant).
3. **Routed Outbox:** each OrderCreated event published to NATS with subject `vanan.cloud.order.created.{shopInstanceId}` so only the correct ShopERP receives it.
4. **PG remains source of truth for Orders** — payment webhook unchanged (Q4 resolved).
5. **Onboarding simplification:** remove product seeding from `OnboardingController` (was the bug source — Gateway PG had wrong products).

**Out of scope:** ShopERP OrderSyncSubscriber routing key update (Phase 4), KhachLink UI changes (Phase 5), Admin UI (Phase 6).

---

## 2. Reverse Impact Analysis

### POS Create.razor (collateral update — same OrderItemRequest contract)
- **`5_WebApps/ShopERP/Components/Pages/POS/Create.razor:394`** — UPDATE `OrderItemRequest` construction:
  - Add `TenantId = TenantProvider.TenantId` (POS runs in tenant context — `TenantProvider` injected).
  - Add `ProductName = c.ProductName` (PosCartItem already has it, line 423).
  - Add `VatRate = c.VatRate` (PosCartItem already has it, line 425).
  - **POS flow unchanged otherwise** — still calls `OrderService.CreateOrderFromCommandAsync(command, TenantProvider.TenantId)` in-process on ShopERP SQLite. No HTTP, no Gateway involvement.
  - **Why POS is NOT broken by Option C:** POS uses ShopERP's local `IOrderService` + SQLite. Gateway's Option C change only affects the Gateway-side `PublicOrdersController` checkout path. POS order creation is independent. Both flows share `OrderService.CreateOrderFromCommandAsync` but with different DbContext instances (ShopERP SQLite vs Gateway PG).

### CreateGuestOrder (campaign flow — collateral update)
- **`2_Gateway/Controllers/PublicOrdersController.cs:52-66`** (`CreateGuestOrder` endpoint) — UPDATE:
  - Currently builds `OrderItemRequest` with only `ProductId + Quantity + UnitPrice`.
  - Must fetch product to get `TenantId + ProductName + VatRate` snapshot, OR extend `GuestOrderRequest` DTO to include these fields (caller — Campaign page — provides them).
  - **Recommendation:** Extend `GuestOrderRequest` with `TenantId + ProductName + VatRate` (Campaign.cshtml already has ProductDto when rendering QR — can include in scan payload or fetch on scan).
  - If product fetch needed: Gateway must query... where? Gateway PG doesn't have products. **Same problem as checkout.** → Must use client snapshot. Campaign page provides snapshot in request.
  - **Out of scope for Phase 3 if Campaign flow is rarely used** — document as debt, fallback to `LoadProductsForSnapshotAsync` (will fail with KeyNotFoundException, same as current bug, but Campaign flow is separate from main checkout). **User decision: defer or fix in Phase 3?**

### Product catalog forward routing (NEW — discovered 2026-07-18)
- **`2_Gateway/Program.cs:178`** — currently single `AddHttpClient("shoperp", c => c.BaseAddress = ...)` with 1 fixed BaseUrl.
- **`5_WebApps/KhachLink/Services/Http/ProductHttpService.cs:18`** — calls `shoperp/api/products?shopId={shopId}`. Gateway YARP-forwards to single ShopERP.
- **`5_WebApps/KhachLink/Pages/Scan.razor:152`** — `ProductService.GetProductsAsync(qrPayload.ShopId)` fetches full ProductDto (needed for cart snapshot).
- **Problem in multi-VPS:** Gateway doesn't know which ShopERP serves `shopId`. Forward goes to fixed ShopERP → products from other VPS not found → Scan fails → can't add to cart → can't checkout.
- **Fix in Phase 3:** Gateway `ProductsController` proxy (or YARP config) must lookup `Tenant.ShopInstanceId → ShopInstance.BaseUrl` and forward to correct ShopERP. OR KhachLink calls ShopERP directly (but then KhachLink needs routing table — violates "KhachLink HTTP-only via Gateway" rule).
- **Recommendation:** Add `ProductsForwardingController` (or update YARP config) on Gateway that:
  1. Receives `GET /shoperp/api/products?shopId={tenantId}`.
  2. Lookup `Tenant.ShopInstanceId → ShopInstance.BaseUrl`.
  3. HTTP forward to `{baseUrl}/api/products?shopId={tenantId}`.
  4. Return response.
- **If `shopId` not provided (catalog browse all):** forward to a default ShopERP OR return merged from all active instances (expensive — N HTTP calls). **Recommendation: require shopId for catalog browse in multi-VPS mode.** KhachLink Home page now uses `GET /api/catalog/recommended` (Phase 6), so full per-tenant catalog browse is no longer required.
- **NEW: forward `GET /shoperp/api/products/{id}/validate-price?unitPrice=...&vatRate=...&tenantId=...`** to correct ShopERP (needed for Phase 5 price validation at checkout). Implementation: add a route in `ProductsForwardingController` or a named `IHttpClientFactory` client resolved per ShopInstance.
- **This is significant scope addition to Phase 3.** User decision: include in Phase 3, or split to Phase 3b?
  - Decision (Round 2): keep in Phase 3 because checkout price validation depends on it.

### Command Layer (`3_CoreHub/Commands/`)
- **`CreateOrderCommand.cs`** — ADD `ProductName` + `VatRate` to `OrderItemRequest`:
  ```csharp
  public class OrderItemRequest {
      public Guid ProductId { get; set; }
      public Guid TenantId { get; set; }        // NEW — for grouping + routing
      public string ProductName { get; set; } = "";  // NEW — client snapshot
      public decimal VatRate { get; set; } = 0.10m;  // NEW — client snapshot
      public int Quantity { get; set; }
      public decimal UnitPrice { get; set; }
  }
  ```

### Service Layer (`3_CoreHub/Services/`)
- **`OrderService.CreateOrderFromCommandAsync`** — UPDATE:
  - **Remove `LoadProductsForSnapshotAsync` call** (the bug source). Use command-provided `ProductName` + `VatRate` instead.
  - `OrderItem.Create(...)` call uses `i.ProductName` + `i.VatRate` from command, not from Product entity.
  - **Backward compat:** if command-provided ProductName is empty (legacy caller), fall back to `LoadProductsForSnapshotAsync` (keep method, just don't call it by default). This preserves existing tests.
  - **Validation:** if `i.ProductName` is empty AND no product repository/DbContext → throw with clear message ("Client must provide ProductName for order creation").
- **`LoadProductsForSnapshotAsync`** — KEEP method (backward compat for legacy callers + tests). Not called by default path anymore.

### API Layer (`2_Gateway/Controllers/`)
- **`PublicOrdersController.cs`** — REWRITE `CreateCheckoutOrder`:
  - **REMOVE** `_dbContext.Products` lookup (the broken tenant resolution).
  - **REMOVE** `_orderService` direct call for single-order creation.
  - **ADD** `_serviceProvider` for scoped `IVanAnDbContext` (Tenants + ShopInstances lookup only — NOT Products).
  - New flow:
    1. Validate request: items non-empty, each item has `TenantId` + `ProductName` + `VatRate`.
    2. Group items by `TenantId`.
    3. For each tenant group:
       a. Lookup `Tenant.ShopInstanceId` → `ShopInstance.Id` (for routing key).
       b. Build `CreateOrderCommand` with tenant-scoped items (including ProductName + VatRate snapshot).
       c. Call `_orderService.CreateOrderFromCommandAsync(command, tenantId)`.
       d. After order created, update the Outbox event's NATS subject to include `{shopInstanceId}` (see Infrastructure section below).
    4. Return `CheckoutResponse { orders: [...], successCount, failureCount, errors: [...] }`.
  - **REMOVE** old single-order response shape (BREAKING — Phase 5 updates KhachLink).
- **`OnboardingController.cs`** (`api/v1/onboarding/tenants`) — REWRITE:
  - Keep: create Tenant (with ShopInstanceId), create Owner User, create UserTenant.
  - **Remove:** `IIndustrySeedStrategy` product seeding, `IProductRepository`, ingredient/recipe seeding, shop creation (ShopERP owns shop config now).
  - Update `OnboardTenantRequest` DTO: require `ShopInstanceId`.
  - Update `TenantOnboardingResult` DTO: include `ShopInstanceId` + `ShopBaseUrl`.
- **NEW: `CheckoutResponse.cs`** (DTOs):
  - `CheckoutResponse { List<CreatedOrderDto> Orders, int SuccessCount, int FailureCount, List<CheckoutErrorDto> Errors }`
  - `CreatedOrderDto { Guid OrderId, Guid TenantId, decimal Amount, decimal SubTotal, decimal TotalVatAmount }`
  - `CheckoutErrorDto { Guid TenantId, string Error }`
- **`CheckoutOrderRequest`** — UPDATE (existing inline class in PublicOrdersController):
  - `CheckoutOrderItem` ADD `TenantId` (Guid, required) + `ProductName` (string, required) + `VatRate` (decimal, required).

### Infrastructure Layer (`3_CoreHub/Infrastructure/`)
- **`OutboxEvent`** — REVIEW: currently `OutboxEvent(tenantId, electronicInvoiceId, eventType, eventData)`. The NATS subject is built by `NatsSyncWorker.BuildSubject(eventType, prefix)` → `vanan.cloud.order.created` (no routing key).
  - **Option A:** Add `RoutingKey` property to `OutboxEvent` → `BuildSubject` appends `.{routingKey}` if set.
  - **Option B:** Encode shopInstanceId in `EventType` field → `OrderCreated.{shopInstanceId}` → subject becomes `vanan.cloud.order.created.{shopInstanceId}`. Hacky but no schema change.
  - **Option C:** Add `TargetShopInstanceId` column to OutboxMessages table. `NatsSyncWorker.BuildSubject` checks this column and appends.
  - **Recommendation: Option A** — cleanest. Add `RoutingKey` (nullable string) to `OutboxEvent` value object. Migration adds nullable column. `BuildSubject` appends `.{routingKey}` if non-null.
- **`NatsSyncWorker.BuildSubject`** — UPDATE to append routing key:
  ```csharp
  private static string BuildSubject(string eventType, string prefix, string? routingKey = null)
  {
      var normalized = Regex.Replace(eventType, "([a-z])([A-Z])", "$1.$2").ToLowerInvariant().Replace('_', '.');
      return routingKey != null
          ? $"vanan.{prefix}.{normalized}.{routingKey}"
          : $"vanan.{prefix}.{normalized}";
  }
  ```
- **`OutboxRepository`** — UPDATE `EnqueueAsync` to accept optional routing key, store in new column.
- **Migration:** add `RoutingKey` column (nullable TEXT) to `OutboxMessages` table. Additive, no data loss.

### Service Layer (`2_Gateway/Services/`)
- **`DataSyncSubscriber.cs`** — COMMENT OUT `SyncProductUpsertAsync` cases (per Q3 — Gateway no longer needs products):
  ```csharp
  case "product.created":
  case "productcreated":
      // DISABLED per Option C (2026-07-18): Gateway PG no longer stores products.
      // Products live in ShopERP SQLite. Client provides snapshot at checkout.
      _logger.LogDebug("DataSyncSubscriber: product.sync disabled per Option C — event ignored");
      break;
  case "product.updated":
  case "productupdated":
      // DISABLED per Option C
      _logger.LogDebug("DataSyncSubscriber: product.sync disabled per Option C — event ignored");
      break;
  ```
- **Keep:** `SyncOrderCompletedAsync`, `SyncOrderStatusAsync`, `SyncCustomerCreatedAsync` (still relevant).

### DI Registration (`2_Gateway/Program.cs`)
- No new registrations needed. `IOrderService` + `IVanAnDbContext` already registered.

### Tests
- **NEW: `6_Tests/VanAn.Integration.Tests/PublicOrdersCreatorTests.cs`**:
  - `Checkout_WithSingleTenant_CreatesOrderInPG_WithClientSnapshot`
  - `Checkout_WithTwoTenants_CreatesTwoOrders_WithCorrectTenantPerOrder`
  - `Checkout_WithClientSnapshot_DoesNotQueryProductsTable` (verify no Products table hit — mock DbContext, assert Products never queried)
  - `Checkout_WithUnknownTenant_ReturnsErrorInResponse`
  - `Checkout_OutboxEvent_HasRoutingKeySetToShopInstanceId`
  - `Onboarding_CreatesTenantMetadata_WithoutProducts` (verify no product rows in PG after onboarding)
- **UPDATE existing `OrderServiceTests.cs`:** tests that relied on `LoadProductsForSnapshotAsync` being called — update to provide ProductName + VatRate in command. Keep some tests using the legacy fallback path (empty ProductName → LoadProducts).

### TDD Plan
1. Update `OrderItemRequest` DTO (add TenantId + ProductName + VatRate). Build → compile errors in tests (good).
2. Update existing OrderService tests to provide snapshot. Run → pass.
3. Write failing integration tests for new PublicOrdersController flow (mock DbContext, verify no Products query).
4. Rewrite `PublicOrdersController.CreateCheckoutOrder`.
5. Run tests → pass.
6. Add `RoutingKey` to `OutboxEvent` + migration + `NatsSyncWorker.BuildSubject` update.
7. Write test: `Checkout_OutboxEvent_HasRoutingKeySetToShopInstanceId`.
8. Rewrite `OnboardingController` to remove product seeding.
9. Comment out `DataSyncSubscriber` product sync cases.
10. Full regression: `dotnet build` + all tests + `guard-check.ps1`.

---

## 3. Contract Definitions (used by Phase 4 + 5)

### `POST /api/public/orders/checkout` (Gateway — incoming from KhachLink)
**Request:**
```json
{
  "customerDeviceId": "guid-string",
  "customerName": "string?",
  "customerPhone": "string?",
  "customerAddress": "string?",
  "customerId": "guid?",
  "items": [
    {
      "productId": "guid",
      "tenantId": "guid",
      "productName": "Cà phê sữa đá",
      "vatRate": 0.10,
      "quantity": 2,
      "unitPrice": 30000,
      "notes": "string?"
    }
  ]
}
```

**Response (200 OK):**
```json
{
  "orders": [
    {
      "orderId": "guid",
      "tenantId": "guid",
      "amount": 60000,
      "subTotal": 54545,
      "totalVatAmount": 5455
    }
  ],
  "successCount": 1,
  "failureCount": 0,
  "errors": []
}
```

**Response (200 OK with partial failure):**
```json
{
  "orders": [
    { "orderId": "...", "tenantId": "tenant-A-guid", "amount": 60000, ... }
  ],
  "successCount": 1,
  "failureCount": 1,
  "errors": [
    { "tenantId": "tenant-B-guid", "error": "Tenant not found or no ShopInstance assigned" }
  ]
}
```

### `GET /api/public/orders/{id}` (Gateway — UNCHANGED)
- Queries Gateway PG directly (Orders table is source of truth per Q1).
- No routing needed. Existing implementation works.
- **No change in Phase 3.**

### Outbox NATS subject (for Phase 4)
- Old: `vanan.cloud.order.created` (all ShopERP instances receive all orders)
- New: `vanan.cloud.order.created.{shopInstanceId}` (only matching ShopERP receives)
- Phase 4 updates `OrderSyncSubscriber` to subscribe to `vanan.cloud.order.created.{env.SHOP_INSTANCE_ID}` instead of wildcard.

---

## 4. Detailed Coding Plan

### Namespace Strategy
- `VanAn.CoreHub.Commands` (OrderItemRequest update)
- `VanAn.CoreHub.Services` (OrderService update)
- `VanAn.CoreHub.Infrastructure` (OutboxEvent, OutboxRepository, NatsSyncWorker — NatsSyncWorker is in CoreHub.Services)
- `VanAn.Gateway.Controllers` (PublicOrdersController rewrite, CheckoutResponse DTOs, OnboardingController rewrite)
- `VanAn.Gateway.Services` (DataSyncSubscriber update)
- `VanAn.Integration.Tests` (new tests)

### Implementation Steps
**Step 1 — Update `OrderItemRequest` DTO (1 file):**
- Add `TenantId`, `ProductName`, `VatRate` to `OrderItemRequest`.
- Build → compile errors in OrderService tests (expected — guides updates).

**Step 2 — Update existing OrderService tests (1 file):**
- Tests that call `CreateOrderFromCommandAsync` must now provide `ProductName` + `VatRate` in `OrderItemRequest`.
- Some tests can use empty `ProductName` to exercise the fallback path (LoadProducts).
- Run tests → all pass.

**Step 3 — Update `OrderService.CreateOrderFromCommandAsync` (1 file):**
- Replace `LoadProductsForSnapshotAsync` call with command-provided snapshot:
  ```csharp
  List<OrderItem> orderItems = command.Items.Select(i => {
      string productName = i.ProductName;
      decimal vatRate = i.VatRate;
      // Backward compat: if client didn't provide snapshot, try loading from DB
      if (string.IsNullOrEmpty(productName)) {
          var product = await LoadProductSingleAsync(i.ProductId, tenantIdObj);
          productName = product?.Name ?? "Unknown";
          vatRate = product?.VatRate ?? 0.10m;
      }
      return OrderItem.Create(Guid.NewGuid(), tenantIdObj, orderId, i.ProductId, i.Quantity, i.UnitPrice, productName, vatRate);
  }).ToList();
  ```
  (Note: Select with async lambda → use `SelectAsync` pattern or loop. Simpler: load all needed products in one query if any item lacks snapshot, then build list in loop.)
- Run tests → pass.

**Step 4 — CheckoutResponse DTOs (1 new file):**
- `CheckoutResponse.cs` with `CheckoutResponse`, `CreatedOrderDto`, `CheckoutErrorDto`.
- Build → 0 errors.

**Step 5 — OutboxEvent + NatsSyncWorker routing (3 files):**
- `OutboxEvent` value object: add `RoutingKey` (nullable string) property + constructor param.
- `OutboxRepository`: `EnqueueAsync` accepts optional routing key, stores in new column.
- `NatsSyncWorker.BuildSubject`: append `.{routingKey}` if non-null.
- Migration: add `RoutingKey` column to `OutboxMessages` (nullable TEXT, additive).
- Build → 0 errors.

**Step 6 — Rewrite `PublicOrdersController.CreateCheckoutOrder` (1 file):**
- Remove `_dbContext.Products` lookup.
- Add grouping by `TenantId`.
- For each group: lookup `Tenant.ShopInstanceId`, call `_orderService.CreateOrderFromCommandAsync`, then update Outbox event routing key.
  - **Issue:** `_orderService.CreateOrderFromCommandAsync` internally enqueues Outbox event. To set routing key, either (a) pass routing key into the method, or (b) update the event after creation, or (c) have OrderService accept a routing key param.
  - **Recommendation: (c)** — add `string? routingKey = null` param to `CreateOrderFromCommandAsync`. Pass through to `OutboxRepository.EnqueueAsync`. Cleanest.
- Return `CheckoutResponse`.
- Build → 0 errors.

**Step 7 — Integration tests (1 new file):**
- `PublicOrdersCreatorTests.cs` with mocked `IVanAnDbContext` (verify Products never queried).
- Run → all pass.

**Step 8 — Rewrite `OnboardingController` (1 file):**
- Remove `IIndustrySeedStrategy`, `IProductRepository` deps.
- Add `IShopInstanceService` dep (for ShopBaseUrl in response).
- `OnboardTenant`: create Tenant with ShopInstanceId, create Owner, create UserTenant. NO product seeding.
- Update `OnboardTenantRequest` + `TenantOnboardingResult` DTOs.
- Add test: `Onboarding_CreatesTenantMetadata_WithoutProducts`.
- Build → 0 errors.

**Step 9 — Disable product sync in `DataSyncSubscriber` (1 file):**
- Comment out `SyncProductUpsertAsync` cases with clear comment.
- Build → 0 errors.

**Step 10 — Full regression:**
- `dotnet build VanAn.sln` — 0 errors.
- `dotnet test` — all pass.
- `guard-check.ps1` PASS.

### Active Skills
- `system-refactor-safety` (architectural shift — Option B → Option C, but less drastic than pure router)
- `outbox-pattern-implementation` (routing key addition to Outbox)
- `domain-integrity-validation` (ensure no Domain change in this phase)

---

## 5. Validation Gates

| Gate | Command | Expected |
|---|---|---|
| Build | `dotnet build VanAn.sln` | 0 errors |
| Unit tests | `dotnet test 6_Tests/VanAn.Core.Tests` | All pass (existing + updated) |
| Integration tests | `dotnet test 6_Tests/VanAn.Integration.Tests --filter PublicOrdersCreator` | All pass |
| Onboarding test | `dotnet test --filter Onboarding` | Pass — no products in PG |
| Outbox routing test | `dotnet test --filter OutboxRoutingKey` | Pass — routing key set to ShopInstanceId |
| Existing tests | `dotnet test 6_Tests/VanAn.Core.Tests` | No regressions |
| Guard check | `./guard-check.ps1` | PASS |

---

## 6. Deliverables

- Modified: `3_CoreHub/Commands/CreateOrderCommand.cs` (OrderItemRequest: TenantId, ProductName, VatRate)
- Modified: `3_CoreHub/Services/OrderService.cs` (use client snapshot, add routingKey param)
- Modified: `1_Shared/Domain.cs` (add `RoutingKey` property to `OutboxEvent` — Domain modification)
- Modified: `3_CoreHub/Infrastructure/OutboxRepository.cs` (EnqueueAsync routing key)
- Modified: `3_CoreHub/Services/NatsSyncWorker.cs` (BuildSubject appends routing key)
- NEW: `3_CoreHub/Infrastructure/Migrations/{timestamp}_AddOutboxRoutingKey.cs`
- Modified: `2_Gateway/Controllers/PublicOrdersController.cs` (rewrite checkout, new response)
- Modified: `2_Gateway/Controllers/OnboardingController.cs` (remove product seeding)
- New: `2_Gateway/Controllers/CheckoutResponse.cs` (DTOs)
- Modified: `2_Gateway/Services/DataSyncSubscriber.cs` (disable product sync)
- New: `6_Tests/VanAn.Integration.Tests/PublicOrdersCreatorTests.cs`
- Modified: `6_Tests/VanAn.Core.Tests/Services/OrderServiceTests.cs` (update for snapshot)

---

## 7. Approval Gate

**Architectural shift + breaking API change + collateral updates — user must explicitly approve:**
- [ ] OrderItemRequest breaking change (added required fields TenantId, ProductName, VatRate) approved
- [ ] CheckoutResponse breaking change (array of orders instead of single order) approved
- [ ] OnboardingController removing product seeding approved (tenant owner runs QuickSetup manually)
- [ ] OutboxEvent + OutboxMessages table migration (additive) approved
- [ ] DataSyncSubscriber product sync disable approved
- [ ] OrderService accepting routingKey param approved
- [ ] POS Create.razor collateral update (add 3 fields to OrderItemRequest construction) approved
- [ ] CreateGuestOrder (Campaign flow) — defer fix or fix in Phase 3? **User decision needed.**
- [ ] Product catalog forward routing — include in Phase 3 OR split to Phase 3b? **User decision needed.** (Multi-VPS will break Scan.razor + Home.razor product fetch if not handled.)

**No Domain modification in this phase** (Phase 1 already approved).
