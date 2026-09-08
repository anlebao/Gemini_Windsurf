# Task Card W3: Revenue Proof — Counters + Dashboard (D1)

> **Status:** ⏳ PLANNED (awaiting session start)
> **Week:** W3 / 5
> **Effort:** ~1 tuần
> **Master plan:** `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
> **Top-level card:** `docs/AI/tasks/gtm_drill_mvp/task_card.md`
> **Prerequisite:** ✅ W2 COMPLETE · ✅ Domain mod D1 APPROVED (2026-09-06)

## Objective

Merchant tự thấy "Vạn An đã tạo ra bao nhiêu doanh số cho bạn" — dashboard "THÁNG NÀY" per merchant: lượt xem cửa hàng → lượt chat → đơn → GMV. Pay-after-value: merchant thấy tiền trước, upgrade sau.

**Đồng thời là tính năng retention** — nối thẳng vào FI premium (một công đôi việc).

## Scope Checklist

### Task 3.1: DOMAIN MOD D1 (✅ APPROVED) — audit entity
**File:** `1_Shared/Domain/Aggregates/GrowthAggregate/StoreMetricDaily.cs` — NEW
- [ ] Precedent: `CrawlSource` (audit entity, PG-only, FK qua `BaseEntity.TenantId`, Single-Identity: Id = PK GUIDv7, không business key VO)
- [ ] Fields: `MetricDate` (date-only), `StoreViews` (int), `ChatClicks` (int) — unique (TenantId, MetricDate)
- [ ] KHÔNG đụng `AccountingEntry`/`BaseEntity`. Orders/GMV KHÔNG denormalize — query runtime từ Orders.
- [ ] Constructor sync: `Id = StoreMetricDailyId.Value` (Single-Identity pattern)
- [ ] EF config: `builder.Ignore(e => e.StoreMetricDailyId)` — no separate DB column

### Task 3.2: EF config + migration
**Files:** `3_CoreHub/Infrastructure/Configurations/StoreMetricDailyConfiguration.cs` — NEW · `IVanAnDbContext` + `VanAnDbContext` + DbSet · `5_WebApps/ShopERP/ShopERPDbContext` + `Ignore<StoreMetricDaily>()` (PG-only, pattern TenantClaimRequest) · PG migration — NEW
- [ ] `StoreMetricDailyConfiguration` — unique index (TenantId, MetricDate)
- [ ] `IVanAnDbContext.StoreMetricDailies` DbSet
- [ ] `VanAnDbContext.StoreMetricDailies` DbSet
- [ ] `ShopERPDbContext.Ignore<StoreMetricDaily>()` (PG-only, not in SQLite)
- [ ] PG migration `AddStoreMetricDaily` — create table + unique index

### Task 3.3: Metrics API
**File:** `2_Gateway/Controllers/GrowthController.cs` — UPDATE
- [ ] `POST /api/v1/growth/metrics/{tenantId}/event` (body `{type: "view"|"chat"}`) — `[AllowAnonymous]` + rate limit `growth-metrics` (60/IP/min) — upsert ngày hiện tại (idempotent increment)
- [ ] `GET /api/v1/growth/metrics/{tenantId}/monthly?month=yyyy-MM` → `{ storeViews, chatClicks, orders, gmv }` (orders/GMV query Orders theo tháng; Pattern #8: filter `e.TenantId == tenantId` trực tiếp)
- [ ] Rate limit policy `growth-metrics`: 60/IP/min FixedWindow (pattern `growth-audit`)

### Task 3.4: KhachLink counter wiring
**Files:** `5_WebApps/KhachLink/Services/Http/GrowthHttpService.cs` — NEW · `5_WebApps/KhachLink/Pages/Store.razor` — UPDATE
- [ ] `GrowthHttpService.RecordViewAsync(tenantId)` — POST event type "view"
- [ ] `GrowthHttpService.RecordChatAsync(tenantId)` — POST event type "chat"
- [ ] Store page load xong → POST view (1 call, không batch MVP); fire-and-forget
- [ ] Click nút chat/call → POST chat
- [ ] HTTP-only qua Gateway — KHÔNG inject DbContext (hard stop KhachLink)

### Task 3.5: Merchant dashboard
**File:** `5_WebApps/ShopERP/Pages/Growth/Monthly.razor` — NEW
- [ ] "Tháng này của bạn": lượt xem cửa hàng → lượt chat → đơn → GMV (funnel Revenue Proof — pay-after-value)
- [ ] `[Authorize(Policy="RequireOwnerRole")]`, **UI Platform components**
- [ ] Query: `GET /api/v1/growth/metrics/{tenantId}/monthly?month=current` qua Gateway (ShopERP → Gateway internal HTTP)
- [ ] Display: funnel visualization (views → chat → orders → GMV) + "Vạn An đã tạo ra ...đ doanh số cho bạn"

### Task 3.6: Tests
- [ ] Service: upsert idempotent (2 lần POST view → StoreViews = 2, không tạo row mới)
- [ ] Controller integration: anonymous POST 200; monthly aggregate đúng số
- [ ] Domain: `StoreMetricDaily` constructor sync Id, EF config Ignore business key
- [ ] Arch test: `StoreMetricDaily` không đụng `AccountingEntry`/`BaseEntity`

## Prerequisites

- ✅ W2 COMPLETE (Demo page live)
- ✅ Domain mod D1 APPROVED (2026-09-06)
- ✅ `GrowthController.cs` exists (W1)
- ✅ Gateway rate limiter infrastructure
- ✅ `Order` aggregate + PG query (for GMV)
- ✅ `ShopERPDbContext` PG-only Ignore pattern (precedent `TenantClaimRequest`)
- ✅ Pattern #8 documented (TenantId value object query)

## Verification

1. **Build:** `dotnet build VanAn.sln` → 0 errors
2. **Migration:** PG migration apply success — `StoreMetricDaily` table + unique index tồn tại
3. **API POST:** anonymous `POST /api/v1/growth/metrics/{tenantId}/event` `{type:"view"}` → 200; 2 lần POST → `StoreViews = 2`
4. **API GET:** `GET /api/v1/growth/metrics/{tenantId}/monthly?month=2026-09` → `{ storeViews: 2, chatClicks: 0, orders: N, gmv: M }`
5. **Rate limit:** 61st request within 1 min → 429
6. **KhachLink Store page:** load → POST view fires (verify Network tab); click chat → POST chat fires
7. **ShopERP dashboard:** `/growth/monthly` render funnel (views → chat → orders → GMV)
8. **Tests:** unit + integration + arch ALL PASS
9. **E2E:** `gtm-metrics.spec.ts` PASS (chạy sau build, theo Playwright rules)

## Governance checklist

- [ ] Domain purity: D1 = audit-type entity (precedent CrawlSource), FK qua `BaseEntity.TenantId`, Single-Identity. KHÔNG đụng `AccountingEntry`/`BaseEntity`.
- [ ] KhachLink HTTP-only: `GrowthHttpService` → Gateway, KHÔNG inject DbContext
- [ ] UI Platform: Monthly.razor dùng VanAn.UI.Platform components — no custom CSS
- [ ] Không tạo .csproj mới
- [ ] Pattern #8: query tenant/metrics so sánh property trực tiếp — cấm `EF.Property<Guid>`
- [ ] PG-only: `StoreMetricDaily` không trong SQLite (ShopERPDbContext.Ignore)
- [ ] Idempotent upsert: 2 lần POST view → increment, không tạo row mới
- [ ] Rate limit: 60/IP/min + 429 JSON response

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Domain mod D1 phá AccountingEntry/BaseEntity | Audit-type entity (precedent CrawlSource). KHÔNG đụng immutable entities. |
| R2 | Counter wiring gây chậm Store page | 1 POST view call/page load (fire-and-forget); rate limit 60/IP/min |
| R3 | PG migration fail trên production | Test migration local trước; backup PG trước deploy |
| R4 | GMV query chậm (Orders theo tháng) | Query runtime (không denormalize); index trên `Order.TenantId` + `Order.CreatedAt` đã có [V] |
| R5 | ShopERP → Gateway internal HTTP auth | Verify Gateway auth for internal calls; nếu cần API key → use existing ApiKey infrastructure |

## Files

| # | File | Action | Status |
|---|---|---|---|
| 1 | `1_Shared/Domain/Aggregates/GrowthAggregate/StoreMetricDaily.cs` | NEW (D1) | ⏳ |
| 2 | `3_CoreHub/Infrastructure/Configurations/StoreMetricDailyConfiguration.cs` | NEW | ⏳ |
| 3 | `3_CoreHub/Infrastructure/IVanAnDbContext.cs` + `VanAnDbContext.cs` | UPDATE (DbSet) | ⏳ |
| 4 | `5_WebApps/ShopERP/ShopERPDbContext.cs` | UPDATE (Ignore) | ⏳ |
| 5 | `3_CoreHub/Infrastructure/Migrations/AddStoreMetricDaily.cs` | NEW (PG migration) | ⏳ |
| 6 | `2_Gateway/Controllers/GrowthController.cs` | UPDATE (metrics endpoints) | ⏳ |
| 7 | `2_Gateway/Program.cs` | UPDATE (growth-metrics rate limit) | ⏳ |
| 8 | `5_WebApps/KhachLink/Services/Http/GrowthHttpService.cs` | NEW | ⏳ |
| 9 | `5_WebApps/KhachLink/Pages/Store.razor` | UPDATE (counter wiring) | ⏳ |
| 10 | `5_WebApps/ShopERP/Pages/Growth/Monthly.razor` | NEW | ⏳ |
| 11 | `6_Tests/VanAn.Core.Tests/Growth/StoreMetricDailyTests.cs` | NEW | ⏳ |
| 12 | `6_Tests/VanAn.Integration.Tests/Growth/GrowthControllerTests.cs` | NEW | ⏳ |
| 13 | `6_Testing/e2e-tests/gtm-metrics.spec.ts` | NEW | ⏳ |

## Open questions (resolve before/during W3 session)

1. **ShopERP → Gateway internal HTTP:** ShopERP dashboard gọi Gateway qua HTTP hay query PG trực tiếp? → Verify architecture. Nếu ShopERP có quyền query PG (Accounting DB) → query trực tiếp `StoreMetricDaily` + `Orders` (đơn giản hơn, không cần Gateway round-trip). Nếu không → Gateway HTTP.
2. **GMV calculation:** GMV = tổng `Order.TotalAmount` cho orders completed trong tháng? Hay bao gồm pending/cancelled? → Verify `Order.Status` enum + business definition.
3. **Funnel visualization:** Simple text/table hay chart component? → UI Platform có chart component không? Nếu không → simple table MVP.

## Related

- Master plan: `docs/AI/tasks/gtm_drill_mvp/master_plan.md`
- Top-level card: `docs/AI/tasks/gtm_drill_mvp/task_card.md`
- W2 (done): `docs/AI/tasks/gtm_drill_mvp/task_card_w2_interactive_demo.md`
- W4 (next): `docs/AI/tasks/gtm_drill_mvp/task_card_w4_merchant_referral.md`
- CrawlSource (D1 precedent): `1_Shared/Domain/Aggregates/TenantAggregate/CrawlSource.cs`
- TenantClaimRequest (PG-only Ignore precedent): `1_Shared/Domain/Aggregates/TenantAggregate/TenantClaimRequest.cs`
- GrowthController (W1): `2_Gateway/Controllers/GrowthController.cs`
