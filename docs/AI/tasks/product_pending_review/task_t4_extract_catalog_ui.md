# Task T4 — TenantAiKnowledge: extract → products Pending

> **Status:** DONE · **Created:** 2026-11-25 · **Phase:** T4 (sau T1 + T3)
> **Plan:** D4 + D5 — `docs/AI/plans/product-pending-review-master-plan.md`
> **TDD:** extract logic testable → UI wiring → verify build

## Mục tiêu

Trong `/admin/tenant-ai/knowledge`, sau khi extract xong: owner tick "Đưa vào danh mục sản phẩm (chờ duyệt)" → items kind=product/service tạo `Product` Status=Pending trong catalog tenant (cùng lúc doc vẫn vào knowledge pending như cũ).

## Files

| File | Thay đổi |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Admin/TenantAi/TenantAiKnowledge.razor` | Inject `IProductService` (VanAn.CoreHub.Services — check tên inject trong ProductManagement); preview items += hiển thị `Price` (nếu có) + badge kind; checkbox `_importProductsToCatalog` (default true) + label giải thích; `ConfirmExtractAsync` + `ConfirmGroupsAsync`: sau ingest OK → `ImportExtractedProductsAsync(items)` |
| (optional) `5_WebApps/ShopERP/Services/` | Helper nhỏ `ExtractedProductImporter` nếu logic đủ phức tạp để test — wrap: filter kind∈{product,service} + name non-empty → `CreateProductAsync(new CreateProductRequest { Name=Title, Description=Content (cap?), Price=Price??0, Category="Menu", PendingReview=true })`; dedupe theo tên trong batch; per-item try/catch (1 lỗi không chặn batch); trả `{created, skipped}` |

## Quyết định kỹ thuật

- **Category:** cố định `"Menu"` phase này (owner đổi sau trong ProductManagement). Nếu muốn đẹp hơn: select category từ categories hiện có — optional, default "Menu".
- **Description:** `item.Content` (đã cap 500 chars từ extractor).
- **Dedupe:** trong 1 batch theo `Name` trim+lower; KHÔNG check trùng với product đã có (owner tự dọn — tránh false-positive chặn import).
- **TenantId:** `TenantProvider.TenantId` — page StaffOrAbove, tenant context sẵn.
- **Groups flow:** chỉ group `menu` (items kind product/service trong group đó) → cùng helper; groups khác bỏ qua.
- **Ordering:** ingest doc trước (giữ nguyên), product import sau — product fail không rollback doc (log warning + alert note "X sản phẩm đã vào danh mục chờ duyệt, Y bỏ qua").
- `Price ?? 0m`, `VatRate` default 0.10, `CostPrice` 0.

## UI spec (preview section ~L188-225)

- Checkbox trên đầu preview: ☑ "Đưa N sản phẩm/dịch vụ vào danh mục (chờ duyệt)" — disabled khi 0 items product/service.
- Item rows: kind badge + title + content + `💰 {price:N0}đ` nếu Price.HasValue.

## Tests

- Nếu tách `ExtractedProductImporter` → unit test: filter kinds, dedupe, per-item failure isolation, PendingReview flag set.
- Nếu giữ inline trong razor → verify bằng build + manual/RV (ghi chú trong card).

## Acceptance

- [ ] Extract xong → tick → confirm → N products Pending trong `/products` (badge Chờ duyệt) + doc vẫn pending-review
- [ ] Bỏ tick → không tạo product (chỉ doc như cũ)
- [ ] Item kind faq/policy/hours/other KHÔNG thành product
- [ ] Product tạo có đúng Name=Title, Price=item.Price??0, Status=Pending
- [ ] Build 0 err
