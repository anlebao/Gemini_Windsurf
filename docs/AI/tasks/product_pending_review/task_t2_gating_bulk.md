# Task T2 — Public gating + bulk-status endpoint

> **Status:** PENDING · **Created:** 2026-11-25 · **Phase:** T2 (sau T1)
> **Plan:** `docs/AI/plans/product-pending-review-master-plan.md` (D2)
> **TDD:** test fail trước → implement → green

## Mục tiêu

Pending products biến mất khỏi mọi anonymous/public product API; thêm endpoint bulk đổi status cho owner.

## Files

| File | Thay đổi |
|---|---|
| `5_WebApps/ShopERP/Controllers/ProductsController.cs` | Thêm `p.Status == ProductStatus.Ready` vào: `GetProducts` (~L52), `GetProduct` (~L95), `grouped-by-tenant` (~L134), `recommended` (~L185 — check query bên trong), `{id}/qr` (~L241), `validate-price` (~L304). `manage` KHÔNG filter. Endpoint mới `POST bulk-status` |
| `3_CoreHub/Services/IProductService.cs` + `ProductService.cs` | `Task<int> BulkSetStatusAsync(Guid tenantId, IReadOnlyList<Guid> productIds, ProductStatus status, CancellationToken ct)` — loop repo, chỉ đếm product thuộc tenant + tồn tại; trả số updated |
| `1_Shared/DTOs/` | `BulkProductStatusRequest { List<Guid> ProductIds; string Status }` (validate: ids 1..500, status ∈ ready/pending) |

## Endpoint spec

```
POST /api/products/bulk-status        [Authorize OwnerOnly]
{ "productIds": ["guid",...], "status": "ready" | "pending" }
→ 200 { updated: N } · 400 invalid · 401/403
```

- Tenant-scoped: `GetByIdAsync(productId, tenantId)` per item — product tenant khác bị skip (không lộ existence).
- `status="ready"` → `MarkReady()`; `"pending"` → `MarkPending()`; giá trị khác → 400.
- Cap 500 ids/request.

## Tests (TDD)

1. `ProductsController` public GET — pending product không xuất hiện (nếu có controller test infra; nếu không → test ở service/query level)
2. `BulkSetStatusAsync_OnlyOwnTenantProducts_CountsCorrectly`
3. `BulkSetStatusAsync_EmptyIds_ReturnsZero` / `>500 → reject`
4. `BulkSetStatusAsync_ReadyToPending_RoundTrip`

## Lưu ý verify thủ công các query

`recommended` dùng `CustomerRecommendationService` — check nó query Products ở đâu (có thể service riêng, phải thêm filter Status ở đó nữa nếu pending lọt vào recommendation).

## Acceptance

- [ ] Pending không trả về từ 6 public endpoints (verify bằng test hoặc curl sau deploy)
- [ ] bulk-status hoạt động tenant-scoped, audit log
- [ ] Build 0 err
