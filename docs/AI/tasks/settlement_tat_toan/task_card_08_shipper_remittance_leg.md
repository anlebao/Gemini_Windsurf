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

## ✅ RV PRODUCTION PASS (2026-09-22, `b5da7819` + `193006e6`, CD Multi-VPS SUCCESS)

L2: `RemitCodAsync`/`WithdrawalRequest` trong CoreHub.dll · `WithdrawalAdminController`/`wallet/remit` trong Gateway.dll · `WithdrawalApiClient`/`admin/withdrawals` trong ShopERP.dll · migration `WithdrawalRequests` applied.

L1 (fixtures trên tenant "Vạn An Test", shipper `6e4edec9`):
- Marketplace: confirm-cod sai amount → **409**; đúng 65000 → **200**; pending-remittances liệt kê đơn; remit → **200** (shipper 65000→0); double-remit → **409**. Legs: CODCollection +65000 shipper · Settlement −65000 tenant · **Remittance −65000 shipper · Settlement +65000 tenant** — ledger khép.
- Reseller (order CommerceMode=1, Sell 70000 + Fee 15000): confirm 85000 → **200**; remit → **200**. Legs: CODCollection +85000 shipper · Settlement +40000 tenant (giá vốn) · DeliveryFee +15000 shipper · PlatformFee +15000 Platform · CommunityFund +3000 · **Remittance −85000 shipper · Settlement +85000 → PlatformWallet** ✅ (Q1b đúng).
- Wallet summary: `codHeld`/`availableBalance` đúng (available loại trừ pending withdrawal).
- Cleanup: toàn bộ fixture xoá khỏi PG (orders/tasks/wallet/requests/outbox/journal), dev-token window đóng (env=0, endpoint 404).

**Bug phát hiện + fixed trong RV:** `CreateWalletTxCoreAsync` PG path — `FromSqlRaw … LIMIT 1 FOR UPDATE` compose bên trong tenant filter → latest tx dưới tenant khác bị lọc sau LIMIT → `balanceBefore=0` → BalanceAfter sai (−500000 thay vì đúng). Fix `193006e6`: `IgnoreQueryFilters` trên last-tx lookups (PG + SQLite + `GetBalanceAsync`) + regression test T46. Re-verify production: pay → **BalanceAfter=1,100,000 đúng** (1.6M − 500k).
