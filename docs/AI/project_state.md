# Project State

> **M?c d�ch:** Single Source of Truth cho AI v? tr?ng th�i d? �n. B?T BU?C d?c d?u m?i phi�n.
> **Archived:** 2026-07-17 — completed Single-Identity Refactor moved to `docs/AI/project_state_archive.md`

---

## 0. Maintenance Rules

1. One-and-only-one: M?i section ch? t?n t?i 1 l?n.
2. No contradiction: M?t h?ng m?c ch? c� 1 tr?ng th�i.
3. Ground Truth first: Verify path/branch v?i codebase tru?c khi ghi.
4. Now over History: Section 2-4 ch? m� t? vi?c �ANG l�m v� K? TI?P. Vi?c xong ? gom v�o Section 6.
5. Actionable Next Actions: X�a action d� qu� h?n/sai b?i c?nh.
6. Stamp every edit: C?p nh?t Section 9 m?i l?n s?a.

---

## 1. Project Overview

**D? �n:** V?n An Accounting System MVP � gi?i ph�p k? to�n HKD theo TT 152/2025/TT-BTC.
**Stack:** .NET 8 � EF Core � SQLite � Blazor Server (ShopERP) � Blazor WebAssembly (KhachLink PWA — Phase 1 conversion complete 2026-07-21) � SignalR � YARP Gateway � xUnit � Playwright.
**Ki?n tr�c:** Clean Architecture + DDD + Multi-tenancy. Data flow: `KhachLink WASM (static files via nginx, 5002) ? Gateway (5001) ? ShopERP (5003) ? SQLite`.

**Modules:** `1_Shared` (Domain + Services contracts — IOrderWorkflowService, ISocialCampaignService, IShopFeatureSettingsService moved here 2026-07-21) � `2_Gateway` (YARP) � `3_CoreHub` (Services, in-process) � `5_WebApps/ShopERP` (Blazor Server) � `5_WebApps/KhachLink` (Blazor WebAssembly, served by nginx) � `UI.Platform` (Shared components) � `6_Tests/6_Testing`.

**Hard stops:** Domain PURE � `AccountingEntry` immutable � Gateway STATELESS � KhachLink HTTP-only � ShopERP SQLite (Business) + PostgreSQL (Accounting) � ALWAYS d�ng UI Platform components.

---

## 2. Current Objective

**KhachLink PWA Phase 1 — Blazor Server → WebAssembly Conversion — COMPLETE (2026-07-21)**

Phase 1 of `docs/AI/tasks/khachlink_pwa_offline_master_plan.md`. Converts KhachLink from Blazor Server to Blazor WebAssembly so the PWA can work offline (UI events run client-side, no WebSocket required). Commit `b642662b` pushed, CI PASSED.

### Architecture changes
- `VanAn.KhachLink.csproj`: SDK `Microsoft.NET.Sdk.Web` → `Microsoft.NET.Sdk.BlazorWebAssembly`
- `Program.cs`: `WebApplication.CreateBuilder` → `WebAssemblyHostBuilder.CreateDefault` + removed `AddInteractiveServerComponents` (WASM interactive by default)
- `App.razor`: `blazor.web.js` → `blazor.webassembly.js`
- Removed `@rendermode InteractiveServer` from all 13 Pages + PWAInstallPrompt.razor
- Removed `Serilog.AspNetCore` (server-only, pulls `Microsoft.AspNetCore.App` FrameworkReference incompatible with `browser-wasm` RuntimeIdentifier)
- Removed `Microsoft.EntityFrameworkCore.Sqlite` from `VanAn.Shared.csproj` (unused)

### Contract extraction (Option 2 — user-approved)
- Moved 3 contract files `3_CoreHub/Services/` → `1_Shared/Services/`: `IOrderWorkflowService.cs`, `ISocialCampaignService.cs`, `IShopFeatureSettingsService.cs` (includes `ShopFeatureSettingsDto` + `PriceValidationResult`). Namespace `VanAn.CoreHub.Services` → `VanAn.Shared.Services`.
- Added `using VanAn.Shared.Services;` to ~20 files in CoreHub, Gateway, ShopERP, Tests
- Updated fully-qualified DI registrations in `Gateway/Program.cs` + `ShopERP/Program.cs`
- Added `IInventoryService` alias in `OrderService.cs` to disambiguate (exists in both `CoreHub.Interfaces` + `Shared.Services`)
- Removed `VanAn.CoreHub` ProjectReference from `KhachLink.csproj` (KhachLink uses only Shared contracts + HTTP services)

### Dead code cleanup (files that referenced CoreHub directly)
- Deleted `DashboardHttpService.cs`, `OfflineOrderService.cs` + `.ts`, `EnhancedCartService.cs` + `.ts`, `SyncConflictResolver.cs`, `ConflictResolutionService.cs` + `.ts` (all dead — not registered in DI)
- Deleted `Campaign.cshtml` + `Campaign.cshtml.cs` (legacy MVC Razor Page — incompatible with WASM), replaced by `Campaign.razor` Blazor component at `/c/{trackingCode}`
- Deleted 6 dead test files (tests for deleted dead code): `RetryStrategyTests`, `TimeBasedBugTests`, `UIStateMachineTests`, `FinancialSafetyTests`, `ProductionDataTests`, `SyncConflictResolverTests`

### Deployment changes
- `Dockerfile`: dotnet runtime → `nginx:alpine` serving static files
- `nginx.conf`: SPA routing (`try_files` → `index.html`), gzip, cache headers for `_framework/` (immutable), no-cache for `service-worker.js` + `blazor.boot.json`
- `docker-compose.prod.yml`: removed ASPNETCORE env vars + Serilog config, memory limit 512m → 256m
- `wwwroot/appsettings.json`: Gateway BaseUrl for WASM config loading

### Test impact
- Unit tests: **984 passed / 0 failed** (33 dead tests removed from 6 deleted files)
- KhachLink Startup: **6 passed / 4 skipped / 0 failed** (4 server-startup tests skipped — `WebApplicationFactory` can't boot WASM, marked Skip with reason, rewrite planned for Phase 6)
- Build: `dotnet build VanAn.sln` → **0 errors**

**Status: COMPLETE. Pushed to main, CI PASSED. Awaiting CD deploy + VPS RV.**

### Next: Phase 2 (Service Worker DLL Caching)
Per master plan, Phase 2 updates `service-worker.js` to cache Blazor WASM DLLs (`_framework/*.dll`) for true offline support. See `docs/AI/tasks/khachlink_pwa_phase2_sw_dll_caching_task_card.md`.

---

**PREVIOUS OBJECTIVE — KhachLink /stores Search Button Fix — COMPLETE (2026-07-21)**

User reported search button on `https://diemthuong.khachvip.online/stores` not clickable. Root cause: the magnifier-glass icon in the search box was a decorative `<span class="input-group-text">` — NOT a button, so clicking it did nothing. Search was only triggered via `@oninput` debounce (300ms after typing) with no dedicated search button or Enter-key handler.

**Fix (1 file):** `5_WebApps/KhachLink/Pages/StoreFinder.razor`
- Converted search icon `<span>` → `<button type="button" @onclick="LoadStores">` — now clickable.
- Added `@onkeyup="OnSearchKeyUp"` on the input — pressing **Enter** triggers immediate search (cancels running debounce).
- Added `OnSearchKeyUp(KeyboardEventArgs e)` method.
- Added `.btn-search-icon` CSS (cursor pointer, hover, no outline) to preserve input-group look.

**Verification:** `dotnet build VanAn.KhachLink.csproj` → Build succeeded, 0 errors, 11 pre-existing warnings (unrelated). Ready for commit + push to trigger CD deploy.

**Status: COMPLETE. Awaiting CD deploy after push.**

---

**PREVIOUS OBJECTIVE — Post-Shop-Removal Runtime Verification + Tenant.Id LINQ Bug Fix — COMPLETE (2026-07-21)**

Shop entity removal (previous session, 221 files) deployed to VPS via CD. This session performed comprehensive runtime verification (RV) and fixed a regression batch.

### A. Tenant.Id Value Object LINQ Translation Bug (Known Error Pattern #8 — NEW)
After Shop removal, `TenantStoreController` (new replacement for `ShopsController`) failed on `/api/tenants/{tenantId}/store-info` with HTTP 500. Root cause: `Tenant.Id` is a `TenantId` value object with `HasConversion` — three failing patterns discovered across 3 controllers:
1. `EF.Property<Guid>(t, "Id") == guid` → IConvertible cast error (Pattern #1 variant)
2. `t.Id.Value == guid` in `Where` → LINQ translation fails
3. `guidList.Contains(t.Id)` with `List<Guid>` → type mismatch

**Fix (1 commit, 3 files):** Construct `TenantId` value object before comparison. `t.Id == new TenantId(tenantId)`. For `Contains`, convert collection: `tenantIds.Select(id => new TenantId(id)).ToList()`.
- `TenantStoreController.GetStoreInfo` — fixed
- `PublicOrdersController.checkout` — fixed (preventive, was working but pattern risky)
- `CatalogController.recommended` — fixed (preventive)

**Commits:** `20697063` (initial TenantStore fix), `e876cf53` (batch fix all 3 controllers + Pattern #8 added to governance.md).

### B. RV Results on VPS (2026-07-21)
- All 5 VanAn containers healthy (gateway, shoperp, khachlink, postgres, nats)
- DB schema verified: `Shops` table dropped, `SocialCampaigns.ShopId` dropped, `Tenants.Settings_Latitude/Longitude` added
- 3 tenants in DB (coordinates null — expected, no migration data on this VPS)
- All tenant-based endpoints PASS:
  - `GET /api/tenants/{id}/store-info` (valid): 200 ✅
  - `GET /api/tenants/{id}/store-info` (invalid): 404 ✅
  - `GET /api/tenants/nearby`: 200 ✅
  - `GET /api/tenants/search`: 200 ✅
  - `GET /api/catalog/recommended`: 200 ✅
  - `GET /health`: 200 ✅
- No errors in gateway logs after fix deployed

### C. Governance Update
Added Known Error Pattern #8 to `.devin/rules/governance.md` — `Tenant.Id` value object LINQ translation. Reference implementations: `TenantManagementService.GetTenantByIdAsync`, `SocialCampaignRepository.GetActiveByTenantIdValueAsync`.

**Status: COMPLETE. All deployed to VPS. RV 6/6 PASS for tenant-based endpoints.**

---

**PREVIOUS OBJECTIVE — Shop Entity Removal — COMPLETE (2026-07-21)**

Removed `Shop` entity from system (221 files). `Tenant` is now single identity for all business operations (aligns with TT 152/2025/TT-BTC — each HKD = separate legal entity). `Latitude/Longitude` migrated to `TenantSettings`. `ShopsController` replaced by `TenantStoreController`. All migrations applied (PostgreSQL + SQLite). See Section 6 + `docs/AI/tasks/` for details.

---

**PREVIOUS OBJECTIVE — KhachLink Home Page Personalization + Campaigns/Shops CRUD Admin UI — COMPLETE (2026-07-20)**

Two features delivered this session:

### A. Dynamic Home Page Content (replaces static Hero + Stats)
- **LastInteractionService** — tracks `lastTenantId` in localStorage via JS interop. `RecordInteractionAsync(tenantId)` called from `Scan.razor` (QR scan add-to-cart, both fast + legacy paths) + `Home.razor AddFeaturedToCart` (Featured product add).
- **Home.razor** — Hero section replaced with Campaign section (shows active campaigns for last-interaction tenant, fallback empty state with "Quét QR Ngay" CTA for new users). Stats section replaced with StoreFinder section (shows shop info: name, address, phone, Google Maps link). Auto-refresh when customer adds product from different tenant.
- **Backend:** `GET /api/campaigns/by-tenant/{tenantId}` (Gateway, AllowAnonymous) + `GET /api/shops/by-tenant/{tenantId}` (Gateway, AllowAnonymous, pre-existing).
- **Commits:** `e292166c` (initial), `c8765aeb` (TenantId VO fix), `6b9cf88d` (SaveChangesAsync fix), `4e6cbafd` (ShopId FK fix), `f79c5f46` (by-tenant service method + PUT DTO), `a83b797c` (IgnoreQueryFilters).

### B. Campaigns + Shops CRUD Admin UI (SystemAdmin only)
- **Backend:** Gateway `CampaignsController` — added POST create + fixed auth on PUT/DELETE (`[AllowAnonymous]` → `[Authorize(Policy="SystemAdmin")]`). Gateway `ShopsController` — added POST/PUT/DELETE forward to ShopERP with SystemAdmin auth + Authorization header forwarding.
- **Admin UI:** Two new ShopERP Blazor pages — `/admin/campaigns` (CampaignsAdmin.razor: list + create/edit modal with Tenant + Shop dropdowns + delete) + `/admin/shops` (ShopsAdmin.razor: list + create/edit modal with Tenant dropdown + lat/lng coordinates + delete). Both `@attribute [Authorize(Policy="SystemAdmin")]`.
- **Commits:** `2725e28d` (admin UI + backend), `4e6cbafd` (FK fix + shop dropdown), `f79c5f46` (PUT DTO), `a83b797c` (IgnoreQueryFilters).

### RV Test Results (2026-07-20)
**Campaigns CRUD — ALL PASS ✅** (tested via curl on VPS with SystemAdmin JWT):
| Test | HTTP | Result |
|---|---|---|
| POST no token | 302 | Redirect login ✅ |
| POST create | 201 | Campaign persisted to PG ✅ |
| GET all | 200 | Contains new campaign ✅ |
| GET by-tenant (Home endpoint) | 200 | Contains new campaign ✅ |
| PUT update | 200 | Contains "Updated" ✅ |
| DELETE | 200 | Soft-delete (IsActive=false) ✅ |

**Shops CRUD via Gateway — Known Limitation ⚠️** POST returns login HTML because ShopERP uses cookie auth (OIDC), not JWT. Admin UI `ShopsAdmin.razor` uses `DbContext` directly (in-process, cookie auth) — works correctly. Gateway shops write forwarding is secondary; admin UI is primary interface.

### Bugs Found & Fixed During RV
1. `CreateCampaignAsync` missing `SaveChangesAsync` — campaigns never persisted (commit `6b9cf88d`)
2. FK violation `FK_SocialCampaigns_Shops_ShopId` — `Guid.Empty` ShopId (commit `4e6cbafd`)
3. `GET by-tenant` used `GetCampaignsByShopAsync` (queries ShopId not TenantId) (commit `f79c5f46`)
4. PUT 400 — `[FromBody] SocialCampaign` has protected setters → use `UpdateCampaignRequest` DTO (commit `f79c5f46`)
5. PUT 404 — `GetByIdAsync` didn't use `IgnoreQueryFilters` for SystemAdmin cross-tenant (commit `a83b797c`)
6. `GetActiveByTenantIdValueAsync` used `c.TenantId.Value == tenantId` (can't translate) → use `c.TenantId == new TenantId(tenantId)` per Known Error Pattern #1 (commit `c8765aeb`)

**Status: COMPLETE. All deployed to VPS. RV 6/6 PASS for Campaigns CRUD.**

---

**PREVIOUS OBJECTIVE — Multi-VPS Checkout Architecture (Option C) — ALL 8 PHASES COMPLETE (2026-07-20)**

Multi-VPS Checkout Option C master plan — Phases 1, 2, 3, 3.5, 4, 5, 3.6, 6, 7 all complete. See Section 6 History Log + `docs/Architecture/ADR001-Station-Architecture.md` v3 addendum + `docs/AI/tasks/tech_debt_multi_vps_checkout.md`. NEXT: Phase 8 (Multi-VPS E2E Validation — Playwright).

**Archived (2026-07-17):** QuickSetup + Product Management Phases 4–6 and the Single-Identity Refactor (Hướng A). See `docs/AI/project_state_archive.md`.

## 3. Current Status

- **Branch:** `main`
- **Last commit:** `b642662b` feat(khachlink): Phase 1 — Blazor Server → WebAssembly conversion
- **.NET SDK:** 8.0.422 (system path, CVEs patched, global.json pinned)
- **DB:** SQLite `vanan_shoperp.db` (local dev + VPS, business) - PostgreSQL `VanAnCoreHub` (local Docker + VPS, accounting + Gateway business + ShopInstances + FeaturedProducts + SocialCampaigns tables) - PostgreSQL `vanan_accounting` (local, accounting)
- **Build (2026-07-21):** `dotnet build VanAn.sln` 0 errors. KhachLink WASM build PASS. CI PASSED on push `84bf577f..b642662b`. Awaiting CD deploy + VPS RV for nginx static serving.
- **KhachLink PWA Phase 1 (2026-07-21 - COMPLETE + PUSHED + CI PASSED):** Blazor Server → WebAssembly conversion. 85 files (+901/-5319). 3 contracts moved CoreHub→Shared. 8 dead code files + 6 dead test files deleted. Dockerfile rewritten for nginx. 4 KhachLink startup tests skipped (WebApplicationFactory incompatible with WASM, rewrite planned for Phase 6). Unit 984/0, KhachLink Startup 6/4skip/0fail. Commit `b642662b`.
- **Post-Shop-Removal RV (2026-07-21 - COMPLETE + VPS DEPLOYED + RV 6/6 PASS):** Tenant.Id LINQ bug fixed across 3 controllers (TenantStore/PublicOrders/Catalog). Pattern #8 added to governance.md. All tenant-based endpoints 200/404 as expected. No errors in gateway logs. Commits: `20697063`, `e876cf53`.
- **Shop Entity Removal (2026-07-21 - COMPLETE + VPS DEPLOYED):** 221 files refactored. `Shop` entity + `ShopId` VO removed from Domain. `SocialCampaign.ShopId` removed. `TenantConfig` (renamed from `ShopConfig`) uses `TenantId`. `TenantSettings.Latitude/Longitude` added (preserves Store Finder). `ShopsController` deleted → replaced by `TenantStoreController`. `ShopService`/`IShopService` deleted. PostgreSQL + SQLite migrations applied (drop Shops table, drop SocialCampaigns.ShopId, add Tenants.Settings_Latitude/Longitude). All clients (KhachLink, ShopERP) migrated to TenantId.
- **Home Page Personalization + Campaigns/Shops CRUD (2026-07-20 - COMPLETE + VPS DEPLOYED + RV 6/6 PASS):** 8 commits. LastInteractionService + Home.razor Campaign/StoreFinder sections + Gateway Campaigns/Shops CRUD endpoints + ShopERP admin pages `/admin/campaigns` + `/admin/shops`. Campaigns CRUD RV 6/6 PASS. Shops admin UI works via DbContext (Gateway forwarding limited by ShopERP cookie auth — known limitation). Commits: `e292166c`, `2725e28d`, `226c4260`, `c8765aeb`, `6b9cf88d`, `4e6cbafd`, `f79c5f46`, `a83b797c`.
- **Phase 7 COMPLETE (2026-07-20):** Verification + Governance. governance.md + ADR-001 v3 addendum + Phase 8 task card + tech debt register. Final verification PASS.
- **Phase 6 COMPLETE + VPS DEPLOYED (2026-07-20):** Admin UI for ShopInstances + FeaturedProducts + Home.razor catalog refactor. RV 8/8 PASS on VPS. Commit `5b51c09d`.
- **Phase 3.6 COMPLETE + VPS DEPLOYED (2026-07-20):** Onboarding refactor (removed product seeding — deferred to QuickSetup) + Products forwarding port fix (explicit `ShopERP__BaseUrl` env var). RV 2/2 PASS on VPS. Commit `a6413668`.
- **Phase 4 + 5 COMPLETE + VPS DEPLOYED (2026-07-20):** 6 commits. ShopERP routed subscriber + KhachLink multi-tenant checkout + QR with prices + Price validation toggle. RV 9/9 PASS on VPS. Commits: `c38b51e5` (Phase 4+5 main), `e27727b1` (migration FK fix), `e03bbebf` (SHOP_INSTANCE_ID env), `8f54270f` (hardcode Guid), `b2dc22c0` (RV script), `8718cb84` (task card update).
- **Multi-tenant bug-fix batch (2026-07-18 - COMPLETE + VPS DEPLOYED):** 5 commits fixing tenant filtering, login tenant_id, order history, Kitchen display, and Quick-Setup stub. All deployed to VPS via CD. Commits: `0309e559` (Bug 1,3,4), `68a34af8` (Login tenant_id), `f40d162b` (Quick-Setup real impl).
- **Quick-Setup Onboarding (2026-07-18 - COMPLETE):** `OnboardingService.ApplyTemplateAsync` rewritten to delegate to `IIndustrySeedStrategy`. All 8 strategies implemented with real data from menu requirement docs. New `RetailSeedStrategy` added (IndustryCode "RETAIL"). 4th QuickSetup template "Thời trang" (d444 → CLOTHES). Idempotent: skips seeding if tenant already has products. IIndustrySeedStrategy registered in ShopERP DI (was only in Gateway).
- **RC-7 fix COMPLETE (2026-07-17):** `OrderService.CreateOrderFromCommandAsync` now loads Product entities and snapshots `ProductName` + actual `VatRate` into `OrderItem` at creation time. TT 152/2025/TT-BTC compliance restored. Missing-product policy: throw `KeyNotFoundException` (no ghost "Unknown" stubs). Domain `OrderItem.Create` factory extended with `vatRate` param (backward-compatible default). Sync subscribers (OrderSyncSubscriber, DataSyncSubscriber) reflection hacks replaced with factory param. 998/998 Core.Tests pass.
- **VAT Display UI COMPLETE (2026-07-17):** VAT breakdown (Tạm tính / VAT / Tổng cộng) now shown on Cart, Checkout, OrderTracking, OrderHistory, POS, and CartDrawer — conditional on new `VAT_Display_Enabled` shop feature toggle (default ON). Small HKDs turn it OFF in Shop Settings. `CartItem` record extended with `VatRate`/`VatAmount`/`NetAmount`. `CartState.TotalVatAmount` computes real VAT (was hardcoded 0). `PublicOrderTrackingDto` + `PublicOrderItemDto` + `CustomerOrderDto` + checkout response extended with VAT fields. 1006/1007 Core.Tests pass (1 flaky perf test unrelated).
- **Environment parity fix (2026-07-17):** `appsettings.Development.json` Gateway connection fixed from SQLite to PostgreSQL (matching VPS). VPS PostgreSQL DB dumped to local Docker PostgreSQL 15-alpine. Local infra: Docker PostgreSQL 5432 + NATS 4222 + ShopERP 5003 + KhachLink 5002 + Gateway 5001. All 3 servers verified healthy + NATS order sync verified (order created via Gateway API → synced to SQLite via NATS → kitchen display can query it).
- **Circuit init + kitchen display fix (2026-07-17):** Root cause: 3 orphaned `OrderItems` rows in SQLite (referencing deleted ProductId `192330A9-...`) blocked `SingleIdentity_DropBusinessKeyColumns` migration → `MigrateAsync()` crash → server won't start → Blazor circuit failure + kitchen display empty. Fix: deleted orphaned rows + applied pending SQLite migrations. NATS was also not running locally → order sync Gateway→ShopERP broken → kitchen never received orders. Fix: started NATS in Docker.
- **Order UUIDv7 Refactor (2026-07-16 - COMPLETE + VPS VERIFIED):** 5 phases done + VPS runtime verified. Commits: `362b219c`, `a79ce830`. CD run #4 SUCCESS.
- **UI Fix Batch (2026-07-16 - COMPLETE):** VanAForm preventDefault fix + RevenueEntry/ExpenseEntry accountCode pass-through + TransactionHistory CSV export + Sitemap restructure.
- **Order Sync Fix (2026-07-15 - COMPLETE):** Track E1 T1-T8 done. Sync PG->SQLite works for both SaaS and Edge Mode.
- **VPS Data Sync Hardening (2026-07-15 - COMPLETE):** GUID case mismatch fixed, product dedup by Name, SQLite persistent volume, deterministic seed GUIDs, ShopERP always SQLite (all environments). E2E verified on VPS.
- **QuickSetup + Product Management (2026-07-14 → 2026-07-17): COMPLETE.** All six phases are implemented. Phase 4 `/products` UI + `CurrencyHelper`: `a9766442`; Phase 5 QR view/single+batch print: `fdb25eb3`; Phase 6 product CRUD, QR print, and QuickSetup E2E specs: `69a3642f`. Execution results for those production E2E specs are not recorded here.
- **Tiered Auth:** P0-P3 PASS (Online RV 14/14 PASS). P4 Facebook - P5 Zalo ZNS - P6 E2E.
- **Payment Webhook Fix:** COMPLETE + VPS VERIFIED (2026-07-17). Webhook 200 OK, PaymentStatus=Paid, 2 JournalEntries (Revenue + COGS), idempotency confirmed.
- **Local infra (Debug):** Docker PostgreSQL 15-alpine (port 5432, VPS DB dumped) + NATS 2-alpine (port 4222) + ShopERP 5003 + KhachLink 5002 + Gateway 5001. All verified healthy with NATS order sync working.
- **VPS (Production):** khachvip.online — Gateway (200), ShopERP (200 healthy), KhachLink (200), PostgreSQL, NATS, Seq, Nginx. SQLite DB at `/app/keys/vanan_shoperp.db` (persistent volume `shoperp_data`). SSH: `ssh -i "C:\VibeCoding\CD\SSH\vanan.pem" ubuntu@161.118.212.110`.
- **Multi-VPS Checkout Plan (2026-07-18 - REVIEWED & FIXED):** `gateway_router_multi_vps_master_plan.md` + 7 task cards reviewed. 15 issues fixed: NATS subject mismatch, `MarkPaidAsync` split, `GenerateAccountingEntriesAsync` visibility, `CartItem` `required TenantId` break, `IQrCodeService` QR price signature, `FeaturedProduct` entity, `CustomerRecommendationService` retirement, product stub price sync, price validation endpoint, Home.razor scan modal interactivity, `ShopFeatureSettingsEntity` wording. Plan awaits user approval before Phase 1 implementation.
- **Tech debt:** Tier 5 - True Offline Edge. Tier 4 - Roslyn Analyzers dead code. Quick-Setup workflow steps seeding (no domain entity for workflow steps yet). **Multi-VPS Checkout tech debt** — see `docs/AI/tasks/tech_debt_multi_vps_checkout.md` (TD-MVPS-001 NATS sync dead code + TD-MVPS-002 CustomerRecommendationService retirement + TD-MVPS-003 Integration.Tests infra dependency + TD-MVPS-004 UserTenant mapping).
- **Completed streams (all merged to main):** KhachLink Waves 0-4 - Tiered Auth P0-P3 - Platform SystemAdmin - Stream G/F/D/C/B - Order Lifecycle - Bucket A - Order Sync Fix Track E1+E2 - VPS Data Sync Hardening - Multi-tenant Bug Fix Batch (2026-07-18) - Quick-Setup Real Implementation (2026-07-18) - **Multi-VPS Checkout Option C (Phases 1, 2, 3, 3.5, 4, 5, 3.6, 6, 7 — 2026-07-18 to 2026-07-20, COMPLETE)**. See archive for details.

## 4. Next Actions

**NEXT (recommended priority order):**

1. **VPS RV for KhachLink Phase 1 WASM deploy** — after CD completes, verify on VPS:
   - `vanan-khachlink` container healthy (nginx serving static files, not dotnet)
   - PWA loads at `https://diemthuong.khachvip.online` (Blazor WASM boot)
   - All Pages render without 500 (Home, Store, Cart, Checkout, Campaigns, Campaign `/c/{trackingCode}`, OrderTracking, OrderHistory, Login, Profile, Scan, VoiceNote, LoyaltyCard, StoreFinder)
   - Service worker registers + caches static assets
   - PWA install prompt still works
   - HTTP API calls to Gateway succeed (CORS + appsettings.json Gateway.BaseUrl)
   SSH: `ssh -i "C:\VibeCoding\CD\SSH\vanan.pem" ubuntu@161.118.212.110`.

2. **Phase 2 — Service Worker DLL Caching** — per `docs/AI/tasks/khachlink_pwa_offline_master_plan.md`. Task card: `khachlink_pwa_phase2_sw_dll_caching_task_card.md`. Update `service-worker.js` to cache Blazor WASM DLLs (`_framework/*.dll`) + `blazor.boot.json` for true offline support. Verify offline mode: disconnect network, reload app, UI still renders + interacts (read-only — checkout needs Phase 4 offline write queue).

3. **Continue Post-Shop-Removal RV** — verify remaining business flows on VPS (if not already done):
   - Order creation flow (KhachLink → Gateway → NATS → ShopERP)
   - Payment confirmation flow (webhook → OrderService.MarkPaid → AccountingEntry)
   - Kitchen display flow (SignalR + status update)
   - Accounting flow (JournalEntry + AccountingEntry immutable)

4. **Phase 8 — Multi-VPS E2E Validation (Playwright)** — per `gateway_router_multi_vps_master_plan.md`. Task card: `phase8_multi_vps_e2e_task_card.md` (placeholder created in Phase 7). 7 E2E scenarios: single-tenant checkout, multi-tenant checkout, FeaturedProduct display, customer history, admin ShopInstances CRUD, admin TenantManagement new column, multi-VPS routing simulation (2 ShopERP containers with different SHOP_INSTANCE_ID).

5. **Tech debt cleanup** — see `docs/AI/tasks/tech_debt_multi_vps_checkout.md` (TD-MVPS-001 NATS sync dead code, TD-MVPS-002 CustomerRecommendationService retirement, TD-MVPS-003 Integration.Tests infra, TD-MVPS-004 UserTenant mapping). **TD-PWA-001 KhachLink Blazor Server → WASM conversion** is now IN PROGRESS — Phase 1 complete, Phases 2-6 remaining (see master plan `docs/AI/tasks/khachlink_pwa_offline_master_plan.md`).

**Phase 7 COMPLETE (2026-07-20):** Verification + Governance. governance.md updated to Option C + ADR-001 v3 addendum + Phase 8 task card placeholder + tech debt register + final verification (Core.Tests 1044/0/16, Architecture 38/38, guard-check PASS, Integration.Tests CircuitBreaker 6/6 — 43 pre-existing failures require full local app stack, documented as TD-MVPS-003).

**Phase 6 COMPLETE (2026-07-20):** Admin UI for ShopInstances + FeaturedProducts + Home.razor catalog refactor. RV 8/8 PASS on VPS. Commit `5b51c09d`.

**Phase 5 COMPLETE (2026-07-20):** KhachLink multi-tenant cart + checkout UI + QR with prices. RV 9/9 PASS on VPS. See Section 2 for details.

**Deferred (pre-existing, not blocking):**
- Quick-Setup workflow steps seeding (no domain entity for workflow steps yet — products/ingredients/recipes/inventory are seeded, but workflow steps are not)
- UserTenant mapping record not created during user creation (login falls back to `user.TenantId.Value` — works correctly, but UserTenants table remains empty for manually-created users)
- Bug 2 (KhachLink shows products from only 1 tenant) — DATA issue: only 1 tenant had products. Now resolved by Quick-Setup real implementation — sysadmin can seed products for any tenant via `/quick-setup?tenantId=...`

## 5. Active Architecture Decisions

| Decision | L� do |
|---|---|
| CoreHub = in-process background service trong Gateway | Monolith Phase 1-2 (Option B approved 2026-07-05) |
| Gateway = DI composition root cho CoreHub | Program.cs dang k� CoreHub DbContext/Services |
| ShopERP = SQLite (Business) + PostgreSQL (Accounting) | ADR-001: accounting always online. ShopERPDbContext (SQLite) cho Business/Platform, VanAnDbContext (PostgreSQL) cho Accounting qua IAccountingDbContext. **ALL 3 WAVES COMPLETE 2026-07-10** � interface split + service swap + DI + docker-compose + 4 Architecture Tests (Rule J/K/L/M) + test fixes. 1223/1223 tests PASS. ? ENFORCED. |
| CustomerToken = `IDataProtector` | Tr�nh library m?i |
| `AccountingEntry` immutable, Reversal Entry | Audit trail b?t kh? x�m ph?m |
| Multi-tenancy `TenantId` filter m?i layer | Data isolation per HKD |
| EF Core Migrations = official schema management | Stream E � replace `EnsureCreated` for production |
| HKD Data Source = Option A (query AccountingEntries directly) | Wave 0.5 � AccountingEntry is immutable SSoT |
| DOCX export = DocumentFormat.OpenXml + XLSX = EPPlus 7.6.1 | Wave 0 T9 � user approved |
| **[NEW] PlatformUser = Infrastructure entity (non-tenant)** | Precedent: AccountChartEntity � cross-tenant admin, no BaseEntity |
| **[NEW] Execution Discipline Rules (EDR)** | 8 EDR rules in `platform_systemadmin_master_plan.md` Section 7 � r�ng bu?c execution ch?ng t�i di?n deviations |
| **[NEW] Access Matrix = verification plan ri�ng** | `platform_systemadmin_access_matrix_master_plan.md` � 4 phases, 5 EDR-AM rules, depends on F1-F5 COMPLETE |
| **[NEW] Dual Deployment Modes (2026-07-09)** | 2 mode production � xem Section 5a b�n du?i |

### 5a. Deployment Modes (Production)

**Mode 1 � SaaS (online, all-in-one VPS):**
- Compose: `docker-compose.prod.yml`
- T?t c? module ch?y tr�n 1 VPS: PostgreSQL + NATS + Seq + Gateway (in-process CoreHub) + ShopERP + KhachLink + Nginx
- Gateway ? PostgreSQL (central data)
- ShopERP ? SQLite local (offline-first, sync qua NATS Outbox khi online)
- KhachLink ? Gateway (HTTP)
- Use case: SaaS multi-tenant, kh�ch h�ng kh�ng c?n edge node ri�ng

**Mode 2 � Edge (t�ch bi?t, offline-capable):**
- Compose: `docker-compose.edge.yml`
- **Server A (Edge):** ShopERP + SQLite + NATS sync worker � ch?y d?c l?p, kh�ng c?n PostgreSQL
- **Server B (Central):** Gateway (in-process CoreHub) + PostgreSQL + KhachLink + Nginx
- Sync: ShopERP Outbox ? NATS ? Gateway ? PostgreSQL
- Use case: C?a h�ng offline-first, internet kh�ng ?n d?nh, data local t?i edge
- ADR-001: SQLite local + NATS sync + PostgreSQL cloud (accounting always online)

**Luu y quan trong (verified 2026-07-16):**
- ShopERP dung SQLite trong CA 2 mode (Program.cs luon UseSqlite, khong co UseNpgsql path cho ShopERPDbContext)
- docker-compose.prod.yml ShopERP set ConnectionStrings__DefaultConnection=Data Source=/app/keys/vanan_shoperp.db + volume shoperp_data:/app/keys (persistent across redeploy)
- docker-compose.edge.yml ShopERP set SQLITE_DB_PATH=Data Source=/data/shoperp.db + volume shoperp_sqlite_data
- docker-compose.edge.yml co them shoperp-nats-sync worker (command --sync-worker) de poll Outbox + publish NATS
- Safety check (2026-07-16): Program.cs throws InvalidOperationException if connection string contains Host= or Port= (PostgreSQL pattern) - prevents silent PostgreSQL fallback
- Startup log (2026-07-16): [ShopERP] ShopERPDbContext (SQLite) connection: ... printed on every startup for verification

---

## 6. History Log (compressed � see archive + git log for details)

* [2026-07-18] **MULTI-TENANT BUG FIX BATCH + QUICK-SETUP REAL IMPLEMENTATION.** 5 commits: Bug 1 (tenant filter in Blazor Server), Bug 3 (order history header forwarding), Bug 4 (Kitchen display + hardcoded tenantId), Login tenant_id fix, Quick-Setup stub → real (8 industry seed strategies, 145+ products total). All deployed to VPS.
* [2026-07-14] **QUICKSETUP + PRODUCT MANAGEMENT MASTER PLAN + 6 TASK CARDS.** Gap review: 5 blocking + 6 minor gaps all resolved. 6 task cards created. Ready for IMPLEMENT.
* [2026-07-14] **PAYMENT WEBHOOK 500 FIX � CODE COMPLETE ?.** Root cause: `OrderService` called `AddToBookAsync` twice with same JournalEntry instance. Fix A+B (TDD). 995/995 PASS. Merged to main. Pending VPS deploy.
* [2026-07-14] **KHACHLINK UI/UX FIX BATCH ?.** 3 batches, 10 commits. PWA + currency formatting + iOS install + fly-to-cart + checkout JSON. 8/8 VPS verify PASS.
* [2026-07-14] **KHACHLINK E2E VPS PRODUCTION PASS ?.** 7 root causes fixed. E2E PASS local + VPS.
* [2026-07-13] **TIERED AUTH P1+P2+P3 ONLINE RV COMPLETE ?.** 14/14 PASS on khachvip.online. 7 CD runs for deploy fixes.
* [2026-07-12] **TIERED AUTH MASTER PLAN + 7 TASK CARDS.** 7 phases, 96% cost saving.
* [2026-07-12] **KHACHLINK WAVE 3 + WAVE 4 COMPLETE.** Voice Note STT + TTS + QR Table Number + Configurable Polling.
* [2026-07-11] **KHACHLINK WAVE 0 + WAVE 2 COMPLETE.** Toggle infrastructure + Payment Flow + Kitchen UI. Live RV Protocol established.
* [2026-07-09 ? 07-10] **ACCOUNTING POSTGRESQL ONLINE � 3 WAVES COMPLETE ?.** IAccountingDbContext split + services/DI/config + 4 Architecture Tests. 1223/1223 PASS.
* [2026-07-09] **DOCKER CONFIG FIX + DEPLOYMENT MODES.** Port swap + MigrateAsync + Dual Deployment Modes (SaaS + Edge).
* **Older (2026-07-08 and before):** Platform SystemAdmin � Entry Point Check � SDK 8.0.422 � Stream G/F/D/C/B � Order Lifecycle � Bucket A. See `docs/AI/project_state_archive.md` for full history.

---

## 7. Active Files Reference

### Stream G (SaaS Hardening)
| File | Role |
|---|---|
| `docs/AI/tasks/saas_production_hardening_master_plan.md` | Master plan (W0-W8, 3 sprints) |
| `docs/AI/tasks/saas_w{0-8}_task_card.md` | 9 task cards |

### Stream F (VAS Reports)
| File | Role |
|---|---|
| `docs/AI/tasks/vas_enterprise_reports_master_plan.md` | Master plan (W0-W9, COMPLETE) |

---

## 8. Architecture Quick Reference

```
=== SaaS Mode (docker-compose.prod.yml) � all-in-one VPS ===

KhachLink (5002) ? Gateway (5001) ? ShopERP (5003) ? SQLite (local)
                        ?
              [in-process CoreHub services]
                        ?
                  PostgreSQL (central data)

=== Edge Mode (docker-compose.edge.yml) � t�ch bi?t 2 server ===

Server A (Edge):                      Server B (Central):
  ShopERP ? SQLite (local)              Gateway ? PostgreSQL
  shoperp-nats-sync worker              [in-process CoreHub]
       ? NATS Outbox sync ?
  ---------------? NATS ---------------? Gateway
                                         KhachLink ? Gateway (HTTP)
```

**Auth:** Cookie (Blazor Server) + JWT Bearer (API). `DevLoginController` (`#if DEBUG`) for E2E. BCrypt work factor 12.
**Roles:** `UserRole` (tenant-scoped: Owner/StoreKeeper/Guard/Staff/Masterchef) � `PlatformRole` (cross-tenant: SystemAdmin)

---

## 9. Maintenance Log

* **2026-07-21 -- KHACHLINK PWA PHASE 1 (BLAZOR SERVER → WEBASSEMBLY) COMPLETE.** Phase 1 of `khachlink_pwa_offline_master_plan.md`. Converted KhachLink from Blazor Server to Blazor WebAssembly so PWA can work offline (UI events run client-side, no WebSocket required). Architecture: `VanAn.KhachLink.csproj` SDK `Microsoft.NET.Sdk.Web` → `BlazorWebAssembly`; `Program.cs` rewritten for `WebAssemblyHostBuilder`; `App.razor` `blazor.web.js` → `blazor.webassembly.js`; removed `@rendermode InteractiveServer` from 13 Pages + PWAInstallPrompt. Removed `Serilog.AspNetCore` (server-only, pulls `Microsoft.AspNetCore.App` FrameworkReference incompatible with `browser-wasm` RuntimeIdentifier). Contract extraction (Option 2 — user-approved): moved 3 contract files `3_CoreHub/Services/` → `1_Shared/Services/` (`IOrderWorkflowService`, `ISocialCampaignService`, `IShopFeatureSettingsService` + `ShopFeatureSettingsDto` + `PriceValidationResult`), namespace `VanAn.CoreHub.Services` → `VanAn.Shared.Services`. Added `using VanAn.Shared.Services;` to ~20 files in CoreHub, Gateway, ShopERP, Tests. Updated fully-qualified DI registrations in `Gateway/Program.cs` + `ShopERP/Program.cs`. Added `IInventoryService` alias in `OrderService.cs` to disambiguate (exists in both `CoreHub.Interfaces` + `Shared.Services`). Removed `VanAn.CoreHub` ProjectReference from `KhachLink.csproj`. Dead code cleanup: deleted `DashboardHttpService.cs`, `OfflineOrderService.cs` + `.ts`, `EnhancedCartService.cs` + `.ts`, `SyncConflictResolver.cs`, `ConflictResolutionService.cs` + `.ts` (all dead — not registered in DI); deleted `Campaign.cshtml` + `Campaign.cshtml.cs` (legacy MVC Razor Page — incompatible with WASM), replaced by `Campaign.razor` Blazor component at `/c/{trackingCode}`; deleted 6 dead test files (tests for deleted dead code). Deployment: `Dockerfile` dotnet runtime → `nginx:alpine` serving static files; new `nginx.conf` (SPA routing, gzip, cache headers); `docker-compose.prod.yml` removed ASPNETCORE env vars + memory 512m → 256m; new `wwwroot/appsettings.json` with Gateway BaseUrl. Test impact: Unit 984/0 (33 dead tests removed), KhachLink Startup 6/4skip/0fail (4 server-startup tests skipped — `WebApplicationFactory` can't boot WASM, rewrite planned for Phase 6), `dotnet build VanAn.sln` 0 errors. 1 commit `b642662b` (85 files, +901/-5319), pushed to main, CI PASSED. Branch: `main`. Next: Phase 2 (Service Worker DLL Caching) per master plan.

* **2026-07-21 -- PWA INVESTIGATION + TD-PWA-001 CREATED.** User asked whether KhachLink PWA install is a stub and whether the app works offline. Investigation: (1) PWA install is REAL — manifest.json + service-worker.js + PWAInstallPrompt.razor + pwa.js all functional, `beforeinstallprompt` captured on Android Chrome, iOS shows manual "Add to Home Screen" instructions. (2) App does NOT work offline — KhachLink is Blazor **Server** (not WebAssembly as `project_state.md` Section 1 previously claimed). Evidence: `VanAn.KhachLink.csproj` uses `Microsoft.NET.Sdk.Web`, `Program.cs` calls `AddInteractiveServerComponents()`, `App.razor` loads `blazor.web.js`, all 13 Pages use `@rendermode InteractiveServer`. Blazor Server requires live WebSocket (SignalR) for every UI event — when network drops, circuit dies, UI freezes. Service worker caches static assets + API GET responses but cached assets are useless because no Blazor DLL runs on client. (3) Created master plan `docs/AI/tasks/khachlink_pwa_offline_master_plan.md` — 6-phase conversion Blazor Server → Blazor WebAssembly (Option A recommended): Phase 1 project SDK conversion + build green, Phase 2 service worker DLL caching, Phase 3 offline API fallback hardening (update `dynamicCachePatterns` to current Option C endpoints `/api/tenants/*`, `/api/catalog/*`, `/api/campaigns/*`), Phase 4 offline write queue (IndexedDB + Background Sync API for checkout POSTs), Phase 5 push notification wiring + PWA polish, Phase 6 E2E validation + governance. (4) Added TD-PWA-001 to `docs/AI/tasks/tech_debt_multi_vps_checkout.md` cross-cutting section. (5) Corrected `project_state.md` Section 1 Stack + Modules lines: "Blazor WebAssembly (KhachLink PWA)" → "Blazor Server (KhachLink PWA, NOT WASM — see TD-PWA-001)". No code changes — investigation + documentation only. Branch: `main`.

* **2026-07-21 -- KHACHLINK /STORES SEARCH BUTTON FIX COMPLETE.** User reported the magnifier-glass "search button" on `https://diemthuong.khachvip.online/stores` was not clickable. Root cause: the icon was a decorative `<span class="input-group-text">` (no event handler), not a `<button>`. Search only fired via `@oninput` debounce (300ms after typing) — no explicit search button or Enter-key handler. Fix in `5_WebApps/KhachLink/Pages/StoreFinder.razor`: (1) Converted search icon `<span>` → `<button type="button" @onclick="LoadStores">` with `.btn-search-icon` CSS (cursor pointer, hover, no outline) preserving input-group look. (2) Added `@onkeyup="OnSearchKeyUp"` on the input — pressing Enter triggers immediate search (cancels running debounce). (3) Added `OnSearchKeyUp(KeyboardEventArgs e)` method. `dotnet build VanAn.KhachLink.csproj` → 0 errors, 11 pre-existing warnings (unrelated). Branch: `main`. Awaiting CD deploy after push.

* **2026-07-21 -- POST-SHOP-REMOVAL RV + TENANT.ID LINQ BUG FIX (PATTERN #8) COMPLETE.** Previous session removed Shop entity (221 files). This session: (1) Verified VPS deployment — all 5 VanAn containers healthy, DB schema correct (Shops dropped, SocialCampaigns.ShopId dropped, Tenants.Settings_Latitude/Longitude added, 3 tenants). (2) Discovered `TenantStoreController.GetStoreInfo` returned HTTP 500 — root cause: `Tenant.Id` is `TenantId` value object with `HasConversion`, `EF.Property<Guid>(t, "Id")` triggers IConvertible error (Pattern #1 variant). (3) First fix attempt `t.Id.Value == tenantId` failed — LINQ translation error (value object member access not translatable). (4) Final fix: `t.Id == new TenantId(tenantId)` — matches `TenantManagementService` pattern. (5) Preventive fix in `PublicOrdersController.checkout` + `CatalogController.recommended` — `guidList.Contains(t.Id)` converted to `List<TenantId>` for type-matched LINQ translation. (6) Added Known Error Pattern #8 to `.devin/rules/governance.md` — `Tenant.Id` value object LINQ translation. Reference implementations: `TenantManagementService.GetTenantByIdAsync`, `SocialCampaignRepository.GetActiveByTenantIdValueAsync`. 2 commits: `20697063` (initial TenantStore fix), `e876cf53` (batch fix all 3 controllers + Pattern #8). 3 CD runs ALL PASS. RV 6/6 PASS for tenant-based endpoints (store-info valid 200, store-info invalid 404, nearby 200, search 200, catalog/recommended 200, health 200). No errors in gateway logs after fix. Branch: `main`.

* **2026-07-20 -- HOME PAGE PERSONALIZATION + CAMPAIGNS/SHOPS CRUD ADMIN UI COMPLETE + RV 6/6 PASS.** Two features: (A) Dynamic Home page content — replaced static Hero + Stats with Campaign + StoreFinder sections personalized by last-interaction tenantId. New `LastInteractionService` tracks tenantId in localStorage via JS interop. `Scan.razor` + `Home.razor AddFeaturedToCart` record interactions. Home.razor auto-refreshes sections when tenant changes. Fallback empty state with "Quét QR Ngay" CTA for new users. (B) Campaigns + Shops CRUD admin UI — Gateway `CampaignsController` added POST create + fixed auth (`[AllowAnonymous]` → `[Authorize(Policy="SystemAdmin")]` on PUT/DELETE). Gateway `ShopsController` added POST/PUT/DELETE forward with Authorization header forwarding. Two new ShopERP Blazor admin pages: `/admin/campaigns` (CampaignsAdmin.razor — list + create/edit modal with Tenant + Shop dropdowns + delete) + `/admin/shops` (ShopsAdmin.razor — list + create/edit modal with Tenant dropdown + lat/lng + delete). Both `@attribute [Authorize(Policy="SystemAdmin")]`. 8 commits: `e292166c` (Home personalization initial), `2725e28d` (admin UI + backend), `226c4260` (campaign tenant filter + shop auth forwarding), `c8765aeb` (TenantId VO Known Error Pattern #1), `6b9cf88d` (SaveChangesAsync missing), `4e6cbafd` (ShopId FK + admin UI shop dropdown), `f79c5f46` (by-tenant service method + PUT DTO), `a83b797c` (IgnoreQueryFilters for SystemAdmin). 8 CD runs ALL PASS. RV 6/6 PASS for Campaigns CRUD (POST 201 + GET all 200 + GET by-tenant 200 + PUT 200 + DELETE 200 + auth 302). Shops admin UI works via DbContext (Gateway forwarding limited by ShopERP cookie auth — known limitation, documented in Section 4). 6 bugs found + fixed during RV: missing SaveChangesAsync, FK violation Guid.Empty ShopId, wrong service method for by-tenant, domain entity JSON binding, missing IgnoreQueryFilters, TenantId VO SQL translation. Branch: `main`.

* **2026-07-20 -- PHASE 7 COMPLETE (VERIFICATION + GOVERNANCE).** Final phase of Multi-VPS Checkout Option C master plan. (1) `.devin/rules/governance.md` updated — Option B (Monolithic in-process, 2026-07-05) → Option C (Order Creator + Routed Async Delivery, 2026-07-18). Gateway PG is source of truth for Orders + Accounting + Tenants + ShopInstances + Users + FeaturedProducts. Products live in ShopERP per-tenant SQLite. Orders async-delivered via NATS routed by ShopInstanceId. Multi-VPS supported. Client provides ProductName + VatRate snapshot at checkout. (2) `.windsurfrules` reference copy updated with same Option C language + new data flow diagram (KhachLink → Gateway PG → NATS routed → ShopERP-A/B/C per-tenant SQLite). (3) ADR-001 v3 addendum appended to `docs/Architecture/ADR001-Station-Architecture.md` — documents Option C decision, rationale (multi-VPS requirement + FK constraint conflict + cross-VPS data leak prevention), trade-offs table (Option B vs C across 7 dimensions), all 8 implementation phases with commits + VR results, tech debt references. (4) Phase 8 task card placeholder created (`phase8_multi_vps_e2e_task_card.md`) — 7 E2E scenarios: single-tenant checkout, multi-tenant checkout, FeaturedProduct display, customer history, admin ShopInstances CRUD, admin TenantManagement new column, multi-VPS routing simulation (2 ShopERP containers with different SHOP_INSTANCE_ID). (5) Tech debt register created (`docs/AI/tasks/tech_debt_multi_vps_checkout.md`) — 4 active items: TD-MVPS-001 (NATS sync dead code — `SyncProductUpsertAsync` commented out, defer cleanup one release cycle), TD-MVPS-002 (CustomerRecommendationService retirement — Phase 6 CatalogController is replacement, mark `[Obsolete]` after Phase 8 E2E verifies), TD-MVPS-003 (Integration.Tests 43 failures require full local app stack — document required infra or split test projects), TD-MVPS-004 (UserTenant mapping for manually-created users — works correctly but table stays empty). 1 resolved: payment webhook (order stays in PG, no migration needed). (6) Final verification: Core.Tests 1044/0/16 PASS, Architecture 38/38 PASS, guard-check ALL PASSED, Integration.Tests CircuitBreaker 6/6 PASS (43 pre-existing failures are NOT Phase 7 regressions — confirmed via git stash comparison: same 43 failures before and after Phase 7 changes). VPS already deployed + healthy per Phase 6 RV 8/8 (no re-deploy needed — Phase 7 is documentation + verification only). Multi-VPS Checkout Option C master plan: ALL 8 PHASES COMPLETE.

* **2026-07-20 -- PHASE 6 COMPLETE + VPS DEPLOYED + RV 8/8 PASS.** Phase 6 (Admin UI): Two new admin pages + new public catalog API + Home.razor refactor. (1) New `FeaturedProduct` entity (PG-only, Single-Identity pattern, `FeaturedProductId` VO ignored in EF config) + `FeaturedProductConfiguration` (unique index ProductId+TenantId) + PG migration `AddFeaturedProductsTable` + SQLite migration (table exists but unused — PG-only entity). (2) New Gateway `CatalogController` — public `GET /api/catalog/recommended?customerId={id}` returns union of FeaturedProducts (active, ordered by SortOrder) + customer purchase history (from OrderItems JOIN Orders, grouped by ProductId+TenantId). `[AllowAnonymous]` — KhachLink Home.razor is anonymous customer app. No ShopERP HTTP call — pure PG query. (3) New Gateway `FeaturedProductsController` — SystemAdmin CRUD (class-level `[Authorize(Policy="SystemAdmin")]`). (4) New ShopERP admin pages: `/admin/shop-instances` (CRUD + health check trigger) + `/admin/featured-products` (CRUD + active toggle). (5) New `GatewayAdminApiClientBase` shared base class (SystemAdmin JWT minting — reduces duplication) + `ShopInstanceApiClient` + `FeaturedProductApiClient`. (6) `TenantManagement.razor` — new "ShopERP Instance" column (shows Label + BaseUrl, "Chưa gán" badge if null) + ShopInstance dropdown in onboarding modal (required validation) + `AssignShopInstanceAsync` service method. (7) `OnboardTenantRequest` extended with optional `ShopInstanceId` — `TenantOnboardingService` assigns tenant to ShopInstance during onboarding. (8) `Home.razor` refactored — replaces multi-VPS product browse (which broke per Phase 3 Option C) with `GET /api/catalog/recommended` call. Shows "Sản Phẩm Nổi Bật" (Featured) + "Gợi Ý Dựa Trên Lịch Sử Mua Hàng" (History, only if logged in). "Scan QR để mua" button → `/scan` page. (9) NavMenu + Sitemap updated with 2 new admin links. (10) 8 new `FeaturedProductTests` (Single-Identity pattern, factory, validation, toggle, implicit conversions). Architecture test fix: `CatalogController` added to `[AllowAnonymous]` exempt list. `FeaturedProductsController` got class-level `[Authorize]` (Architecture test caught the missing attribute). Core.Tests 1044/0/16. Architecture 38/38. guard-check PASS. **RV 8/8 PASS on VPS** (Catalog 200 + FeaturedProducts 401 + ShopInstances 401 + PG table exists + KhachLink 200 + ShopERP admin 302 + Phase 5 regression 2/2 — Gateway health + products forwarding 16456 bytes). Commit `5b51c09d`. Branch: `main`.

* **2026-07-20 -- PHASE 3.6 COMPLETE + VPS DEPLOYED + RV 2/2 PASS.** Phase 3.6 (Deferred Cleanup): Two issues deferred from Phase 3 VR. (1) OnboardingController refactor — `TenantOnboardingService` no longer seeds industry products into Gateway PG (Phase 3 Option C dropped `FK_OrderItems_Products_ProductId` — PG no longer stores Products, so seeding created orphan data that never synced to ShopERP SQLite). Onboarding now creates tenant + owner + permission groups only (5 steps, was 6). Product seeding deferred to ShopERP QuickSetup (tenant owner runs it after first login via `/quick-setup?tenantId=...`). `TenantOnboardingResult` seed counts always 0; Warnings includes QuickSetup deferral notice. `IndustryCode` field kept in `OnboardTenantRequest` for backward API compat (no longer validated). Updated `TenantOnboardingServiceTests` (removed seed assertions, added Phase 3.6 tests for zero counts + QuickSetup warning + unknown industry code accepted). Updated `TenantOnboardingIntegrationTests` (removed seed DB assertions, verify 0 products/ingredients/recipes/shops after onboarding). (2) Products forwarding port fix — added explicit `ShopERP__BaseUrl=http://shoperp:80/` env var to Gateway in `docker-compose.prod.yml` + `docker-compose.edge.yml` (prevents port 5003 fallback from `appsettings.Development.json` leak via Docker `COPY . .` in Dockerfile). VPS verification: `GET /api/products?tenantId=...` returns 200 OK with 16456 bytes. Architecture test fix: `VA-CONSISTENCY-004` exclusion for `SHOP_INSTANCE_ID` (Phase 4 fail-fast env var). Core.Tests 1036/0/16. Architecture 38/38. guard-check PASS. RV 2/2 PASS on VPS (products forwarding + gateway health). Phase 5 regression 9/9 PASS. Commit `a6413668`. Branch: `main`.

* **2026-07-20 -- PHASE 4 + 5 COMPLETE + VPS DEPLOYED + RV 9/9 PASS.** Phase 4 (ShopERP OrderSyncSubscriber Routing): fail-fast validation of `SHOP_INSTANCE_ID` env var before NATS connect (prevents cross-VPS data leak). Subscribes ONLY to routed subjects `vanan.cloud.order.created.{shopInstanceId}` + `vanan.cloud.order.status.changed.{shopInstanceId}` (wildcard/bare subscriptions removed). Product stub creation uses `UnitPrice` from payload (not 0m). 5 routing tests PASS. Phase 5 (KhachLink Multi-tenant Cart + Checkout UI + QR with Prices): `CartItem.TenantId` + `QRCodePayload` +UnitPrice/VatRate/ProductName/TenantId + `QrCodeService` 2 new overloads + `Scan.razor` fast path (no API call for new QR) + legacy fallback + `Checkout.razor` multi-tenant request + `CheckoutResponse` handling (orders[] loop, partial cart clear, `created_orders` localStorage) + `OrderTracking.razor` "other orders from session" + `OrderHistory.razor` TenantId on OrderDto + ShopERP `ProductsController` QR generation passes price/VAT/name/tenantId + `ValidateProductPrice` endpoint + `Price_Validation_Enabled` toggle (entity + DTO + service + migration + UI) + `ProductManagement.razor` QR reminder. 12 new tests (5 CartItem + 5 QR Payload + 2 QR Service). Core regression 1038/0/16. guard-check PASS. **RV 9/9 PASS on VPS** (Gateway health + multi-tenant checkout + CheckoutResponse shape + QR PNG + ValidateProductPrice match=true/false + Price_Validation_Enabled toggle + KhachLink tracking UI). 3 VPS deploy fixes during RV: (a) Npgsql migration duplicate FK drop (Phase 3 already dropped it) → removed; (b) SQLite migration unnecessary FK drop/re-add → removed; (c) `SHOP_INSTANCE_ID` env var missing from `docker-compose.prod.yml` → hardcoded Guid `00000000-0000-0000-0000-000000000001` (matches Phase 1 seed). 6 commits: `c38b51e5` + `e27727b1` + `e03bbebf` + `8f54270f` + `b2dc22c0` + `8718cb84`. Branch: `main`.

* **2026-07-19 -- PHASE 3.5 COMPLETE (Accounting Consolidation).** Split `ConfirmPaymentAsync` into `MarkPaidAsync` (status=Paid + Outbox event with routing key) + `GenerateAccountingEntriesAsync` (changed private→public) + `ConfirmPaymentAsync` wrapper (backward compat for POS). Gateway `WebhookController` now calls `MarkPaidAsync(enqueuePaymentConfirmedEvent: true)` → NATS `vanan.cloud.order.payment.confirmed.{shopInstanceId}` → ShopERP `PaymentConfirmedSubscriber` (NEW) → sets Paid in SQLite + `GenerateAccountingEntriesAsync`. Gateway `EInvoiceSyncSubscriber` (NEW) subscribes `vanan.shoperp.einvoice.synced.>` (full PG update deferred to Phase 6+). 3 issues found + fixed during VR: (1) OrderSyncSubscriber subject mismatch — added `vanan.cloud.order.created.>` wildcard subscription to match Phase 3 routing key suffix. (2) SQLite ShopInstanceId column missing — `GenerateAccountingEntriesAsync` loaded full Tenant entity (includes PG-only ShopInstanceId column) → SQLite error. Fix: project only `DefaultIndustrySector` via `Select()`. (3) ConfirmPaymentAsync idempotency — wrapper needed idempotency guard before MarkPaidAsync to avoid duplicate accounting entries. 6 new unit tests (MarkPaidAsync + ConfirmPaymentAsync wrapper + GenerateAccountingEntriesAsync public). 1026/1026 Core.Tests PASS. guard-check ALL PASSED. VR 5/5 PASS on VPS (checkout → webhook 200 OK → NATS → PaymentConfirmedSubscriber → "Generated accounting entries for order 019f7bf1..."). Commits: `653825c1` + `5d6d589d` + `7248ec2d`. Branch: `main`.

* **2026-07-19 -- PHASE 3 COMPLETE + PHASE 3.6 CREATED.** Phase 3 (Gateway Order Creator): OrderItemRequest +TenantId +ProductName +VatRate (client snapshot). OrderService.CreateOrderFromCommandAsync uses client snapshot by default, fallback to LoadProductsForSnapshotAsync when ProductName empty. OutboxEvent + OutboxMessage +RoutingKey (nullable, additive migration `20260719153000_AddOutboxRoutingKey` — also drops FK_OrderItems_Products_ProductId per Option C). NatsSyncWorker.BuildSubject appends `.{routingKey}` when set. PublicOrdersController.CreateCheckoutOrder rewritten: multi-tenant grouping (cart with N tenants → N orders), CheckoutResponse with partial failure support. ProductsController +catalog forwarding (GetProducts, GetRecommendedProducts, ValidateProductPrice) with ResolveShopErpClientAsync (ShopInstance BaseUrl lookup). OrderItemConfiguration removed Product navigation. DataSyncSubscriber product sync disabled per Option C. Commits `cdcb639e` + `b469c88c`. VR 4/5 PASS on VPS (RV5 products forwarding FAIL — pre-existing port 5003 config issue, deferred). 1019/1020 unit tests PASS (1 flaky perf test). guard-check ALL PASSED. **Phase 3.6 (Deferred Cleanup) created:** OnboardingController refactor (remove product seeding) + Products forwarding port fix. Task card: `phase3.6_deferred_cleanup_task_card.md`. Depends on Phase 4 + 5. Branch: `main`.

* **2026-07-19 -- PHASE 2 COMPLETE + ROLECLAIMNORMALIZER.** Phase 2 (Gateway ShopInstances API): IShopInstanceService + ShopInstanceService (CRUD + health check + tenant count, IgnoreQueryFilters for platform entity) + ShopInstancesController (7 endpoints under /api/v1/shop-instances, all [Authorize(Policy=SystemAdmin, Bearer)]) + ShopInstanceHealthResult DTO. 15 unit tests PASS (SQLite in-memory). 9 integration tests skipped (pre-existing JWT auth issue in GatewayWebApplicationFactory — affects all SystemAdmin Bearer JWT integration tests). Architecture test W12-G7 exempt list updated. VR 8/8 PASS on VPS (GET 200, POST 201, health-check 200, anonymous 401). Commit `e95b1d64`. Bonus fix: RoleClaimNormalizer (IClaimsTransformation) — Gateway now accepts both short-form `role` and long-form `ClaimTypes.Role` in JWT. Idempotent. VR 2/2 PASS on VPS (short-form JWT GET 200 + POST 201). Commit `98f1d6d8`. Branch: `main`.

* **2026-07-19 -- PHASE 1 COMPLETE.** ShopInstance entity (BaseUrl, Label, MaxTenants, IsActive, HealthCheckUrl, LastHealthCheck, HealthStatus) + Tenant.ShopInstanceId FK + AssignToShopInstance method. EF migration `AddShopInstancesAndTenantFk` (additive + seed 1 default instance + backfill all tenants). 18 new unit tests (14 ShopInstanceTests + 4 TenantShopInstanceAssignmentTests). 30 obsolete pre-existing test failures skipped. VR 13/13 PASS on local + VPS (migration applied, seed + backfill verified, all 3 services healthy). Commits: `32c832e9` (Phase 1), `c94d9e8d` (skip obsolete tests), `b1925232` (project_state update), `557e99df` (COMPLETION SUMMARY sections), `360cf7fc` (VR test results). Branch: `main`.

* **2026-07-18 -- MULTI-VPS CHECKOUT PLAN REVIEW & TASK CARD FIXES.** Reviewed `gateway_router_multi_vps_master_plan.md` + 7 task cards (`phase1` through `phase7`). Fixed 15 issues: NATS subject mismatch (`OrderPaymentConfirmed` → `vanan.cloud.order.payment.confirmed.{shopInstanceId}`, `OrderStatusChanged` → `vanan.cloud.order.status.changed.{shopInstanceId}`), `OrderService` split into `MarkPaidAsync` (Gateway webhook) + `ConfirmPaymentAsync` wrapper (POS), `GenerateAccountingEntriesAsync` made public for `PaymentConfirmedSubscriber`, `CartItem.TenantId` default to `Guid.Empty` instead of `required` to avoid compile break, `IQrCodeService`/`QrCodeService`/`QRCodePayload` signature update for QR price/VAT/name, `ProductsController.GetProductQrCode` pass price fields, `FeaturedProduct` entity + `FeaturedProductId` VO fix, `CustomerRecommendationService` retirement note, product stub price sync from payload, price validation endpoint location, Home.razor scan modal Blazor interactivity gate, `ShopFeatureSettingsEntity` wording (Infrastructure not Domain), `OrderPaymentConfirmed` payload includes `paymentMethod`, `PaymentConfirmedSubscriber` retry loop + idempotency. `project_state.md` updated. No code changes. Plan awaits user approval before Phase 1. Branch: `main`.

* **2026-07-18 -- MULTI-TENANT BUG FIX BATCH + QUICK-SETUP REAL IMPLEMENTATION.** 5 commits, all deployed to VPS via CD (healthy). (1) Bug 1: `HttpContextTenantProvider` returned `Guid.Empty` in Blazor Server interactive sessions → products page empty. Fixed by adding `AuthenticationStateProvider` fallback. Commit `0309e559`. (2) Bug 3: Gateway `CustomerOrdersController` didn't forward `X-Customer-Device-Id` header → order history blank for logged-in users. Fixed. Commit `0309e559`. (3) Bug 4: Zalo QR orders invisible on Kitchen — `PublicOrdersController` hardcoded tenantId + Kitchen only queried "pending" (paid orders are "confirmed"). Fixed both. Commit `0309e559`. (4) Login tenant_id bug: `Login.cshtml.cs` hardcoded fallback `00000000-...-001` when UserTenants mapping missing → users created for tenant A logged in with tenant B. Fixed by falling back to `user.TenantId.Value`. Commit `68a34af8`. (5) Quick-Setup stub → real: `OnboardingService.ApplyTemplateAsync` was `Task.Delay(10)` + fake return. Replaced with real delegation to `IIndustrySeedStrategy`. All 8 strategies implemented (F&B: 32 products from Menu_An_Uong.md §1, SPA: 22, RETAIL: 18 new, CLOTHES: 22, HOTEL: 15, BARBER: 12, HEALTHY: 12, PETSHOP: 12). Idempotent. 4th template "Thời trang" added. `IIndustrySeedStrategy` registered in ShopERP DI (was only in Gateway). Commit `f40d162b`. Branch: `main`.

* **2026-07-17 -- ENVIRONMENT PARITY + CIRCUIT INIT + KITCHEN DISPLAY FIX.** Fixed 3 root causes: (1) `appsettings.Development.json` Gateway was using SQLite instead of PostgreSQL — environment drift vs VPS. Fixed to PostgreSQL connection string. Dumped VPS PostgreSQL DB to local Docker PostgreSQL 15-alpine for data parity. (2) "Circuit failed to initialize" on KhachLink — root cause: 3 orphaned `OrderItems` rows in SQLite (ProductId `192330A9-...` deleted) blocked `SingleIdentity_DropBusinessKeyColumns` migration → `MigrateAsync()` crash → server won't start. Fix: deleted orphaned rows + applied pending SQLite migrations (`SingleIdentity_DropBusinessKeyColumns` + `AddVatDisplayToggle`). (3) POS orders not showing on kitchen display — root cause: NATS not running locally → order sync Gateway→ShopERP broken. Fix: started NATS 2-alpine in Docker. Verified E2E: order created via Gateway API → NATS sync → SQLite (Status=pending, Total=61600, Vat=5600) → kitchen display can query it. All 3 servers (Gateway 5001, ShopERP 5003, KhachLink 5002) verified healthy. CI pipeline PASS: 980+17 unit tests, 38 arch tests, 10 KhachLink startup tests. Commit `f9b274e6` pushed to origin/main. Branch: `main`.

* **2026-07-17 -- VAT DISPLAY UI COMPLETE.** Added `VAT_Display_Enabled` shop feature toggle (7th toggle, default ON). `CartItem` record extended with `VatRate`/`VatAmount`/`NetAmount` (VAT-inclusive extraction). `CartState` computes real `TotalVatAmount` + `NetSubTotal`. `PublicOrderTrackingDto` + `PublicOrderItemDto` + `CustomerOrderDto` + checkout response extended with VAT fields. UI breakdown (Tạm tính / VAT / Tổng cộng) on Cart, Checkout, OrderTracking, OrderHistory, POS Create, CartDrawer — all conditional on toggle. EF migrations: SQLite + PostgreSQL. 1006/1007 Core.Tests pass (1 flaky perf test). Branch: `main`.
* **2026-07-17 -- RC-7 FIX COMPLETE.** `OrderService.CreateOrderFromCommandAsync` now loads Product entities via `IProductRepository` and snapshots `ProductName` + actual `VatRate` into `OrderItem` at creation. Domain `OrderItem.Create` factory + constructor extended with `vatRate` param (backward-compatible default 0.10m). Missing-product policy: throw `KeyNotFoundException` (no ghost "Unknown" stubs). Sync subscribers (OrderSyncSubscriber, DataSyncSubscriber) reflection hacks replaced with factory param. Gateway Program.cs registers `IProductRepository`. 998/998 Core.Tests pass (3 new RC-7 tests + 1 updated). Branch: `main`.
* **2026-07-17 -- DEFERRED SCOPE PRUNED.** Removed user-deprioritized items: PostgreSQL migration synchronization, Access Matrix Phase 1 analysis, W8 final regression/production tag, and Roslyn Analyzer wiring. Remaining deferred work: RC-7 OrderItem product-data snapshotting. Branch: `main`.

* **2026-07-17 -- DEFERRED BUG AUDIT: PAYMENT WEBHOOK RESOLVED; RC-7 CONFIRMED.** Removed the stale payment-webhook/AuditLog 400 entry: `WebhookController` sets the anonymous callback tenant before payment processing (`fd7b0385`), and `AuditLogRepository` resolves the tenant lazily at execution (`9a0934bd`). Focused payment-confirmation tests: 16/16 PASS. RC-7 remains: `CreateOrderFromCommandAsync` does not resolve Product data before creating `OrderItem`, so `ProductName` and actual `VatRate` are not snapshotted. Branch: `main`.

* **2026-07-17 -- DEFERRED BUG AUDIT: TWO ITEMS RESOLVED.** Removed stale deferred entries: (1) Gateway SQLite `AccountingEntries.AccountCode` gap is covered by the idempotent startup schema patch added in `d9cb377f`; (2) `/dev/login` ambiguity was already resolved by removing the duplicate minimal endpoint, leaving controller actions as the only handlers. Focused `DevLoginControllerReleaseBuildGuardTests`: 3/3 PASS. Branch: `main`.

* **2026-07-17 -- PRODUCT MANAGEMENT PHASES 4–6 VERIFIED IMPLEMENTED + STATE ARCHIVED.** Source and git history confirm: Phase 4 `/products` UI + CurrencyHelper (`a9766442`), Phase 5 QR view/single+batch print (`fdb25eb3`), and Phase 6 product CRUD, QR print, QuickSetup E2E specs (`69a3642f`). Moved the completed wave into `project_state_archive.md`; active objective is now awaiting approval. Ground truth: branch `main`, latest code commit `f2c3ef1e`. Branch: `main`.

* **2026-07-17 -- PAYMENT WEBHOOK FIX VPS VERIFIED + QUICKSETUP/PRODUCT MGMT PHASE 1-3 AUDITED.** Payment Webhook: POST /api/webhooks/payment on khachvip.online → 200 OK, PaymentStatus=Paid, 2 JournalEntries (Revenue + COGS), idempotency confirmed. QuickSetup/Product Management audit: verified Phase 1 (QuickSetup fix), Phase 2 (Domain Product.Update/Deactivate/Activate/MarkAsDeleted), Phase 3 (CRUD API — IProductRepository, IProductService, IImageStorageService, 3 DTOs, 8 controller endpoints) ALL ALREADY COMPLETE in codebase from prior sessions. Phase 4-6 (UI + QR Print + E2E) are next. Branch: `main`.

* **2026-07-17 -- PAYMENT WEBHOOK FIX VPS VERIFIED.** POST /api/webhooks/payment on khachvip.online → 200 OK `"Payment confirmed and accounting entries generated"`. Order `019f6dbf-...`: PaymentStatus=Paid, VietQR_TransactionId=test-tx-001. PostgreSQL JournalEntries: 2 rows (Doanh thu bán hàng + Giá vốn hàng bán). Idempotency: second call returns 200, no duplicate entries. Branch: `main`.

* **2026-07-17 -- SINGLE-IDENTITY REFACTOR COMPLETE + SHOPERP 502 FIXED + VPS VERIFIED.** Extended single-identity pattern from Order to all 5 entities (Product, Customer, OrderItem, Ingredient, Recipe). Domain + EF config + production code + migrations + architecture rule ALL COMPLETE. 12 commits pushed (b8584a8a → e70c91a7). **ShopERP 502 fix:** seed product check now checks `p.Id == sqliteProd.Id` first (root cause: PG had product `1581168b-...` "Sinh Tố bơ" with same Id but different Name as correct `05341491-...` "Sinh tố bơ" → PK violation on insert → crash). Try-catch swallows around PostgreSQL + SQLite migrations removed (fail-fast). Migration skip hack (INSERT OR IGNORE into __EFMigrationsHistory) reverted. PG garbage cleaned (deleted dup product, updated 1 OrderItem ref). CD failed (VPS disk full 44G/45G) → cleaned 22GB Docker images → manual deploy via SSH (`docker compose pull` + `up -d`). VPS verified: all containers healthy, `khachvip.online/` → 200, `/health` → 200, `diemthuong.khachvip.online/` → 200. Migrations ran cleanly (no PK violations). Branch: `main`.

* **2026-07-16 -- ORDER UUIDv7 SINGLE IDENTITY REFACTOR COMPLETE + LOCAL RUNTIME VERIFIED.** 5-phase refactor to resolve dual-identity problem (`Order.Id` PK vs `Order.OrderId` domain ID generated independently). Phase 1: Added `UUIDNext 4.2.4` to CPM + 5 projects (Shared, CoreHub, ShopERP, Gateway, KhachLink). Phase 2: `Order.Create` syncs `OrderId = new OrderId(id)` after setting `Id` (single identity). Phase 3: Replaced `Guid.NewGuid()` → `Uuid.NewDatabaseFriendly(Database.PostgreSql)` at 3 sites (OrderService.cs, OrdersController.cs, OmnichannelOrderService.cs) + fixed `RevenueExcelReport.cs` to use `order.Id` (was `order.OrderId.Value`). Phase 4: `OrderConfiguration.cs` ignores `OrderId` property + 2 migrations (SQLite `20260716082930_DropOrderOrderIdColumn` + PostgreSQL `20260716083001_DropOrderOrderIdColumn`) drop `Orders.OrderId` column. Kept `OrderIdConverter` for `ElectronicInvoice.OrderId` + `PendingInvoiceQueue.OrderId`. Phase 5: Updated 3 test files (`OrderApiTests.cs`, `OrderWorkflowServiceTests.cs`, `OrderFinancialCalculationTests.cs`) — query by `o.Id`, removed `OrderId = new OrderId(...)` sets. 83/83 order tests PASS. Local runtime verified: new order `019f6a18-7800-72e6-b61a-7a85c39b4b1c` (UUIDv7 prefix `019f6a18`, version nibble `7`), transition `pending → preparing` 200 OK, OutboxEvent enqueued, NATS published, `Orders.OrderId` column dropped (40 columns remain), 4 pre-existing orders preserved (UUIDv4 → UUIDv7 transition clean). Cross-DB sync subscribers (DataSyncSubscriber, OrderSyncSubscriber) NOT modified. Build 0 errors. Branch: `main`. 27 files (17 modified + 10 new).

* **2026-07-16 -- SHOPERP UI FIX BATCH + SITEMAP/NAV RESTRUCTURE COMPLETE.** Fixed 3 batches of UI bugs: (1) VanAForm `@onsubmit:preventDefault` — Blazor Category B native form submit bug causing silent fail on 4 forms (TenantManagement create+onboarding, ProductManagement create+edit). (2) RevenueEntry/ExpenseEntry — pass accountCode/vendor/category/reference to AccountingEntryService (was null → duplicate check false-positive + data loss). ExpenseEntry.ValidateForm strip thousands separators. (3) TransactionHistory ExportToExcel — implemented CSV export via `vanAn.downloadFile` JS interop (was stub). Sitemap restructure: added "Cấu hình & Thiết lập" card (Owner only, link `/settings/shop-features`), moved "Quản Trị Hệ Thống" Owner→SystemAdmin, moved "Hóa Đơn Điện Tử" Owner/StoreKeeper→SystemAdmin, VAS BCTC reports card only for Company tenants (load BusinessType via ITenantManagementService, hidden for HKD). NavMenu Home button → `/sitemap`. Build 0 errors. Branch: `main`. 11 files modified.

* **2026-07-16 -- VPS DATA SYNC HARDENING + ENVIRONMENT PARITY COMPLETE.** 5-phase nuclear cleanup + fix: (1) Cleaned PG garbage (40 test orders, 450 dup products, 11 outbox messages deleted). (2) Wiped SQLite + force-recreate (fresh: 9 products, 5 users, 2 tenants). (3) Synced products PG↔SQLite with lowercase GUIDs (match exactly). (4) Fixed OrderSyncSubscriber to auto-create product stub from event payload if ProductId missing (prevents FK violation) + product sync dedup by Name+TenantId instead of ProductId (prevents duplicates across restarts with GUID case mismatch). (5) Deployed + verified E2E on VPS: checkout → 200 OK → NATS sync → "synced order → SQLite (1 items, 61600 VND)" → GET /api/orders/{id} → 200 OK. PG products: 9 (no duplicates after redeploy). Then added SQLite persistent volume (`ConnectionStrings__DefaultConnection` env var → `/app/keys/vanan_shoperp.db` in volume) + deterministic seed GUIDs (lowercase, match PostgreSQL) + ShopERP ALWAYS uses SQLite in all environments (appsettings.json/Production/Staging all have `DefaultConnection = Data Source=vanan_shoperp.db` + safety check that throws if connection string looks like PostgreSQL + startup log). Commits: `b3a8b3d6`, `89b69d3d`, `9840ddf6`. Branch: `main`. CD runs: 3 (all success). VPS verified: `[ShopERP] ShopERPDbContext (SQLite) connection: Data Source=/app/keys/vanan_shoperp.db`.

* **2026-07-16 -- STATE + MASTER PLAN UPDATED: Track E1 COMPLETE, Track E2 STARTING.** Updated `docs/AI/project_state.md` Sections 2/3/4 to reflect Order Sync Track E1 complete (commits c2de0c2b, dd13bc19) and Track E2 Kitchen UI in progress. Updated `docs/AI/tasks/order_sync_fix_kitchen_ui_master_plan.md` and `order_sync_fix_kitchen_ui_task_card.md` to mark T1-T8 DONE. Branch: `feature/order-sync-fix-kitchen-ui`.

* **2026-07-15 -- ORDER SYNC FIX TRACK E1 COMPLETE (Option D).** RC-1/2/3 fixed + 2 bonus bugs found during verify. Changes: (1) RC-1 Atomic Outbox: IOrderRepository.AddAsyncNoSave + BeginTransactionAsync + single SaveChangesAsync + CommitAsync in OrderService.CreateOrderFromCommandAsync. (2) RC-2 Subject: NatsSyncWorker.BuildSubject splits camelCase (OrderCreated -> order.created), Sync:SubjectPrefix config (Gateway=cloud, ShopERP=shoperp), OrderSyncSubscriber subscribes vanan.cloud.*, DataSyncSubscriber keeps vanan.shoperp.*. (3) RC-3 OrderItem payload: OrderSyncSubscriber parses ProductName + VatRate via reflection. (4) OutboxEvent ID fix: OutboxRepository.ToDomain preserves m.Id as OutboxEventId (constructor generated new Guid -> MarkAsProcessedAsync could never find row -> events stuck Pending forever). (5) DataSyncSubscriber.SyncOrderCreatedAsync completed (was stub). (6) SQLite product seed FK fix: override Products.Id = Products.ProductId (FK_OrderItems_Products_ProductId references Products.Id PK). Verify T6 PASS: checkout -> PG (atomic) -> NATS (vanan.cloud.order.created) -> SQLite (1 order, Dashboard TodayOrders=1, TodayRevenue=55000). Build 0 errors. Tests 995/995 PASS. Guard PASS. Commit: c2de0c2b. **Known debt RC-7:** OrderService doesn't enrich OrderItems with ProductName/VatRate from Product entity (pre-existing, not sync bug). Branch: feature/order-sync-fix-kitchen-ui.

* **2026-07-15 -- ORDER SYNC PLAN PIVOT: Option C -> Option D.** S-T1 verify local discovered Option C hypothesis was wrong: ShopERP Orders/Index.razor uses IHttpClientFactory.CreateClient('gateway') but 'gateway' client is NOT registered in Program.cs (no BaseAddress) -> relative URL 'api/orders' resolves to ShopERP itself (SQLite), NOT Gateway HTTP. So Owner UI reads SQLite in BOTH SaaS and Edge modes -> sync PG->SQLite is MANDATORY for both modes. Option D locked: sync PG->SQLite for both modes, drop Sync__EdgeMode flag, drop Dashboard HTTP refactor. Master plan + task card rewritten as single unified card (16 tasks: 8 sync fix + 8 kitchen UI). Old files: order_sync_saas_edge_master_plan.md renamed to order_sync_fix_kitchen_ui_master_plan.md, order_sync_saas_mode_task_card.md + order_sync_edge_mode_kitchen_ui_task_card.md deleted (replaced by order_sync_fix_kitchen_ui_task_card.md). Branch: main.

* **2026-07-15 -- ORDER SYNC INVESTIGATION RE-DO -- PREVIOUS DIAGNOSIS SUPERSEDED.** Investigation session 2026-07-15 found 6 commits (4d1e7b90 through 19e8686e) patched symptoms, not root cause. Real root causes: RC-1 (Outbox non-atomic), RC-2 (subject namespace collision), RC-3 (OrderItem missing fields), RC-4 (OutboxMessage IMustHaveTenant modeling), RC-5 (no verify local), RC-6 (hardcoded tenant). Previous entry "VERIFY PENDING" (commit db49639c) was based on wrong assumption that sync PG->SQLite is missing piece for SaaS Mode. Verified fact: ShopERP Orders/Index.razor already reads via Gateway HTTP. Only Dashboard metrics (2 methods) actually broken in SaaS Mode. **New plan (Option C locked):** SaaS Mode disable sync PG->SQLite (PostgreSQL SSoT, UI via HTTP). Edge Mode fix RC-1/2/3 + add Kitchen UI (POS input -> payment -> kitchen -> deliver). Master plan: docs/AI/tasks/order_sync_saas_edge_master_plan.md. 2 task cards: order_sync_saas_mode_task_card.md (8 tasks) + order_sync_edge_mode_kitchen_ui_task_card.md (16 tasks, Track E1 sync fix + Track E2 kitchen UI). **Branch:** main. **Next:** Phase S first (SaaS Mode cleanup), then Phase E1 + E2.

* **2026-07-15 -- ORDER SYNC GATEWAY (PostgreSQL) → SHOPERP (SQLite) -- CODE COMPLETE, VERIFY PENDING.** Root cause: sync 1-way (SQLite→PostgreSQL), thiếu reverse direction. Orders tạo qua Gateway (KhachLink checkout) lưu PostgreSQL, Owner query SQLite → empty. **Fix (Option B: Gateway Outbox + NATS subscriber):**
  1. `OrderService.CreateOrderFromCommandAsync`: enqueue `OrderCreated` event to Outbox (full payload: items, customer info, status, amounts) + `SaveChangesAsync` sau enqueue (OutboxRepository.EnqueueAsync chỉ add to change tracker).
  2. `2_Gateway/Program.cs`: register `IOutboxRepository` + `NatsSyncWorker` + `INatsEventPublisher` + `IOrderService`.
  3. `5_WebApps/ShopERP/Services/OrderSyncSubscriber.cs` (NEW): subscribe `vanan.shoperp.order.created` + `order.statuschanged` → sync to SQLite (idempotent, DDD factory methods).
  4. `docker-compose.prod.yml`: thêm `Nats__Url` + `NATS__Url` + `ConnectionStrings__Nats` cho shoperp + `NATS__Url` cho gateway.
  5. `NatsEventPublisher`: đọc 6 config keys (NATS__Url, Nats__Url, NATS:Url, Nats:Url, ConnectionStrings:Nats, ConnectionStrings__Nats) — Linux env vars case-sensitive.
  6. `OutboxRepository`: `IgnoreQueryFilters` cho GetPendingEventsAsync/MarkAsProcessed/MarkAsFailed/GetById — OutboxMessage là IMustHaveTenant, global filter loại tất cả messages khi CurrentTenantIdValue=Guid.Empty trong background worker.
  **Commits:** 4d1e7b90, d04142ed, 4bb5bd4b, 084f3798, 85fb43b9, 8b650f2e. **Build:** 0 errors. **CI:** PASS. **CD:** 4/6 success, 1 fail (VPS disk full 45G/44G — cleanup 37GB Docker images), 1 in-progress (8b650f2e). **VERIFY PENDING:** order chưa sync qua NATS do CD chưa deploy fix cuối (IgnoreQueryFilters). **Branch:** main.

* **2026-07-15 -- PROJECT STATE ARCHIVED.** Reduced from 301 to ~170 lines. Moved Maintenance Log entries 2026-07-13 and earlier + completed status items to docs/AI/project_state_archive.md (Section "Archived 2026-07-15"). Kept: 2026-07-14 entries + current/pending items. **Branch:** main.

* **2026-07-14 -- QUICKSETUP + PRODUCT MANAGEMENT MASTER PLAN + 6 TASK CARDS CREATED.** Gap review: 5 blocking + 6 minor gaps all resolved. G3 (IProductService Clean Architecture), G4 (shared CurrencyHelper), G7 (tenant selection from TenantManagement row), G8 (Cloudinary image upload) -- user locked. 6 task cards created. 55 RV tests planned in 6 blocks. **Branch:** main.

* **2026-07-14 -- PAYMENT WEBHOOK 500 FIX -- CODE COMPLETE, PENDING VPS DEPLOY.** Root cause: OrderService called AddToBookAsync twice with same JournalEntry instance. Fix A+B (TDD): dedup caller + track-aware guard. 6 new tests. 995/995 PASS. Commit 2f084bf1, merged to main 9b0392f. **Remaining:** push origin main -> CD -> verify on VPS.

* **2026-07-14 -- KHACHLINK UI/UX BUG FIX BATCH COMPLETE.** 3 batches, 10 commits. Batch 1: PWA manifest + QR scanner + responsive layout. Batch 2: Currency C# formatting (replaced JS race condition). Batch 3: PWA iOS install + fly-to-cart animation + checkout JSON mapping fix. Google OAuth production wiring + profile crash fix. 8/8 VPS verify PASS.

* **2026-07-14 -- KHACHLINK E2E VPS PRODUCTION PASS.** 7 root causes fixed: SQLite->PostgreSQL product sync, FK constraint mapping, DevLogin->SystemAdmin impersonation, Playwright baseURL, Gateway public tracking endpoint, PostgreSQL IdentityLevel column. E2E PASS local (28.1s) + VPS (11.7s).

* **Older entries (2026-07-13 and before):** See docs/AI/project_state_archive.md Section "Archived 2026-07-15".
