# Task Card #178: Referral products QRcode — config + danh sách

> **Status:** Phần 1 ✅ FIXED + DEPLOYED + RV PRODUCTION PASS 2026-09-19 (POST rate=0 → 201) · Phần 2 ✅ CODE DONE (`6ab09cbc`, Batch 2) — chờ deploy + RV
> **Priority:** P1 (phần 1 — config) · P2 (phần 2 — feature)
> **Created:** 2026-09-19
> **Master plan:** `docs/AI/tasks/issues_176_181/master_plan.md`
> **Effort:** 6-8h (1-2h fix config + 4-6h feature)

## Problem (issue #178)

1. Referral config KHÔNG lưu được khi `CommissionRate = 0` hoặc `AppInstallBonus = 0` (sản phẩm free/charity). Đề xuất: default `Commission Rate = 0.01`, `App Install Bonus (VND) = 1.000`, phạm vi 0 → 0.5.
2. Salesman đã tạo QR referral product phải được lưu thành danh sách riêng (gian hàng kinh doanh của salesman) — add/remove khi cần, link đồng bộ giá khi giá sản phẩm đổi.

## Root cause (VERIFIED — phần 1)

`1_Shared/Domain.cs:4400-4401` (constructor) + `:4414-4415` (Update):
```csharp
if (commissionRate < 0.02m || commissionRate > 0.05m)
    throw new ArgumentOutOfRangeException(...)  // "must be between 0.02 and 0.05 (2-5%)"
```
→ rate=0 (free/charity) → exception → `ProductReferralConfigController.cs:50-52` → **400** → save fail. `AppInstallBonus` min 0 đã OK.

Phần 2: QR composite là **deterministic** (`SalesmanService.cs:148-149` — `$"{SalesmanCode}|{ProductShortCode}"`) → không cần storage mới; thiếu UI list cho salesman.

## Solution

### Phase A — Domain + config (cần IMPLEMENT approval — Domain change)
- [ ] A1: `Domain.cs ProductReferralConfig` — range `CommissionRate` → `0m..0.5m` (0-50%), default đề xuất 0.01/1.000. Ghi chú: base tính hoa hồng cho free/charity = 0 → commission 0đ (không lỗi, chỉ là 0).
- [ ] A2: Update tests `6_Tests/VanAn.Core.Tests/Community/ProductReferralConfigServiceTests.cs` (range cũ 0.02-0.05).
- [ ] A3: Update UI form (`ProductReferralConfigs.razor`): default 0.01 + 1.000, validation hint 0-0.5.

### Phase B — Feature: "Gian hàng của tôi" (salesman QR list)
- [ ] B1: API — `GET /api/community/salesman/products` (list sản phẩm salesman đã config referral: product + config + composite code + qrUrl + giá hiện tại từ catalog) — dựa `SalesmanService.GetSalesmanReferralProductsAsync` (mới) — tham chiếu `GetNearbyReferralProductsAsync` + `GetCompositeSalesmanQrAsync`.
- [ ] B2: KhachLink trang `/community/salesman-store` (hoặc `/community/nearby-products` mở rộng): danh sách sản phẩm đã tạo QR + nút Tải QR / Xóa config / "Đồng bộ giá" (re-fetch catalog → hiển thị giá mới + cảnh báo QR cũ vẫn trỏ đúng sản phẩm vì composite code không đổi).
- [ ] B3: Add config từ trang (chọn product chưa config → tạo `ProductReferralConfig`).
- [ ] B4: Remove config (DeactivateAsync — soft).

## Acceptance
- [ ] Lưu config rate=0 (free/charity) OK · range 0-0.5
- [ ] Salesman thấy list QR sản phẩm của mình, tải QR, xóa, đồng bộ giá
