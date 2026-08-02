# TASK CARD — Phase 4: Product Management UI

> **Master plan:** `docs/AI/tasks/quicksetup_product_management_master_plan.md` (Section 5)
> **Branch:** `feature/product-mgmt-phase4-ui`
> **Priority:** 1 (High)
> **Mode:** IMPLEMENT
> **Prerequisite:** Phase 3 merged + Prerequisite Fixes (VanAnButton disabled bug + VanAnDataGrid render order bug — master plan Section 10)

---

## 0. CONTEXT & DECISIONS (locked)

### UI facts (verified 2026-07-14)
- UI Platform components tồn tại:
  - <ref_file file="C:/VibeCoding/Gemini_Windsurf/UI.Platform/Components/Data/VanAnDataGrid.razor" />
  - <ref_file file="C:/VibeCoding/Gemini_Windsurf/UI.Platform/Components/VanAForm.razor" />
  - `VanAnButton`, `VanAnCard`, `VanAnAlert`, `VanAnInput`, `VanAnModal` (UI.Platform/Components/Atomic/)
- `AccountingLayout`: <ref_file file="C:/VibeCoding/Gemini_Windsurf/5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor" /> (có sidebar, inject `ITenantProvider`)
- Pattern reference (OwnerOnly + InteractiveServer + AccountingLayout):
  - `Components/Pages/Accounting/ExpenseEntry.razor` line 4: `@attribute [Authorize(Policy = "OwnerOnly")]`
- `CurrencyHelper.FormatVND()` chỉ tồn tại ở KhachLink: <ref_file file="C:/VibeCoding/Gemini_Windsurf/5_WebApps/KhachLink/Components/Shared/CurrencyHelper.cs" /> — **KHÔNG** ở ShopERP
- `GatewayClient` named HttpClient đã register (Program.cs:342)

### User decisions (locked 2026-07-14)
- **G4 — CurrencyHelper:** Tạo **shared helper** mới ở `1_Shared/` (hoặc `UI.Platform/`) — KHÔNG duplicate, KHÔNG dùng `MoneyFormatter` (CoreHub, khác API). Move `CurrencyHelper` lên shared layer để cả ShopERP + KhachLink dùng chung.
- **G7 — Tenant context:** Owner/Admin vào `/products` → `ITenantProvider.TenantId` tự động có tenant context (Owner thuộc 1 tenant). **KHÔNG** cần UI chọn tenant (khác SystemAdmin QuickSetup).

### Prerequisite bugs (master plan Section 10 — phải fix TRƯỚC Phase 4)
1. `VanAnButton.razor`: `disabled="False"` → browser interpret as disabled. Fix: `disabled="@(State.IsDisabled ? true : null)"`.
2. `VanAnDataGrid.razor`: empty table (columns register after table render). Fix: move `<CascadingValue>@Columns</CascadingValue>` lên trước `<table>` + `StateHasChanged()` trong `RegisterColumn`.

---

## 1. TASKS

### 1A. Prerequisite Fixes (UI Platform bugs)

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P4-P1 | Fix `VanAnButton.razor` disabled attribute: `disabled="@(State.IsDisabled ? true : null)"` (null = attribute không render). | `UI.Platform/Components/Atomic/VanAnButton.razor` | ⬜ |
| 2 | P4-P2 | Fix `VanAnDataGrid.razor` render order: di chuyển `<CascadingValue>` wrap `<table>` (columns register trước khi table render) + `StateHasChanged()` trong `RegisterColumn` method. | `UI.Platform/Components/Data/VanAnDataGrid.razor` | ⬜ |
| 3 | P4-P3 | Verify build sau 2 fixes: 0 errors. | Solution-wide | ⬜ |

### 1B. Shared CurrencyHelper (G4)

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 4 | P4-S1 | Tạo `1_Shared/Helpers/CurrencyHelper.cs` (NEW) — copy logic từ `KhachLink/Components/Shared/CurrencyHelper.cs`. Namespace `VanAn.Shared.Helpers`. Method `FormatVND(decimal)` + `FormatVND(int)`. | `1_Shared/Helpers/CurrencyHelper.cs` (NEW) | ⬜ |
| 5 | P4-S2 | Update `KhachLink/Components/Shared/CurrencyHelper.cs` → delegate to shared helper (keep public API for backward compat): `return VanAn.Shared.Helpers.CurrencyHelper.FormatVND(amount);`. **KHÔNG xóa** file cũ (tránh break KhachLink references). | `5_WebApps/KhachLink/Components/Shared/CurrencyHelper.cs` | ⬜ |
| 6 | P4-S3 | Verify build: 0 errors. | Solution-wide | ⬜ |

### 1C. Product Management Page

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 7 | P4-U1 | Tạo `Components/Pages/Products/ProductManagement.razor` (NEW). Header: `@page "/products"`, `@rendermode InteractiveServer`, `@attribute [Authorize(Policy = "OwnerOnly")]`, `@layout AccountingLayout`. Inject: `IHttpClientFactory`, `ITenantProvider`, `ILogger<ProductManagement>`, `IJSRuntime`, `NavigationManager`. | `5_WebApps/ShopERP/Components/Pages/Products/ProductManagement.razor` (NEW) | ⬜ |
| 8 | P4-U2 | State: `List<ProductDetailDto> _products`, `ProductDetailDto? _selected`, `bool _isLoading`, `string? _errorMessage`, `CreateProductRequest _createForm`, `UpdateProductRequest _editForm`, `bool _showCreateModal`, `bool _showEditModal`. | same | ⬜ |
| 9 | P4-U3 | `OnInitializedAsync`: load `GET /api/products/manage` qua `HttpClientFactory.CreateClient("GatewayClient")`. Set `_isLoading=false` sau khi load. Try-catch → `_errorMessage`. | same | ⬜ |
| 10 | P4-U4 | DataGrid (VanAnDataGrid) với columns: Tên, Category, Price (format VNĐ via `CurrencyHelper.FormatVND`), VAT (%), Trạng thái (Active/Inactive badge), Hành động (Edit, Delete, Reactivate, QR buttons). | same | ⬜ |
| 11 | P4-U5 | Create button (VanAnButton) → mở `_showCreateModal=true`. Modal dùng `VanAnModal` + `VanAForm` với `VanAnInput` fields: Name, Description, Price, Category, VatRate, ImageUrl. Submit → `POST /api/products` → reload list. | same | ⬜ |
| 12 | P4-U6 | Edit button mỗi row → load product vào `_editForm` → mở `_showEditModal`. Submit → `PUT /api/products/{id}` → reload list. | same | ⬜ |
| 13 | P4-U7 | Delete button → confirm dialog (VanAnModal confirm) → `DELETE /api/products/{id}` → reload list. | same | ⬜ |
| 14 | P4-U8 | Reactivate button (chỉ hiện nếu `IsActive=false`) → `PUT /api/products/{id}/activate`. Deactivate button (chỉ hiện nếu `IsActive=true`) → `PUT /api/products/{id}/deactivate`. | same | ⬜ |
| 15 | P4-U9 | Image upload: trong Create/Edit modal, thêm `<InputFile>` Blazor component → upload qua `POST /api/products/{id}/image` (multipart). Hiển thị preview `ImageUrl` nếu có. **Lưu ý:** Create modal upload image SAU khi create (cần product ID trước). | same | ⬜ |
| 16 | P4-U10 | Price format: dùng `VanAn.Shared.Helpers.CurrencyHelper.FormatVND(p.Price)` trong DataGrid cell. | same | ⬜ |

### 1D. Navigation Integration

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 17 | P4-N1 | Thêm menu item "Sản phẩm" (`/products`) vào NavMenu sidebar. Pattern theo existing menu items. | `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` | ⬜ |
| 18 | P4-N2 | Thêm menu item "Sản phẩm" vào AccountingLayout sidebar. | `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` | ⬜ |
| 19 | P4-N3 | Thêm card "Sản phẩm" vào Sitemap (icon 📦, link `/products`, mô tả "Quản lý sản phẩm, giá, VAT, hình ảnh"). | `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | ⬜ |
| 20 | P4-N4 | Verify build: `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` pass. | Solution-wide | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] `VanAnButton` disabled bug fixed (prerequisite)
- [ ] `VanAnDataGrid` render order bug fixed (prerequisite)
- [ ] `1_Shared/Helpers/CurrencyHelper.cs` tồn tại, KhachLink delegate to it
- [ ] `/products` page load, hiển thị DataGrid với danh sách products
- [ ] Create modal (VanAForm + VanAnInput) tạo product → `POST /api/products` → list refresh
- [ ] Edit modal update product → `PUT /api/products/{id}` → list refresh
- [ ] Delete button confirm → `DELETE /api/products/{id}` → list refresh (product biến mất)
- [ ] Reactivate/Deactivate buttons toggle `IsActive`
- [ ] Image upload qua `<InputFile>` → `POST /api/products/{id}/image` → preview hiện
- [ ] Price hiển thị `55.000 ₫` (CurrencyHelper.FormatVND)
- [ ] NavMenu có "Sản phẩm" → `/products`
- [ ] AccountingLayout có "Sản phẩm" → `/products`
- [ ] Sitemap có card "Sản phẩm"
- [ ] Mọi UI dùng VanAnButton/VanAnCard/VanAForm/VanAnInput/VanAnModal/VanAnDataGrid — KHÔNG custom HTML/CSS
- [ ] Build: 0 errors

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Custom HTML/CSS cho form/table/modal (phải dùng UI Platform components)
- ❌ Hardcoded color/spacing — dùng design tokens
- ❌ Duplicate `CurrencyHelper` vào ShopERP (dùng shared helper)
- ❌ Bypass `@attribute [Authorize(Policy = "OwnerOnly")]`
- ❌ Gọi `IVanAnDbContext` trực tiếp từ Blazor component (phải qua API)
- ❌ Hardcode tenantId (dùng `ITenantProvider.TenantId.Value`)
- ❌ Upload image trong `CreateProductRequest` JSON (upload tách endpoint multipart)
- ❌ Bỏ qua prerequisite fixes (VanAnButton/VanAnDataGrid bugs) — DataGrid sẽ trống + buttons disabled

---

## 4. ROLLBACK PLAN

Nếu Phase 4 fail sau 3 rounds:
1. Revert `ProductManagement.razor` (file mới — xóa)
2. Revert NavMenu, AccountingLayout, Sitemap (file cũ — git checkout)
3. Revert shared CurrencyHelper (file mới — xóa) + KhachLink CurrencyHelper (git checkout)
4. Revert VanAnButton + VanAnDataGrid fixes (git checkout) — **chỉ nếu** fixes gây regression. Nếu fixes OK nhưng UI phase fail, giữ fixes (đã fix bug đúng).
5. Report: error cụ thể, evidence, recommend next step

---

## 5. VERIFICATION CHECKLIST

```powershell
# 1. Build
dotnet build VanAn.sln
# Expected: 0 errors

# 2. Guard check
.\scripts\guard-check.ps1
# Expected: PASS

# 3. UI Platform prerequisite fixes verify
# - Mở page dùng VanAnButton với disabled=false → button KHÔNG disabled (click được)
# - Mở page dùng VanAnDataGrid với data → table HIỆN rows (không trống)

# 4. Manual smoke test (Owner login)
# - Vào /products → DataGrid load danh sách products
# - Bấm "Tạo sản phẩm" → modal mở → nhập info → submit → product mới xuất hiện trong list
# - Bấm "Sửa" trên 1 row → modal edit mở → đổi Name → submit → list refresh
# - Bấm "Xóa" → confirm → product biến mất khỏi list
# - Bấm "Kích hoạt" trên product inactive → status đổi thành Active
# - Upload image trong edit modal → preview hiện → save → ImageUrl cập nhật
# - Verify price format: 55000 → "55.000 ₫"

# 5. Navigation verify
# - NavMenu sidebar có "Sản phẩm" → click → /products
# - AccountingLayout sidebar có "Sản phẩm"
# - Sitemap có card "Sản phẩm"
```
