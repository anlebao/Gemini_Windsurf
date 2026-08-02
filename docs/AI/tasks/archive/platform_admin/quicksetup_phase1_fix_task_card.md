# TASK CARD — Phase 1: Fix QuickSetup Orphan Page

> **Master plan:** `docs/AI/tasks/quicksetup_product_management_master_plan.md` (Section 2)
> **Branch:** `feature/quicksetup-fix-phase1`
> **Priority:** 1 (High)
> **Mode:** IMPLEMENT (sau khi plan đã chốt)
> **Prerequisite fixes:** None (Phase 1 độc lập)

---

## 0. CONTEXT & DECISIONS (locked)

### Architecture facts (verified 2026-07-14)
- `Pages/QuickSetup.razor` hiện tại: <ref_file file="C:/VibeCoding/Gemini_Windsurf/5_WebApps/ShopERP/Pages/QuickSetup.razor" />
  - `@page "/quick-setup"`, `@inject HttpClient Http` (DI không register → crash)
  - Không `@rendermode`, không `@attribute [Authorize]`
  - `ProcessSetupAsync()` line 392: `var shopId = Guid.NewGuid(); // In real implementation, this would come from authentication` → **HARDCODED random Guid** → tạo ra tenant ma
  - `TemplateType = "cafe"` (string) gửi lên Gateway
- `OnboardingController.QuickSetup` ở **GATEWAY** (không phải ShopERP): <ref_file file="C:/VibeCoding/Gemini_Windsurf/2_Gateway/Controllers/OnboardingController.cs" />
  - Line 114: `if (!Guid.TryParse(request.TemplateType, out Guid templateId))` → **yêu cầu TemplateType là Guid string**. Gửi `"cafe"` → **luôn BadRequest**.
- `GatewayClient` named HttpClient đã register trong ShopERP Program.cs:342
- `ITenantProvider` tồn tại: `5_WebApps/ShopERP/Services/TenantProvider.cs`
- `TenantManagement.razor` đã có list tenants (`_tenants`), dùng `ITenantManagementService`, có `@foreach (var t in _tenants)` line 91

### User decisions (locked 2026-07-14)
- **G7 — Tenant selection UX:** SystemAdmin vào trang `/admin/tenants` (TenantManagement.razor) → bấm vào 1 dòng tenant (button "Khởi tạo nhanh" trên mỗi row) → redirect `/quick-setup?tenantId={t.Id}` → QuickSetup page đọc `tenantId` từ query string.
- **KHÔNG** dùng `ITenantProvider.TenantId` (vì SystemAdmin không thuộc tenant đích).

---

## 1. TASKS

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P1-T1 | Thêm `@rendermode InteractiveServer` + `@attribute [Authorize(Policy = "SystemAdminOnly")]` vào đầu `QuickSetup.razor` | `5_WebApps/ShopERP/Pages/QuickSetup.razor` | ⬜ |
| 2 | P1-T2 | Đổi `@inject HttpClient Http` → `@inject IHttpClientFactory HttpClientFactory`. Tạo property `private HttpClient Http => HttpClientFactory.CreateClient("GatewayClient");` | same | ⬜ |
| 3 | P1-T3 | Inject `NavigationManager` (đã có) → parse `tenantId` từ query string trong `OnInitializedAsync`: `var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri); Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query).TryGetValue("tenantId", out var tid); _selectedTenantId = Guid.Parse(tid!)`. Nếu thiếu/invalid → hiển thị error "Thiếu tenantId" + link về `/admin/tenants`. | same | ⬜ |
| 4 | P1-T4 | **FIX BUG TemplateType:** Đổi `TemplateInfo.Type` từ `string "cafe"` → lưu `Guid TemplateId`. Load templates qua `GET /api/v1/onboarding/templates` (existing endpoint) → mỗi template có `Id` (Guid). Khi gửi `QuickSetupRequest.TemplateType = selectedTemplate.TemplateId.ToString()` (Guid string, parse được bởi `Guid.TryParse`). | same | ⬜ |
| 5 | P1-T5 | Fix `ProcessSetupAsync()`: thay `var shopId = Guid.NewGuid()` → `var shopId = _selectedTenantId`. Gửi `POST /api/v1/onboarding/shops/{shopId}/quick-setup` qua `GatewayClient`. | same | ⬜ |
| 6 | P1-T6 | Hiển thị tenant info (tên + ID) ở đầu wizard để SystemAdmin xác nhận đang setup cho tenant nào. Có thể fetch `GET /api/v1/tenants/{tenantId}` qua GatewayClient hoặc inject `ITenantManagementService` (in-process CoreHub). | same | ⬜ |
| 7 | P1-T7 | TenantManagement.razor: thêm button "Khởi tạo nhanh" trong mỗi row của `_tenants` foreach (line 91). `@onclick="() => NavigateToQuickSetup(t.Id)"` → `NavigationManager.NavigateTo($"/quick-setup?tenantId={t.Id}")`. | `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | ⬜ |
| 8 | P1-T8 | Sitemap.razor: thêm link "Khởi tạo nhanh" vào card "Quản Lý Tenant" → `/admin/tenants` (redirect qua TenantManagement, không link thẳng `/quick-setup` vì cần chọn tenant). | `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | ⬜ |
| 9 | P1-T9 | Verify build: `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` pass. | Solution-wide | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] `QuickSetup.razor` có `@rendermode InteractiveServer` + `@attribute [Authorize(Policy = "SystemAdminOnly")]`
- [ ] `@inject IHttpClientFactory` thay cho `HttpClient` → không crash DI
- [ ] `tenantId` lấy từ query string (`?tenantId=...`) — **KHÔNG** generate random Guid, **KHÔNG** dùng `ITenantProvider`
- [ ] `TemplateType` gửi lên Gateway là **Guid string** (parse được bởi `Guid.TryParse`)
- [ ] TenantManagement có button "Khởi tạo nhanh" mỗi row → redirect `/quick-setup?tenantId={id}`
- [ ] Sitemap card Tenant có link → `/admin/tenants`
- [ ] QuickSetup page hiển thị tên tenant đang setup
- [ ] Build: 0 errors

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Hardcode `shopId = Guid.NewGuid()` (sinh tenant ma)
- ❌ Gửi `TemplateType = "cafe"` (string, Gateway reject)
- ❌ Dùng `ITenantProvider.TenantId` cho SystemAdmin context (SystemAdmin không có tenant)
- ❌ Bypass `[Authorize]` để "test cho nhanh"
- ❌ Custom HTML/CSS cho wizard — giữ existing CSS trong QuickSetup.razor (wizard đã có style riêng, không thuộc UI Platform scope)

---

## 4. ROLLBACK PLAN

Nếu Phase 1 fail sau 3 rounds fix:
1. Revert `QuickSetup.razor` về commit trước phase
2. Revert TenantManagement.razor + Sitemap.razor
3. Report: bug cụ thể, evidence, recommend next step
4. **KHÔNG** commit code lỗi lên main

---

## 5. VERIFICATION CHECKLIST (sau khi implement)

```powershell
# 1. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 3. Manual smoke test (nếu có local env)
# - Login as SystemAdmin
# - Vào /admin/tenants → bấm "Khởi tạo nhanh" trên 1 tenant
# - Verify redirect /quick-setup?tenantId={id}
# - Verify page render, không crash DI
# - Verify template list load (GET /api/v1/onboarding/templates qua GatewayClient)
# - Chọn template → nhập info → Complete Setup
# - Verify POST /api/v1/onboarding/shops/{tenantId}/quick-setup trả 200 (không 400 Bad Request)
```
