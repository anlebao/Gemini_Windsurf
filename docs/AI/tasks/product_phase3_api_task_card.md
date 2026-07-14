# TASK CARD — Phase 3: Product CRUD API (Clean Architecture Layer)

> **Master plan:** `docs/AI/tasks/quicksetup_product_management_master_plan.md` (Section 4)
> **Branch:** `feature/product-mgmt-phase3-api`
> **Priority:** 1 (High)
> **Mode:** IMPLEMENT
> **Prerequisite:** Phase 2 merged (Product.Update/Deactivate/Activate/MarkAsDeleted tồn tại)

---

## 0. CONTEXT & DECISIONS (locked)

### Architecture facts (verified 2026-07-14)
- Existing `ProductsController`: <ref_file file="C:/VibeCoding/Gemini_Windsurf/5_WebApps/ShopERP/Controllers/ProductsController.cs" />
  - 4 GET endpoints (list, by-id, recommended, qr) — **read-only**
  - Dùng `IVanAnDbContext` trực tiếp (KHÔNG qua service layer)
  - `[Authorize]` class-level, GET endpoints `[AllowAnonymous]`
- **Không có `IProductService` / `IProductRepository`** trong codebase (verified grep)
- Pattern reference: `IOrderService` + `OrderService` + `IOrderRepository` + `OrderRepository`
  - <ref_file file="C:/VibeCoding/Gemini_Windsurf/3_CoreHub/Services/IOrderService.cs" />
  - <ref_file file="C:/VibeCoding/Gemini_Windsurf/3_CoreHub/Repositories/IOrderRepository.cs" />
  - Repository inject `IVanAnDbContext`, filter by `TenantId`
- DTOs folder: `1_Shared/DTOs/` (existing), namespace `VanAn.Shared.DTOs`
- `OwnerOnly` policy: đã register (Program.cs:421)
- DI registration pattern (Program.cs:152): `_ = builder.Services.AddScoped<IOrderService, OrderService>();`

### User decisions (locked 2026-07-14)
- **G3 — Architecture:** Tạo `IProductService` mới (Clean Architecture layer) — **KHÔNG** dùng `IVanAnDbContext` trực tiếp trong controller cho write operations. Read endpoints (existing) giữ nguyên (không refactor — out of scope).
- **G8 — Image upload:** Cloudinary. Tạo `IImageStorageService` + `CloudinaryImageStorageService` (new). Product API nhận `ImageUrl` (string URL từ Cloudinary) — KHÔNG nhận file binary trực tiếp trong Product API. Upload tách thành endpoint riêng `POST /api/products/{id}/image` (multipart/form-data) → Cloudinary → update `ImageUrl` qua `Product.Update()`.
- **G6 — Delete semantics:** `DELETE /api/products/{id}` → gọi `Product.MarkAsDeleted()` (set `IsDeleted = true`). `PUT /api/products/{id}/deactivate` → `Product.Deactivate()`. `PUT /api/products/{id}/activate` → `Product.Activate()`.
- **G9 — DTOs:** Tạo 3 DTOs trong `1_Shared/DTOs/` với validation attributes (DataAnnotations OK ở DTO layer, KHÔNG ở Domain).

---

## 1. TASKS

### 1A. Repository Layer (3_CoreHub/Repositories/)

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P3-R1 | Tạo `IProductRepository.cs` — interface với methods: `GetByIdAsync(ProductId, TenantId, ct)`, `GetAllForManagementAsync(TenantId, ct)` (include inactive, exclude deleted), `AddAsync(Product, ct)`, `UpdateAsync(Product, ct)`, `SaveChangesAsync(ct)`. Pattern theo `IOrderRepository`. | `3_CoreHub/Repositories/IProductRepository.cs` (NEW) | ⬜ |
| 2 | P3-R2 | Tạo `ProductRepository.cs` — implement `IProductRepository`, inject `IVanAnDbContext` + `ILogger<ProductRepository>`. Filter by `TenantId` mọi query. Try-catch + log error pattern theo `OrderRepository`. | `3_CoreHub/Repositories/ProductRepository.cs` (NEW) | ⬜ |
| 3 | P3-R3 | Register DI trong `3_CoreHub/Program.cs`: `_ = services.AddScoped<IProductRepository, ProductRepository>();` (pattern line 98) | `3_CoreHub/Program.cs` | ⬜ |

### 1B. Service Layer (3_CoreHub/Services/)

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 4 | P3-S1 | Tạo `IProductService.cs` — interface: `GetProductForManagementAsync(productId, tenantId, ct) → ProductDetailDto?`, `GetAllForManagementAsync(tenantId, ct) → List<ProductDetailDto>`, `CreateProductAsync(CreateProductRequest, tenantId, ct) → ProductDetailDto`, `UpdateProductAsync(productId, UpdateProductRequest, tenantId, ct) → bool`, `DeleteProductAsync(productId, tenantId, ct) → bool` (MarkAsDeleted), `DeactivateProductAsync(productId, tenantId, ct) → bool`, `ActivateProductAsync(productId, tenantId, ct) → bool`, `UploadImageAsync(productId, IFormFile, tenantId, ct) → string?` (returns URL). | `3_CoreHub/Services/IProductService.cs` (NEW) | ⬜ |
| 5 | P3-S2 | Tạo `ProductService.cs` — implement `IProductService`. Inject `IProductRepository`, `IImageStorageService`, `ILogger<ProductService>`. Mọi method verify product thuộc tenant trước khi mutate (load → check `TenantId == tenantId` → throw `UnauthorizedAccessException` nếu mismatch). | `3_CoreHub/Services/ProductService.cs` (NEW) | ⬜ |
| 6 | P3-S3 | Register DI trong `5_WebApps/ShopERP/Program.cs`: `_ = builder.Services.AddScoped<CoreHub.Services.IProductService, CoreHub.Services.ProductService>();` (pattern line 152) | `5_WebApps/ShopERP/Program.cs` | ⬜ |

### 1C. Image Storage (Cloudinary)

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 7 | P3-C1 | Add NuGet package `CloudinaryDotNet` (latest stable, ≥7 days old) vào `3_CoreHub/3_CoreHub.csproj`. **Verify version published ≥7 days trước khi add.** | `3_CoreHub/3_CoreHub.csproj` | ⬜ |
| 8 | P3-C2 | Tạo `IImageStorageService.cs` — interface: `Task<string?> UploadAsync(IFormFile file, string folder, CancellationToken ct)` (returns URL or null), `Task<bool> DeleteAsync(string publicId, CancellationToken ct)`. | `3_CoreHub/Services/IImageStorageService.cs` (NEW) | ⬜ |
| 9 | P3-C3 | Tạo `CloudinaryImageStorageService.cs` — implement với `CloudinaryDotNet.Cloudinary` client. Đọc config từ `IConfiguration` section `"Cloudinary": { "CloudName", "ApiKey", "ApiSecret" }`. Upload to folder `products/{tenantId}/{productId}`. Validate file: max 5MB, extensions `.jpg/.jpeg/.png/.webp`. | `3_CoreHub/Services/CloudinaryImageStorageService.cs` (NEW) | ⬜ |
| 10 | P3-C4 | Register DI: `_ = builder.Services.AddScoped<CoreHub.Services.IImageStorageService, CoreHub.Services.CloudinaryImageStorageService>();` | `5_WebApps/ShopERP/Program.cs` | ⬜ |
| 11 | P3-C5 | Add Cloudinary config placeholder vào `5_WebApps/ShopERP/appsettings.json`: `"Cloudinary": { "CloudName": "", "ApiKey": "", "ApiSecret": "" }`. **KHÔNG commit real credentials** — document trong `appsettings.Development.json` (gitignored) hoặc user-secrets. | `5_WebApps/ShopERP/appsettings.json` + README note | ⬜ |

### 1D. DTOs (1_Shared/DTOs/)

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 12 | P3-D1 | Tạo `ProductDetailDto.cs` — fields: `Guid ProductId`, `Guid TenantId`, `string Name`, `string? Description`, `decimal Price`, `decimal CostPrice`, `string Category`, `bool IsActive`, `string? ImageUrl`, `decimal VatRate`, `DateTime CreatedAt`, `DateTime UpdatedAt`. Namespace `VanAn.Shared.DTOs`. | `1_Shared/DTOs/ProductDetailDto.cs` (NEW) | ⬜ |
| 13 | P3-D2 | Tạo `CreateProductRequest.cs` — fields với validation: `[Required] string Name`, `string? Description`, `[Range(0, double.MaxValue)] decimal Price`, `[Required] string Category`, `[Range(0, 1)] decimal VatRate = 0.10m`, `string? ImageUrl`, `decimal CostPrice = 0m`. | `1_Shared/DTOs/CreateProductRequest.cs` (NEW) | ⬜ |
| 14 | P3-D3 | Tạo `UpdateProductRequest.cs` — same fields as Create (trừ `CostPrice` — update cost price qua endpoint riêng nếu cần). | `1_Shared/DTOs/UpdateProductRequest.cs` (NEW) | ⬜ |

### 1E. Controller Endpoints (5_WebApps/ShopERP/Controllers/ProductsController.cs)

| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 15 | P3-A1 | Inject `IProductService` vào `ProductsController` constructor (thêm parameter, giữ existing deps). | `5_WebApps/ShopERP/Controllers/ProductsController.cs` | ⬜ |
| 16 | P3-A2 | `POST api/products` — `[Authorize(Policy = "OwnerOnly")]`. Body `CreateProductRequest`. Lấy `tenantId` từ `ITenantProvider.TenantId.Value`. Gọi `_productService.CreateProductAsync(req, tenantId, ct)`. Return `201 Created` với `ProductDetailDto`. | same | ⬜ |
| 17 | P3-A3 | `PUT api/products/{id:guid}` — `[Authorize(Policy = "OwnerOnly")]`. Body `UpdateProductRequest`. Gọi `_productService.UpdateProductAsync(id, req, tenantId, ct)`. Return `200 OK` or `404`. | same | ⬜ |
| 18 | P3-A4 | `DELETE api/products/{id:guid}` — `[Authorize(Policy = "OwnerOnly")]`. Gọi `_productService.DeleteProductAsync(id, tenantId, ct)` (MarkAsDeleted). Return `204 No Content` or `404`. | same | ⬜ |
| 19 | P3-A5 | `PUT api/products/{id:guid}/activate` — `[Authorize(Policy = "OwnerOnly")]`. Gọi `_productService.ActivateProductAsync(...)`. Return `200 OK` or `404`. | same | ⬜ |
| 20 | P3-A6 | `PUT api/products/{id:guid}/deactivate` — `[Authorize(Policy = "OwnerOnly")]`. Gọi `_productService.DeactivateProductAsync(...)`. Return `200 OK` or `404`. | same | ⬜ |
| 21 | P3-A7 | `GET api/products/manage` — `[Authorize(Policy = "OwnerOnly")]`. Gọi `_productService.GetAllForManagementAsync(tenantId, ct)`. Return `List<ProductDetailDto>` (include inactive, exclude deleted). | same | ⬜ |
| 22 | P3-A8 | `POST api/products/{id:guid}/image` — `[Authorize(Policy = "OwnerOnly")]`. Multipart form `IFormFile`. Gọi `_productService.UploadImageAsync(id, file, tenantId, ct)`. Return `200 OK` với `{ imageUrl: "..." }` or `400` if file invalid. | same | ⬜ |
| 23 | P3-A9 | Verify build: `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` pass. | Solution-wide | ⬜ |

---

## 2. EXIT CRITERIA

- [ ] `IProductRepository` + `ProductRepository` tồn tại, filter by TenantId mọi query
- [ ] `IProductService` + `ProductService` tồn tại, verify tenant ownership trước khi mutate
- [ ] `IImageStorageService` + `CloudinaryImageStorageService` tồn tại, đọc config từ `IConfiguration`
- [ ] 3 DTOs (`ProductDetailDto`, `CreateProductRequest`, `UpdateProductRequest`) trong `1_Shared/DTOs/`
- [ ] `POST api/products` tạo product với TenantId từ `ITenantProvider`
- [ ] `PUT api/products/{id}` update qua `Product.Update()`
- [ ] `DELETE api/products/{id}` soft delete via `Product.MarkAsDeleted()`
- [ ] `PUT api/products/{id}/activate` + `/deactivate` toggle `IsActive`
- [ ] `GET api/products/manage` list all (incl. inactive, excl. deleted)
- [ ] `POST api/products/{id}/image` upload Cloudinary → update `ImageUrl`
- [ ] Mọi write endpoint `[Authorize(Policy = "OwnerOnly")]`
- [ ] Multi-tenancy: mọi query filter by `TenantId`
- [ ] Build: 0 errors

---

## 3. ANTI-PATTERNS (KHÔNG làm)

- ❌ Dùng `IVanAnDbContext` trực tiếp trong controller cho write operations (G3 — phải qua `IProductService`)
- ❌ Refactor existing GET endpoints (out of scope — chỉ thêm endpoints mới)
- ❌ Commit Cloudinary credentials vào `appsettings.json` (dùng user-secrets hoặc `appsettings.Development.json` gitignored)
- ❌ Nhận file binary trong `CreateProductRequest`/`UpdateProductRequest` (upload tách endpoint riêng)
- ❌ Bypass tenant check trong service (luôn load → verify `TenantId == tenantId` → mutate)
- ❌ Hardcode Cloudinary config values
- ❌ Thêm DataAnnotations vào `Product` domain entity (chỉ vào DTOs)
- ❌ Thêm `IFormFile` vào DTO (DTO thuần JSON, file qua multipart riêng)

---

## 4. ROLLBACK PLAN

Nếu Phase 3 fail sau 3 rounds:
1. Revert tất cả files mới (Repository, Service, DTOs, ImageStorage)
2. Revert `ProductsController.cs` về commit trước phase (giữ 4 GET endpoints)
3. Revert DI registrations trong Program.cs
4. Revert NuGet package `CloudinaryDotNet` (`dotnet remove package`)
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

# 3. DI verification (manual)
# - Check Program.cs có 3 registrations: IProductRepository, IProductService, IImageStorageService
# - Check 3_CoreHub/Program.cs có IProductRepository registration

# 4. API smoke test (Swagger/Postman, cần Owner login)
# - POST /api/products với CreateProductRequest → 201
# - GET /api/products/manage → list có product vừa tạo
# - PUT /api/products/{id} với UpdateProductRequest → 200
# - DELETE /api/products/{id} → 204
# - GET /api/products/manage → product biến mất (IsDeleted = true)
# - PUT /api/products/{id}/activate (trước đó deactivate) → 200
# - POST /api/products/{id}/image với file .jpg 1MB → 200 + imageUrl

# 5. Multi-tenancy test
# - Owner tenant A tạo product → Owner tenant B GET /api/products/manage → KHÔNG thấy product của A
```
