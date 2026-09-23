# Task Card 185-5: Chỉ đơn "giao hàng" mới show địa chỉ + map GPS + bắt buộc SĐT người nhận

> **Status:** DONE 2026-09-23 — commit 8a47f40a (SĐT bắt buộc khi DELIVERY, address ẩn khi non-delivery)
> **Priority:** P1 — shipper không gọi được khách = đơn giao thất bại
> **Created:** 2026-09-23
> **Master plan:** `docs/AI/tasks/issue_185_loyalty/master_plan.md`
> **Effort:** 1-2h + E2E spec

## Problem

Issue yêu cầu: chỉ khi đơn loại "giao hàng" mới show "Địa chỉ nhận hàng", map GPS, và **bắt buộc nhập SĐT người nhận**.

## Verify (VERIFIED — `KhachLink/Pages/Checkout.razor`)

| Yêu cầu | Hiện trạng | Kết luận |
|---|---|---|
| Map GPS chỉ khi DELIVERY | `@if (selectedOrderType == "DELIVERY")` (line ~383/192) — đã đúng | ✅ Done |
| Địa chỉ chỉ khi DELIVERY | Field **luôn hiển thị**, chỉ đổi label `*` vs "(giao hàng)" (line ~357-375/180-186); required khi DELIVERY (line 633) | ⚠️ Cần ẩn field khi không DELIVERY |
| SĐT bắt buộc cho đơn giao hàng | SĐT chỉ required khi `selectedPaymentMethod == "transfer"` (line 618-630). **DELIVERY + cash → đặt được không cần SĐT** | ❌ BUG — gap thật |

## Solution

### Phase A — Checkout.razor
- [ ] A1: Bọc address field trong `@if (selectedOrderType == "DELIVERY")` (hiện tại label đã conditional — gộp luôn field). Non-delivery không render input address.
- [ ] A2: Label SĐT thêm `*` khi `selectedOrderType == "DELIVERY"` (ngoài `transfer` hiện có).
- [ ] A3: Validation: thêm `if (selectedOrderType == "DELIVERY" && string.IsNullOrWhiteSpace(guestPhone))` → error "Vui lòng nhập số điện thoại người nhận khi chọn giao hàng." (đặt TRƯỚC/SAU check address hiện tại, line ~625-637).
- [ ] A4 (kiểm tra): `selectedOrderType` đổi sang non-DELIVERY → `guestAddress` cũ không được gửi (line 763 đã `selectedOrderType == "DELIVERY" ? guestAddress : null` ✓ — chỉ cần confirm không regression).
- [ ] A5 (kiểm tra): logged-in user path — `guestPhone` prefill từ profile (line ~508) → validation vẫn pass với profile có SĐT; profile thiếu SĐT + DELIVERY → bắt nhập.

### Phase B — Server-side guard (đề xuất, cần duyệt scope)
- Gateway `CheckoutOrderRequest`: khi `OrderType == "DELIVERY"` reject nếu `CustomerPhone` rỗng → 400. Client-side-only validation bypass được (defense in depth). Nếu duyệt thì làm; nếu không ghi nhận client-only.

### Phase C — E2E (Gate 4 — UI layout change bắt buộc)
- [ ] C1: spec `6_Testing/e2e-tests/` — TAKEAWAY: không thấy address/map, checkout OK không SĐT · DELIVERY cash: submit không SĐT → lỗi hiển thị · DELIVERY + SĐT → OK.
- [ ] C2: unit-level không áp dụng (Razor UI); dựa E2E + manual.

## Acceptance
- [ ] DELIVERY+cash không SĐT → chặn + message rõ
- [ ] DINEIN/TAKEAWAY không thấy address + map
- [ ] E2E spec pass
