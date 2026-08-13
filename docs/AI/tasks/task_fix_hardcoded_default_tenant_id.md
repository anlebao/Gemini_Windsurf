# TASK CARD: Hardcoded Default Tenant ID Cleanup — Sprint A

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thay thế 4 vị trí hardcode `Guid.Parse("00000000-0000-0000-0000-000000000001")` bằng config-driven resolution qua `IConfiguration["Seed:TenantId"]`.
- **Context:** Codebase scan (2026-08-13) phát hiện default tenant ID hardcode tại 4 file production code. Các file khác (Program.cs, BirthdayBonusJob, VoucherExpiryReminder) đã config-overridable.
- **Branch:** `fix/hardcoded-default-tenant-id`
- **Status:** READY FOR IMPLEMENT

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Fix_Errors.md` (FIX_ONLY — pattern-based fix, no new features)
- **Execution Mode:** FIX_ONLY
- **Pattern:** Replace hardcoded Guid.Parse with IConfiguration read + fallback

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần MODIFY (4)
1. `3_CoreHub/Services/ProductReferralConfigService.cs` — line 52: inject IConfiguration, replace `Guid.Parse("00000000-...")` with `IConfiguration["Seed:TenantId"]` fallback
2. `5_WebApps/ShopERP/Controllers/SocialAuthController.cs` — line 136: `GetDefaultTenantId()` method, use `_configuration["Seed:TenantId"]` (IConfiguration already injected)
3. `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — line 265: inject IConfiguration, replace hardcoded fallback
4. `5_WebApps/ShopERP/Components/Pages/Admin/PermissionGroupManagement.razor.cs` — line 146: inject IConfiguration, replace hardcoded fallback

### Files READ ONLY
- `5_WebApps/ShopERP/Program.cs:665` — reference pattern: `builder.Configuration["Seed:TenantId"] ?? "00000000-0000-0000-0000-000000000001"`
- `5_WebApps/ShopERP/Services/BirthdayBonusJob.cs:91` — reference pattern: `_configuration["Seed:TenantId"] ?? "00000000-..."`

### Files NOT CHANGED (by design)
- `1_Shared/Domain.cs:2009` — ShopConfig record default (requires Domain modification approval)
- `1_Shared/Domain/Common/SystemWalletIds.cs` — system-level constants
- `3_CoreHub/Infrastructure/Migrations/20260719102319_AddShopInstancesAndTenantFk.cs` — migration seed data
- `6_Tests/` — test files use hardcoded GUID as test fixture (acceptable)

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **NO Domain modification** — no changes to Domain.cs or Domain/ folder
- [ ] **NO new migrations** — no schema changes
- [ ] **Config key:** `Seed:TenantId` (same key as Program.cs, BirthdayBonusJob, VoucherExpiryReminder)
- [ ] **Fallback:** `?? "00000000-0000-0000-0000-000000000001"` (same default, but now overridable)
- [ ] **DI:** Inject `IConfiguration` where not already injected (ProductReferralConfigService, CustomerIdentityController, PermissionGroupManagement.razor.cs)
- [ ] **Behavior unchanged:** Production already sets `Seed:TenantId` in config — this fix only makes the 4 hardcoded spots consistent

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** `ProductReferralConfigService.cs` reads tenant ID from `IConfiguration["Seed:TenantId"]` with fallback
- [ ] **SC2:** `SocialAuthController.GetDefaultTenantId()` reads from `_configuration["Seed:TenantId"]` with fallback
- [ ] **SC3:** `CustomerIdentityController` reads tenant ID from `IConfiguration["Seed:TenantId"]` with fallback
- [ ] **SC4:** `PermissionGroupManagement.razor.cs` reads tenant ID from `IConfiguration["Seed:TenantId"]` with fallback
- [ ] **SC5:** `dotnet build VanAn.sln` 0 errors
- [ ] **SC6:** `guard-check.ps1` pass
- [ ] **SC7:** No behavior change — grep confirms 0 remaining `Guid.Parse("00000000-0000-0000-0000-000000000001")` in non-test, non-migration, non-Domain production code

---

## 6. ACTIVE SKILLS (MAX 3)
- `system-refactor-safety` — safe replacement pattern, no behavior change
- `pattern-based-fixing` — same fix pattern applied to 4 files
- `domain-integrity-validation` — verify no Domain layer impact

---

## 7. AI HEALTH CHECK MATRIX
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: `Program.cs:665` uses `builder.Configuration["Seed:TenantId"] ?? "00000000-0000-0000-0000-000000000001"` — config-overridable pattern exists. Verified.
  - Fact 2: `BirthdayBonusJob.cs:91` uses `_configuration["Seed:TenantId"] ?? "00000000-..."` — same pattern in service. Verified.
  - Fact 3: `VoucherExpiryReminderJob.cs:90` uses same pattern. Verified.
  - Fact 4: `ProductReferralConfigService.cs:52` — `Guid.Parse("00000000-...")` hardcoded, no IConfiguration injected. Verified.
  - Fact 5: `SocialAuthController.cs:136` — `GetDefaultTenantId()` returns hardcoded GUID, IConfiguration already injected via constructor. Verified.
  - Fact 6: `CustomerIdentityController.cs:265` — hardcoded fallback, need to check if IConfiguration injected. Verified.
  - Fact 7: `PermissionGroupManagement.razor.cs:146` — `GetTenantId()` uses hardcoded GUID when TenantProvider returns Empty. Verified.
  - Fact 8: `Domain.cs:2009` ShopConfig — record init, NOT changed (Domain protection). Verified.
- **Assumptions:** 0
- **Open Questions:** 0
- **Recommended Action:** PROCEED TO IMPLEMENT — Assumptions (0) < Verified Facts (8), Open Questions (0) < 3. Gate 1 + Gate 6 PASSED.
