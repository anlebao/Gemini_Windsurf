# TASK CARD: UI - WAVE 6 - User Management & Permission Group Blazor Pages

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo `UserManagement.razor` và `PermissionGroupManagement.razor` Blazor Server pages trong `5_WebApps/ShopERP/Components/Pages/Admin/` — giao diện Owner quản lý users (tạo, assign role, deactivate/reactivate) và PermissionGroups (tạo, add/remove roles) trong tenant của họ.
- **Nghiệp vụ áp dụng:** Owner của Tenant thực hiện: tạo user mới với role, assign user vào PermissionGroup, deactivate user vi phạm, reactivate user quay lại, quản lý nhóm quyền (PermissionGroup) với danh sách roles được bundle. StoreKeeper không có quyền truy cập các trang này (403).

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `5_WebApps/ShopERP/Components/Pages/Admin/UserManagement.razor` — TẠO MỚI
  - `5_WebApps/ShopERP/Components/Pages/Admin/UserManagement.razor.cs` — TẠO MỚI
  - `5_WebApps/ShopERP/Components/Pages/Admin/PermissionGroupManagement.razor` — TẠO MỚI
  - `5_WebApps/ShopERP/Components/Pages/Admin/PermissionGroupManagement.razor.cs` — TẠO MỚI
  - `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — SỬA: thêm nav links với AuthorizeView OwnerOnly
  - `docs/UI_Platform_Implementation_Guide.md` — ĐỌC để biết component API
  - `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` — ĐỌC để lấy pattern (từ W5-T9)
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG inject domain services (`IUserManagementService`, `IRoleAssignmentService`, etc.) trực tiếp vào Blazor pages — chỉ gọi qua `HttpClient` → Gateway API
  - KHÔNG inject `IVanAnDbContext` vào UI layer
  - KHÔNG tạo custom HTML/CSS khi UI Platform component đã có — Hard Stop Rule
  - KHÔNG hiển thị raw password sau khi tạo user — chỉ confirm message
  - KHÔNG thêm business logic vào code-behind — chỉ UI state management và API calls

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **UI Platform Components Only:** `VanAnButton`, `VanAnCard`, `VanATable`, `VanAForm`, `VanAnInput`, `VanAnAlert`, `VanAModal` — KHÔNG dùng native HTML elements khi component đã có.
- [ ] **Auth Attribute:** `@attribute [Authorize(Policy = "OwnerOnly")]` trên cả 2 `.razor` files.
- [ ] **Password Field:** `VanAnInput` với `type="password"` cho password input — password masking bắt buộc.
- [ ] **Confirm Dialog:** Deactivate user actions PHẢI có confirm dialog (VanAModal) — KHÔNG deactivate ngay khi click button.
- [ ] **Role Dropdown:** UserRole dropdown KHÔNG bao gồm `None` value — chỉ hiển thị `Owner, StoreKeeper, Guard, Staff, Masterchef`.
- [ ] **Error Handling:** 422 từ API (last owner guard, domain exceptions) → hiển thị error message qua `VanAnAlert` — KHÔNG silent fail, KHÔNG show raw exception.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** `UserManagement.razor` render đúng với OwnerOnly role — table hiển thị users (Username, DisplayName, Role, Status).
- [ ] **SC-2:** StoreKeeper role truy cập `/admin/users` → 403 page (không thấy content).
- [ ] **SC-3:** Create user form: fields required — Username, Password, DisplayName, Role. Submit với Username trống → validation error, KHÔNG gọi API.
- [ ] **SC-4:** Create user form submit hợp lệ → gọi `POST /api/users` via HttpClient → table refresh.
- [ ] **SC-5:** Role dropdown hiển thị 5 values: Owner, StoreKeeper, Guard, Staff, Masterchef (không có None).
- [ ] **SC-6:** Password field có type="password" — text bị mask, không hiển thị plain text.
- [ ] **SC-7:** Deactivate user button → VanAModal confirm dialog → Cancel → không gọi API.
- [ ] **SC-8:** Deactivate user → Confirm → `POST /api/users/{id}/deactivate` → nếu 422 → VanAnAlert hiển thị error message.
- [ ] **SC-9:** `PermissionGroupManagement.razor`: table groups (Name, Roles list). Form tạo group mới. Add/Remove role checkboxes (checkboxes cho tất cả UserRole ngoại trừ None).
- [ ] **SC-10:** `dotnet build VanAn.sln` → 0 errors. `guard-check.ps1` PASS.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave6-user-rbac-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Verify tất cả UI dùng Platform components, password masking
- `accounting-ui-implementation` — Blazor Server patterns, code-behind, form validation
- `domain-integrity-validation` — Verify no direct service injection, API-only calls

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: Governance UI Platform Hard Stop: `ALWAYS use UI Platform components. NEVER bypass.`
  - Fact 2: Governance: `5_WebApps/KhachLink MUST NOT inject IVanAnDbContext` — áp dụng cho tất cả UI pages
  - Fact 3: Policy `OwnerOnly` đã tồn tại tại `ShopERP/Program.cs`
  - Fact 4: `enum UserRole`: `None, Owner, StoreKeeper, Guard, Staff, Masterchef` — UI dropdown exclude `None`
  - Fact 5: UI Platform components: `VanAnButton, VanAnCard, VanAnAlert, VanAnInput, VanAModal, VanASpinner, VanAForm, VanATable` (Layer 1 + Layer 2)
  - Fact 6: Endpoints từ W6-T7: `POST /api/users`, `GET /api/users`, `PATCH /api/users/{id}`, `POST /api/users/{id}/deactivate`, `POST /api/users/{id}/roles`, `DELETE /api/users/{id}/roles/{role}`
  - Fact 7: TenantManagement.razor (W5-T9) pattern đã có — reuse pattern cho HttpClient calls, error handling, confirm dialog
- **Assumptions:**
  - Named HttpClient cho Gateway API đã registered (từ W5-T9 hoặc existing code)
  - `Admin/` folder trong Pages đã tồn tại (từ W5-T9 TenantManagement)
- **Open Questions:**
  - Q1: Assign user vào PermissionGroup — multiselect pattern trong VanAForm: có sẵn multiselect component không? (nếu không → dùng checkboxes)
  - Q2: Reactivate user button — hiển thị chỉ khi user IsActive=false, hoặc hiển thị cả 2 (Deactivate/Reactivate toggle)?
- **Recommended Action:** IMPLEMENT — sau khi W6-T7 (Controllers) hoàn thành và endpoints sẵn sàng

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `UserManagement.razor` (mới) | Không có downstream impact | N/A |
| `UserManagement.razor.cs` (mới) | Không có downstream impact | N/A |
| `PermissionGroupManagement.razor` (mới) | Không có downstream impact | N/A |
| `PermissionGroupManagement.razor.cs` (mới) | Không có downstream impact | N/A |
| `NavMenu.razor` | Thêm 2 nav links — existing nav unaffected | AuthorizeView OwnerOnly wraps both links |

## 9. TDD & E2E TESTING STRATEGY
- **Playwright E2E Tests (sau IMPLEMENT, Gate 3):**
  - Test: Owner login → `/admin/users` → page loads (200)
  - Test: Create user form → fill all fields → submit → table refreshes
  - Test: Password field → type characters → characters masked (not visible)
  - Test: Deactivate → modal appears → Cancel → no 422 call
  - Test: StoreKeeper login → `/admin/users` → 403
  - Test: PermissionGroup page → create group → add role checkboxes
- **Blazor Unit Tests (bUnit):**
  - Role dropdown: renders 5 items (no None)
  - Validation: empty Username → error message shown
  - Status display: inactive user shows "Inactive" badge in red
- **Test boundary:**
  - Unit tests: bUnit component tests nếu project setup cho phép
  - Integration tests: N/A
  - E2E tests: `6_Testing/e2e-tests/` — Playwright (enable only after IMPLEMENT complete)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này cần 2 sessions: Session 1 `UserManagement.razor` đầy đủ. Session 2 `PermissionGroupManagement.razor` + nav + polish.

### Micro-phase breakdown cho W6-T10

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc `TenantManagement.razor` (W5-T9) → lấy HttpClient pattern, error handling pattern, VanAModal pattern. Đọc `UI_Platform_Implementation_Guide.md` → VanAForm với password input, dropdown binding. Xác định UserDto response shape từ API | Tạo `UserManagement.razor`: `@attribute [Authorize(Policy="OwnerOnly")]`, VanATable users, Create form (VanAForm với username/password/displayName/role dropdown). Tạo `.razor.cs`: OnInitializedAsync (GET /api/users), CreateUser(), DeactivateUser() với confirm. Verify render |
| **S2** | Xác định PermissionGroup form: checkboxes vs multiselect cho roles. Xác định Reactivate button display logic. Review nav link placement | Tạo `PermissionGroupManagement.razor`: VanATable groups, Create form, role checkboxes (UserRole enum minus None). `.razor.cs`: API calls. Thêm 2 nav links vào `NavMenu.razor` với AuthorizeView OwnerOnly. Run `guard-check.ps1` |

### Rules
- Code-behind: `private bool _isCreating`, `private string? _errorMessage`, `private List<UserDto> _users = new()` — UI state only
- Form submit: `async void` handler hoặc `async Task` — KHÔNG block UI thread
- Password: `<VanAnInput Type="password" @bind-Value="_createForm.Password" />` — type must be "password"
- Role dropdown: `Enum.GetValues<UserRole>().Where(r => r != UserRole.None)` for dropdown options

## 11. ESTIMATED EFFORT
- 2 sessions (90-120 phút total)
- **Phụ thuộc:** W6-T7 (UserController + PermissionGroupController endpoints live), W5-T9 (pattern established)
- **BLOCKER:** Nếu VanAModal không có confirm dialog variant → phải implement confirm modal in UI.Platform project first (không bypass với window.confirm JavaScript) — có thể tốn thêm 1 session
