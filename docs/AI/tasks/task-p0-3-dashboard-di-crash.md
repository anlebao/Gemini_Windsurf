# TASK CARD: [P0-3] - Fix VanAnDashboard.razor DI Crash

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix runtime `InvalidOperationException` khi navigate đến `/dashboard` do `@inject IDashboardService` bị remove nhưng vẫn còn reference trong Razor file.
- **Nghiệp vụ áp dụng:** ShopERP dashboard — production crash risk.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/Fix_Errors.md`
- **Execution Mode:** FIX_ONLY

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md`
  - `5_WebApps/ShopERP/Components/Pages/VanAnDashboard.razor`
  - `5_WebApps/ShopERP/Services/DashboardService.cs` (nếu tồn tại)
  - `5_WebApps/ShopERP/Program.cs` (DI registration)

## 4. TECHNICAL CONSTRAINTS
- [ ] **UI Compliance:** Không thay đổi UI layout, chỉ fix DI.
- [ ] **No new interfaces:** Nếu `IDashboardService` không tồn tại, inject concrete class hoặc remove reference.

## 5. SUCCESS CRITERIA
- [ ] **SC1:** Navigate to `/dashboard` không throw exception.
- [ ] **SC2:** `dotnet build VanAn.sln --configuration Release` → 0 errors.
- [ ] **SC3:** `guard-check.ps1` → PASS.

## 6. ROOT CAUSE (from project_state.md)
`@inject IDashboardService` trong `VanAnDashboard.razor` nhưng service bị remove khỏi DI container hoặc interface không tồn tại.

## 7. FIX OPTIONS
- **Option 1:** Re-register `IDashboardService` trong `Program.cs` nếu service còn tồn tại.
- **Option 2:** Remove `@inject IDashboardService` và thay bằng inline data fetch nếu service đã bị xóa.
- **Option 3:** Inject concrete `DashboardService` nếu không cần interface.
