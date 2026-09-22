# Task Card TC-02: Advance Payment — idempotency + cross-tenant hole

> **Status:** ✅ DONE + DEPLOYED + RV PASS (2026-09-22, `e9b4789a`) — ConfirmAdvance idempotent (1 advance/order, check trong transaction); ConfirmAdvanceReceived verify order.TenantId==caller. RV: advance dup→409, cross-tenant→reject không Settlement, same-tenant→200, dup→409. Note: cross-tenant trả 500 thay 403 → TC-10/S11.
> **Severity:** P0 — tiền (Reseller duplicate = in tiền; confirm-received cross-tenant)
> **Findings:** C4, C5
> **Files:** `3_CoreHub/Services/WalletService.cs` (ConfirmAdvanceAsync ~L351-415, ConfirmAdvanceReceivedAsync ~L421-454), `2_Gateway/Controllers/CommunityController.cs` (~L1041-1145)

## Root cause (verified)

1. **`ConfirmAdvanceAsync` không idempotent:** không có check advance đã tồn tại cho order → N calls = N tx.
   - Reseller path: mỗi call tạo `AdvancePayment(−amount, PlatformWallet)` + `Settlement(+amount, tenant)` → **duplicate call = chuyển tiền thật từ platform sang tenant nhiều lần** (`WalletService.cs:371-399`).
   - Marketplace path: shipper bị trừ N lần (`WalletService.cs:404-414`).
2. **`ConfirmAdvanceReceivedAsync` cross-tenant:** `shopOwnerId` = tenant của caller, nhưng **không verify `advanceTx.RelatedOrderId` thuộc tenant đó**. Bất kỳ customer nào biết `advanceTransactionId` đều confirm được → `+Settlement` vào ví tenant mình (sai chủ) + mark advance settled → chủ thật không còn thấy pending (`GetPendingAdvancesAsync` lọc theo settledSet).

## Fix approach (đề xuất)

- `ConfirmAdvanceAsync`: dedup — check đã có `AdvancePayment` tx với `RelatedOrderId == orderId` → throw 409 (idempotent return existing cũng OK). Làm trong cùng transaction.
- `ConfirmAdvanceReceivedAsync`: load `advanceTx` → load order theo `RelatedOrderId` → verify `order.TenantId == new TenantId(shopOwnerId)` → mismatch = 403. Settlement owner phải là `order.TenantId`, không phải tenant caller truyền mù.
- Cân nhắc composite idempotency key: unique filtered index `WalletTransactions(Type=AdvancePayment, RelatedOrderId)` — 1 advance/đơn (cần migration, duyệt).

## Tests

- Gọi confirm-advance 2 lần (cả 2 mode) → chỉ 1 bộ tx, lần 2 → 409/noop.
- Reseller: duplicate advance không tạo thêm `−PlatformWallet`/`+tenant`.
- Customer tenant A confirm advance của order thuộc tenant B → 403, không tx, advance B vẫn pending.
- Confirm-received đúng chủ → Settlement owner = tenant của order.

## Acceptance

- [ ] Advance idempotent theo order.
- [ ] Không path nào credit ví tenant sai chủ.
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.
