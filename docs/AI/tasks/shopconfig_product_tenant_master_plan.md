# MASTER IMPLEMENTATION PLAN — ShopConfig Product→Tenant Refactor

**Created:** 2026-07-02
**Last Updated:** 2026-07-02
**Current Status:** PLANNING — Ready for Phase 1 (Revert + DTO)
**Branch strategy:** feature/shopconfig-product-tenant-phase[X]
**Execution principle:** JIT Planning + Pure Execution

---

## 0. EXECUTION RULES

### Context Management Strategy (NO CONTEXT OVERFLOW)

**Recommended Execution: 1 Phase Per Session (1-2 hours)**
- Total: ~3 sessions over 2-3 days
- Context per session: ~5-10 files only
- Risk of context overflow: **LOW**

**Session Protocol:**
1. **Start session:** Load context for CURRENT phase only (task card + relevant files)
2. **Planning Phase:** Read task card → Analyze → Plan (30 min max)
3. **Execution Phase:** Implement per plan (1-1.5 hours)
4. **End session:** Commit + Update project_state.md
5. **Next session:** Load fresh context for NEXT phase only

**Estimated Sessions:**
- Phase 1: 1 session (Revert Approach 1 + DTO TenantId)
- Phase 2: 1 session (ShopConfigHttpService product-based)
- Phase 3: 1 session (Wire-up + Integration tests + Build verify)
- **Total: 3 sessions (~3-5 hours)**

### JIT Planning Strategy
**Nguyên tắc cốt lõi:** KHÔNG code mò mẫm - Investigate trước, Implement sau

**Bước 1: INVESTIGATE & ANALYZE (Planning Phase)**
- Đọc và hiểu rõ hiện trạng implementation
- Verify DTO structures, API endpoints, DI registrations
- Identify gaps và requirements
- Chốt approach trước khi viết bất kỳ dòng code nào

**Bước 2: IMPLEMENT (Execution Phase)**
- Thực hiện viết code theo plan đã chốt ở Bước 1
- KHÔNG thay đổi approach khi đang implement (trừ khi phát hiện critical issue)
- Mỗi bước implement xong, build để verify

### Branch protocol
```
main (align-consumer-phase4)
  └── feature/shopconfig-product-tenant-phase1-dto (Phase 1 - DTO + Revert)
      └── feature/shopconfig-product-tenant-phase2-service (Phase 2 - HttpService)
          └── feature/shopconfig-product-tenant-phase3-wireup (Phase 3 - Wire-up + Tests)
```

### Hard rules (không violate)
- **Domain layer MUST NOT be modified** — ShopConfig record stays as-is
- **Multi-tenancy MUST be enforced** — products filtered by tenant
- **KhachLink MUST use HTTP via Gateway** — no direct CoreHub DI for ShopConfig
- **Build MUST PASS** — 0 errors after each phase
- **Existing tests MUST NOT break** — retrofit if needed
- **KHÔNG CODE MÒ MẪM** — Luôn Planning trước, Implement sau

---

## 1. CURRENT ISSUES SUMMARY

### Issue 1: ShopConfig is a Stub (Hardcoded, No Persistence)
**Status:** 🔴 CRITICAL
**Priority:** 1 (Critical)

**Current State:**
- ❌ `ShopConfigService.GetShopConfigAsync()` returns hardcoded values (line 18-35)
- ❌ `CreateShopConfigAsync` / `UpdateShopConfigAsync` / `DeleteShopConfigAsync` are no-ops
- ❌ ShopConfig is a `record` — NOT persisted to DB, no DbSet, no migration
- ❌ Every shop sees identical branding ("Vạn An Group", #8B4513 brown)
- ❌ Any "update" is lost on restart

**Root Cause:**
- ShopConfig was created as stub in Wave 17 (KhachLink End-User Layout)
- Persistence layer never built
- Service returns hardcoded defaults

**Impact:**
- Multi-tenant branding impossible — all shops look identical
- ShopConfigService inject directly from CoreHub violates KhachLink boundary
- No real shop data (name, address, phone) displayed to customers

**Files:**
- `3_CoreHub/Services/ShopConfigService.cs` (stub implementation)
- `1_Shared/Domain.cs` line 1170-1195 (ShopConfig record)
- `5_WebApps/KhachLink/Program.cs` line 65-67 (DI registration — direct CoreHub inject)

### Issue 2: KhachLink→CoreHub Architectural Violation
**Status:** 🔴 ARCHITECTURE VIOLATION
**Priority:** 1 (Critical)

**Current State:**
- ❌ KhachLink injects `IShopConfigService` from CoreHub directly (Program.cs line 67)
- ❌ Violates boundary: KhachLink → Gateway → ShopERP (governance rule)
- ❌ TODO comment in Program.cs acknowledges debt but not resolved
- ❌ Documented in `docs/AI/tasks/TD-001_KhachLink_ArchitecturalViolation.md`

**Root Cause:**
- Wave 17 took shortcut — injected CoreHub service directly instead of creating HTTP service
- ShopConfigHttpService never created

**Impact:**
- KhachLink has hidden dependency on CoreHub runtime
- Cannot deploy KhachLink independently
- Violates Clean Architecture dependency direction

**Files:**
- `5_WebApps/KhachLink/Program.cs` line 65-67
- `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` line 7
- `5_WebApps/KhachLink/Pages/Home.razor` (was modified, reverted)

### Issue 3: Product DTO Missing TenantId (Multi-Tenancy Gap)
**Status:** 🟡 MEDIUM — Multi-tenancy violation
**Priority:** 2 (High)

**Current State:**
- ✅ `Product` entity has `TenantId` (via BaseEntity)
- ❌ `ProductCatalogItem` (ShopERP DTO) does NOT expose TenantId
- ❌ `ProductDto` (KhachLink DTO) does NOT expose TenantId
- ❌ `GetProductsAsync(shopId=null)` returns ALL products across ALL tenants
- ❌ KhachLink cannot determine which tenant a product belongs to

**Root Cause:**
- DTO mapping strips TenantId during projection
- No tenant filter in public product catalog endpoint

**Impact:**
- Multi-tenancy violation: customer sees products from all tenants
- Cannot derive ShopConfig from product data (no TenantId available)
- Data isolation broken

**Files:**
- `5_WebApps/ShopERP/Controllers/ProductsController.cs` line 149-158 (ProductCatalogItem)
- `5_WebApps/KhachLink/Models/ProductDto.cs` (missing TenantId)
- `5_WebApps/KhachLink/Services/Http/ProductHttpService.cs` (no tenant filter)

### Issue 4: Approach 1 Remnants (Order-based, to revert)
**Status:** 🟡 INCOMPLETE — needs revert
**Priority:** 1 (Blocking Phase 1)

**Current State — files modified during Approach 1 (order-based):**
- `5_WebApps/ShopERP/Controllers/CustomerOrdersController.cs` — added TenantId to CustomerOrderDto
- `2_Gateway/Controllers/ShopsController.cs` — added GET /api/shops/by-tenant/{tenantId} endpoint
- `5_WebApps/ShopERP/Controllers/ShopsController.cs` — added GET /api/shops/by-tenant/{tenantId} endpoint
- `5_WebApps/KhachLink/Services/Http/ShopConfigHttpService.cs` — created (order-based, needs rewrite)
- `5_WebApps/KhachLink/Program.cs` — was modified then reverted (back to original IShopConfigService)

**Decision:** Revert Approach 1 order-based logic. Keep `by-tenant` endpoint (useful for Approach 2). Rewrite ShopConfigHttpService to be product-based.

**Files to revert/keep:**
- REVERT: `CustomerOrdersController.cs` TenantId addition (not needed for Approach 2)
- KEEP: `ShopsController.cs` by-tenant endpoint (both Gateway + ShopERP)
- REWRITE: `ShopConfigHttpService.cs` (product-based instead of order-based)

---

## 2. REVERSE IMPACT ANALYSIS

### Impact on ShopERP
| Component | Current State | Impact After Fix | Mitigation |
|-----------|---------------|------------------|------------|
| ProductsController | No TenantId in DTO | Add TenantId to ProductCatalogItem | Backward compatible (new field) |
| ShopsController | No by-tenant endpoint | Endpoint already added (Approach 1 remnant) | Keep as-is |
| CustomerOrdersController | TenantId added (Approach 1) | Revert TenantId addition | Restore original DTO |

### Impact on Gateway
| Component | Current State | Impact After Fix | Mitigation |
|-----------|---------------|------------------|------------|
| ShopsController | by-tenant endpoint added | Keep as-is | No change needed |

### Impact on KhachLink
| Component | Current State | Impact After Fix | Mitigation |
|-----------|---------------|------------------|------------|
| Program.cs | Direct CoreHub IShopConfigService inject | Replace with ShopConfigHttpService | Update DI registration |
| KhachLinkLayout.razor | @inject IShopConfigService | @inject ShopConfigHttpService | Update inject + load method |
| Home.razor | No ShopConfig inject | Add ShopConfigHttpService + SocialHub | Re-apply from earlier work |
| ProductDto.cs | No TenantId | Add TenantId field | Backward compatible |
| ProductHttpService.cs | No tenant filter | Optional: filter by tenant | Low risk |

### Impact on Domain Layer
| Component | Impact | Mitigation |
|-----------|--------|------------|
| ShopConfig record | **NO CHANGE** | Domain protection — record stays as-is |
| Product entity | **NO CHANGE** | TenantId already exists via BaseEntity |
| Shop entity | **NO CHANGE** | Already has all needed fields |

---

## 3. PHASE 1 — Revert Approach 1 + Add TenantId to Product DTOs

**Branch:** feature/shopconfig-product-tenant-phase1-dto
**Estimated sessions:** 1
**Conflict risk:** LOW (DTO additions + revert)
**Priority:** 1 (Blocking)
**Task Card:** `docs/AI/tasks/shopconfig_phase1_revert_dto_task_card.md`
**Status:** PENDING

### Rationale
Revert the order-based Approach 1 remnants and add TenantId to product DTOs — the foundation for product-based ShopConfig loading.

### Tasks (sequential)
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | P1-T1 | Revert CustomerOrderDto TenantId addition | ShopERP/Controllers/CustomerOrdersController.cs | PENDING |
| 2 | P1-T2 | Add TenantId to ProductCatalogItem (ShopERP) | ShopERP/Controllers/ProductsController.cs | PENDING |
| 3 | P1-T3 | Map TenantId in GetProducts projection | ShopERP/Controllers/ProductsController.cs | PENDING |
| 4 | P1-T4 | Add TenantId to ProductDto (KhachLink) | KhachLink/Models/ProductDto.cs | PENDING |
| 5 | P1-T5 | Build verify (0 errors) | All projects | PENDING |

### Entry criteria
- [ ] Project builds successfully
- [ ] Approach 1 remnants identified
- [ ] Domain layer NOT modified

### Exit criteria — ALL PASSED
- [ ] CustomerOrderDto reverted to original (no TenantId)
- [ ] ProductCatalogItem has TenantId field
- [ ] ProductDto has TenantId field
- [ ] GetProducts projection maps TenantId
- [ ] Build: 0 errors
- [ ] No Domain layer changes

---

## 4. PHASE 2 — Rewrite ShopConfigHttpService (Product-Based)

**Branch:** feature/shopconfig-product-tenant-phase2-service
**Estimated sessions:** 1
**Conflict risk:** LOW (new service + DI change)
**Priority:** 2 (Critical)
**Task Card:** `docs/AI/tasks/shopconfig_phase2_http_service_task_card.md`
**Status:** PENDING

### Rationale
Rewrite ShopConfigHttpService to load ShopConfig from product data: products → extract TenantId → GET /api/shops/by-tenant/{tenantId} → build ShopConfig from real Shop entity.

### Tasks (sequential)
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | P2-T1 | Rewrite ShopConfigHttpService (product-based) | KhachLink/Services/Http/ShopConfigHttpService.cs | PENDING |
| 2 | P2-T2 | Remove IShopConfigService DI, add ShopConfigHttpService DI | KhachLink/Program.cs | PENDING |
| 3 | P2-T3 | Build verify (0 errors) | All projects | PENDING |

### Entry criteria
- [ ] Phase 1 complete (ProductDto has TenantId)
- [ ] by-tenant endpoint exists (Gateway + ShopERP)
- [ ] Domain layer NOT modified

### Exit criteria — ALL PASSED
- [ ] ShopConfigHttpService loads from products → TenantId → shop data
- [ ] No direct CoreHub IShopConfigService dependency in KhachLink
- [ ] Fallback to default config when no products / shop not found
- [ ] Build: 0 errors

---

## 5. PHASE 3 — Wire-up Components + Integration Tests

**Branch:** feature/shopconfig-product-tenant-phase3-wireup
**Estimated sessions:** 1
**Conflict risk:** MEDIUM (UI component changes + tests)
**Priority:** 3 (High)
**Task Card:** `docs/AI/tasks/shopconfig_phase3_wireup_tests_task_card.md`
**Status:** PENDING

### Rationale
Wire ShopConfigHttpService into KhachLinkLayout + Home.razor (with SocialHub). Add integration test assertions. Verify end-to-end build.

### Tasks (sequential)
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | P3-T1 | Update KhachLinkLayout to use ShopConfigHttpService | KhachLink/Components/Layout/KhachLinkLayout.razor | PENDING |
| 2 | P3-T2 | Re-apply Home.razor SocialHub + ShopConfig inject | KhachLink/Pages/Home.razor | PENDING |
| 3 | P3-T3 | Add KhachLinkStartupTests assertion for ShopConfigHttpService | 6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs | PENDING |
| 4 | P3-T4 | Build verify (0 errors) + guard-check | All projects | PENDING |
| 5 | P3-T5 | Update project_state.md | docs/AI/project_state.md | PENDING |

### Entry criteria
- [ ] Phase 2 complete (ShopConfigHttpService registered in DI)
- [ ] ShopConfigHttpService builds successfully

### Exit criteria — ALL PASSED
- [ ] KhachLinkLayout loads real shop data (name, phone, address)
- [ ] Home.razor renders SocialHub with real ShopConfig
- [ ] KhachLinkStartupTests asserts ShopConfigHttpService DI
- [ ] Build: 0 errors
- [ ] guard-check.ps1 passes
- [ ] No IShopConfigService (CoreHub) reference in KhachLink
- [ ] project_state.md updated

---

## 6. ARCHITECTURE DECISION RECORD

### Decision: Product-Based ShopConfig Loading (Approach 2)

**Context:** ShopConfig needs real data per tenant. Two approaches considered:
- Approach 1: Order → TenantId (requires login + order history)
- Approach 2: Product → TenantId (works for anonymous visitors)

**Decision:** Approach 2 — Product-based

**Rationale:**
1. KhachLink is customer-facing — product is first touchpoint, not order
2. Anonymous visitors see correct branding without login
3. Simpler flow: products (already loaded) → TenantId → shop data
4. Fixes multi-tenancy violation (products currently cross-tenant)

**Consequences:**
- ProductCatalogItem + ProductDto gain TenantId field (backward compatible)
- ShopConfigHttpService depends on ProductHttpService (or duplicates product fetch)
- Branding fields (PrimaryColor, SecondaryColor, Theme) remain defaults — not stored in Shop entity
- Future: may need BrandingSettings value object if per-tenant branding required

**Compliance:**
- ✅ Domain layer NOT modified
- ✅ KhachLink uses HTTP via Gateway (no direct CoreHub)
- ✅ Multi-tenancy enforced (products carry TenantId)
- ✅ Clean Architecture dependency direction preserved

---

## 7. FILE INVENTORY

### Files to modify
| Phase | File | Change |
|---|---|---|
| P1 | `5_WebApps/ShopERP/Controllers/CustomerOrdersController.cs` | Revert TenantId addition |
| P1 | `5_WebApps/ShopERP/Controllers/ProductsController.cs` | Add TenantId to ProductCatalogItem + projection |
| P1 | `5_WebApps/KhachLink/Models/ProductDto.cs` | Add TenantId field |
| P2 | `5_WebApps/KhachLink/Services/Http/ShopConfigHttpService.cs` | Rewrite (product-based) |
| P2 | `5_WebApps/KhachLink/Program.cs` | Replace IShopConfigService DI with ShopConfigHttpService |
| P3 | `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` | Use ShopConfigHttpService |
| P3 | `5_WebApps/KhachLink/Pages/Home.razor` | Add ShopConfigHttpService + SocialHub |
| P3 | `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` | Add DI assertion |
| P3 | `docs/AI/project_state.md` | Update progress |

### Files to keep (already modified, no further change)
| File | Status |
|---|---|
| `2_Gateway/Controllers/ShopsController.cs` | by-tenant endpoint — KEEP |
| `5_WebApps/ShopERP/Controllers/ShopsController.cs` | by-tenant endpoint — KEEP |

### Files NOT to modify (Domain protection)
| File | Reason |
|---|---|
| `1_Shared/Domain.cs` | Domain layer — ShopConfig record stays as-is |
| `1_Shared/Domain/Common.cs` | BaseEntity already has TenantId |
| `1_Shared/Domain/Aggregates/TenantAggregate/*` | Tenant aggregate — no changes |

---

## 8. RISK ASSESSMENT

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| ProductDto TenantId breaks existing consumers | LOW | LOW | New field — backward compatible |
| ShopConfigHttpService fails when no products | MEDIUM | LOW | Fallback to DefaultShopConfig |
| Multi-tenant product query returns wrong tenant | MEDIUM | MEDIUM | Filter by first product's TenantId |
| KhachLinkLayout render fails with new service | LOW | MEDIUM | Build verify + fallback defaults |
| Integration tests fail (DI change) | LOW | MEDIUM | Add assertion in Phase 3 |

---

## 9. SUCCESS METRICS

| Metric | Before | After |
|---|---|---|
| ShopConfig data source | Hardcoded stub | Real Shop entity via API |
| KhachLink→CoreHub direct dependency | YES (violation) | NO (HTTP via Gateway) |
| Product DTO has TenantId | NO | YES |
| Multi-tenant product isolation | BROKEN | ENFORCED |
| Anonymous visitor sees correct branding | NO | YES |
| Build errors | 0 | 0 |
| Domain layer modified | — | NO (protected) |
