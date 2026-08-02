# Task Card: Phase 3.6 — Deferred Cleanup (Onboarding Refactor + Products Forwarding Port Fix)

> **Master plan:** `gateway_router_multi_vps_master_plan.md`
> **Workflow:** `newfeaturebuild.md`
> **Phase:** 3.6 of 8 (NEW — deferred from Phase 3)
> **Depends on:** Phase 4 (ShopERP OrderSyncSubscriber routing), Phase 5 (KhachLink Multi-tenant Cart)
> **Unlocks:** Clean multi-VPS onboarding + working product catalog forwarding

---

## 1. Use Case & Business Design

**Problem:**
Two issues discovered during Phase 3 VR testing were deferred to avoid scope creep + breaking existing tests:

### Issue 1: OnboardingController refactor (remove product seeding)
- **Current:** `TenantOnboardingService.OnboardAsync` seeds industry products into Gateway PG via `IIndustrySeedStrategy`.
- **Problem (Option C):** Gateway PG no longer stores Products (FK dropped in Phase 3). Product seeding into PG creates orphan data that never syncs to ShopERP SQLite.
- **Target:** Onboarding creates tenant metadata + owner only. Product seeding delegated to ShopERP remote (tenant owner runs QuickSetup after first login).
- **Complexity:** HIGH — `TenantOnboardingService` has 6-step orchestration, multiple test suites depend on it (`TenantOnboardingServiceTests`, `FnbSeedStrategyTests`, integration tests).

### Issue 2: Products forwarding port fix (5003 vs 80)
- **Current:** `ProductsController.ResolveShopErpClientAsync` falls back to named HttpClient "shoperp" when tenant has no ShopInstance. Named client BaseAddress = `config["ShopERP:BaseUrl"] ?? "http://shoperp:80/"`.
- **Problem:** VPS logs show HttpClient using `shoperp:5003` instead of `shoperp:80`. Root cause unclear:
  - `appsettings.json` on VPS has NO `ShopERP` section.
  - `appsettings.Production.json` has NO `ShopERP` section.
  - No env var `ShopERP__BaseUrl` or `ShopERP:BaseUrl` set.
  - No Docker volume mounts overriding config.
  - But log consistently shows `http://shoperp:5003/api/products`.
- **Target:** Products forwarding works correctly on VPS (port 80 or correct ShopInstance BaseUrl).
- **Complexity:** LOW (config fix) but investigation needed to find root cause.

---

## 2. Implementation Plan

### Step 1: Investigate Products forwarding port issue
- Check if `appsettings.Development.json` is being loaded despite `ASPNETCORE_ENVIRONMENT=Production`.
- Check if `AddHttpClient("shoperp", ...)` registration is overridden by another `AddHttpClient` call.
- Check if `IHttpClientFactory` default handler has a BaseAddress override.
- Check Docker image build — `COPY . .` in Dockerfile copies `appsettings.Development.json` (has `5003`). Verify it's not loaded in Production.
- **Fix:** Add `ShopERP__BaseUrl=http://shoperp:80/` to `docker-compose.edge.yml` Gateway env vars (explicit override).

### Step 2: OnboardingController refactor
- `TenantOnboardingService.OnboardAsync` — remove `IIndustrySeedStrategy` step (step 4 of 6).
- Keep steps 1-3, 5-6 (create tenant, owner user, role assignment, permission groups, owner-to-group assignment).
- Update tests: `TenantOnboardingServiceTests` — remove product seeding assertions.
- Update `FnbSeedStrategyTests` — keep strategy tests (strategies still used by ShopERP QuickSetup), but remove from onboarding test scope.
- Document: tenant owner must run QuickSetup manually after first login (`/quick-setup?tenantId=...`).

### Step 3: Multi-VPS onboarding design (future-proofing)
- When multi-VPS: onboarding should create tenant + assign ShopInstance + trigger remote product seeding via NATS or HTTP.
- For now: onboarding creates tenant + owner only. Product seeding is manual (QuickSetup).
- Future Phase 6+ may add automated remote seeding.

---

## 3. Validation Gates

| Gate | Command | Expected |
|---|---|---|
| Build | `dotnet build VanAn.sln` | 0 errors |
| Unit tests | `dotnet test 6_Tests/VanAn.Core.Tests` | All pass (updated onboarding tests) |
| VR: Products forwarding | `curl http://localhost/api/products?shopId=...` on VPS | 200 OK (not 500) |
| VR: Onboarding | POST /api/v1/onboarding/tenants → check PG (no products) | No products in PG after onboarding |
| Guard check | `./guard-check.ps1` | PASS |

---

## 4. Deliverables

- Modified: `3_CoreHub/Services/Onboarding/TenantOnboardingService.cs` (remove product seeding step)
- Modified: `6_Tests/VanAn.Core.Tests/Services/Onboarding/TenantOnboardingServiceTests.cs` (remove product assertions)
- Modified: `docker-compose.edge.yml` (add `ShopERP__BaseUrl` env var to Gateway)
- Modified: `2_Gateway/Controllers/ProductsController.cs` (if port fix needs code change)

---

## 5. Approval Gate

- [ ] OnboardingController removing product seeding approved (tenant owner runs QuickSetup manually)
- [ ] Products forwarding port fix approach approved (config override vs code change)

---

## 6. COMPLETION SUMMARY

**Phase 3.6 COMPLETE** — commit `a6413668` on `main`.

### Issue 1: OnboardingController refactor (remove product seeding)
- `TenantOnboardingService` no longer takes `IIndustrySeedStrategy` or `IVanAnDbContext`.
- Onboarding creates tenant + owner + role + permission groups only (5 steps, was 6).
- Product seeding deferred to ShopERP QuickSetup (tenant owner runs it after first login).
- `TenantOnboardingResult` seed counts always 0; Warnings includes QuickSetup deferral notice.
- `IndustryCode` field kept in `OnboardTenantRequest` for backward API compat (no longer validated).
- Updated `TenantOnboardingServiceTests` (removed seed assertions, added Phase 3.6 tests).
- Updated `TenantOnboardingIntegrationTests` (removed seed DB assertions, verify 0 products/shops/etc).

### Issue 2: Products forwarding port fix
- Added explicit `ShopERP__BaseUrl=http://shoperp:80/` env var to Gateway in `docker-compose.prod.yml` + `docker-compose.edge.yml`.
- Prevents port 5003 fallback (appsettings.Development.json leak via Docker `COPY . .`).
- VPS verification: `GET /api/products?tenantId=...` returns 200 OK with 16456 bytes.

### Architecture test fix
- `VA-CONSISTENCY-004`: Added `SHOP_INSTANCE_ID` to single-underscore exclusion list (Phase 4 fail-fast env var).

### Validation
- Build: 0 errors
- Core.Tests: 1036/0/16 PASS
- Architecture.Tests: 38/38 PASS
- guard-check: ALL PASSED
- CD: PASS (commit `a6413668`)
- RV1 (Products forwarding): PASS — 200 OK, 16456 bytes
- RV2 (Gateway health): PASS — Healthy
- Phase 5 regression: 9/9 PASS (no regression)
