# MASTER IMPLEMENTATION PLAN — QuickSetup Fix + Product Management with QR Code

> **Status:** APPROVED — 6 task cards created, ready for IMPLEMENT
> **Created:** 2026-07-14
> **Reviewed:** 2026-07-14 — gap review complete, 5 blocking gaps + 6 minor gaps resolved
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → feature branches per phase
> **Execution principle:** JIT Planning + Pure Execution + Domain-First
> **Prerequisite:** Verify session (2026-07-14) — QuickSetup orphan page + Product CRUD missing confirmed
> **Reference:** `docs/AI/tasks/tiered_auth_loyalty_master_plan.md` (format template)
>
> **Task cards (locked 2026-07-14):**
> - Phase 1: `docs/AI/tasks/quicksetup_phase1_fix_task_card.md`
> - Phase 2: `docs/AI/tasks/product_phase2_domain_task_card.md`
> - Phase 3: `docs/AI/tasks/product_phase3_api_task_card.md`
> - Phase 4: `docs/AI/tasks/product_phase4_ui_task_card.md`
> - Phase 5: `docs/AI/tasks/product_phase5_qr_print_task_card.md`
> - Phase 6: `docs/AI/tasks/product_phase6_e2e_task_card.md`

---

## 0. EXECUTION RULES

### JIT Planning Strategy
**Nguyên tắc:** Investigate trước, Implement sau. KHÔNG code mò mẫm.

**Bước 1: INVESTIGATE** — Verify existing code structure, service signatures, UI component patterns
**Bước 2: IMPLEMENT** — Theo plan đã chốt, mỗi phase xong chạy `guard-check.ps1` + `dotnet build`

### Session protocol
1. Mỗi session chỉ làm 1 phase
2. Bắt đầu session: Đọc `project_state.md` + task card phase đang làm
3. Sau khi plan chốt: Execution Phase
4. Trước session end: Build + test
5. Sau mỗi phase: Commit `[PROD P{N}] Task description`

### Branch protocol
```
main
  └── feature/quicksetup-fix-phase1
      └── feature/product-mgmt-phase2-domain
          └── feature/product-mgmt-phase3-api
              └── feature/product-mgmt-phase4-ui
                  └── feature/product-mgmt-phase5-qr-print
                      └── feature/product-mgmt-phase6-e2e
```

### Hard rules
- **Domain layer:** Phase 2 được phép sửa `Domain.cs` (thêm `Product.Update()` method) — có user approval
- **UI Platform:** Mọi UI mới PHẢI dùng VanAnButton, VanAnCard, VanAForm, VanAnDataGrid — KHÔNG custom HTML/CSS
- **QR Code:** Dùng existing `IShopQrCodeService` (QRCoder library) — KHÔNG thêm dependency mới
- **QR Payload:** JSON format `{ProductId, ShopId, Timestamp, TableNumber?}` — existing `QRCodePayload` class. `TableNumber` chỉ include khi có giá trị (existing behavior trong `ProductsController.GetProductQrCode`).
- **Print:** Browser native print (`window.print()`) + CSS `@media print` — KHÔNG thêm PDF library
- **Multi-tenancy:** Mọi product query PHẢI filter by `TenantId` từ `ITenantProvider`
- **ShopERP = Blazor Server** — hosts in-process CoreHub services (Option B)
- **Playwright DISABLED** cho đến Phase 6 (E2E tests)
- **Clean Architecture (G3 locked):** Phase 3 tạo `IProductService` + `IProductRepository` mới — KHÔNG dùng `IVanAnDbContext` trực tiếp trong controller cho write operations. Read endpoints (existing) giữ nguyên.
- **Image upload (G8 locked):** Cloudinary. Tạo `IImageStorageService` + `CloudinaryImageStorageService`. Upload tách endpoint `POST /api/products/{id}/image` (multipart) — KHÔNG nhận file binary trong JSON DTO.
- **CurrencyHelper (G4 locked):** Tạo shared helper `1_Shared/Helpers/CurrencyHelper.cs` — cả ShopERP + KhachLink dùng chung. KHÔNG duplicate.
- **Tenant selection QuickSetup (G7 locked):** SystemAdmin vào `/admin/tenants` → bấm "Khởi tạo nhanh" trên row tenant → redirect `/quick-setup?tenantId={id}`. KHÔNG dùng `ITenantProvider.TenantId` (SystemAdmin không thuộc tenant đích).
- **Domain audit (G5 locked):** Mọi mutation method trong `Product` (Update/Deactivate/Activate/MarkAsDeleted) BẮT BUỘC gọi `UpdateAudit()` hoặc `base.MarkAsDelete()` — audit trail integrity.
- **Delete semantics (G6 locked):** `Deactivate()` = `IsActive=false` (hide catalog, vẫn hiện management). `MarkAsDeleted()` = `IsDeleted=true` (true soft delete, ẩn khỏi mọi query). DELETE endpoint gọi `MarkAsDeleted()`.

### Critical context
- **Architecture:** KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite (business) + PostgreSQL (accounting)
- **Product entity:** `Domain.cs:559-603` — có `ProductId`, `Name`, `Description`, `Price`, `CostPrice`, `Category`, `IsActive`, `ImageUrl`, `VatRate`, `TenantId`. **KHÔNG có `Update()` method** (chỉ có `UpdateCostPrice`)
- **BaseEntity:** `1_Shared/Domain/Common.cs:75-117` — có `IsDeleted` (separate từ `IsActive`), `UpdateAudit(string? updatedBy)`, `MarkAsDelete(string? updatedBy)`. Mọi mutation method trong Domain gọi `UpdateAudit()` (47 occurrences).
- **Product API:** `ProductsController.cs` — chỉ có 4 GET endpoints (list, by-id, recommended, QR). **KHÔNG có POST/PUT/DELETE**. Dùng `IVanAnDbContext` trực tiếp (read-only).
- **Service/Repository layer:** **KHÔNG có** `IProductService` / `IProductRepository` (verified grep). Pattern reference: `IOrderService` + `OrderService` + `IOrderRepository` + `OrderRepository`.
- **QR Service:** `IShopQrCodeService` — `GenerateProductQRCode(productId, shopId, tableNumber?)` → `byte[]` PNG
- **QR Endpoint:** `GET /api/products/{id:guid}/qr` — existing, returns PNG
- **QuickSetup:** `Pages/QuickSetup.razor` — orphan page, thiếu `@rendermode`, thiếu `[Authorize]`, dùng `@inject HttpClient` (DI không register). **BUG:** line 392 `var shopId = Guid.NewGuid()` (hardcode random). **BUG:** gửi `TemplateType = "cafe"` (string) nhưng Gateway `OnboardingController.QuickSetup` line 114 yêu cầu `Guid.TryParse(request.TemplateType)` → **luôn BadRequest**.
- **QuickSetup API:** `POST /api/v1/onboarding/shops/{shopId}/quick-setup` — existing in `2_Gateway/Controllers/OnboardingController.cs` (KHÔNG phải ShopERP — phải gọi qua `GatewayClient` HttpClient)
- **CurrencyHelper:** Chỉ tồn tại ở `5_WebApps/KhachLink/Components/Shared/CurrencyHelper.cs` — **KHÔNG** ở ShopERP. Phase 4 tạo shared helper.
- **Sitemap:** `Components/Pages/Sitemap.razor` — có cards cho Orders, Accounting, EInvoice, KhachLink, Admin, Audit, Tenant. **KHÔNG có card Product**
- **TenantManagement:** `Components/Pages/Admin/TenantManagement.razor` — có onboarding modal, hiển thị `ProductsCreated` count. **KHÔNG có link QuickSetup**. Có `_tenants` list với `@foreach` line 91.
- **NavMenu:** `Components/Layout/NavMenu.razor` — sidebar menu. **KHÔNG có Product menu item**
- **DTOs folder:** `1_Shared/DTOs/` (existing), namespace `VanAn.Shared.DTOs`. Phase 3 thêm `ProductDetailDto`, `CreateProductRequest`, `UpdateProductRequest`.
- **DI registration pattern:** `5_WebApps/ShopERP/Program.cs` line 152 (`AddScoped<IOrderService, OrderService>`), line 342 (`AddHttpClient("GatewayClient")`).
- **Cloudinary:** **CHƯA** dùng trong codebase (verified grep). Phase 3 add NuGet `CloudinaryDotNet` + `IImageStorageService`.

---

## 1. CURRENT ISSUES SUMMARY

### Issue A1: QuickSetup page bị bỏ rơi (orphan)
**Status:** ❌ BROKEN
**Priority:** 1 (High — SystemAdmin cần tool khởi tạo nhanh)

`Pages/QuickSetup.razor` tồn tại nhưng:
- Thiếu `@rendermode InteractiveServer` → buttons `@onclick` không fire
- Thiếu `@attribute [Authorize]` → security hole (ai cũng vào được)
- Dùng `@inject HttpClient` → DI không register → crash khi hydrate
- Không được link từ menu nào → user không biết page tồn tại
- Gọi API `/api/v1/onboarding/shops/{shopId}/quick-setup` — `shopId` không được truyền đúng

### Issue B1: Không có Product Management UI
**Status:** ❌ MISSING
**Priority:** 1 (High — Owner cần quản lý sản phẩm)

Không có page nào cho Owner/Admin quản lý products:
- Không có `/products` page (Blazor component)
- Không có Product CRUD API (POST/PUT/DELETE)
- Không có menu item "Sản phẩm" trong NavMenu, AccountingLayout, Sitemap

### Issue B2: Product Domain thiếu Update method
**Status:** ❌ MISSING
**Priority:** 0 (Critical — BLOCKING Product CRUD)

`Product` entity (`Domain.cs:559-603`) chỉ có `UpdateCostPrice()`. Không có method update `Name`, `Description`, `Price`, `Category`, `IsActive`, `ImageUrl`, `VatRate`. Cần thêm `Update()` method để edit product info.

### Issue B3: Không có QR Code print feature
**Status:** ❌ MISSING
**Priority:** 2 (Medium — Owner cần in QR code dán lên sản phẩm/bàn)

QR code generation đã có (`IShopQrCodeService` + endpoint `GET /api/products/{id}/qr`), nhưng:
- Không có UI để xem QR code của product
- Không có chức năng in 1 QR code
- Không có chức năng in nhiều QR code cùng lúc (batch print)

---

## 2. PHASE 1 — Fix QuickSetup Orphan Page

**Branch:** `feature/quicksetup-fix-phase1`
**Priority:** 1 (High)
**Task Card:** `docs/AI/tasks/quicksetup_phase1_fix_task_card.md`

### Mục tiêu
Fix QuickSetup page thành tool hoạt động: SystemAdmin chọn tenant → chọn template → nhập thông tin shop → chạy quick-setup → tạo products/ingredients/workflows.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P1-T1 | Thêm `@rendermode InteractiveServer` + `@attribute [Authorize(Policy = "SystemAdminOnly")]` | `5_WebApps/ShopERP/Pages/QuickSetup.razor` | ⬜ |
| 2 | P1-T2 | Đổi `@inject HttpClient Http` → `@inject IHttpClientFactory HttpClientFactory` + dùng `HttpClientFactory.CreateClient("GatewayClient")` | `5_WebApps/ShopERP/Pages/QuickSetup.razor` | ⬜ |
| 3 | P1-T3 | Parse `tenantId` từ query string (`?tenantId=...`) trong `OnInitializedAsync` — **KHÔNG** dùng `ITenantProvider` (SystemAdmin không thuộc tenant đích). Nếu thiếu/invalid → error + link về `/admin/tenants`. | `5_WebApps/ShopERP/Pages/QuickSetup.razor` | ⬜ |
| 4 | P1-T4 | **FIX BUG TemplateType:** Đổi `TemplateInfo.Type` từ string `"cafe"` → lưu `Guid TemplateId`. Load templates qua `GET /api/v1/onboarding/templates` → mỗi template có `Id` (Guid). Gửi `TemplateType = selectedTemplate.TemplateId.ToString()` (Guid string, parse được bởi `Guid.TryParse` ở Gateway). | `5_WebApps/ShopERP/Pages/QuickSetup.razor` | ⬜ |
| 5 | P1-T5 | Fix `ProcessSetupAsync()`: thay `var shopId = Guid.NewGuid()` (hardcode random) → `var shopId = _selectedTenantId` (từ query string). Gửi `POST /api/v1/onboarding/shops/{shopId}/quick-setup` qua `GatewayClient`. | `5_WebApps/ShopERP/Pages/QuickSetup.razor` | ⬜ |
| 6 | P1-T6 | Hiển thị tenant info (tên + ID) ở đầu wizard để SystemAdmin xác nhận đang setup cho tenant nào. | `5_WebApps/ShopERP/Pages/QuickSetup.razor` | ⬜ |
| 7 | P1-T7 | TenantManagement: thêm button "Khởi tạo nhanh" trong mỗi row của `_tenants` foreach (line 91) → `NavigationManager.NavigateTo($"/quick-setup?tenantId={t.Id}")`. | `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | ⬜ |
| 8 | P1-T8 | Sitemap: thêm link "Khởi tạo nhanh" vào card "Quản Lý Tenant" → `/admin/tenants` (redirect qua TenantManagement, không link thẳng `/quick-setup` vì cần chọn tenant). | `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | ⬜ |
| 9 | P1-T9 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] QuickSetup page có `@rendermode InteractiveServer` → buttons click được
- [ ] QuickSetup page có `[Authorize(Policy = "SystemAdminOnly")]` → chỉ SystemAdmin vào được
- [ ] `@inject IHttpClientFactory` thay cho `HttpClient` → không crash DI
- [ ] `tenantId` lấy từ query string (`?tenantId=...`) — KHÔNG generate random Guid, KHÔNG dùng `ITenantProvider`
- [ ] `TemplateType` gửi lên Gateway là Guid string (parse được bởi `Guid.TryParse`)
- [ ] TenantManagement có button "Khởi tạo nhanh" mỗi row → redirect `/quick-setup?tenantId={id}`
- [ ] Sitemap có link "Khởi tạo nhanh" trong card Tenant → `/admin/tenants`
- [ ] QuickSetup page hiển thị tên tenant đang setup
- [ ] Build: 0 errors

---

## 3. PHASE 2 — Domain: Product.Update() Method

**Branch:** `feature/product-mgmt-phase2-domain`
**Priority:** 0 (Critical — BLOCKING Phase 3+)
**Task Card:** `docs/AI/tasks/product_phase2_domain_task_card.md`

### Mục tiêu
Thêm `Update()` method vào `Product` entity để cho phép edit product info (Name, Description, Price, Category, IsActive, ImageUrl, VatRate). Đây là nền tảng cho Product CRUD.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P2-T1 | Thêm `Update(name, description, price, category, isActive, imageUrl, vatRate, updatedBy?)` method — validation + `UpdateAudit(updatedBy)` | `1_Shared/Domain.cs` (line 559-603) | ⬜ |
| 2 | P2-T2 | Thêm `Deactivate(updatedBy?)` method — `IsActive = false; UpdateAudit(updatedBy);` | `1_Shared/Domain.cs` | ⬜ |
| 3 | P2-T3 | Thêm `Activate(updatedBy?)` method — `IsActive = true; UpdateAudit(updatedBy);` | `1_Shared/Domain.cs` | ⬜ |
| 4 | P2-T4 | Thêm `MarkAsDeleted(updatedBy?)` method — `base.MarkAsDelete(updatedBy);` (set `IsDeleted = true`, separate từ Deactivate) | `1_Shared/Domain.cs` | ⬜ |
| 5 | P2-T5 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] `Product.Update(name, description, price, category, isActive, imageUrl, vatRate, updatedBy?)` method tồn tại — gọi `UpdateAudit(updatedBy)`
- [ ] `Product.Deactivate(updatedBy?)` method tồn tại (set `IsActive = false`, gọi `UpdateAudit`)
- [ ] `Product.Activate(updatedBy?)` method tồn tại (set `IsActive = true`, gọi `UpdateAudit`)
- [ ] `Product.MarkAsDeleted(updatedBy?)` method tồn tại (set `IsDeleted = true` via `base.MarkAsDelete`, gọi `UpdateAudit`)
- [ ] Validation: Price < 0 → throw, VatRate < 0 → throw, Name empty → throw
- [ ] Build: 0 errors
- [ ] Domain layer vẫn pure (no EF Core, no DbContext, no DataAnnotations)

---

## 4. PHASE 3 — Product CRUD API

**Branch:** `feature/product-mgmt-phase3-api`
**Priority:** 1 (High)
**Task Card:** `docs/AI/tasks/product_phase3_api_task_card.md`

### Mục tiêu
Thêm POST/PUT/DELETE endpoints vào `ProductsController` qua **`IProductService` mới** (Clean Architecture layer — G3 locked). Read endpoints (GET) đã có — giữ nguyên, không refactor. Thêm `IImageStorageService` (Cloudinary) cho image upload (G8 locked).

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P3-R1 | Tạo `IProductRepository` + `ProductRepository` (filter by TenantId, pattern theo `IOrderRepository`) | `3_CoreHub/Repositories/IProductRepository.cs` + `ProductRepository.cs` (NEW) | ⬜ |
| 2 | P3-R2 | Register DI `IProductRepository` trong `3_CoreHub/Program.cs` | `3_CoreHub/Program.cs` | ⬜ |
| 3 | P3-S1 | Tạo `IProductService` + `ProductService` (inject `IProductRepository` + `IImageStorageService`, verify tenant ownership trước mutate, pattern theo `IOrderService`) | `3_CoreHub/Services/IProductService.cs` + `ProductService.cs` (NEW) | ⬜ |
| 4 | P3-S2 | Register DI `IProductService` trong `5_WebApps/ShopERP/Program.cs` | `5_WebApps/ShopERP/Program.cs` | ⬜ |
| 5 | P3-C1 | Add NuGet `CloudinaryDotNet` (≥7 days stable) vào `3_CoreHub.csproj` | `3_CoreHub/3_CoreHub.csproj` | ⬜ |
| 6 | P3-C2 | Tạo `IImageStorageService` + `CloudinaryImageStorageService` (config từ `IConfiguration` section `"Cloudinary"`, validate file 5MB max + .jpg/.png/.webp) | `3_CoreHub/Services/IImageStorageService.cs` + `CloudinaryImageStorageService.cs` (NEW) | ⬜ |
| 7 | P3-C3 | Register DI `IImageStorageService` + add Cloudinary config placeholder vào `appsettings.json` (KHÔNG commit real credentials) | `5_WebApps/ShopERP/Program.cs` + `appsettings.json` | ⬜ |
| 8 | P3-D1 | Tạo `ProductDetailDto` (full fields + audit timestamps) | `1_Shared/DTOs/ProductDetailDto.cs` (NEW) | ⬜ |
| 9 | P3-D2 | Tạo `CreateProductRequest` + `UpdateProductRequest` (DataAnnotations validation: `[Required]`, `[Range]`) | `1_Shared/DTOs/CreateProductRequest.cs` + `UpdateProductRequest.cs` (NEW) | ⬜ |
| 10 | P3-A1 | Inject `IProductService` vào `ProductsController` + thêm `POST api/products` (OwnerOnly, TenantId từ `ITenantProvider`) | `5_WebApps/ShopERP/Controllers/ProductsController.cs` | ⬜ |
| 11 | P3-A2 | `PUT api/products/{id}` — update qua `Product.Update()` (OwnerOnly) | same | ⬜ |
| 12 | P3-A3 | `DELETE api/products/{id}` — soft delete via `Product.MarkAsDeleted()` (G6 — set `IsDeleted=true`, KHÔNG dùng Deactivate) (OwnerOnly) | same | ⬜ |
| 13 | P3-A4 | `PUT api/products/{id}/activate` + `PUT api/products/{id}/deactivate` — toggle `IsActive` (OwnerOnly) | same | ⬜ |
| 14 | P3-A5 | `GET api/products/manage` — list all (incl. inactive, excl. deleted) for management (OwnerOnly) | same | ⬜ |
| 15 | P3-A6 | `POST api/products/{id}/image` — multipart upload → Cloudinary → update `ImageUrl` via `Product.Update()` (OwnerOnly) | same | ⬜ |
| 16 | P3-A7 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] `IProductRepository` + `ProductRepository` tồn tại, filter by TenantId
- [ ] `IProductService` + `ProductService` tồn tại, verify tenant ownership trước mutate
- [ ] `IImageStorageService` + `CloudinaryImageStorageService` tồn tại, config từ `IConfiguration`
- [ ] 3 DTOs trong `1_Shared/DTOs/` với validation attributes
- [ ] `POST api/products` tạo product với TenantId từ `ITenantProvider`
- [ ] `PUT api/products/{id}` update qua `Product.Update()`
- [ ] `DELETE api/products/{id}` soft delete via `Product.MarkAsDeleted()` (IsDeleted=true)
- [ ] `PUT api/products/{id}/activate` + `/deactivate` toggle IsActive
- [ ] `GET api/products/manage` list all (incl. inactive, excl. deleted)
- [ ] `POST api/products/{id}/image` upload Cloudinary → update ImageUrl
- [ ] Mọi write endpoint `[Authorize(Policy = "OwnerOnly")]`
- [ ] Multi-tenancy: mọi query filter by TenantId
- [ ] Build: 0 errors

---

## 5. PHASE 4 — Product Management UI

**Branch:** `feature/product-mgmt-phase4-ui`
**Priority:** 1 (High)
**Task Card:** `docs/AI/tasks/product_phase4_ui_task_card.md`

### Mục tiêu
Tạo Product Management page (`/products`) cho Owner/Admin: list products trong tenant, DataGrid với columns, create/edit modal, delete/reactivate buttons, image upload. **Prerequisite:** Fix VanAnButton disabled bug + VanAnDataGrid render order bug (Section 10). **G4:** Tạo shared `CurrencyHelper` ở `1_Shared/Helpers/`.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P4-P1 | **Prerequisite fix:** `VanAnButton.razor` disabled attribute: `disabled="@(State.IsDisabled ? true : null)"` | `UI.Platform/Components/Atomic/VanAnButton.razor` | ⬜ |
| 2 | P4-P2 | **Prerequisite fix:** `VanAnDataGrid.razor` render order: `<CascadingValue>` wrap `<table>` + `StateHasChanged()` trong `RegisterColumn` | `UI.Platform/Components/Data/VanAnDataGrid.razor` | ⬜ |
| 3 | P4-S1 | Tạo shared `1_Shared/Helpers/CurrencyHelper.cs` (namespace `VanAn.Shared.Helpers`, copy logic từ KhachLink). Update KhachLink `CurrencyHelper.cs` delegate to shared (backward compat). | `1_Shared/Helpers/CurrencyHelper.cs` (NEW) + `5_WebApps/KhachLink/Components/Shared/CurrencyHelper.cs` | ⬜ |
| 4 | P4-T1 | Tạo `Components/Pages/Products/ProductManagement.razor` — `@page "/products"` + `@rendermode InteractiveServer` + `@attribute [Authorize(Policy = "OwnerOnly")]` + `@layout AccountingLayout` | `5_WebApps/ShopERP/Components/Pages/Products/ProductManagement.razor` (NEW) | ⬜ |
| 5 | P4-T2 | DataGrid (VanAnDataGrid): columns Tên, Category, Price (FormatVND), VAT, Trạng thái, Hành động | same file | ⬜ |
| 6 | P4-T3 | Load products: `GET api/products/manage` qua `IHttpClientFactory.CreateClient("GatewayClient")` | same file | ⬜ |
| 7 | P4-T4 | Create modal (VanAForm + VanAnInput): Name, Description, Price, Category, VatRate, ImageUrl → `POST api/products` | same file | ⬜ |
| 8 | P4-T5 | Edit modal: pre-fill existing values → `PUT api/products/{id}` | same file | ⬜ |
| 9 | P4-T6 | Delete button: confirm dialog (VanAnModal) → `DELETE api/products/{id}` (soft delete) | same file | ⬜ |
| 10 | P4-T7 | Reactivate/Deactivate buttons: `PUT api/products/{id}/activate` or `/deactivate` | same file | ⬜ |
| 11 | P4-T8 | Image upload: `<InputFile>` trong Create/Edit modal → `POST api/products/{id}/image` (multipart) → preview ImageUrl. **Lưu ý:** Create modal upload SAU khi create (cần product ID trước). | same file | ⬜ |
| 12 | P4-T9 | Price format: `VanAn.Shared.Helpers.CurrencyHelper.FormatVND(p.Price)` (shared helper, G4) | same file | ⬜ |
| 13 | P4-T10 | Thêm menu item "Sản phẩm" vào NavMenu sidebar | `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` | ⬜ |
| 14 | P4-T11 | Thêm menu item "Sản phẩm" vào AccountingLayout sidebar | `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` | ⬜ |
| 15 | P4-T12 | Thêm card "Sản phẩm" vào Sitemap | `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | ⬜ |
| 16 | P4-T13 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] VanAnButton disabled bug fixed (prerequisite)
- [ ] VanAnDataGrid render order bug fixed (prerequisite)
- [ ] `1_Shared/Helpers/CurrencyHelper.cs` tồn tại, KhachLink delegate to it
- [ ] `/products` page load được, hiển thị DataGrid với danh sách products
- [ ] Create modal (VanAForm) tạo product mới thành công
- [ ] Edit modal update product thành công
- [ ] Delete button soft delete product (IsDeleted=true via MarkAsDeleted)
- [ ] Reactivate/Deactivate buttons toggle IsActive
- [ ] Image upload qua `<InputFile>` → Cloudinary → preview ImageUrl
- [ ] Price hiển thị format VNĐ (55.000 ₫) via shared CurrencyHelper
- [ ] NavMenu có menu item "Sản phẩm"
- [ ] AccountingLayout có menu item "Sản phẩm"
- [ ] Sitemap có card "Sản phẩm"
- [ ] Mọi UI dùng UI Platform components — KHÔNG custom HTML/CSS
- [ ] Build: 0 errors

---

## 6. PHASE 5 — QR Code View + Print

**Branch:** `feature/product-mgmt-phase5-qr-print`
**Priority:** 2 (Medium)
**Task Card:** `docs/AI/tasks/product_phase5_qr_print_task_card.md`

### Mục tiêu
(1) Mỗi product có QR code chứa `{ProductId, ShopId, Timestamp}` — khách hàng quét QR → mở KhachLink → thêm product vào giỏ. (2) UI xem QR code của 1 product. (3) Chức năng in 1 QR code. (4) Chức năng in nhiều QR code cùng lúc (batch print — 1 QR per product).

### QR Code Flow
```
Owner tạo product → system generate QR code (PNG)
  → QR payload: {ProductId, ShopId, Timestamp} (JSON)
  → Owner in QR code → dán lên sản phẩm/bàn
  → Khách hàng quét QR bằng camera phone
  → Mở KhachLink URL với QR payload
  → KhachLink parse QR → AddToCart(product) → bắt đầu luồng đặt hàng
```

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P5-T1 | ProductManagement: thêm column "QR" với icon QR code — click → mở QR modal | `5_WebApps/ShopERP/Components/Pages/Products/ProductManagement.razor` | ⬜ |
| 2 | P5-T2 | QR Modal: hiển thị QR code image (gọi `GET api/products/{id}/qr`) + product name + price | same file | ⬜ |
| 3 | P5-T3 | QR Modal: nút "In QR code" — `window.print()` với CSS `@media print` layout (QR + product info) | same file | ⬜ |
| 4 | P5-T4 | Batch print: checkbox column trong DataGrid — chọn nhiều products | same file | ⬜ |
| 5 | P5-T5 | Batch print: nút "In QR đã chọn" — generate print layout với tất cả QR codes đã chọn (1 QR per row) | same file | ⬜ |
| 6 | P5-T6 | Print layout: CSS `@media print` — mỗi QR code trên 1 card (200x200px) + product name + price + shop name | same file | ⬜ |
| 7 | P5-T7 | Print layout: A4 page, 2 columns x 5 rows = 10 QR codes per page, page break giữa các trang | same file | ⬜ |
| 8 | P5-T8 | QR code URL: dùng `GET api/products/{id}/qr?tenantId={tenantId}` — existing endpoint | same file | ⬜ |
| 9 | P5-T9 | Verify QR payload: scan QR → JSON `{ProductId, ShopId, Timestamp}` → KhachLink parse → AddToCart | Manual test | ⬜ |
| 10 | P5-T10 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Print Layout Spec
```
┌─────────────────────────────────────┐
│  A4 Portrait (210mm x 297mm)        │
│  ┌───────────┐  ┌───────────┐       │
│  │  ██████   │  │  ██████   │       │
│  │  ██  ██   │  │  ██  ██   │       │
│  │  ██████   │  │  ██████   │       │
│  │  QR Code  │  │  QR Code  │       │
│  │  ██████   │  │  ██████   │       │
│  ├───────────┤  ├───────────┤       │
│  │ Product A │  │ Product B │       │
│  │ 55.000 ₫  │  │ 30.000 ₫  │       │
│  │ Shop Name │  │ Shop Name │       │
│  └───────────┘  └───────────┘       │
│  ┌───────────┐  ┌───────────┐       │
│  │  QR Code  │  │  QR Code  │       │
│  ├───────────┤  ├───────────┤       │
│  │ Product C │  │ Product D │       │
│  └───────────┘  └───────────┘       │
│  ... (5 rows x 2 cols = 10 per page)│
└─────────────────────────────────────┘
```

### Exit criteria
- [ ] Mỗi product có QR code icon trong DataGrid → click mở QR modal
- [ ] QR Modal hiển thị QR image + product name + price
- [ ] Nút "In QR code" → `window.print()` → in 1 QR code
- [ ] Checkbox column cho chọn nhiều products
- [ ] Nút "In QR đã chọn" → in nhiều QR codes (batch print)
- [ ] Print layout: A4, 2 columns x 5 rows, mỗi card có QR + product name + price + shop name
- [ ] Page break giữa các trang (nếu > 10 QR codes)
- [ ] Scan QR → JSON payload → KhachLink AddToCart hoạt động
- [ ] Build: 0 errors

---

## 7. PHASE 6 — E2E Tests

**Branch:** `feature/product-mgmt-phase6-e2e`
**Priority:** 3 (Final validation)
**Task Card:** `docs/AI/tasks/product_phase6_e2e_task_card.md`

### Mục tiêu
E2E test full luồng: (1) Owner login → Product Management → create product → verify in list. (2) Edit product → verify update. (3) Delete product → verify soft delete. (4) View QR code → verify QR image. (5) Batch print → verify print layout.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P6-T1 | `product-crud-flow.spec.ts` — create → edit → delete → reactivate | `6_Testing/e2e-tests/product-crud-flow.spec.ts` (NEW) | ⬜ |
| 2 | P6-T2 | `product-qr-print.spec.ts` — view QR → print 1 → batch print | `6_Testing/e2e-tests/product-qr-print.spec.ts` (NEW) | ⬜ |
| 3 | P6-T3 | Page Object Model: ProductManagement page object | `6_Testing/e2e-tests/pages/ProductManagementPage.ts` (NEW) | ⬜ |
| 4 | P6-T4 | Test: QuickSetup flow (SystemAdmin login → select tenant → quick-setup) | `6_Testing/e2e-tests/quicksetup-flow.spec.ts` (NEW) | ⬜ |
| 5 | P6-T5 | Run E2E tests + fix flaky issues | `6_Testing/` | ⬜ |
| 6 | P6-T6 | Verify: all E2E tests pass | `6_Testing/` | ⬜ |

### Exit criteria
- [ ] Product CRUD flow pass (create → edit → delete → reactivate)
- [ ] QR view + print flow pass
- [ ] QuickSetup flow pass
- [ ] Không có flaky test
- [ ] E2E coverage: product CRUD, QR view, QR print, batch print, QuickSetup

---

## 8. PHASE DEPENDENCY GRAPH

```
PHASE 1 (QuickSetup Fix) ← independent, can start immediately
      │
      └── (no dependency on other phases)

PHASE 2 (Domain: Product.Update) ← BLOCKING Phase 3+
      │
      └── PHASE 3 (Product CRUD API)
            │
            └── PHASE 4 (Product Management UI)
                  │
                  └── PHASE 5 (QR Code View + Print)
                        │
                        └── PHASE 6 (E2E Tests)
```

**Critical path:** P2 → P3 → P4 → P5 → P6
**Parallel option:** P1 (QuickSetup Fix) can run parallel with P2-P6 (independent)

---

## 9. RISK ASSESSMENT

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Domain.Update() method break existing code | Low | High | Add method, không sửa existing methods. Build check sau P2. |
| ProductsController POST/PUT không filter TenantId | Medium | High | Tất cả query filter by `TenantProvider.TenantId`. Service verify tenant ownership. Test multi-tenant. |
| QR code print layout không đúng A4 | Medium | Medium | CSS `@media print` + `@page` size A4. Test trên Chrome/Firefox. |
| Batch print > 10 QR codes gây page break sai | Medium | Low | CSS `page-break-inside: avoid` cho mỗi card. Test với 15+ products. |
| QuickSetup API call fail do `shopId` sai | Medium | High | Lấy `shopId` từ query string `?tenantId=...` (G7) — KHÔNG hardcode, KHÔNG dùng ITenantProvider. |
| QuickSetup API fail do TemplateType string "cafe" (G2 bug) | High | High | Đổi TemplateType thành Guid string (parse được bởi `Guid.TryParse` ở Gateway). |
| VanAnDataGrid render order bug (Issue 2 từ verify session) | High | High | Fix VanAnDataGrid trước Phase 4 (prerequisite P4-P2). |
| SignalR hydration fail → buttons disabled | Medium | High | Fix VanAnButton `disabled` attribute bug trước Phase 4 (prerequisite P4-P1). |
| Cloudinary credentials leak | Medium | High | Dùng user-secrets hoặc `appsettings.Development.json` (gitignored). KHÔNG commit real credentials. |
| Cloudinary upload fail (network/timeout) | Medium | Medium | Try-catch trong `CloudinaryImageStorageService` + log error + return null. UI hiển thị error message. |
| IProductService/IProductRepository DI registration missing | Medium | High | Register trong cả `3_CoreHub/Program.cs` (repo) + `5_WebApps/ShopERP/Program.cs` (service). Verify build + smoke test. |
| Image upload trong Create modal cần product ID trước | High | Medium | Create product trước (POST) → lấy ID → upload image (POST /image) → update ImageUrl. UI flow 2 bước. |

---

## 10. PREREQUISITE FIXES (from verify session 2026-07-14)

Trước khi bắt đầu Phase 4 (UI), cần fix 2 bugs từ verify session:

| # | Bug | Fix | File |
|---|-----|-----|------|
| 1 | VanAnButton `disabled="False"` → browser interpret as disabled | `disabled="@(State.IsDisabled ? true : null)"` | `UI.Platform/Components/Atomic/VanAnButton.razor` |
| 2 | VanAnDataGrid empty table (columns register after table render) | Di chuyển `<CascadingValue>@Columns</CascadingValue>` lên trước `<table>` + `StateHasChanged()` trong `RegisterColumn` | `UI.Platform/Components/Data/VanAnDataGrid.razor` |

**Lý do:** Product Management UI dùng VanAnDataGrid + VanAnButton. Nếu 2 bugs này không fix trước, DataGrid sẽ trống + buttons sẽ disabled.

---

## 11. FILE INVENTORY

### Files to CREATE (NEW)
| File | Phase | Purpose |
|------|-------|---------|
| `3_CoreHub/Repositories/IProductRepository.cs` | P3 | Product repository interface (G3 Clean Architecture) |
| `3_CoreHub/Repositories/ProductRepository.cs` | P3 | Product repository impl (filter by TenantId) |
| `3_CoreHub/Services/IProductService.cs` | P3 | Product service interface (G3) |
| `3_CoreHub/Services/ProductService.cs` | P3 | Product service impl (verify tenant ownership) |
| `3_CoreHub/Services/IImageStorageService.cs` | P3 | Image storage interface (G8 Cloudinary) |
| `3_CoreHub/Services/CloudinaryImageStorageService.cs` | P3 | Cloudinary impl (config from IConfiguration) |
| `1_Shared/DTOs/ProductDetailDto.cs` | P3 | Product detail DTO for management API |
| `1_Shared/DTOs/CreateProductRequest.cs` | P3 | Create request DTO (DataAnnotations validation) |
| `1_Shared/DTOs/UpdateProductRequest.cs` | P3 | Update request DTO (DataAnnotations validation) |
| `1_Shared/Helpers/CurrencyHelper.cs` | P4 | Shared currency helper (G4 — ShopERP + KhachLink dùng chung) |
| `5_WebApps/ShopERP/Components/Pages/Products/ProductManagement.razor` | P4, P5 | Product management UI + QR view + print |
| `6_Testing/e2e-tests/product-crud-flow.spec.ts` | P6 | E2E test product CRUD |
| `6_Testing/e2e-tests/product-qr-print.spec.ts` | P6 | E2E test QR + print |
| `6_Testing/e2e-tests/quicksetup-flow.spec.ts` | P6 | E2E test QuickSetup |
| `6_Testing/e2e-tests/pages/ProductManagementPage.ts` | P6 | Page object |

### Files to MODIFY (EXISTING)
| File | Phase | Changes |
|------|-------|---------|
| `5_WebApps/ShopERP/Pages/QuickSetup.razor` | P1 | Add `@rendermode`, `[Authorize]`, fix DI, fix shopId (query string), fix TemplateType (Guid) |
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor` | P1 | Add "Khởi tạo nhanh" button mỗi row → redirect `/quick-setup?tenantId={id}` |
| `5_WebApps/ShopERP/Components/Pages/Sitemap.razor` | P1, P4 | Add QuickSetup link + Product card |
| `1_Shared/Domain.cs` | P2 | Add `Product.Update()`, `Deactivate()`, `Activate()`, `MarkAsDeleted()` (gọi `UpdateAudit`/`base.MarkAsDelete`) |
| `3_CoreHub/3_CoreHub.csproj` | P3 | Add NuGet `CloudinaryDotNet` |
| `3_CoreHub/Program.cs` | P3 | Register `IProductRepository` DI |
| `5_WebApps/ShopERP/Program.cs` | P3 | Register `IProductService` + `IImageStorageService` DI |
| `5_WebApps/ShopERP/appsettings.json` | P3 | Add `Cloudinary` config section (placeholder, no real credentials) |
| `5_WebApps/ShopERP/Controllers/ProductsController.cs` | P3 | Inject `IProductService` + add POST/PUT/DELETE/image endpoints |
| `5_WebApps/KhachLink/Components/Shared/CurrencyHelper.cs` | P4 | Delegate to shared `VanAn.Shared.Helpers.CurrencyHelper` (backward compat) |
| `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` | P4 | Add "Sản phẩm" menu item |
| `5_WebApps/ShopERP/Components/Pages/Accounting/AccountingLayout.razor` | P4 | Add "Sản phẩm" menu item |
| `UI.Platform/Components/Atomic/VanAnButton.razor` | Prereq P4 | Fix `disabled` attribute bug |
| `UI.Platform/Components/Data/VanAnDataGrid.razor` | Prereq P4 | Fix render order bug |

---

## 12. SUCCESS METRICS

| Metric | Target |
|--------|--------|
| QuickSetup page hoạt động | ✅ SystemAdmin vào → chọn template → chạy setup → tạo products |
| Product Management page | ✅ Owner vào → xem list → create/edit/delete products |
| QR code generation | ✅ Mỗi product có QR code (PNG) |
| QR code print | ✅ In 1 QR code → A4 layout |
| Batch print | ✅ In nhiều QR codes → A4 layout, 2 cols x 5 rows |
| QR scan → AddToCart | ✅ Khách hàng quét QR → KhachLink → thêm vào giỏ |
| Build | ✅ 0 errors |
| E2E tests | ✅ All pass |
| Multi-tenancy | ✅ Mọi query filter by TenantId |
| Image upload | ✅ Owner upload ảnh product → Cloudinary → ImageUrl hiển thị |
| Clean Architecture | ✅ IProductService + IProductRepository (KHÔNG dùng DbContext trực tiếp trong controller cho write) |

---

## 13. GAP REVIEW RESOLUTION (2026-07-14)

Review session phát hiện 5 blocking gaps + 6 minor gaps. Tất cả đã được resolve trong task cards + master plan update:

| Gap | Resolution | Affected Phase |
|-----|------------|----------------|
| G1 — 6 task card files missing | ✅ Created 6 task cards: `quicksetup_phase1_fix_task_card.md`, `product_phase2_domain_task_card.md`, `product_phase3_api_task_card.md`, `product_phase4_ui_task_card.md`, `product_phase5_qr_print_task_card.md`, `product_phase6_e2e_task_card.md` | All |
| G2 — TemplateType bug (string "cafe" vs Guid) | ✅ P1-T4: đổi TemplateType thành Guid string. Documented in critical context + Phase 1 task card. | P1 |
| G3 — IProductService architecture decision | ✅ User locked: tạo IProductService mới (Clean Architecture). Phase 3 task card 1B + master plan hard rules. | P3 |
| G4 — CurrencyHelper not in ShopERP | ✅ User locked: shared helper `1_Shared/Helpers/CurrencyHelper.cs`. Phase 4 task card 1B + master plan hard rules. | P4 |
| G5 — UpdateAudit() not mentioned | ✅ Phase 2 exit criteria require `UpdateAudit()`/`base.MarkAsDelete()` call. Master plan hard rules. | P2 |
| G6 — IsActive vs IsDeleted conflated | ✅ User locked: Deactivate=IsActive=false, MarkAsDeleted=IsDeleted=true. DELETE endpoint dùng MarkAsDeleted. Master plan hard rules + Phase 2/3 task cards. | P2, P3 |
| G7 — SystemAdmin tenant selection UX | ✅ User locked: TenantManagement list → click row "Khởi tạo nhanh" → redirect `/quick-setup?tenantId={id}`. Phase 1 task card + master plan hard rules. | P1 |
| G8 — Image upload flow missing | ✅ User locked: Cloudinary. `IImageStorageService` + `CloudinaryImageStorageService`. Upload tách endpoint `POST /api/products/{id}/image`. Phase 3 task card 1C + master plan hard rules. | P3, P4 |
| G9 — DTO field definitions missing | ✅ Phase 3 task card 1D: 3 DTOs với full field list + DataAnnotations validation. | P3 |
| G10 — QR payload inconsistency | ✅ Reconciled: `{ProductId, ShopId, Timestamp, TableNumber?}`. TableNumber optional (existing behavior). Master plan hard rules + Phase 5 task card. | P5 |
| G11 — DB migration not discussed | ✅ No migration needed (Phase 2 chỉ thêm method, không thêm field). Phase 6 task card confirms: test chỉ verify existing schema, seed data qua fixture. | P6 |

**Status:** All gaps resolved. Plan ready for IMPLEMENT.
