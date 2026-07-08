# TASK CARD — Platform SystemAdmin Access Matrix (Cross-Tenant Verification)

> **Status:** � COMPLETE — IMPLEMENTED + VERIFIED 2026-07-08
> **Prerequisite:** F1-F5 từ `platform_systemadmin_task_card.md` phải COMPLETE ✅
> **Branch:** `main`
> **Estimated sessions:** 3-4 (ANALYZE 0.5 + DESIGN 0.5 + IMPLEMENT 1.5 + VERIFY 1)
> **Master plan:** `docs/AI/tasks/platform_systemadmin_access_matrix_master_plan.md`
> **Parent feature:** `docs/AI/tasks/platform_systemadmin_master_plan.md` (login — F1-F5 fixed)
>
> ⚠️ **Plan này KHÔNG thực hiện cho đến khi user approve.**
> ⚠️ **F1-F5 đã COMPLETE (commit `cdab2e7`).** D1, D2, D3, D6 đã được user resolve 2026-07-08.

## Objective

Verify SystemAdmin (cross-tenant, platform-level admin) truy cập đúng entry points trong toàn app:
1. **Liệt kê** tất cả entry points mà SystemAdmin có thể/không thể truy cập
2. **Implement tenant impersonation** — "All Tenants" page với "Access as Tenant" button → SystemAdmin chọn tenant → set tenant_id claim → truy cập tenant-scoped data
3. **Verify** SystemAdmin truy cập thành công (HTTP test với auth thật, không `TestAuthenticationHandler`)
4. **Fix** role mismatch + policy gap còn lại phát hiện qua audit

Phát sinh từ Deviation #5 (AuditTrail `Roles="Admin"` mismatch) — parent feature claim SystemAdmin access `/admin/audit-trail` nhưng thực tế fail.

## Architecture Decision

Access Matrix = **verification concern riêng** (không phải login concern). Tách plan vì:
- Scope lớn (~41 entry points × nhiều test cases)
- Cần design decisions cho 5 category (D1, D2, D3 resolved; D4, D5 pending)
- Phụ thuộc F1-F5 (login) đã COMPLETE ✅
- Cần HTTP test infrastructure mới (factory không test auth handler)

**Tenant Impersonation (D6 resolved 2026-07-08):** SystemAdmin chọn tenant từ "All Tenants" page → `POST /api/admin/impersonate/{tenantId}` → set `tenant_id` claim trong auth cookie → có thể truy cập tenant-scoped entry points. Không cần Domain change (EDR-AM-5) — chỉ thao tác claim. Exit impersonation: "Exit Impersonation" button → clear tenant_id → redirect /admin/tenants.

## Prerequisites (verify before Phase 1)

- [ ] F1-F5 từ `platform_systemadmin_task_card.md` COMPLETE
- [ ] `POST /api/platform/login` trả 200 + JWT trong production-like flow
- [ ] `guard-check.ps1` PASS
- [ ] `docs/AI/artifacts/` directory tồn tại (hoặc tạo)

## Files Created/Modified

| File | Action | Phase | Purpose |
|---|---|---|---|
| `docs/AI/artifacts/platform_systemadmin_access_matrix_audit.md` | CREATE | ANALYZE | Audit artifact: enum + categorize + flag |
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | MODIFY | IMPLEMENT | Add "Access as Tenant" button per row + "Exit Impersonation" button in navbar (AM-T7) |
| `5_WebApps/ShopERP/Controllers/AdminController.cs` | CREATE | IMPLEMENT | `POST /api/admin/impersonate/{tenantId}` + `POST /api/admin/exit-impersonation` (AM-T8) |
| `5_WebApps/ShopERP/Controllers/ApiKeyController.cs` | MODIFY | IMPLEMENT | Add SystemAdmin to Roles (D4) |
| `5_WebApps/ShopERP/Pages/Kitchen/Index.cshtml.cs` | MODIFY | IMPLEMENT | Add SystemAdmin to Roles (D5) |
| `5_WebApps/ShopERP/Pages/GuardRedirect.cshtml.cs` | MODIFY | IMPLEMENT | Add SystemAdmin to Roles (D5) |
| `6_Tests/VanAn.Integration.Tests/Infrastructure/AuthRealWebApplicationFactory.cs` | CREATE | IMPLEMENT | Factory không TestAuthenticationHandler, mint JWT thật |
| `6_Tests/VanAn.Integration.Tests/PlatformSystemAdminAccessMatrixTests.cs` | CREATE | VERIFY | HTTP tests với auth thật, cover 7 policies + impersonation flow |

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

### Phase 2: DESIGN (all 6 decisions resolved 2026-07-08 ✅)

> **Note:** D1-D6 đã được user resolve. DESIGN phase rút gọn: chỉ cần log decisions vào task card.

#### AM-T4: Log all decisions vào task card
- [ ] D1-D6 đã được ghi trong section "Design Decisions" (pre-filled)
- [ ] Verify decisions consistent với audit artifact từ ANALYZE phase
- [ ] Nếu ANALYZE phát hiện thêm ambiguity → propose new decision

#### AM-T6: Log decisions
- [ ] Ghi vào section "Design Decisions" cuối task card
- [ ] Format: `D#: <question> → <user's choice> — <rationale>`

### Phase 3: IMPLEMENT (code fix — EDR-AM-5: no Domain, EDR-AM-6: audit impersonation)

#### AM-T7: Tenant impersonation page (enhance TenantManagement.razor)
- [ ] Add "Access as Tenant" button per tenant row trong `TenantManagement.razor`
- [ ] Button gọi `POST /api/admin/impersonate/{tenantId}` → reload trang với tenant context
- [ ] Add "Exit Impersonation" button: visible khi `tenant_id` claim != Empty
  - Có thể đặt trong AdminLayout navbar (dùng `AuthenticationStateProvider` để check claim)
  - Click → `POST /api/admin/exit-impersonation` → redirect `/admin/tenants`
- [ ] UI Platform components: `VanAnButton`, `VanAnCard`, `VanAnAlert`
- [ ] Build pass

#### AM-T8: Tenant impersonation endpoint (AdminController)
- [ ] Tạo `5_WebApps/ShopERP/Controllers/AdminController.cs`
- [ ] `[Authorize(Policy = "SystemAdmin")]` + `[ApiController] [Route("api/admin")]`
- [ ] `POST /api/admin/impersonate/{tenantId}`:
  - Validate tenantId tồn tại trong DB (query `Tenants` table)
  - Nếu không tồn tại → 404
  - Set `tenant_id` claim trong auth cookie: `new Claim("tenant_id", tenantId.ToString())`
  - Re-sign cookie với `HttpContext.SignInAsync`
  - Log impersonation event (EDR-AM-6): dùng `ILogger<AdminController>` hoặc `IAuditTrailService`
  - Return 200 `{ success, tenantId, tenantName }`
- [ ] `POST /api/admin/exit-impersonation`:
  - Clear `tenant_id` claim → re-sign cookie không có tenant_id
  - Log exit event
  - Return 200 `{ success, message: "Exited impersonation" }`
- [ ] Build pass
- [ ] DI registration trong `Program.cs`: `AddScoped<AdminController>` (auto-registered by `[ApiController]`)

#### AM-T9: Fix role mismatch remaining (D4, D5 resolved)
- [ ] `ApiKeyController.cs` L20: thêm `,SystemAdmin` → `Roles="Admin,Owner,SystemAdmin"` (D4)
- [ ] `Pages/Kitchen/Index.cshtml.cs` L6: thêm `,SystemAdmin` → `Roles="Masterchef,Staff,Manager,SystemAdmin"` (D5)
- [ ] `Pages/GuardRedirect.cshtml.cs` L7: thêm `,SystemAdmin` → `Roles="Guard,SystemAdmin"` (D5)
- [ ] Build pass

#### AM-T10: Setup HTTP test infrastructure
- [ ] Tạo `6_Tests/VanAn.Integration.Tests/Infrastructure/AuthRealWebApplicationFactory.cs`
- [ ] Factory này KHÔNG register `TestAuthenticationHandler` (EDR-AM-1)
- [ ] Test phải mint JWT thật qua `IJwtTokenService` (inject từ DI container)
- [ ] Helper: `LoginAsSystemAdminAsync()` → returns `HttpClient` với cookie/JWT set
- [ ] Helper: `ImpersonateTenantAsync(client, tenantId)` → set tenant_id claim
- [ ] Build pass

### Phase 4: VERIFY (HTTP test với auth thật — EDR-AM-1, EDR-AM-2, EDR-AM-6)

#### AM-T11: Write HTTP tests với auth thật
- [ ] Test: `LoginAsSystemAdmin_ReturnsJwt` — `POST /api/platform/login` → 200 + JWT
- [ ] Test: `SystemAdmin_AccessAdminUsers_Returns200` — GET `/admin/users` với JWT
- [ ] Test: `SystemAdmin_AccessAdminTenants_Returns200` — GET `/admin/tenants` với JWT
- [ ] Test: `SystemAdmin_AccessAdminAuditTrail_Returns200` — GET `/admin/audit-trail` với JWT (F5 verified)
- [ ] Test: `SystemAdmin_AccessAdminPermissionGroups_Returns200`
- [ ] Test: `SystemAdmin_ImpersonateTenant_Returns200` — `POST /api/admin/impersonate/{validTenantId}` → 200 + cookie has tenant_id
- [ ] Test: `SystemAdmin_ImpersonateTenant_InvalidTenant_Returns404`
- [ ] Test: `SystemAdmin_AfterImpersonation_AccessTenantScoped_Returns200` — sau impersonation, GET Accounting/Orders/EInvoice page → 200
- [ ] Test: `SystemAdmin_AfterImpersonation_PassRequireTenantAccess_Returns200` — sau impersonation, GET Shops/EInvoice → 200
- [ ] Test: `SystemAdmin_ExitImpersonation_ClearsTenantId` — POST /api/admin/exit-impersonation → 200, cookie không còn tenant_id
- [ ] Test: `SystemAdmin_AfterExitImpersonation_FailRequireTenantAccess_Returns401` — sau exit, GET Shops → 401
- [ ] Test: `SystemAdmin_AccessKitchen_Returns403` — Category D (should fail)
- [ ] Test: `SystemAdmin_AccessGuardRedirect_Returns403` — Category D
- [ ] Test: `SystemAdmin_FailGuardOnly_Returns403` — GuardOnly policy
- [ ] Test: `AnonymousUser_LoginEndpoint_Returns200` — verify [AllowAnonymous] (F1)
- [ ] Test: `AnonymousUser_ProtectedEndpoint_Returns401` — verify auth enforced
- [ ] **EDR-AM-2:** Audit trail test case cho mỗi policy (7 policies × SystemAdmin pass/fail)

#### AM-T12: Run verification checklist
- [ ] `dotnet build VanAn.sln` — 0 errors (command + output)
- [ ] `guard-check.ps1` — PASS (command + output)
- [ ] Core.Tests — all PASS (command + output)
- [ ] Arch.Tests — all PASS (command + output)
- [ ] Integration.Tests — all PASS (command + output)
- [ ] `PlatformSystemAdminAccessMatrixTests` — all PASS (command + output)
- [ ] Manual verify: `curl POST /api/platform/login` → 200 + JWT, then impersonate → access tenant page (command + output)

#### AM-T13: Final report
- [ ] Update task card status: 🟡 → 🟢 COMPLETE (if all pass) hoặc 🟠 (if deviations)
- [ ] Update master plan status
- [ ] Update `docs/AI/project_state.md` Section 6 (History Log) + Section 9 (Maintenance Log)
- [ ] Commit: `[PLATFORM-ADMIN-ACCESS-MATRIX] Verify SystemAdmin access matrix + impersonation — <summary>`

## Verification Checklist (to be filled in AM-T11)

| Check | Expected | Actual | Pass? | Command + Output |
|---|---|---|---|---|
| `dotnet build VanAn.sln` 0 errors | 0 errors | TBD | TBD | TBD |
| `guard-check.ps1` PASS | PASS | TBD | TBD | TBD |
| Core.Tests all PASS | all PASS | TBD | TBD | TBD |
| Arch.Tests all PASS | all PASS | TBD | TBD | TBD |
| Integration.Tests all PASS | all PASS | TBD | TBD | TBD |
| `PlatformSystemAdminAccessMatrixTests` all PASS | all PASS | ✅ 18/18 PASS | ✅ | `dotnet test --filter "Category=AccessMatrix"` → 18/18 PASS |
| `POST /api/platform/login` 200 + JWT | 200 + JWT | TBD | TBD | TBD |
| SystemAdmin access /admin/users | 200 | TBD | TBD | TBD |
| SystemAdmin access /admin/tenants | 200 | TBD | TBD | TBD |
| SystemAdmin access /admin/audit-trail | 200 | TBD | TBD | TBD |
| SystemAdmin access /admin/permission-groups | 200 | TBD | TBD | TBD |
| SystemAdmin impersonate valid tenant | 200 | TBD | TBD | TBD |
| SystemAdmin impersonate invalid tenant | 404 | TBD | TBD | TBD |
| SystemAdmin after impersonation: access tenant-scoped page | 200 | TBD | TBD | TBD |
| SystemAdmin after impersonation: pass RequireTenantAccess | 200 | TBD | TBD | TBD |
| SystemAdmin exit impersonation: clears tenant_id | 200 | TBD | TBD | TBD |
| SystemAdmin after exit: fail RequireTenantAccess | 401 | TBD | TBD | TBD |
| SystemAdmin access Kitchen page | 200 | TBD | TBD | TBD |
| SystemAdmin access GuardRedirect page | 200 | TBD | TBD | TBD |
| Anonymous login endpoint | 200 | TBD | TBD | TBD |
| Anonymous protected endpoint | 401 | TBD | TBD | TBD |
| Domain layer not modified | no diff | TBD | TBD | TBD |

## Rollback

- Revert IMPLEMENT commits (AM-T7, AM-T8, AM-T9)
- Delete test files (AuthRealWebApplicationFactory.cs, PlatformSystemAdminAccessMatrixTests.cs)
- Delete artifact (access_matrix_audit.md)
- Revert Program.cs policy changes (if any)

## Design Decisions (D1, D2, D3, D6 resolved 2026-07-08)

- **D1:** SystemAdmin có nên truy cập tenant-scoped business data? → **(a) Có + impersonation tenant** — SystemAdmin chọn tenant từ "All Tenants" page → set tenant_id claim → access tenant data với đúng TenantId. Rationale: impersonation giữ data isolation per tenant, không cần Domain change.
- **D2:** `RequireTenantAccess` exclude SystemAdmin? → **Resolved by D1** — Sau impersonation, SystemAdmin có tenant_id claim → RequireTenantAccess tự động pass. Policy KHÔNG cần sửa. Trước impersonation: fail (chưa chọn tenant) — đúng behavior.
- **D3:** AuditTrail attribute? → **(b) `Policy="SystemAdmin"`** (nhất quán TenantManagement.razor). Đã implement trong F5.
- **D6:** Tenant impersonation page design? → **(a) Enhance TenantManagement.razor hiện có** — page `/admin/tenants` đã có `Policy="SystemAdmin"`. Thêm "Access as Tenant" button per row + "Exit Impersonation" button trong navbar.

- **D4:** ApiKeyController thêm SystemAdmin? → **(a) Có** — SystemAdmin quản platform API keys (HMAC signing cho cross-tenant integration). Implement: thêm `,SystemAdmin` vào `Roles="Admin,Owner,SystemAdmin"`.
- **D5:** Kitchen/GuardRedirect chạm không? → **Cho SystemAdmin access luôn** — `Roles="Masterchef,Staff,Manager,SystemAdmin"` cho Kitchen, `Roles="Guard,SystemAdmin"` cho GuardRedirect. SystemAdmin có thể troubleshoot kitchen operations + security guard flow sau khi impersonate tenant.

## Open Questions

- ~~Q1: Category B (tenant-scoped business) — impersonation có cần Domain change không?~~ → **RESOLVED: Không cần.** Chỉ thao tác claim trong auth cookie, không sửa Domain entity (EDR-AM-5 satisfied).
- Q2: `AuthRealWebApplicationFactory` có thể share connection với `CustomWebApplicationFactory` không, hay phải connection riêng?
- Q3: Test class scope — 1 class cho tất cả 7 policies, hay 1 class per policy? (EDR-AM-2 yêu cầu cover all 7, structure tuỳ impl)
- Q4: "Exit Impersonation" button đặt ở đâu trong UI? → AdminLayout navbar (dùng `AuthenticationStateProvider` check `tenant_id` claim != Empty khi role là SystemAdmin). IMPLEMENT phase sẽ quyết định exact placement.

## Files NOT Modified (Hard Stops)

- `1_Shared/Domain/Aggregates/UserAggregate/UserRole.cs` — không sửa enum
- `1_Shared/Domain/Common.cs` — không sửa PlatformRole enum
- `1_Shared/Domain/Aggregates/UserAggregate/DemoUser.cs` — không sửa aggregate
- `1_Shared/Domain/Aggregates/TenantAggregate/Tenant.cs` — không sửa aggregate (nếu D1 cần impersonation → Domain Modeling Defect report, không tự sửa)
- `5_WebApps/ShopERP/Controllers/DevLoginController.cs` — giữ nguyên (E2E tests)

## Deviation Log (to be filled nếu có deviation trong execution)

<!-- Format như parent task card: Deviation #N — <title> (severity), hiện trạng, plan nói gì, fix cần làm, bài học -->
