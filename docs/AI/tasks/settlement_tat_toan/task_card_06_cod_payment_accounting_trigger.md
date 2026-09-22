# Task Card TC-06: Đơn COD — trigger accounting khi thu tiền thật + platform bookset

> **Status:** ✅ DONE + DEPLOYED + RV PRODUCTION PASS (2026-09-22, `80dfdfc3`) — **Q3 = MarkCodCollected → Paid**; **bookset = PG + replicate** (tái dùng `OrderPaymentConfirmed` → `PaymentConfirmedSubscriber`). RV: confirm-cod 65000 → order Paid/COD + ví + outbox + 1 bộ 511/3331/632 trên PG; shop-a SQLite order Paid + subscriber gen entries (accounting → PG qua AccountingConnection, dedup reference); duplicate → 409.
> **Severity:** P1 — sổ sách (toàn bộ doanh thu COD không vào sổ kế toán)
> **Findings:** B1, B5
> **Files:** `3_CoreHub/Services/WalletService.cs` (MarkCodCollected call sites ~L218, ~L337, ~L664), `3_CoreHub/Services/OrderService.cs` (ConfirmPaymentAsync ~L1304-1366, GenerateAccountingEntriesAsync ~L163-406, GetPlatformAccountingTenantIdAsync ~L413-431), `1_Shared/Domain.cs` (Order.ConfirmPayment ~L1873-1879, MarkCodCollected ~L1767-1774)

## Root cause (verified)

`GenerateAccountingEntriesAsync` (511/3331/632 + JournalEntry + Reseller 3-bookset) chỉ chạy khi `PaymentStatus → "Paid"` qua:
- POS `ConfirmPaymentAsync` (Payment.razor), hoặc
- `WebhookController` → `MarkPaidAsync` (VietQR/bank webhook).

Đơn COD (community commerce — luồng chính): `MarkCodCollected` chỉ set `CodAmount`/`CodCollectedAt`, **`PaymentStatus` không đổi** → không bao giờ có bút toán. Doanh thu COD = 0 trên sổ HKD.

Phụ (B5): Reseller platform bookset phụ thuộc `SystemSetting["PlatformAccountingTenantId"]` — chưa config → skip im lặng, platform fee income không hạch toán đâu cả.

## Implementation (2026-09-22 — đã duyệt)

- **Domain:** `Order.MarkCodCollected(codAmount, paymentMethod="COD", transactionId=null)` — set `PaymentStatus="Paid"` + `PaymentMethod` + `VietQR_TransactionId` (nếu có ref); thêm guard `PaymentStatus=="Paid"` → throw (chặn double-payment bất kể path).
- **PaymentMethodConstants:** thêm `Cod="COD"` (→111) + `External="EXTERNAL"` (→112).
- **WalletService** (Gateway-only service): ctor += optional `IOrderService`/`IOutboxRepository`/`IShopFeatureSettingsService` (không break tests/DI hiện có).
  - `ConfirmCodAsync`: trong cùng tx → `MarkCodCollected(amount, COD, shipperTx.Id)` + `EnqueueOrderPaymentConfirmedAsync` (payload giống `MarkPaidAsync`, routingKey=`Tenant.ShopInstanceId`, correlationId=orderId); sau commit → `TryGenerateOrderAccountingAsync` (best-effort, gated `Accounting_Sync_Enabled`, reload order với Items+Product cho COGS).
  - `ConfirmExternalPaymentAsync`: thêm guard `PaymentStatus=="Paid"` → throw; `MarkCodCollected(amount, EXTERNAL, paymentRef)` + outbox + accounting trigger tương tự.
- **Replicate SQLite:** không cần subscriber mới — `OrderPaymentConfirmed` → `vanan.cloud.order.payment.confirmed.{shopInstanceId}` → `PaymentConfirmedSubscriber` mark Paid + `GenerateAccountingEntriesAsync` trên SQLite (idempotent theo reference + toggle-aware).
- **B5:** reseller path thiếu `PlatformAccountingTenantId` → `LogWarning` (trước đây LogDebug — platform fee income bị skip im lặng).
- **Tests:** +7 (T26-T30 WalletServiceTests: Paid/COD/ref, outbox event, accounting call, toggle-off skip, accounting-failure non-fatal; T16-T17 DualMode: external Paid/EXTERNAL/ref + outbox + entries, already-paid reject).

## Fix approach (đề xuất — Q3)

**Đề xuất:** khi `MarkCodCollected` thành công (= thực thu, đúng TT 152 cash-basis) → order được coi là Paid → trigger accounting:

- **Domain change (cần approval):** `Order.MarkCodCollected` đồng thời set `PaymentStatus = "Paid"` + `PaymentMethod = "COD"` (hoặc method riêng `MarkCodPaid`). Nếu tách khỏi MarkCodCollected thì service gọi `order.ConfirmPayment(codTxId, "COD")`.
- Sau mark: `WalletService` (hoặc caller) gọi `IOrderService.GenerateAccountingEntriesAsync` — đã idempotent theo reference → POS/webhook confirm sau đó không double.
- Lưu ý DB context: ConfirmCod chạy trên **Gateway PG**; GenerateAccountingEntriesAsync hiện viết vào `IVanAnDbContext` của process đang chạy → cần quyết định entries COD ghi ở PG hay replicate xuống ShopERP SQLite (đề xuất: ghi PG + đã có DataSync order status; nếu cần tenant xem sổ trên ShopERP thì sync entries xuống — **scope decision, có thể để accounting entries chỉ tồn tại PG nếu BCTC render từ PG**).
- B5: thêm startup check/log warning khi `PlatformAccountingTenantId` chưa config + admin UI surface; Reseller orders không có platform bookset → liệt kê vào report.

## Tests

- COD order: delivered → confirm COD → PaymentStatus=Paid + có đủ 511/3331/632 entries (1 lần duy nhất).
- COD order sau đó webhook/POS confirm (edge) → không double entries (reference guard).
- Reseller COD → 3 bookset entries (supplier/reseller/platform khi config tồn tại).
- `Accounting_Sync_Enabled` OFF → vẫn mark paid nhưng skip entries (giữ semantics toggle).

## Acceptance

- [ ] Mọi đơn COD confirm xong có đầy đủ bút toán trên đúng bookset.
- [ ] Không double với path payment-confirm hiện có.
- [ ] Domain change có approval trước khi IMPLEMENT.
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.
