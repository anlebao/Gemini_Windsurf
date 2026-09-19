# Task Card #177: Đơn hàng sản phẩm free/charity bị lỗi

> **Status:** ✅ IMPLEMENTED + DEPLOYED 2026-09-19 (client-only fix, theo quyết định) — UI test (L3/L4) pending
> **Priority:** P1 — chặn đặt hàng free/charity
> **Created:** 2026-09-19
> **Master plan:** `docs/AI/tasks/issues_176_181/master_plan.md`
> **Effort:** 4-6h

## Problem

"Đơn hàng của sản phẩm free hoặc charity bị lỗi" (body issue trống — cần RV bắt error path thật).

## Root cause (VERIFIED — 2 gap)

**Gap A — Referral QR bỏ `IsFree`:** `Scan.razor:289-299` (`ProcessReferralCodeAsync`) build `ProductDto` từ `ReferralScanDto` nhưng KHÔNG gán `IsFree`/`ProductType` dù Gateway đã trả đúng (`SalesmanService.cs:472` — `IsFree = fp.ProductType != Paid`; DTO có field `IsFree` — `CommunityHttpService.cs:784`).
→ Hệ quả: cart item `IsFree=false`. Nếu product KHÔNG nằm trong FeaturedProducts → Gateway Tier 0 (`PublicOrdersController.cs:157-159`) reject "giá không hợp lệ". Nếu có featured → đơn vẫn tạo nhưng C2/C3 UI hỏng (payment selector hiện cho giỏ 0đ, mất bước "Quyên góp từ thiện").

**Gap B — Product QR giá 0:** fast-path `Scan.razor:372` `if (qrPayload.UnitPrice > 0)` loại QR giá 0 → legacy path `GetProductsAsync` — ShopERP product API **không có ProductType** → `IsFree=false` → free/charity không featured → Tier 0 reject.

## Evidence

```
Scan.razor:289  var product = new ProductDto { ProductId=..., Price=resolved.Price, ... }  // thiếu IsFree
Scan.razor:372  if (qrPayload.UnitPrice > 0 && ...) → fast path; QR 0đ rơi vào legacy path
PublicOrdersController.cs:157  bool isFree = item.IsFree || serverFreeMap.GetValueOrDefault(...)  // serverFreeMap chỉ từ FeaturedProducts
CartState.cs:44  IsFree = product.IsFree  // phụ thuộc client set đúng
```

## Solution

### Phase A — Client IsFree propagation
- [ ] A1: `Scan.razor` — gán `IsFree = resolved.IsFree` (+ `ProductType` nếu DTO bổ sung) khi build `ProductDto` từ referral scan.
- [ ] A2: Legacy product-QR path — khi `Price == 0`, enrich `IsFree` từ catalog featured (`CatalogService.GetRecommendedAsync`) — mirror Store.razor:751-768; fallback giữ `IsFree=true` khi giá 0 (sản phẩm 0đ hiếm khi là paid).
- [ ] A3: Verify C2/C3 hoạt động cho cả 3 entry: store page, product QR, referral QR.

### Phase B — Gateway Tier 0 fallback (cần duyệt tradeoff)
- [ ] B1 (đề xuất): Khi item `UnitPrice=0` + client `IsFree=true` + KHÔNG có FeaturedProduct khớp → accept + `LogWarning` (thay vì reject). Tradeoff: client có thể spoof 0đ — nhưng đơn 0đ không tạo doanh thu, rủi ro thấp; sản phẩm free/charity là dữ liệu tenant ở ShopERP (Gateway không truy vấn được — Option C).

### Phase C — RV + tests
- [ ] C1: RV production — đặt mua free (Store page + QR product + QR referral) và charity (bước donation) end-to-end.
- [ ] C2: Test unit cho Scan.IsFree mapping + Gateway Tier 0 fallback.

## Acceptance
- [ ] 3 path đặt mua free/charity tạo được đơn, payment selector ẩn đúng (C2), bước donation đúng (C3)
