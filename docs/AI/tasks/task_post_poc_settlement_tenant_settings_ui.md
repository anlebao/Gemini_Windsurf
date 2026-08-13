# TASK CARD: Post-PoC UI — Settlement History + Tenant Settings — Sprint B

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement 2 admin UI pages đang thiếu: Settlement History (`/admin/settlements`) và Tenant Settings (`/admin/tenant-settings`).
- **Context:** Docs `02-owner.md` ghi "Post-PoC" / "chưa triển khai" cho 2 page này. Owner phải gọi Admin API trực tiếp thay vì dùng UI.
- **Branch:** `feature/post-poc-settlement-tenant-settings-ui`
- **Status:** ANALYZE — READY FOR PLAN REVIEW
- **Prerequisite:** Sprint A (hardcoded tenant ID fix) COMPLETE

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE (Steps 1-4) → IMPLEMENT (Steps 5-7) after approval
- **Current Phase:** ANALYZE
- **Dependency:** Sprint A COMPLETE (config-driven tenant ID for Tenant Settings page)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### B1: Settlement History Page

#### Files cần CREATE
- `5_WebApps/ShopERP/Components/Pages/Admin/Settlements.razor` — Settlement history table page
- `5_WebApps/ShopERP/Services/SettlementHistoryApiClient.cs` — Gateway API client for settlement transactions

#### Files cần MODIFY
- `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — +1 nav link (Settlements)
- `2_Gateway/Controllers/CommunityController.cs` — +admin endpoint `GET /api/admin/community/settlements` (SystemAdmin JWT, paged, date filter) OR add to new admin controller

#### Files READ ONLY
- `3_CoreHub/Services/WalletService.cs` — ConfirmCodAsync, ConfirmAdvanceReceivedAsync, ConfirmExternalPaymentAsync (all create Settlement tx)
- `1_Shared/Domain.cs` — WalletTransaction entity, WalletTransactionType.Settlement enum
- `2_Gateway/Controllers/CommunityController.cs` — existing `GET /api/community/wallet/transactions` (customer-facing, X-Customer-Token)

### B2: Tenant Settings Page

#### Files cần CREATE
- `5_WebApps/ShopERP/Components/Pages/Admin/TenantSettingsPage.razor` — Tenant settings form page
- `2_Gateway/Controllers/TenantSettingsAdminController.cs` — `GET /api/admin/tenant-settings/{tenantId}` + `PUT /api/admin/tenant-settings/{tenantId}` (SystemAdmin JWT)
- `5_WebApps/ShopERP/Services/TenantSettingsApiClient.cs` — Gateway API client

#### Files cần MODIFY
- `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — +1 nav link (Tenant Settings)
- `2_Gateway/Program.cs` — DI registration if new service needed

#### Files READ ONLY
- `1_Shared/Domain/Aggregates/TenantAggregate/TenantSettings.cs` — value object with 15+ fields + `With*` immutable update methods
- `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` — `UpdateProfile(name, settings)` method
- `3_CoreHub/Infrastructure/VanAnDbContext.cs` — Tenants DbSet

### Shared
#### Files cần CREATE (tests)
- `6_Tests/e2e-tests/admin-settlements.spec.ts` — E2E for Settlement history page (Gate 4)
- `6_Tests/e2e-tests/admin-tenant-settings.spec.ts` — E2E for Tenant Settings page (Gate 4)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **NO Domain modification** — TenantSettings, Tenant, WalletTransaction all already exist
- [ ] **NO new migrations** — no schema changes
- [ ] **UI Platform mandatory** — VanAnTable, VanAnCard, VanAnButton, VanAnForm, VanAnInput (Gate 5)
- [ ] **E2E tests required** — Gate 4: UI layout change → E2E test
- [ ] **Auth:** Admin endpoints = SystemAdmin JWT Bearer only (OQ1 resolved: Owner cannot edit Tenant Settings)
- [ ] **Settlement data:** Query `WalletTransactions WHERE Type == Settlement` — read-only, no financial logic
- [ ] **Tenant Settings:** Use `Tenant.UpdateProfile(name, settings)` — existing Domain method, no new Domain behavior
- [ ] **TenantSettings immutable update:** Use existing `With*` methods (WithContactEmail, WithAddress, etc.) — no direct setter
- [ ] **Multi-tenant:** Settlement page scoped by tenantId query param (SystemAdmin views all); Tenant Settings page requires tenantId param (SystemAdmin selects tenant)

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** `GET /api/admin/community/settlements` returns paged Settlement transactions (SystemAdmin JWT)
- [ ] **SC2:** `Settlements.razor` page displays settlement history table with date filter + pagination
- [ ] **SC3:** `GET /api/admin/tenant-settings/{tenantId}` returns current TenantSettings (SystemAdmin JWT)
- [ ] **SC4:** `PUT /api/admin/tenant-settings/{tenantId}` updates TenantSettings via `Tenant.UpdateProfile()` (SystemAdmin JWT)
- [ ] **SC5:** `TenantSettingsPage.razor` displays form with all editable TenantSettings fields
- [ ] **SC6:** NavMenu has +2 links (Settlements, Tenant Settings) under Admin section
- [ ] **SC7:** All UI uses VanAn Platform components (VanAnTable, VanAnCard, VanAnButton, VanAnForm)
- [ ] **SC8:** `dotnet build VanAn.sln` 0 errors
- [ ] **SC9:** `guard-check.ps1` pass
- [ ] **SC10:** 2 E2E specs PASS (admin-settlements.spec.ts, admin-tenant-settings.spec.ts)
- [ ] **SC11:** Settlement page shows real data (not empty — verify with test order from API testing session)
- [ ] **SC12:** Tenant Settings page edits persist (save → reload → verify)

---

## 6. ACTIVE SKILLS (MAX 3)
- `accounting-ui-implementation` — Settlement table + Tenant Settings form
- `ui-platform-migration` — VanAn component compliance
- `domain-integrity-validation` — Verify Tenant.UpdateProfile immutability, no Domain changes

---

## 7. AI HEALTH CHECK MATRIX
- **Evidence Count:** 7
- **Verified Facts:**
  - Fact 1: `WalletTransactionType.Settlement` exists in Domain.cs — used in WalletService ConfirmCodAsync (line 202), ConfirmAdvanceReceivedAsync (line 435), ConfirmExternalPaymentAsync (line 591). Verified.
  - Fact 2: `TenantSettings` value object exists with 15+ fields and `With*` immutable update methods (WithContactEmail, WithAddress, WithTaxCode, WithCoordinates, WithSlug, WithSocialLinks, WithBrandStory, WithTheme, WithCommerceModeOverride, WithStyleCustomization). Verified.
  - Fact 3: `Tenant.UpdateProfile(string name, TenantSettings settings)` method exists (Tenant.cs:164). Verified.
  - Fact 4: No `/admin/settlements` Razor page exists in ShopERP Components/Pages. Verified (grep: 0 results).
  - Fact 5: No `/admin/tenant-settings` Razor page exists. Verified (grep: 0 results).
  - Fact 6: `CommunityController` has customer-facing `GET /api/community/wallet/transactions` (X-Customer-Token) but no admin variant. Verified.
  - Fact 7: NavMenu.razor has existing Admin section with Sprint 6 + Sprint 7 nav links. Verified.
- **Assumptions:** 1
  - Assumption 1: Settlement admin endpoint can be added to `CommunityController` or new `SettlementAdminController` — need to verify Gateway routing + auth pipeline in Step 2.
- **Open Questions:** 0
  - OQ1 (RESOLVED 2026-08-13): SystemAdmin-only (JWT Bearer). Owner cannot edit Tenant Settings directly. Page accessible at `/admin/tenant-settings` with SystemAdmin JWT auth.
- **Recommended Action:** PROCEED TO ANALYZE Step 2 (Reverse Impact Analysis) — Assumptions (1) < Verified Facts (7), Open Questions (0) < 3. Gate 1 + Gate 6 PASSED.
