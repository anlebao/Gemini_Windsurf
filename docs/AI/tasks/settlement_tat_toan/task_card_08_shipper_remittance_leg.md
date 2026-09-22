# Task Card TC-08: Shipper remittance leg — khép kín dòng tiền COD

> **Status:** ✅ DONE (2026-09-22 — Settlement Batch-3; data reconcile lịch sử còn pending trên production)
> **Severity:** P0 — mô hình (ledger không bao giờ khép; shipper được coi như giữ credit vô hạn)
> **Findings:** C1, C2
> **Files:** `3_CoreHub/Services/WalletService.cs` (~L199-225 Marketplace, ~L270-345 Reseller), `1_Shared/Domain.cs` (WalletTransactionType ~L3729-3746), `docs/user-guide/community-commerce/04-shipper.md` (§9.1-9.2, §10), `2_Gateway/Controllers/CommunityController.cs`, `5_WebApps/KhachLink/Pages/Wallet.razor`, `DeliveryTracking.razor`

## Root cause (verified)

- **Spec (04-shipper.md §9.1):** settlement = `−shipper / +shop` khi shipper chuyển tiền COD cho shop. Net shipper = +COD −Settlement −Advance = phí giao hàng.
- **Implementation:** `ConfirmCodAsync` Marketplace chỉ tạo `Settlement(−amount, shop)` — **ngược dấu và thiếu cặp**; shipper giữ `+CODCollection` mãi, shop âm mãi.
- **Reseller:** shipper `+CODCollection(full COD)` + `+DeliveryFee`, KHÔNG có debit khi nộp tiền về Vạn An → ví shipper = platform nợ shipper cả tiền COD hộ.
- Không tồn tại tx type/endpoint nào cho "shipper nộp tiền" → ledger không khép ở mọi mode.

## Design decision Q1 — ĐÃ CHỐT (2026-09-22)

- **Q1a:** Nộp **theo đơn** — `POST /api/community/wallet/remit` (shipper, X-Customer-Token).
- **Q1b:** Reseller COD nộp về **PlatformWallet** (`SystemWalletIds.PlatformWallet`).

## Design decision Q1 (cần user chốt)

Đề xuất thêm `WalletTransactionType.Remittance` (hoặc tái dùng Settlement với chiều đúng):

| Mode | Trigger | Entries |
|---|---|---|
| Marketplace | Shipper nộp tiền mặt cho shop (hoặc qua platform) | `Remittance(−amount, shipper)` + `Settlement(+amount, shop)` |
| Reseller | Shipper nộp COD về Vạn An (định kỳ/từng đơn) | `Remittance(−codAmount, shipper)` + `Settlement(+codAmount, PlatformWallet)` |

Câu hỏi phụ: shipper nộp **theo đơn** hay **gom cuối ngày/kỳ** (batch net-off: nộp = ΣCOD − ΣAdvance − phí ship được giữ)? Ảnh hưởng UI + endpoint (`POST /api/community/wallet/remit` vs admin batch).

## Scope (sau Q1)

- Domain: thêm enum `Remittance` (+ nếu cần `DeliveryTask`/order field `CodRemittedAt`) — **domain approval required**.
- `WalletService.RemitCodAsync(shipperId, orderId|batch)` — verify shipper đã collect, chưa remit, tính amount, tạo cặp tx trong 1 transaction.
- Endpoint + UI (DeliveryTracking/Wallet): nút "Nộp tiền COD" + badge "đang giữ Xđ chưa nộp".
- `GetWalletAsync`/admin report: tách "COD đang giữ hộ" (chưa remit) khỏi "tiền thật của shipper" (DeliveryFee) — **số dư khả dụng ≠ số dư ledger**.
- **Data reconcile (bắt buộc):** wallet tx lịch sử đã ghi sai dấu/thiếu leg → reversal entries + report (append-only, không sửa). Marketplace Settlement −shop hiện có: cần quyết định đảo hay giữ như "marker".

## Tests

- Marketplace: collect COD → remit → shipper balance = 0 (+fee nếu có), shop balance đúng.
- Reseller: collect → remit → shipper balance = DeliveryFee only; PlatformWallet += COD.
- Double remit → 409.
- Balance khả dụng hiển thị đúng (không gồm tiền giữ hộ).

## Acceptance

- [ ] Mọi COD collected có leg nộp về đúng chủ → ledger khép.
- [ ] Shipper balance sau remit chỉ còn phần thực nhận.
- [ ] Dữ liệu cũ được reconcile bằng reversal, có report.
- [ ] Build 0 errors · guard-check PASS · Core.Tests PASS.
