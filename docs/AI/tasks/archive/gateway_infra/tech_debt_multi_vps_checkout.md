# Technical Debt Register — Multi-VPS Checkout Option C

> **Created:** 2026-07-20 (Phase 7 — Verification + Governance)
> **Master plan:** `gateway_router_multi_vps_master_plan.md`
> **ADR:** `docs/Architecture/ADR001-Station-Architecture.md` (v3 addendum)

---

## Active Tech Debt Items

### TD-MVPS-001: NATS sync dead code (Gateway DataSyncSubscriber)

**Status:** Documented (defer cleanup one release cycle)
**Source:** Phase 3 (Option C cutover, 2026-07-18)
**Location:** `3_CoreHub/Services/Sync/DataSyncSubscriber.cs` — `SyncProductUpsertAsync` commented out

**Context:**
Phase 3 disabled product sync from ShopERP → Gateway PG (Option C: Products live in ShopERP SQLite only, Gateway PG no longer stores Products). The `SyncProductUpsertAsync` handler is commented out but the method body remains.

**Cleanup plan:**
- Wait one release cycle (Phase 8 E2E + 1 production deploy) to confirm no regression.
- Then delete `SyncProductUpsertAsync` entirely + remove from `DataSyncSubscriber` dispatch table.
- Verify `DataSyncSubscriber` still handles order status sync (SQLite→PG direction, kept under Option C).

**Risk if cleaned up prematurely:** Low — code is commented, not executed. But removing too early could break sync if a future requirement re-introduces product sync.

---

### TD-MVPS-002: CustomerRecommendationService retirement

**Status:** Pending Phase 8 E2E verification
**Source:** Phase 6 (Admin UI — CatalogController replacement, 2026-07-20)
**Location:** `3_CoreHub/Services/CustomerRecommendationService.cs` (if exists) or related recommendation logic

**Context:**
Phase 6 added `Gateway/Controllers/CatalogController.cs` with `GET /api/catalog/recommended` as the new public endpoint for KhachLink Home.razor. This endpoint returns a union of FeaturedProducts (PG) + customer purchase history (PG OrderItems JOIN Orders). The old `CustomerRecommendationService` (if it exists) is now redundant.

**Cleanup plan:**
1. Phase 8 E2E-3 + E2E-4 verify `CatalogController` works end-to-end (FeaturedProduct display + customer history).
2. After E2E PASS: mark `CustomerRecommendationService` `[Obsolete("Replaced by CatalogController, 2026-07-20")]`.
3. After one release cycle: delete `CustomerRecommendationService` + remove DI registration.

**Risk if cleaned up prematurely:** Medium — if `CatalogController` has a runtime bug not caught by RV, deleting the old service would leave Home.razor with no fallback.

---

### TD-MVPS-003: Integration.Tests require full local app stack

**Status:** Documented (test infrastructure gap)
**Source:** Pre-existing (surfaced during Phase 7 verification, 2026-07-20)
**Location:** `6_Tests/VanAn.Integration.Tests/`

**Context:**
43 Integration.Tests fail when only Docker PostgreSQL + NATS are running locally. These tests require the full local app stack (Gateway + ShopERP + KhachLink running on ports 5001/5003/5002). The guard-check fast test gate correctly uses only `CircuitBreakerIntegrationTests` (6/6 PASS) which doesn't require the full stack.

**Failing test categories (sample):**
- `Order Flow: KhachLink -> ShopERP -> KhachLink` — requires all 3 apps running
- `Golden Flow: Health Check Endpoint` — requires Gateway running
- `Platform login: *` (3 tests) — requires Gateway running with seeded users

**Cleanup plan:**
1. Document required local infra in `6_Tests/VanAn.Integration.Tests/README.md` (or AGENTS.md).
2. Consider splitting Integration.Tests into:
   - `VanAn.Integration.Tests` (DB-only — PostgreSQL + NATS, no app stack)
   - `VanAn.AppStack.Tests` (requires full local stack — run manually or in CI with docker-compose)
3. Or: add a `Category="RequiresAppStack"` trait to these tests + exclude from default `dotnet test` runs.

**Risk if not addressed:** Developers see 43 failures on `dotnet test` and assume regressions. Mitigated by guard-check using only CircuitBreaker subset.

---

### TD-MVPS-004: UserTenant mapping for manually-created users

**Status:** Documented (works correctly, table stays empty)
**Source:** Multi-tenant bug fix batch (2026-07-18)
**Location:** `5_WebApps/ShopERP/Components/Pages/Auth/Login.cshtml.cs` — fallback to `user.TenantId.Value`

**Context:**
Manually-created users (via admin UI or seeding) don't get a `UserTenants` junction record. Login falls back to `user.TenantId.Value` (set during user creation). This works correctly but the `UserTenants` table stays empty for these users, which could confuse future multi-tenant queries.

**Cleanup plan:**
- When a user is created (admin UI or onboarding), also create a `UserTenants` record linking user → tenant.
- Or: deprecate `UserTenants` table entirely if `User.TenantId` is the single source of truth.

**Risk if not addressed:** Low — login works, tenant filtering works. Future feature assuming `UserTenants` is populated could break.

---

## Resolved Tech Debt (kept for history)

### TD-MVPS-RESOLVED-001: Payment webhook Option C migration

**Status:** RESOLVED (2026-07-18 — user decision)
**Context:** Phase 3 Q4 concern about payment webhook under pure-router architecture.
**Resolution:** Order stays in Gateway PG (Option C). Webhook loads from PG as before — no migration needed.

---

## Cross-cutting Tech Debt (non-Multi-VPS)

### TD-PWA-001: KhachLink is Blazor Server, not WebAssembly — PWA does not work offline

**Status:** Documented (master plan created, awaiting Tech Lead approval)
**Source:** PWA investigation 2026-07-21
**Location:** `5_WebApps/KhachLink/` (entire project)
**Master plan:** `docs/AI/tasks/khachlink_pwa_offline_master_plan.md`

**Context:**
`docs/AI/project_state.md` Section 1 claims KhachLink is "Blazor WebAssembly (KhachLink PWA)" but the actual implementation is Blazor Server (`Microsoft.NET.Sdk.Web` + `AddInteractiveServerComponents()` + `blazor.web.js` + `@rendermode InteractiveServer` on all 13 Pages). PWA install is real (manifest + service worker + install prompt all functional), but the app does NOT work offline because Blazor Server requires a live WebSocket (SignalR) connection for every UI event. When the network drops, the circuit dies and the UI freezes completely — cached static assets are useless because no Blazor DLL runs on the client.

**Cleanup plan:**
6-phase conversion: Blazor Server → Blazor WebAssembly (Option A in master plan).
1. Project SDK conversion + build green (no behavior change online).
2. Service worker DLL caching (`_framework/*.dll` + `blazor.boot.json`).
3. Offline API fallback hardening (update `dynamicCachePatterns` to current Option C endpoints).
4. Offline write queue (IndexedDB + Background Sync API for checkout POSTs).
5. Push notification wiring + PWA polish.
6. E2E validation + governance (correct `project_state.md` Section 1 claim + ADR-001 update).

**Risk if not addressed:** PWA install misleads users — they expect offline app but get frozen UI when network drops. Customer-facing UX defect on mobile (the primary platform for KhachLink).

---
