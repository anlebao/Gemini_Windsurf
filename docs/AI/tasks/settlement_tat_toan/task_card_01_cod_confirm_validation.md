# Task Card TC-01: COD Confirm — amount do server derive + validation + race fix

> **Status:** ⬜ PENDING (chờ duyệt)
> **Severity:** P0 — tiền (shipper tự khai số tiền thu hộ; race commit trùng)
> **Findings:** C3, C9 (phần race của confirm-cod)
> **Files:** `3_CoreHub/Services/WalletService.cs` (ConfirmCodAsync ~L162-225, ConfirmCodResellerAsync ~L232-345), `2_Gateway/Controllers/CommunityController.cs` (~L1003-1033)

## Root cause (verified)

1. `ConfirmCodAsync(shipperId, orderId, amount)` nhận `amount` từ client. Check `order.CodAmount.HasValue && != amount` **không bao giờ chạy** vì `CodAmount` chỉ được set bởi chính `MarkCodCollected` (`Domain.cs:1767-1774`) — luôn null ở lần confirm đầu → shipper khai số tiền tùy ý → cả `CODCollection` (+shipper) và `Settlement` (−shop / split Reseller) đều ghi sai.
2. Không check `Order.PaymentMethod` — có thể "confirm COD" cho đơn VietQR/external (double-count).
3. Không check `DeliveryTask.Status` — shipper confirm ngay sau Accept, không cần Delivered.
4. Không check `Order.Status` — đơn Cancelled vẫn confirm COD được.
5. **Race:** `CodCollectedAt` check ngoài transaction; mỗi `CreateTransactionAsync` commit transaction riêng (`WalletService.cs:54-107`). Double-click/concurrent → request thứ 2 tạo xong bộ wallet tx (đã commit) rồi mới `MarkCodCollected` throw → **API trả lỗi nhưng ví đã ghi trùng**.

## Fix approach (đề xuất)

- Server derive `expectedAmount`: Reseller = `SellPrice + DeliveryFee`; Marketplace = `TotalAmount` (hoặc field COD expected riêng nếu có). Nếu client truyền amount ≠ expected → 409.
- Thêm guards: `order.PaymentMethod == COD` (hoặc Reseller-external đi path riêng), `DeliveryTask.Status ∈ {OutForDelivery, Delivered}`, `order.Status != Cancelled`.
- Idempotency mạnh: unique index hoặc check-then-insert trong **cùng 1 transaction** với `MarkCodCollected` — wrap toàn bộ (order load → validate → tạo tx → mark) trong 1 DB transaction duy nhất thay vì N transaction riêng.
- Giữ `CodAmount` field = expectedAmount snapshot (audit).

## Tests

- Confirm COD với amount sai → 409, không tx nào được tạo.
- Confirm COD đơn VietQR → 409.
- Confirm COD khi task chưa Delivered → 409.
- Confirm COD đơn cancelled → 409.
- 2 concurrent confirm-cod cùng order → đúng 1 bộ tx tồn tại; request thua nhận 409.
- Reseller: COD amount derive = SellPrice + DeliveryFee.

## Acceptance

- [ ] Không còn path nào cho client quyết định số tiền COD.
- [ ] Double-submit không sinh tx trùng (kể cả khi API trả lỗi).
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.

## Risks / notes

- `TotalAmount` cho Marketplace COD có gồm phí ship? — verify `Order.TotalAmount` composition trước khi chọn expected formula.
- KHÔNG đổi semantics settlement (dấu/leg) trong card này — đó là TC-08 (cần Q1).
