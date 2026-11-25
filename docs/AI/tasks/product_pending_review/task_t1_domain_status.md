# Task T1 — Domain `ProductStatus` + EF + DTOs + migrations

> **Status:** PENDING · **Created:** 2026-11-25 · **Phase:** T1
> **Plan:** `docs/AI/plans/product-pending-review-master-plan.md` (D1, D4)
> **TDD:** viết test fail trước → implement → green

## Mục tiêu

Thêm `ProductStatus { Ready = 0, Pending = 1 }` vào `Product` entity; `CreateProductRequest.PendingReview` → product tạo ra ở Pending; `ProductDetailDto.Status` cho management UI; migrations SQLite (ShopERP) + PG (VanAnDbContext).

## Files

| File | Thay đổi |
|---|---|
| `1_Shared/Domain.cs` | `enum ProductStatus` (namespace `VanAn.Shared.Domain`, gần Product ~line 709) + `Product.Status` property + ctor optional param + `MarkPending()`/`MarkReady()`/`SetStatus(ProductStatus)` |
| `3_CoreHub/Infrastructure/Configurations/ProductConfiguration.cs` | `builder.Property(p => p.Status).HasConversion<int>().HasDefaultValue(ProductStatus.Ready)` |
| `1_Shared/DTOs/ProductDetailDto.cs` | `public string Status { get; set; }` ("Ready"/"Pending") |
| `1_Shared/DTOs/CreateProductRequest.cs` | `public bool PendingReview { get; set; }` |
| `3_CoreHub/Services/ProductService.cs` | `CreateProductAsync`: sau ctor → `if (request.PendingReview) product.MarkPending()`; `MapToDto` += `Status = p.Status.ToString()` |
| `5_WebApps/ShopERP/Migrations/` | `dotnet ef migrations add AddProductStatus` (ShopERPDbContext, SQLite) |
| `3_CoreHub/Infrastructure/Migrations/` | `dotnet ef migrations add AddProductStatus` (VanAnDbContext, PG) |

## Quyết định kỹ thuật

- `Ready = 0` → `default(int)` + migration default = Ready → mọi row hiện có tự động Ready, zero backfill.
- `Product()` public ctor (UI layer) giữ nguyên — Status init `Ready`.
- Ctor đầy đủ (tenantId, name, description, price, category, isActive, imageUrl, vatRate, costPrice, isPosOnly): thêm trailing optional `ProductStatus status = ProductStatus.Ready` — backward compat mọi caller.
- `Update()` KHÔNG đụng Status (status lifecycle riêng, không qua edit form).
- Domain governance: change này thuộc approved plan (D1) — user đã chọn "Status field mới".

## Tests (TDD — viết trước)

`6_Tests/VanAn.Core.Tests/` — tìm test Product/ProductService hiện có, thêm:

1. `Product_Ctor_DefaultStatus_IsReady`
2. `Product_MarkPending_ThenMarkReady_StatusTransitions`
3. `ProductService_CreateProductAsync_PendingReviewTrue_StatusPending`
4. `ProductService_CreateProductAsync_PendingReviewFalse_StatusReady`
5. `ProductService_GetAllForManagementAsync_DtoContainsStatus` (nếu có test pattern sẵn)

## Acceptance

- [ ] Tests mới PASS, suite hiện có không regression
- [ ] 2 migrations generated + `dotnet ef migrations script` sanity (cột `Status` INTEGER NOT NULL DEFAULT 0)
- [ ] `dotnet build VanAn.sln` 0 err
