# TASK CARD — Platform SystemAdmin Access Matrix (Cross-Tenant Verification)

> **Status:** 🟡 PLANNED — awaiting user approval
> **Prerequisite:** F1-F5 từ `platform_systemadmin_task_card.md` phải COMPLETE
> **Branch:** `main`
> **Estimated sessions:** 2-3 (ANALYZE 0.5 + DESIGN 0.5 + IMPLEMENT 1 + VERIFY 1)
> **Master plan:** `docs/AI/tasks/platform_systemadmin_access_matrix_master_plan.md`
> **Parent feature:** `docs/AI/tasks/platform_systemadmin_master_plan.md` (login — COMPLETE-WITH-DEVIATIONS)
>
> ⚠️ **Plan này KHÔNG thực hiện cho đến khi user approve.**
> ⚠️ **F1-F5 phải COMPLETE trước khi chạy VERIFY phase.**

## Objective

Verify SystemAdmin (cross-tenant, platform-level admin) truy cập đúng entry points trong toàn app:
1. **Liệt kê** tất cả entry points mà SystemAdmin có thể/không thể truy cập
2. **Verify** SystemAdmin truy cập thành công chúng (HTTP test với auth thật, không `TestAuthenticationHandler`)
3. **Fix** role mismatch + policy gap phát hiện qua audit

Phát sinh từ Deviation #5 (AuditTrail `Roles="Admin"` mismatch) — parent feature claim SystemAdmin access `/admin/audit-trail` nhưng thực tế fail.

## Architecture Decision

Access Matrix = **verification concern riêng** (không phải login concern). Tách plan vì:
- Scope lớn (~41 entry points × nhiều test cases)
- Cần design decisions cho 5 category
- Phụ thuộc F1-F5 (login) phải work trước
- Cần HTTP test infrastructure mới (factory không test auth handler)

## Prerequisites (verify before Phase 1)

- [ ] F1-F5 từ `platform_systemadmin_task_card.md` COMPLETE
- [ ] `POST /api/platform/login` trả 200 + JWT trong production-like flow
- [ ] `guard-check.ps1` PASS
- [ ] `docs/AI/artifacts/` directory tồn tại (hoặc tạo)

## Files Created/Modified

| File | Action | Phase | Purpose |
|---|---|---|---|
| `docs/AI/artifacts/platform_systemadmin_access_matrix_audit.md` | CREATE | ANALYZE | Audit artifact: enum + categorize + flag |
| `5_WebApps/ShopERP/Components/Pages/Admin/AuditTrail.razor` | MODIFY | IMPLEMENT | Fix role mismatch (F5 — có thể overlap với parent plan) |
| `5_WebApps/ShopERP/Controllers/ApiKeyController.cs` | MODIFY (if D4 approved) | IMPLEMENT | Add SystemAdmin role |
| `5_WebApps/ShopERP/Program.cs` | MODIFY (if D2 approved) | IMPLEMENT | Update RequireTenantAccess policy |
| `6_Tests/VanAn.Integration.Tests/Infrastructure/AuthRealWebApplicationFactory.cs` | CREATE | IMPLEMENT | Factory không TestAuthenticationHandler, mint JWT thật |
| `6_Tests/VanAn.Integration.Tests/PlatformSystemAdminAccessMatrixTests.cs` | CREATE | VERIFY | HTTP tests với auth thật, cover 7 policies |

## Detailed Task List

### Phase 1: ANALYZE (REVIEW_ONLY — không sửa code)

#### AM-T1: Enum tất cả entry points
- [ ] Grep `[Authorize` trong `5_WebApps/ShopERP/Controllers/*.cs` (17 controllers)
- [ ] Grep `@attribute [Authorize` trong `5_WebApps/ShopERP/Components/Pages/**/*.razor` (24 pages)
- [ ] Grep `AddPolicy(` trong `Program.cs` (8 policies)
- [ ] Grep `[Authorize` trong `5_WebApps/ShopERP/Pages/*.cshtml.cs` (3 pages)
- [ ] Grep `[Authorize` trong `5_WebApps/ShopERP/EInvoice/Controllers/*.cs` (1 controller)
- [ ] Output: bảng entry point × attribute × policy/role string

#### AM-T2: Categorize A-E
- [ ] **Category A — Admin pages** (SystemAdmin SHOULD access): `/admin/*`
- [ ] **Category B — Tenant-scoped business** (policy pass nhưng TenantId=Empty): Accounting/EInvoice/Orders/Products
- [ ] **Category C — RequireTenantAccess** (SystemAdmin fail vì không có `tenant_id` claim): ShopsController, HKDElectronicInvoiceController
- [ ] **Category D — Operational roles** (đúng exclude SystemAdmin): Kitchen, GuardRedirect
- [ ] **Category E — Role string mismatch** (SystemAdmin bị exclude sai): AuditTrail `Roles="Admin"`, ApiKeyController `Roles="Admin,Owner"`
- [ ] Output: classification table

#### AM-T3: Flag potential gaps
- [ ] AuditTrail.razor `Roles="Admin"` — flag (Deviation #5)
- [ ] ApiKeyController `Roles="Admin,Owner"` — flag (D4)
- [ ] RequireTenantAccess — flag (D2)
- [ ] Any entry point mà Objective parent plan claim access nhưng thực tế fail
- [ ] Output: flagged list với reasoning

### Phase 2: DESIGN (await user decision — EDR-AM-3)

#### AM-T4: Propose options cho D1-D5
- [ ] D1 (Category B): SystemAdmin truy cập tenant-scoped business data?
  - (a) Có + impersonation tenant (chọn tenant để query data)
  - (b) Có + aggregated view (data tất cả tenants gộp lại)
  - (c) Không — chỉ admin pages, tenant business là tenant concern
- [ ] D2 (Category C): RequireTenantAccess exclude SystemAdmin intentional?
  - (a) Cố ý — SystemAdmin không cần truy cập tenant-specific resources (Shops, HKDInvoice)
  - (b) Bug — phải pass + auto-pick tenant (cần claim `tenant_id` default)
- [ ] D3 (F5 fix): AuditTrail đổi attribute?
  - (a) `Roles="SystemAdmin"` (string match)
  - (b) `Policy="SystemAdmin"` (nhất quán với TenantManagement.razor)
- [ ] D4 (Category E): ApiKeyController thêm SystemAdmin?
  - (a) Có — SystemAdmin quản platform API keys
  - (b) Không — ApiKey tenant-scoped, SystemAdmin không can thiệp
- [ ] D5 (Category D): Kitchen, GuardRedirect chạm không?
  - (a) Không chạm — đúng exclude SystemAdmin (kitchen staff, guard là tenant operational roles)
  - (b) Review lại — có logic platform-level nào hidden không

#### AM-T5: User decide
- [ ] User chọn option cho D1-D5
- [ ] Nếu user defer decision → mark "deferred", exclude khỏi scope, ghi rõ trong artifact

#### AM-T6: Log decisions
- [ ] Ghi vào section "Design Decisions" cuối task card
- [ ] Format: `D#: <question> → <user's choice> — <rationale>`

### Phase 3: IMPLEMENT (code fix — EDR-AM-5: no Domain)

#### AM-T7: Fix role mismatch
- [ ] `AuditTrail.razor` L16: `Roles="Admin"` → theo D3 (a) `Roles="SystemAdmin"` hoặc (b) `Policy="SystemAdmin"`
- [ ] `ApiKeyController.cs` L20: nếu D4 (a) → thêm `,SystemAdmin` vào `Roles="Admin,Owner,SystemAdmin"`
- [ ] Build pass

#### AM-T8: Fix policy (if D2 = bug)
- [ ] `Program.cs` L313-315: `RequireTenantAccess` — nếu D2 (b) → thêm SystemAdmin bypass:
  ```csharp
  .AddPolicy("RequireTenantAccess", policy =>
      policy.RequireAuthenticatedUser()
           .RequireAssertion(ctx =>
               ctx.User.IsInRole("SystemAdmin") ||
               ctx.User.HasClaim("tenant_id")))
  ```
- [ ] Build pass
- [ ] Existing tests (ShopsController, HKDElectronicInvoiceController) vẫn pass hoặc update nếu cần

#### AM-T9: Setup HTTP test infrastructure
- [ ] Tạo `6_Tests/VanAn.Integration.Tests/Infrastructure/AuthRealWebApplicationFactory.cs`
- [ ] Factory này KHÔNG register `TestAuthenticationHandler` (EDR-AM-1)
- [ ] Test phải mint JWT thật qua `IJwtTokenService` (inject từ DI container)
- [ ] Hoặc: sub-cutaneous test — `HttpClient` với `Authorization: Bearer <jwt>` header
- [ ] Helper: `LoginAsSystemAdminAsync()` → returns `HttpClient` với cookie/JWT set
- [ ] Build pass

### Phase 4: VERIFY (HTTP test với auth thật — EDR-AM-1, EDR-AM-2)

#### AM-T10: Write HTTP tests với auth thật
- [ ] Test: `LoginAsSystemAdmin_ReturnsJwt` — `POST /api/platform/login` → 200 + JWT
- [ ] Test: `SystemAdmin_AccessAdminUsers_Returns200` — GET `/admin/users` với JWT
- [ ] Test: `SystemAdmin_AccessAdminTenants_Returns200` — GET `/admin/tenants` với JWT
- [ ] Test: `SystemAdmin_AccessAdminAuditTrail_Returns200` — GET `/admin/audit-trail` với JWT (fix F5 verify)
- [ ] Test: `SystemAdmin_AccessAdminPermissionGroups_Returns200`
- [ ] Test: `SystemAdmin_AccessTenantScoped_ReturnsExpected` — theo D1 (200 if approved, 200-with-empty-data if not approved)
- [ ] Test: `SystemAdmin_AccessRequireTenantAccess_ReturnsExpected` — theo D2
- [ ] Test: `SystemAdmin_AccessKitchen_Returns403` — Category D (should fail)
- [ ] Test: `SystemAdmin_AccessGuardRedirect_Returns403` — Category D
- [ ] Test: `SystemAdmin_FailGuardOnly_Returns403` — GuardOnly policy
- [ ] Test: `AnonymousUser_LoginEndpoint_Returns200` — verify [AllowAnonymous] (F1) — không qua test auth handler
- [ ] Test: `AnonymousUser_ProtectedEndpoint_Returns401` — verify auth enforced
- [ ] **EDR-AM-2:** Audit trail test case cho mỗi policy (7 policies × SystemAdmin pass/fail)

#### AM-T11: Run verification checklist
- [ ] `dotnet build VanAn.sln` — 0 errors (command + output)
- [ ] `guard-check.ps1` — PASS (command + output)
- [ ] Core.Tests — all PASS (command + output)
- [ ] Arch.Tests — all PASS (command + output)
- [ ] Integration.Tests — all PASS (command + output)
- [ ] `PlatformSystemAdminAccessMatrixTests` — all PASS (command + output)
- [ ] Manual verify: `curl POST /api/platform/login` với sysadmin@vanan.vn → 200 + JWT (command + output)

#### AM-T12: Final report
- [ ] Update task card status: 🟡 → 🟢 COMPLETE (if all pass) hoặc 🟠 (if deviations)
- [ ] Update master plan status
- [ ] Update `docs/AI/project_state.md` Section 6 (History Log) + Section 9 (Maintenance Log)
- [ ] Commit: `[PLATFORM-ADMIN-ACCESS-MATRIX] Verify SystemAdmin access matrix — <summary>`

## Verification Checklist (to be filled in AM-T11)

| Check | Expected | Actual | Pass? | Command + Output |
|---|---|---|---|---|
| `dotnet build VanAn.sln` 0 errors | 0 errors | TBD | TBD | TBD |
| `guard-check.ps1` PASS | PASS | TBD | TBD | TBD |
| Core.Tests all PASS | all PASS | TBD | TBD | TBD |
| Arch.Tests all PASS | all PASS | TBD | TBD | TBD |
| Integration.Tests all PASS | all PASS | TBD | TBD | TBD |
| `PlatformSystemAdminAccessMatrixTests` all PASS | all PASS | TBD | TBD | TBD |
| `POST /api/platform/login` 200 + JWT | 200 + JWT | TBD | TBD | TBD |
| SystemAdmin access /admin/users | 200 | TBD | TBD | TBD |
| SystemAdmin access /admin/tenants | 200 | TBD | TBD | TBD |
| SystemAdmin access /admin/audit-trail | 200 | TBD | TBD | TBD |
| SystemAdmin access /admin/permission-groups | 200 | TBD | TBD | TBD |
| SystemAdmin access Category B (per D1) | per D1 | TBD | TBD | TBD |
| SystemAdmin access Category C (per D2) | per D2 | TBD | TBD | TBD |
| SystemAdmin fail Category D (Kitchen, Guard) | 403 | TBD | TBD | TBD |
| Anonymous login endpoint | 200 | TBD | TBD | TBD |
| Anonymous protected endpoint | 401 | TBD | TBD | TBD |
| Domain layer not modified | no diff | TBD | TBD | TBD |

## Rollback

- Revert IMPLEMENT commits (AM-T7, AM-T8, AM-T9)
- Delete test files (AuthRealWebApplicationFactory.cs, PlatformSystemAdminAccessMatrixTests.cs)
- Delete artifact (access_matrix_audit.md)
- Revert Program.cs policy changes (if any)

## Design Decisions (to be filled in AM-T6)

<!-- Format: D#: <question> → <user's choice> — <rationale> -->

_D1: TBD_
_D2: TBD_
_D3: TBD_
_D4: TBD_
_D5: TBD_

## Open Questions

- Q1: Category B (tenant-scoped business) — impersonation có cần Domain change không? (Nếu có → Hard Stop, EDR-AM-5)
- Q2: `AuthRealWebApplicationFactory` có thể share connection với `CustomWebApplicationFactory` không, hay phải connection riêng?
- Q3: Test class scope — 1 class cho tất cả 7 policies, hay 1 class per policy? (EDR-AM-2 yêu cầu cover all 7, structure tuỳ impl)

## Files NOT Modified (Hard Stops)

- `1_Shared/Domain/Aggregates/UserAggregate/UserRole.cs` — không sửa enum
- `1_Shared/Domain/Common.cs` — không sửa PlatformRole enum
- `1_Shared/Domain/Aggregates/UserAggregate/DemoUser.cs` — không sửa aggregate
- `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` — không sửa aggregate (nếu D1 cần impersonation → Domain Modeling Defect report, không tự sửa)
- `5_WebApps/ShopERP/Controllers/DevLoginController.cs` — giữ nguyên (E2E tests)

## Deviation Log (to be filled nếu có deviation trong execution)

<!-- Format như parent task card: Deviation #N — <title> (severity), hiện trạng, plan nói gì, fix cần làm, bài học -->
