# Task Card TC-04: Cancel/Refund — đảo toàn bộ settlement + commission cho đơn hủy

> **Status:** ⬜ PENDING (chờ duyệt — kèm decision Q6 bật flag)
> **Severity:** P0 — tiền (đơn hủy/hoàn để lại tiền ảo trên ví; commission vẫn trả cho đơn hủy)
> **Findings:** C7
> **Files:** `3_CoreHub/Services/RefundOrchestrationService.cs` (~L126-141), `3_CoreHub/Services/CoolingPeriodJob.cs` (~L58-87), `3_CoreHub/Services/OrderWorkflowService.cs` (~L184-209), `1_Shared/Domain.cs` (SalesReferral ~L4317-4359)

## Root cause (verified)

1. `ValcnV2_RefundReversal` **default OFF** → cancel = silent cancel, không đảo gì.
2. Khi ON, Step 2d chỉ reverse `WalletTransactionType.Commission` — **không reverse** `CODCollection`/`Settlement`/`DeliveryFee`/`PlatformFee`/`CommunityFund`/`AdvancePayment`/`ExternalPayment` → đơn hủy sau COD confirm để lại toàn bộ split trên các ví.
3. `CoolingPeriodJob` trả commission chỉ theo `CommissionStatus` — **không check Order.Status** → đơn hủy trong 24h cooling vẫn được trả hoa hồng.
4. Sau reversal, `SalesReferral` giữ `Paid` — không `MarkRejected` → báo cáo commission sai (đã trả nhưng tiền đã thu hồi).
5. Idempotency refund check `existingEntries.Any(e => e.ReversalEntryId != null)` — nếu order chưa có revenue entry nào (COD chưa confirm payment — xem TC-06) → `allEntries` rỗng → reversal coi như "đã xử lý"? Thực tế `alreadyReversed=false` nhưng accrual skip vì không có revenue → chỉ 2c/2d chạy. Cần verify case order không có entry nào.

## Fix approach (đề xuất)

- Step 2d mở rộng: query **tất cả** WalletTransactions `RelatedOrderId == orderId` (trừ Reversal đã có) → tạo reversal cho từng tx (đúng owner, đúng amount). Giữ quy tắc append-only.
- Mark `SalesReferral` → `MarkRejected("Order cancelled/refunded")` khi cancel (kể cả Pending — chặn cooling job trả).
- `CoolingPeriodJob`: join/check order status trước khi trả (order Cancelled → mark referral Rejected, skip).
- Q6: sau khi coverage đủ → đề xuất default `ValcnV2_RefundReversal` = ON (hoặc bỏ flag).
- DeliveryTask status + `MarkCodCollected` guard: không cho confirm COD trên đơn Cancelled (overlap TC-01).

## Tests

- Reseller order confirm COD rồi cancel → mọi wallet tx của order có reversal đối ứng; balances về trạng thái trước đơn.
- Order cancel trong 24h → CoolingPeriodJob không trả; referral = Rejected.
- Marketplace: advance + COD rồi cancel → shipper/shop balances đảo đủ.
- Flag OFF → không đảo (behavior cũ); flag ON → đảo đủ; run 2 lần idempotent.

## Acceptance

- [ ] Cancel/refund đảo 100% wallet movements của đơn.
- [ ] Không commission nào trả cho đơn Cancelled.
- [ ] SalesReferral status khớp thực tế tiền.
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.
