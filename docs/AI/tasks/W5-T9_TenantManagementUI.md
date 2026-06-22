# TASK CARD: UI - WAVE 5 - Tenant Management Blazor Page

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo `TenantManagement.razor` Blazor Server page trong `5_WebApps/ShopERP/Components/Pages/Admin/` — giao diện quản lý Tenants cho SystemAdmin, bao gồm danh sách tenants, tạo mới, suspend, deactivate với confirm dialogs.
- **Nghiệp vụ áp dụng:** SystemAdmin của VanAn platform quản lý vòng đời các đơn vị kinh doanh (Tenants). Chức năng: xem danh sách tenants với status badges màu sắc, tạo tenant mới (form với validation), và thực hiện lifecycle actions (Suspend/Deactivate) với confirm dialog để tránh nhầm lẫn.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` — TẠO MỚI
  - `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor.cs` — TẠO MỚI (code-behind)
  - `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — SỬA: thêm nav link với AuthorizeView
  - `docs/UI_Platform_Implementation_Guide.md` — ĐỌC để biết component API (VanAnButton, VanATable, etc.)
  - `5_WebApps/ShopERP/Components/Pages/Admin/` — ĐỌC existing admin pages để biết pattern
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG inject `ITenantManagementService` trực tiếp vào Blazor page — chỉ gọi qua `HttpClient` → Gateway API
  - KHÔNG inject `IVanAnDbContext` hoặc bất kỳ DbContext vào UI layer
  - KHÔNG tạo custom HTML/CSS khi UI Platform component đã có — Hard Stop Rule
  - KHÔNG dùng hardcoded values thay vì design tokens
  - KHÔNG thêm business logic vào code-behind — chỉ UI state management và API calls

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **UI Platform Components Only:** Phải dùng `VanAnButton`, `VanAnCard`, `VanATable`, `VanAForm`, `VanAnAlert` — KHÔNG tạo native `<button>`, `<table>`, `<form>` HTML trực tiếp khi component đã có.
- [ ] **Auth Attribute:** `@attribute [Authorize(Policy = "SystemAdmin")]` ở đầu file `.razor` — KHÔNG chỉ hide UI, phải có server-side auth.
- [ ] **HttpClient via Gateway:** API calls phải qua named `HttpClient` được cấu hình trỏ đến Gateway URL — KHÔNG gọi ShopERP directly.
- [ ] **Confirm Dialog:** Suspend và Deactivate actions phải có confirm dialog (dùng `VanAModal` hoặc `VanAnAlert` confirm variant) trước khi gọi API.
- [ ] **Error Handling:** API call thất bại (422, 4xx, 5xx) → hiển thị error message trong `VanAnAlert` — KHÔNG silent fail.

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC-1:** Page render đúng khi truy cập với SystemAdmin role — table hiển thị danh sách tenants.
- [ ] **SC-2:** Staff/StoreKeeper role truy cập `/admin/tenants` → bị redirect hoặc 403 (không thấy page content).
- [ ] **SC-3:** Form tạo tenant mới: submit với tất cả required fields → gọi `POST /api/tenants` qua HttpClient.
- [ ] **SC-4:** Form tạo tenant: submit với Name trống → validation error message hiển thị, KHÔNG gọi API.
- [ ] **SC-5:** Status badge: Active=green, Suspended=yellow/orange, Pending=blue, Inactive=red, Terminated=gray.
- [ ] **SC-6:** Deactivate button click → confirm dialog xuất hiện → cancel → không gọi API.
- [ ] **SC-7:** Deactivate button click → confirm → gọi `POST /api/tenants/{id}/deactivate` → success toast.
- [ ] **SC-8:** API trả về 422 (InvalidOperationException) → hiển thị error message trong VanAnAlert.
- [ ] **SC-9:** Nav menu chỉ hiển thị "Tenant Management" link với `<AuthorizeView Policy="SystemAdmin">`.
- [ ] **SC-10:** `dotnet build VanAn.sln` → 0 errors. `guard-check.ps1` PASS.

**Implementation Date:** 2026-06-23
**Branch:** feature/wave5-tenant-mgmt

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Verify tất cả UI dùng Platform components, không bypass
- `accounting-ui-implementation` — Blazor Server page pattern, code-behind separation
- `domain-integrity-validation` — Verify no direct service injection vào UI layer

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Verified Facts:**
  - Fact 1: Governance UI Platform Hard Stop: `ALWAYS use UI Platform components. NEVER bypass.`
  - Fact 2: Governance: `5_WebApps/KhachLink (Client UI) MUST NOT inject IVanAnDbContext or query local databases. Use HTTP via Gateway only` — áp dụng tương tự cho ShopERP UI components
  - Fact 3: Policies đã có trong `ShopERP/Program.cs`: `OwnerOnly`, `StoreManagement`, `GuardOnly`, `StaffOrAbove` — `SystemAdmin` sẽ được thêm ở W5-T6
  - Fact 4: UI Platform components: `VanAnButton, VanAnCard, VanAnAlert, VanAnInput, VanAModal, VanASpinner, VanAForm, VanATable, VanAChart, VanALayout, VanANavigation`
  - Fact 5: Blazor Server (không phải WebAssembly) — data fetching qua HttpClient, không qua SignalR trực tiếp
  - Fact 6: `TenantStatus` enum: `Pending, Active, Suspended, Inactive, Terminated` (W5-T2)
  - Fact 7: Endpoint list từ W5-T6: `POST /api/tenants`, `GET /api/tenants`, `GET /api/tenants/{id}`, `PATCH /api/tenants/{id}/profile`, `POST /api/tenants/{id}/suspend`, `POST /api/tenants/{id}/deactivate`
- **Assumptions:**
  - `Admin/` folder trong `Pages/` đã tồn tại (có thể từ existing admin pages)
  - Named HttpClient cho Gateway đã được registered — hoặc cần thêm trong `Program.cs`
- **Open Questions:**
  - Q1: VanAModal component API là gì? (confirm dialog pattern) — cần đọc `UI_Platform_Implementation_Guide.md`
  - Q2: Blazor page routing convention: `@page "/admin/tenants"` hay khác? (Xem existing admin pages)
- **Recommended Action:** IMPLEMENT — đọc UI guide + existing admin pages trước → implement page

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `TenantManagement.razor` (mới) | Không có downstream impact | N/A |
| `TenantManagement.razor.cs` (mới) | Không có downstream impact | N/A |
| `NavMenu.razor` | Thêm nav item — existing nav structure unchanged | Dùng AuthorizeView để chỉ SystemAdmin thấy link |
| HttpClient dependency | Nếu Gateway HttpClient chưa register → runtime error | Verify HttpClient named "Gateway" hoặc tương đương trong Program.cs |

## 9. TDD & E2E TESTING STRATEGY
- **Playwright E2E Tests (khi IMPLEMENT complete):**
  - Test: SystemAdmin login → navigate `/admin/tenants` → page loads (status 200)
  - Test: Create tenant form → fill Name + BusinessType → submit → table refreshes với tenant mới
  - Test: Click Deactivate → modal appears → click Cancel → no API call made
  - Test: Staff role → navigate `/admin/tenants` → 403 page shown
- **Blazor Component Tests:**
  - Status badge color mapping: verify Active → green CSS class
  - Form validation: empty Name → validation message shown
- **Test boundary:**
  - Unit tests: N/A (UI logic minimal in code-behind)
  - Integration tests: Blazor test với `bUnit` nếu có
  - E2E tests: `6_Testing/e2e-tests/` — Playwright (enable after IMPLEMENT complete, Gate 3)

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

Task này cần 2 sessions: Session 1 build static structure (table + nav). Session 2 implement form, actions, error handling.

### Micro-phase breakdown cho W5-T9

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | Đọc `UI_Platform_Implementation_Guide.md` → lấy VanATable, VanAForm, VanAModal API. Đọc existing admin page → lấy routing pattern, HttpClient usage pattern. Xác định DTO model cho list (TenantListItem) | Tạo `TenantManagement.razor` với `@attribute [Authorize]`, routing, VanATable hiển thị tenant list (hardcode columns: Id, Name, BusinessType, Status, CreatedAt). Tạo `.razor.cs` với `OnInitializedAsync` gọi GET /api/tenants. Thêm nav link với AuthorizeView. Verify render |
| **S2** | Review VanAForm validation pattern. Xác định confirm dialog pattern (VanAModal hay JavaScript confirm). Design error state management (string? errorMessage) | Implement Create form (VanAForm với Name, BusinessType dropdown, OwnerEmail). Implement Suspend/Deactivate buttons với VanAModal confirm. Implement error display (VanAnAlert). Handle 422 response → show domain error. Verify E2E basic flows |

### Rules
- Code-behind (`.razor.cs`) chỉ chứa: API call methods, UI state (loading, error, selected item), event handlers
- Blazor `@inject` chỉ cho: `HttpClient`, `NavigationManager`, UI services — KHÔNG cho domain services
- Status badge: dùng UI Platform design token colors, không hardcode hex values

## 11. ESTIMATED EFFORT
- 2 sessions (90-120 phút total)
- **Phụ thuộc:** W5-T6 (TenantController + API endpoints phải live), W5-T1 (SystemAdmin policy defined)
- **BLOCKER:** Nếu UI Platform không có `VanAModal` confirm dialog → cần tạo component mới trong UI.Platform project (KHÔNG bypass với custom HTML) — có thể tốn thêm 1 session
