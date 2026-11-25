# Task T5 — ProductManagement: pending badge + bulk approve

> **Status:** DONE · **Created:** 2026-11-25 · **Phase:** T5 (sau T2)
> **Plan:** D6 — `docs/AI/plans/product-pending-review-master-plan.md`
> **TDD:** filter/selection logic trước → UI → verify

## Mục tiêu

Owner quản lý sản phẩm chờ duyệt 1 lần nhiều món: thấy badge, lọc riêng, tick chọn → duyệt hàng loạt.

## Files

| File | Thay đổi |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Products/ProductManagement.razor` | `ProductDetailDto.Status` hiển thị: badge "Chờ duyệt" (warning) cạnh tên khi Pending; filter dropdown/tab "Tất cả / Đang bán / Chờ duyệt (N)"; checkbox column cho pending rows (reuse pattern `_selectedForPrint` ~L64 — tách `_selectedForApprove` riêng); nút "Duyệt đã chọn (N)" + "Duyệt tất cả đang chờ" trong toolbar khi có pending |
| `5_WebApps/ShopERP/Services/` (API client nếu page gọi HTTP) hoặc direct `IProductService` | Page hiện inject `ProductService` trực tiếp (L378 `GetAllForManagementAsync`) → gọi `BulkSetStatusAsync` trực tiếp, không cần HTTP client |

## UI spec

- Badge: `<span class="badge bg-warning text-dark">Chờ duyệt</span>` cạnh product name (hoặc cột trạng thái nếu grid có).
- Filter: VanAn select hoặc tabs — pending count dynamic.
- Checkbox chỉ hiện trên row Pending (row Ready không cần duyệt — hoặc hiện để "trả về pending"? phase này chỉ approve; un-approve optional skip).
- "Duyệt tất cả đang chờ" = bulk-status với toàn bộ pending ids (cap 500 — nếu >500 thì chunk hoặc disable + báo).
- Sau bulk xong: `ReloadAsync()` + alert "Đã duyệt N sản phẩm".

## Tests

- bUnit test nếu infra có (`VanAn.ShopERP.Tests/Components/`): render pending badge, filter, bulk button gọi service đúng ids.
- Nếu không có pattern → verify build + RV.

## Acceptance

- [ ] Pending rows có badge + checkbox; Ready rows không badge
- [ ] Filter "Chờ duyệt" chỉ hiện pending
- [ ] "Duyệt đã chọn" → gọi bulk-status ready → reload → badge mất, product hiện storefront
- [ ] "Duyệt tất cả" xử lý >0 pending; empty → nút disabled
- [ ] Build 0 err
