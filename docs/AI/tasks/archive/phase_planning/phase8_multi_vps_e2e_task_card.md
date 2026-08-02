# Task Card: Phase 8 — Multi-VPS E2E Validation (Playwright)

> **Master plan:** `gateway_router_multi_vps_master_plan.md`
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT) + `playwright_validation.md`
> **Phase:** 8 of 8 (added per Round 2 decisions — post-implementation validation)
> **Depends on:** Phase 7 COMPLETE (governance + ADR + final verification)

---

## 1. Use Case & Business Design

**Problem:** Phases 1-6 + 3.6 implemented + deployed + RV-verified on VPS (single-ShopInstance mode). However:
1. **No Playwright E2E coverage** for the full multi-tenant checkout flow (scan QR → add to cart → checkout → order created in PG → NATS routed → ShopERP SQLite replica).
2. **No multi-VPS simulation** — current VPS has 1 ShopInstance. Phase 8 should validate that 2+ ShopInstances route correctly (tenant A → ShopERP-A, tenant B → ShopERP-B).
3. **CustomerRecommendationService retirement** — Phase 6 added `CatalogController` as replacement. Phase 8 E2E should verify `CatalogController` works end-to-end before deleting the old service.

**Goal:** Comprehensive Playwright E2E suite covering:
- Single-tenant checkout (regression — Phase 5 flow)
- Multi-tenant checkout (cart with 2 tenants → 2 orders, each routed to correct ShopInstance)
- FeaturedProduct display on Home.razor (Phase 6)
- Anonymous vs logged-in customer experience
- Order tracking after checkout
- Admin UI smoke tests (`/admin/shop-instances`, `/admin/featured-products`, `/admin/tenants` with new column)

**Out of scope:** Load testing, chaos engineering, real multi-VPS provisioning (Terraform/Docker swarm — future work).

---

## 2. Scope (PLACEHOLDER — to be detailed in ANALYZE phase)

### E2E Test Scenarios
1. **E2E-1: Single-tenant checkout** — scan QR → add to cart → checkout → order appears in OrderTracking
2. **E2E-2: Multi-tenant checkout** — add product from tenant A + product from tenant B → checkout → 2 orders created, both tracked
3. **E2E-3: FeaturedProduct on Home.razor** — sysadmin adds FeaturedProduct via `/admin/featured-products` → anonymous user sees it on Home.razor
4. **E2E-4: Customer history** — logged-in customer with past orders sees "Gợi Ý Dựa Trên Lịch Sử Mua Hàng" section
5. **E2E-5: Admin ShopInstances CRUD** — create + edit + deactivate + health check
6. **E2E-6: Admin TenantManagement** — new column shows correct ShopInstance, onboarding modal has dropdown
7. **E2E-7: Multi-VPS routing simulation** — 2 ShopERP containers with different `SHOP_INSTANCE_ID` → checkout tenant A → order appears only in ShopERP-A's SQLite

### Infrastructure
- Docker Compose with 2 ShopERP instances (different `SHOP_INSTANCE_ID` + different SQLite volumes)
- Playwright test runner with browser isolation per `playwright.rules.md`
- Test data: 2 tenants, 2 ShopInstances, 4 products (2 per tenant), 1 FeaturedProduct

---

## 3. Validation Gates

| Gate | Command | Expected |
|---|---|---|
| Build | `dotnet build VanAn.sln` | 0 errors |
| All tests | `dotnet test` | No regressions |
| Guard check | `./guard-check.ps1` | PASS |
| Playwright E2E | `npx playwright test` | All 7 scenarios PASS |

---

## 4. Deliverables

- New: `6_Testing/e2e-tests/multi-vps-checkout.spec.ts` (or similar)
- New: `docker-compose.multi-vps-test.yml` (2 ShopERP instances for E2E)
- Modified: `3_CoreHub/Services/CustomerRecommendationService.cs` (mark `[Obsolete]` or delete after E2E-3 verifies CatalogController)
- Modified: `docs/AI/project_state.md` (Phase 8 COMPLETE entry)

---

## 5. Approval Gate

- [ ] Phase 7 COMPLETE
- [ ] User approves Playwright E2E scope
- [ ] User acknowledges multi-VPS Docker Compose test setup

---

## 6. COMPLETION SUMMARY

**Phase 8 COMPLETE** — commit `<HASH>` on `main`.

_TBD_
