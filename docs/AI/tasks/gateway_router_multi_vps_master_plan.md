# Master Plan: Gateway Order Creator + Routed Async Delivery (Option C) — Multi-VPS Checkout

> **Status:** PHASE 1 + 2 + 3 COMPLETE — Phase 3.5 NEXT (in progress)
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Date opened:** 2026-07-18
> **Architecture shift:** Option B (Monolithic in-process, 2026-07-05) → Option C (PG source of truth + routed async delivery, multi-VPS)
> **Origin:** Bug "KhachLink checkout fails: Product not found for tenant 00000000-...-001" + multi-VPS co-location requirement

### Progress Tracker

| Phase | Status | Commit | VR Tests | Notes |
|---|---|---|---|---|
| 1 — Domain + Migration | ✅ COMPLETE | `32c832e9` | 13/13 PASS | ShopInstance entity + Tenant.ShopInstanceId FK + migration + seed + backfill |
| 2 — Gateway ShopInstances API | ✅ COMPLETE | `e95b1d64` | 8/8 PASS | IShopInstanceService + ShopInstancesController (7 endpoints, SystemAdmin Bearer JWT) |
| Bonus — RoleClaimNormalizer | ✅ COMPLETE | `98f1d6d8` | 2/2 PASS | Gateway accepts both short-form `role` + long-form `ClaimTypes.Role` in JWT |
| 3 — Gateway Order Creator | ✅ COMPLETE | `cdcb639e` + `b469c88c` | 4/5 PASS (RV5 pre-existing config) | Client snapshot + multi-tenant grouping + routed outbox + drop FK + product catalog forwarding |
| 3.5 — Accounting Consolidation | ⏳ IN PROGRESS | — | — | Split MarkPaidAsync + PaymentConfirmedSubscriber + EInvoiceSyncSubscriber |
| 4 — ShopERP OrderSyncSubscriber | ⏸ PENDING | — | — | Depends on Phase 3 contract |
| 5 — KhachLink Multi-tenant Cart | ⏸ PENDING | — | — | Depends on Phase 3 contract |
| 6 — Admin UI | ⏸ PENDING | — | — | Depends on Phase 1, 2 |
| 7 — Verification + Governance | ⏸ PENDING | — | — | Depends on all above |
| **3.6 — Deferred Cleanup** (NEW) | ⏸ PENDING | — | — | Onboarding refactor + Products forwarding port fix — see `phase3.6_deferred_cleanup_task_card.md` |

## 0. User Decisions (2026-07-18 — supersedes original §6 Open Questions)

| Q | Decision | Rationale |
|---|---|---|
| Q1 | **PG keeps `Orders` table as source of truth + routing queue.** Order created in PG → Outbox → NATS (routed by ShopInstanceId) → ShopERP SQLite. If ShopERP VPS disconnects, NATS/Outbox retries. | Keeps payment webhook working (Q4). Existing OrderSyncSubscriber already handles PG→SQLite. |
| Q4 | **No issue** — order stays in PG, webhook loads from PG as before. | Resolved by Q1. |
| Product snapshot | **Client (KhachLink) sends ProductName + VatRate per item.** Gateway creates order in PG WITHOUT querying Products table. ShopERP validates/enriches when receiving via NATS. | Gateway PG doesn't have products (sync broken). Client has ProductDto from catalog browse. Trust + verify pattern. |
| Order sync direction | **PG → SQLite (new primary direction for orders).** Existing `OrderSyncSubscriber` already implements this. Add ShopInstanceId routing key for multi-VPS. | Existing infra reused. |
| Domain modification | **Approved** — ShopInstance entity + Tenant.ShopInstanceId. | Foundation for routing. |

### User Decisions Round 2 (2026-07-18 — architecture refinement)

| Q | Decision | Rationale |
|---|---|---|
| QR code content | **Add `UnitPrice` + `VatRate` to QRCodePayload.** Owner must reprint QR when price/VAT changes. | Eliminates Scan.razor API call for product details. Faster scan, offline-capable. Staleness risk accepted with reprint reminder. |
| Scan.razor API call | **Remove API call entirely.** Scan uses QR data directly (ProductId, UnitPrice, VatRate). | Fast scan, no network dependency at scan time. |
| Price validation at checkout | **Gateway validates price via ShopERP HTTP at checkout.** If QR price ≠ current price → block order + notify customer "giá đã thay đổi". | Prevents stale-price orders. Validation at checkout (not scan) because checkout has network round-trip anyway. |
| Home.razor catalog | **Option 5: show only (a) products customer previously purchased + (b) sysadmin-featured products.** No full catalog browse. | Reduces multi-VPS routing complexity. PG stores lightweight FeaturedProduct table (marketing view), not full product catalog. |
| FeaturedProduct entity | **New entity `FeaturedProduct`** (not modify existing Product). | Stable, doesn't touch existing Product domain. Separate marketing concern from operational product data. |
| Accounting entry consolidation | **Create accounting entries ONLY in ShopERP SQLite.** Gateway webhook calls `MarkPaidAsync` (sets status + enqueues `OrderPaymentConfirmed` Outbox event → NATS `vanan.cloud.order.payment.confirmed.{shopInstanceId}`). ShopERP subscriber creates entries + e-invoice. | Single source of truth for accounting. NATS subject follows `NatsSyncWorker.BuildSubject` convention (camelCase split → dots). |
| E-invoice sync back | **Sync e-invoice result from ShopERP → PG** via NATS subject `vanan.shoperp.einvoice.synced.{shopInstanceId}` (produced by `NatsSyncWorker.BuildSubject("EInvoiceSynced", "shoperp")` + routing key). | Gateway admin can view e-invoice status without querying remote ShopERP. |
| Price validation toggle | **Owner tenant can ON/OFF price validation in ShopFeatures Settings.** Toggle `Price_Validation_Enabled` (default ON). When OFF, checkout trusts QR price (no ShopERP HTTP call — faster). | Owner flexibility: shops with stable prices can skip validation. Shops with frequent price changes keep it ON. |
| Home.razor scan button | **"Scan QR để mua" button opens scan window (modal) directly on Home.razor** — no page navigation. | Frictionless UX: discovery (Home) + ordering (Scan) in one screen. |

---

## 1. Business Context & Root Cause

### Bug being fixed
KhachLink checkout currently fails with:
```
KeyNotFoundException: Product {guid} not found for tenant 00000000-0000-0000-0000-000000000001
```

### Root cause (verified on VPS 2026-07-18)
| | SQLite (ShopERP) | PostgreSQL (Gateway) |
|---|---|---|
| Tenants | 8 (Coffee An An, Tạp Hóa Bà 5, Mimosa Spa, ...) | 2 (Vạn An Cafe + Vạn An Trading) |
| Products | 62 (multi-tenant) | 12 (single tenant) |

`PublicOrdersController.CreateCheckoutOrder` (Gateway) resolves `tenantId` by looking up `_dbContext.Products` in PG → product not in PG → falls back to hardcoded `00000000-...-001` → `OrderService.LoadProductsForSnapshotAsync` queries PG → not found → throw.

### Architectural conflict
`governance.md` declares **Option B (Monolithic in-process, 2026-07-05)**: Gateway hosts in-process CoreHub services + shares PostgreSQL with ShopERP. This assumes Gateway + ShopERP co-located on 1 VPS.

**Production reality:** Multi-VPS deployment — multiple ShopERP instances on separate VPS, 1 Gateway on another VPS. Option B breaks:
- Gateway PG cannot be single source of truth for products owned by remote ShopERP instances.
- Sync SQLite→PG across N VPS = N sync streams + data drift + ownership ambiguity.
- HTTP fallback from Gateway to "the ShopERP" only works when there's exactly 1 ShopERP.

### Decision (user-approved 2026-07-18)
- **Schema A:** Dedicated `ShopInstances` table (Id, BaseUrl, Label, MaxTenants, IsActive, HealthStatus, LastHealthCheck) + FK `Tenants.ShopInstanceId`.
- **Onboarding C:** Gateway creates tenant metadata + owner only. Product seeding is delegated to ShopERP remote (existing QuickSetup flow run by tenant owner after first login).
- **Gateway becomes pure router for checkout path:** No product data, no in-process order creation. Forward HTTP per-tenant to correct ShopERP.

### Multi-tenant cart requirement
Customer adds 2 products from 2 different tenants → checkout must create **2 separate orders**, one per tenant, each forwarded to the correct ShopERP.

---

## 2. Phase Breakdown (8 phases — updated per Round 2 decisions 2026-07-18)

| Phase | Focus | Files | Est. LOC | Depends on | Task Card |
|---|---|---|---|---|---|
| 1 | Domain + Migration | 3 | ~150 | — | `phase1_domain_migration_task_card.md` |
| 2 | Gateway ShopInstances API | 3 | ~250 | Phase 1 | `phase2_gateway_shop_instances_api_task_card.md` |
| 3 | Gateway Order Creator (client snapshot + multi-tenant grouping + routed outbox + price validation via ShopERP HTTP) | 5 | ~400 | Phase 1, 2 | `phase3_gateway_router_task_card.md` |
| 3.5 | Accounting Entry Consolidation (Gateway MarkPaid → NATS → ShopERP entries + e-invoice + sync-back) | 8 | ~400 | Phase 3, 4 | `phase3.5_accounting_consolidation_task_card.md` |
| 4 | ShopERP OrderSyncSubscriber routing key update | 2 | ~100 | Phase 3 contract | `phase4_shoperp_public_checkout_task_card.md` |
| 5 | KhachLink Multi-tenant Cart + Checkout UI + QR code with prices + Scan.razor no-API | 6 | ~400 | Phase 3 contract | `phase5_khachlink_multi_tenant_checkout_task_card.md` |
| 6 | Admin UI (Tenant Mgmt + Shop Instances + FeaturedProduct management + Home.razor catalog) | 7 | ~500 | Phase 1, 2 | `phase6_admin_ui_task_card.md` |
| 7 | Verification + Governance + State update | 3 | ~100 | All above | `phase7_verification_governance_task_card.md` |
| **3.6** | **Deferred Cleanup: Onboarding refactor + Products forwarding port fix** | ~5 | ~200 | Phase 4, 5 | `phase3.6_deferred_cleanup_task_card.md` |

**Total:** ~40 files, ~2400 LOC (increased from 1600 — added Phase 3.5 accounting consolidation, QR price content, FeaturedProduct/Home.razor, price validation endpoint).

**Phase boundary clarification:**
- **Phase 3:** Order creation + multi-tenant grouping + Outbox routing key (order.created). Does NOT touch payment/accounting.
- **Phase 3.5:** Split `ConfirmPaymentAsync` into `MarkPaidAsync` + accounting generation; add `OrderPaymentConfirmed` NATS event + e-invoice sync-back. Depends on Phase 3 routing key infrastructure and Phase 4 subscriber routing.
- **Phase 4:** Update `OrderSyncSubscriber` to routed subjects (`order.created.{shopInstanceId}` and `order.status.changed.{shopInstanceId}`).

### Phase dependency graph
```
Phase 1 (Domain/Migration)
  ├──> Phase 2 (ShopInstances API)
  │      └──> Phase 3 (Gateway Order Creator + Routed Outbox + Price Validation)
  │             ├──> Phase 4 (ShopERP OrderSyncSubscriber routing key)
  │             │      └──> Phase 3.5 (Accounting Consolidation)
  │             └──> Phase 5 (KhachLink UI + QR prices + Scan no-API)
  └──> Phase 6 (Admin UI + FeaturedProduct + Home.razor)
All ───> Phase 7 (Verification)
```

Phases 5, 6 can run in parallel after Phase 3. Phase 3.5 depends on Phase 4 (needs routed NATS subject pattern).

---

## 3. Architecture Target (Option C — PG source of truth + routed async delivery)

### Data ownership
| Data | Owner | Stored where | Gateway access |
|---|---|---|---|
| Products | ShopERP (per-tenant SQLite) | SQLite on tenant's VPS | None — Gateway never reads product (uses client-provided snapshot) |
| **Orders** | **Gateway (PG) — source of truth** | Gateway PG + replica in ShopERP SQLite (via NATS) | **Read/write — Gateway creates in PG, async-delivers to ShopERP** |
| Tenants metadata | Gateway (PG) | Gateway PG | Read/write |
| ShopInstances | Gateway (PG) | Gateway PG | Read/write (routing table) |
| Users, UserTenants | Gateway (PG) | Gateway PG | Read/write (auth) |
| Customers | ShopERP (per-tenant SQLite) | SQLite on tenant's VPS | Forward-only (client snapshot in order) |
| SocialCampaigns | Gateway (PG) | Gateway PG | Read/write |
| AccountingEntries | Gateway (PG) | Gateway PG | Read/write (payment webhook unchanged — Q4 resolved) |

### Checkout flow (target)
```
KhachLink (5002)
   │ POST /api/public/orders/checkout
   │ Body: { items: [{productId, tenantId, productName, vatRate, qty, unitPrice}], customerInfo }
   │         ↑ client provides snapshot — Gateway does NOT query Products table
   ▼
Gateway (5001) — Order creator + routing
   │ 1. Validate request (TenantId + ProductName + VatRate required per item)
   │ 2. Group items by TenantId
   │ 3. For each tenant group:
   │    a. Lookup Tenant.ShopInstanceId → for NATS routing key
   │    b. Create order in PG using client-provided snapshot (ProductName, VatRate)
   │       — OrderService.CreateOrderFromCommandAsync updated to use command snapshot
   │    c. Enqueue OrderCreated event to Outbox with subject vanan.cloud.order.created.{shopInstanceId}
   │ 4. Return CheckoutResponse { orders: [...], successCount, failureCount, errors: [] }
   ▼
NATS (routed by shopInstanceId — only matching ShopERP receives)
   ▼
ShopERP-A (VPS-A)                      ShopERP-B (VPS-B)
   │ OrderSyncSubscriber                │ OrderSyncSubscriber
   │ Subscribes vanan.cloud.order       │ Subscribes vanan.cloud.order
   │   .created.{shopInstanceId-A}      │   .created.{shopInstanceId-B}
   │ Writes order to SQLite             │ Writes order to SQLite
   │ Kitchen/POS display                │ Kitchen/POS display
   │                                     │
   │ If network down → NATS redelivers  │
   │   (or Outbox retries on Gateway)   │
```

### Key differences from original "pure router" plan
- **Gateway still creates orders in PG** (not pure router). Payment webhook unchanged (Q4 resolved).
- **No HTTP forward to ShopERP for checkout.** Delivery is async via NATS (existing OrderSyncSubscriber).
- **No new ShopERP public checkout endpoint** (Phase 4 reframed — just routing key update).
- **Client provides ProductName + VatRate** — Gateway doesn't need Products table.
- **Multi-VPS routing via NATS subject** `vanan.cloud.order.created.{shopInstanceId}` — each ShopERP subscribes only to its own.

### Onboarding flow (target, Option C — unchanged from original plan)
```
SysAdmin → /admin/tenants (on ShopERP admin UI, calls Gateway API)
   │ POST /api/v1/onboarding/tenants
   │ Body: { name, businessType, shopInstanceId, ownerUsername, ownerPassword, ... }
   ▼
Gateway (PG only)
   │ 1. Create Tenant row (with ShopInstanceId FK)
   │ 2. Create Owner User + UserTenant mapping
   │ 3. Return { tenantId, ownerId, shopInstanceId, shopBaseUrl }
   │ ❌ NO product seeding (was the bug source — Gateway PG had wrong products)
   ▼
Tenant owner → login on {shopBaseUrl} → /quicksetup
   │ Run QuickSetup (existing flow on ShopERP remote)
   │ Seeds products into SQLite of the correct ShopERP
```

---

## 4. Schema Changes (Phase 1 detail)

### New entity: `ShopInstance`
```csharp
// 1_Shared/Domain.cs (or split: 1_Shared/Domain/ShopInstance.cs)
public class ShopInstance : BaseEntity
{
    public string BaseUrl { get; private set; }       // http://shoperp-a:5003
    public string Label { get; private set; }          // "VPS-A HCM"
    public int MaxTenants { get; private set; }        // capacity, default 50
    public bool IsActive { get; private set; }
    public string? HealthCheckUrl { get; private set; }
    public DateTime? LastHealthCheck { get; private set; }
    public string HealthStatus { get; private set; }   // "Healthy" | "Degraded" | "Down" | "Unknown"
    public DateTime CreatedAt { get; private set; }

    // Factory
    public static ShopInstance Create(string baseUrl, string label, int maxTenants = 50, string? healthCheckUrl = null) { ... }
    public void UpdateHealth(string status, DateTime checkedAt) { ... }
    public void Deactivate() { IsActive = false; }
    public void Activate() { IsActive = true; }
}
```

### Tenant entity change
```csharp
public class Tenant : BaseEntity
{
    // ... existing ...
    public Guid? ShopInstanceId { get; private set; }   // FK to ShopInstance.Id (nullable for backward compat)
    public void AssignToShopInstance(Guid shopInstanceId) { ShopInstanceId = shopInstanceId; }
}
```

### EF Configuration + Migration
- `ShopInstanceConfiguration` in `3_CoreHub/Infrastructure/Configurations/`
- Update `TenantConfiguration` to add FK
- Migration: `AddShopInstancesAndTenantFk` — creates table, adds nullable FK column, **seeds 1 default ShopInstance** (VPS current, BaseUrl from config) + backfills all existing tenants to that instance.
- **NO data loss.** All existing tenants remain, just gain a nullable FK.

### `IVanAnDbContext` change
```csharp
DbSet<ShopInstance> ShopInstances { get; }
```

---

## 5. Gateway Router Contract (Phase 3 detail)

### `PublicOrdersController.CreateCheckoutOrder` rewrite
```csharp
[HttpPost("checkout")]
public async Task<ActionResult<CheckoutResponse>> CreateCheckoutOrder([FromBody] CheckoutOrderRequest request)
{
    // 1. Validate
    if (request?.Items == null || request.Items.Count == 0) return BadRequest(...);
    if (request.Items.Any(i => i.TenantId == Guid.Empty)) return BadRequest("TenantId required per item");

    // 2. Group items by TenantId
    var groups = request.Items.GroupBy(i => i.TenantId).ToList();

    // 3. For each tenant group: lookup ShopInstance, forward HTTP
    var createdOrders = new List<CreatedOrderDto>();
    var errors = new List<CheckoutErrorDto>();

    using var scope = _serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
    var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

    foreach (var group in groups)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == group.Key);
        if (tenant?.ShopInstanceId == null) { errors.Add(...); continue; }

        var shopInstance = await dbContext.ShopInstances.FirstOrDefaultAsync(s => s.Id == tenant.ShopInstanceId.Value);
        if (shopInstance == null || !shopInstance.IsActive) { errors.Add(...); continue; }

        var http = httpClientFactory.CreateClient($"shoperp-{shopInstance.Id}");
        // (or use a single "shoperp" client with dynamic BaseAddress set per call)

        var subRequest = new CheckoutOrderRequest {
            CustomerDeviceId = request.CustomerDeviceId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            CustomerAddress = request.CustomerAddress,
            CustomerId = request.CustomerId,
            Items = group.Select(i => i).ToList(),  // tenant-scoped
        };

        var resp = await http.PostAsJsonAsync("/api/public/orders/checkout", subRequest);
        if (resp.IsSuccessStatusCode) {
            var order = await resp.Content.ReadFromJsonAsync<CreatedOrderDto>();
            createdOrders.Add(order!);
        } else {
            errors.Add(new CheckoutErrorDto { TenantId = group.Key, Error = await resp.Content.ReadAsStringAsync() });
        }
    }

    return Ok(new CheckoutResponse { Orders = createdOrders, SuccessCount = createdOrders.Count, FailureCount = errors.Count, Errors = errors });
}
```

### Removed dependencies from `PublicOrdersController`
- `IOrderService orderService` — NO LONGER USED for checkout (still used for `GET /{id}` order tracking? See Phase 3 task card).
- `IVanAnDbContext dbContext` — STILL USED but only for `Tenants` + `ShopInstances` lookup (NOT for `Products`).

### Response contract change (BREAKING for KhachLink)
Old: `{ orderId, tenantId, qrImageUrl, paymentUrl, amount, subTotal, totalVatAmount }`
New: `{ orders: [{orderId, tenantId, amount, subTotal, totalVatAmount}], successCount, failureCount, errors: [{tenantId, error}] }`

KhachLink must handle list (Phase 5).

---

## 6. Open Questions — RESOLVED 2026-07-18

All open questions from original draft are now resolved per user decisions (see §0). Summary:

| Q | Resolution | Impact on plan |
|---|---|---|
| Q1 Order tracking routing | **PG keeps Orders table** (source of truth). `GET /api/public/orders/{id}` queries PG directly — no routing needed. KhachLink doesn't need to send tenantId for tracking. | Phase 3 simpler — no routing for GET, only for NATS delivery. Phase 5 simpler — no tenantId in tracking request. |
| Q2 Accounting integration | **Unchanged** — accounting stays in Gateway PG. Payment webhook loads order from PG, creates AccountingEntries in PG. | No impact. Webhook unchanged. |
| Q3 NATS sync direction | **PG→SQLite for orders** (primary new direction, routed by ShopInstanceId). SQLite→PG for product/order-status (existing, keep for status updates from kitchen/POS). Disable `SyncProductUpsertAsync` on Gateway's DataSyncSubscriber (Gateway no longer needs products). | Phase 3 disables product sync. Phase 4 updates OrderSyncSubscriber routing key. |
| Q4 Payment webhook | **No issue** — order in PG, webhook works as before. | Removed from risk register. |
| Product snapshot source | **Client (KhachLink) provides ProductName + VatRate per item.** Gateway trusts client snapshot for order creation. | Phase 5 adds ProductName + VatRate to checkout request. Phase 3 OrderService uses command-provided snapshot, no PG product lookup. |
| Domain modification | **Approved.** | Phase 1 unblocked. |

### Remaining open question (minor — resolve in Phase 3)
- **Q5: ShopERP instance identity** — how does each ShopERP know its own ShopInstanceId? Options: (a) env var `SHOP_INSTANCE_ID` set at deploy, (b) config file, (c) auto-register on first boot via Gateway API. **Recommendation: (a) env var — simplest, deploy-time configuration.** Phase 4 task card details.

---

## 7. Verification Strategy

### Per-phase gates
| Phase | Gate |
|---|---|
| 1 | `dotnet build VanAn.sln` 0 errors + migration applies cleanly on local PG + seed backfill works |
| 2 | `dotnet build` 0 errors + curl test: CRUD ShopInstance via Gateway API + health check returns "Healthy" for current VPS |
| 3 | `dotnet build` 0 errors + integration test: 1-tenant checkout forwards to ShopERP + 2-tenant checkout creates 2 orders + degraded test (1 ShopInstance down → graceful error in response) |
| 4 | `dotnet build` 0 errors + curl test: direct call to ShopERP `/api/public/orders/checkout` creates order in SQLite |
| 5 | `dotnet build` 0 errors + manual browser test: cart 2 products from 2 tenants → checkout → 2 orders created, both displayed |
| 6 | `dotnet build` 0 errors + admin UI: create ShopInstance, create tenant with ShopInstance selected, see tenant list with ShopERP URL column |
| 7 | `guard-check.ps1` PASS + VPS deploy + production smoke test + governance.md updated + project_state.md updated |

### Test scope
- **NO Playwright** during IMPLEMENT (per governance Playwright Guard). Playwright deferred to post-implementation validation phase.
- Unit tests for `ShopInstance` domain methods (Phase 1).
- Integration tests for `PublicOrdersController` router behavior with mocked HttpClient (Phase 3).
- Integration test for ShopERP public checkout endpoint (Phase 4).
- Manual browser verification for KhachLink UI (Phase 5).

---

## 8. Rollout Strategy

### Local dev (Phase 1-7 development)
- Single ShopInstance seeded: `BaseUrl = http://shoperp:5003` (local Docker).
- All existing tenants backfilled to this instance.
- Multi-tenant checkout testable locally with 2 tenants both on same local ShopERP.

### VPS production (Phase 7 deploy)
1. Deploy code to VPS.
2. Run migration on Gateway PG (adds table + backfills).
3. Seed 1 ShopInstance (current VPS, `BaseUrl = http://shoperp:5003` internal Docker network).
4. All 8 existing tenants linked to this 1 instance.
5. Smoke test: checkout 1 product → order created in ShopERP SQLite.
6. Smoke test: checkout 2 products from 2 tenants → 2 orders, both in SQLite.
7. Mark project_state.md Phase 1 COMPLETE.

### Future multi-VPS (post-Phase 7)
- SysAdmin creates new ShopInstance via `/admin/shop-instances` (`BaseUrl = http://shoperp-b:5003`).
- New tenant onboarding selects the new ShopInstance.
- Old tenants stay on their existing instance.
- No code change needed — pure config.

---

## 9. Governance Updates Required (Phase 7)

`.devin/rules/governance.md` — update:
- "Gateway operates in MONOLITHIC MODE (Option B approved 2026-07-05)" → "Gateway operates in ROUTER MODE (Option C approved 2026-07-18 — supersedes Option B). Gateway holds only routing + auth + accounting + social campaigns. Product/Order/Customer data lives in ShopERP per-tenant SQLite. Multi-VPS supported via ShopInstances routing table."

`.devin/rules/governance.md` — Critical Architectural Boundaries section:
- Update data flow diagram: KhachLink (5002) → Gateway (5001, router) → ShopERP-A/B/C... (per-tenant SQLite)
- Note: Gateway PG no longer receives product sync. NATS sync direction changes (products disabled, order status kept).

`.devin/rules/session-context.md` — no change (still lazy-load project_state.md).

`docs/Architecture/ADR-001-Station-Architecture.md` — add ADR-001 v3 addendum documenting Option C decision + rationale.

---

## 10. Risk Register (updated 2026-07-18 per user decisions)

| # | Risk | Mitigation |
|---|---|---|
| R1 | Migration on VPS PG fails | Test on local PG first; backup VPS PG before migration; migration is additive (no drop) |
| R2 | Existing KhachLink checkout breaks during deploy | Phase 5 deploy must precede Phase 3 Gateway cutover, OR deploy all phases atomically + feature flag |
| R3 | Order tracking `GET /{id}` regression | **Resolved** — PG keeps Orders, no routing needed for GET. |
| R4 | Payment webhook expects order in Gateway PG | **Resolved** — order stays in PG, webhook unchanged. |
| R5 | NATS delivery fails → ShopERP SQLite misses order | NATS built-in redelivery + Outbox retry on Gateway. OrderSyncSubscriber is idempotent (checks `exists` before insert). |
| R6 | Single ShopInstance down → checkout still works (order in PG), but kitchen/POS doesn't see new orders until ShopERP recovers | Phase 2 health check + admin alerting. Acceptable degradation — order isn't lost, just delayed for kitchen display. |
| R7 | Tenant created without ShopInstanceId → NATS routing fails | Phase 1 migration backfills all existing tenants. Phase 6 admin form requires ShopInstance selection. Add validation in onboarding endpoint. |
| R8 | Client-provided ProductName/VatRate snapshot is wrong (customer tampering, stale cache) | ShopERP validates against SQLite product when receiving order via NATS. If mismatch, log warning + use ShopERP's authoritative ProductName/VatRate. Gateway PG keeps client snapshot (acceptable for receipt display). |
| R9 | ShopERP doesn't know its ShopInstanceId → can't subscribe to routed subject | Env var `SHOP_INSTANCE_ID` set at deploy. Phase 4 validates env var present on startup, fails fast if missing. |

---

## 11. Task Card Index

| Phase | Task card file | Status |
|---|---|---|
| 1 | `phase1_domain_migration_task_card.md` | PLANNING — domain mod approved |
| 2 | `phase2_gateway_shop_instances_api_task_card.md` | PLANNING |
| 3 | `phase3_gateway_router_task_card.md` | PLANNING (rewritten 2026-07-18 — client snapshot + routed outbox, NOT pure router) |
| 4 | `phase4_shoperp_public_checkout_task_card.md` | PLANNING (rewritten 2026-07-18 — routing key update only, NOT new endpoint) |
| 5 | `phase5_khachlink_multi_tenant_checkout_task_card.md` | PLANNING (updated 2026-07-18 — adds ProductName/VatRate snapshot to request) |
| 6 | `phase6_admin_ui_task_card.md` | PLANNING |
| 7 | `phase7_verification_governance_task_card.md` | PLANNING |

Each task card follows `newfeaturebuild.md` workflow: ANALYZE → IMPLEMENT, with TDD plan, file list, validation gates.

---

## 12. Active Skills

- `domain-integrity-validation` — Phase 1 ShopInstance entity creation
- `system-refactor-safety` — Phase 3 Gateway architectural shift
- `outbox-pattern-implementation` — review outbox usage changes in Phase 3
- `ui-platform-compliance-review` — Phase 5 + 6 UI changes

---

## 13. Execution Order Recommendation

**Sequential (safest, slowest):** 1 → 2 → 3 → 4 → 5 → 6 → 7
**Parallel (faster, requires care):** 1 → (2 + 6 parallel) → 3 → (4 + 5 parallel) → 7

For first execution, recommend **sequential** to catch integration issues early. Each phase ends with `dotnet build` gate before next phase starts.

---

## 14. Approval Gate

**User must approve this master plan before any task card execution begins.**

On approval, Phase 1 task card is opened and execution starts. Each subsequent phase requires its own approval gate (per `newfeaturebuild.md` ANALYZE → IMPLEMENT workflow).
