# Task Card TC-03: Reseller commission — trả 2 lần + bypass anti-fraud

> **Status:** ⬜ PENDING (chờ duyệt)
> **Severity:** P0 — tiền (commission Reseller trả ngay + trả lại sau 24h; bypass risk scoring)
> **Findings:** C6
> **Files:** `3_CoreHub/Services/OrderWorkflowService.cs` (~L264-294), `3_CoreHub/Services/WalletService.cs` (~L299-310, ~L627-637), `3_CoreHub/Services/CoolingPeriodJob.cs` (~L58-87), `3_CoreHub/Services/FraudReviewService.cs` (~L188-204), `3_CoreHub/Services/SalesmanService.cs` (CreateCommissionAsync ~L295-392)

## Root cause (verified)

1. `OrderWorkflowService.HandleOrderCompletedAsync` gọi `CreateCommissionAsync` cho **mọi** order có `SalesmanId + ReferralProductId` — kể cả Reseller → `SalesReferral` Pending.
2. `ConfirmCodResellerAsync`/`ConfirmExternalPaymentAsync` tạo `WalletTransaction(Commission)` **ngay** khi confirm — không đụng `SalesReferral`, không qua `IRiskScoringService`, không cooling → referral vẫn Pending.
3. `CoolingPeriodJob` (mỗi giờ) lấy referral Pending + RiskScore<60 + CreatedAt>24h → tạo Commission tx **lần 2** + `MarkCommissionPaid` → **salesman Reseller được trả 2 lần**.
4. Phụ: Reseller commission bypass hoàn toàn anti-fraud (self-referral không bị flag — Marketplace qua risk scoring ở `CreateCommissionAsync`).
5. Phụ: `FraudReviewService.ConfirmAsync` reverse `FirstOrDefault` commission tx của order → khi có 2 tx (immediate + cooling) chỉ đảo được 1.

## Fix approach (đề xuất — chọn 1 trong 2, cần duyệt)

**Option A (khuyến nghị):** Reseller không trả commission tại COD confirm. Bỏ bước Commission tx trong `ConfirmCodResellerAsync`/`ConfirmExternalPaymentAsync`; để `SalesReferral` + `CoolingPeriodJob` là đường duy nhất (đồng nhất Marketplace: risk scoring + 24h cooling). Balance invariant Reseller điều chỉnh: commission là khoản phải trả pending, không phải tx ngay.
**Option B:** Giữ trả ngay nhưng mark `SalesReferral` paid ngay (link `RelatedTransactionId`) + chạy risk scoring đồng bộ trước khi trả. Trade-off: bỏ cooling period → fraud không kịp review.

- `FraudReviewService`: reverse **tất cả** Commission txs của order+salesman (FirstOrDefault → list).
- Idempotency phụ: `CoolingPeriodJob` trước khi trả check đã có Commission tx `RelatedOrderId == referral.OrderId && OwnerId == salesmanId` → skip + mark paid (không tạo).

## Tests

- Reseller order có referral → COD confirm → đúng 1 commission tx tổng cộng sau 24h (Option A) hoặc referral được mark paid ngay (Option B).
- Reseller self-referral → không được trả (Rejected hoặc không tạo).
- Fraud confirm sau khi đã trả → reverse hết mọi commission tx của order.
- CoolingPeriodJob không trả trùng khi commission tx đã tồn tại.

## Acceptance

- [ ] 1 đơn = tối đa 1 commission payout/salesman (mọi mode).
- [ ] Reseller commission qua risk scoring + cooling như Marketplace (Option A) — hoặc ghi rõ quyết định nếu Option B.
- [ ] Fraud reversal đảo đủ số tx.
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.

## Notes

- Cần reconcile dữ liệu đã trả trùng trên production (nếu có): query SalesReferral Paid + count Commission tx/order > 1 → reversal entries, không sửa/xóa.
