# Task P10 — Credit Lots + Expiry Engine (metering core)

**Status:** Approved — user chốt 2026-11-19
**Depends on:** P7 (wallet/topup), P8/P9 (Tingee payment — không đổi)
**Mode:** IMPLEMENT + TDD

## Mục tiêu

Ví credit hỗ trợ **nhiều lot song song**, mỗi lot có hạn dùng riêng. Tiêu thụ theo **FEFO**
(lot hết hạn sớm nhất trước). Credit hết hạn bị tịch thu qua ledger `Adjust(-)` append-only.
Nạp tự do (custom amount) tạo lot **không hết hạn** — đắt hơn gói (pricing differentiation
được xử lý ở P11, P10 chỉ cần `ExpiresAtUtc=null`).

## Bối cảnh / hiện trạng

- `CreditWallet.BalancePurchased` scalar; grant/deduct/adjust mutate trực tiếp.
- `CreditLedgerEntry` append-only; balance = Σ Credits.
- Deduct-at-ingest: conditional UPDATE clamp, thiếu → `UsageEvent.UnpaidCredits`.
- Chưa có khái niệm lot/expiry. `TopupRequest.ExpiresAtUtc` chỉ là TTL pending request.
- SQLite + EF Core; relational path dùng raw SQL conditional UPDATE (decimal stored TEXT,
  phải CAST — xem `WalletService.DeductInTransactionAsync`).

## Quyết định đã chốt

| # | Quyết định | Lựa chọn |
|---|---|---|
| D1 | Lot hết hạn | `ExpiresAtUtc = null` → không hết hạn (nạp tự do / admin grant mặc định) |
| D2 | Thứ tự tiêu thụ | FEFO: `ExpiresAtUtc ASC NULLS LAST`, tie-break `CreatedAtUtc ASC` |
| D3 | Hết hạn xử lý | Lazy write-off: khi Check/Deduct/GetWallet chạm tenant → expire lots quá hạn, ghi 1 `Adjust(-)` ledger entry/lot (idempotent qua `ExpiredWrittenOffAtUtc`) + sweep endpoint optional |
| D4 | Balance semantics | Spendable = Σ `CreditsRemaining` của lots **còn hạn**. `BalancePurchased` scalar vẫn được cập nhật đồng bộ (= spendable) để tương thích check/alert/DTO hiện có |
| D5 | Backfill | Migration: mỗi wallet hiện có → 1 lot "legacy" `ExpiresAtUtc=null`, `CreditsRemaining = BalancePurchased` tại thời điểm migrate |
| D6 | Adjust/Deduct thủ công | `AdjustAsync` admin: trừ vào lot FEFO trước (hoặc lot không hạn cuối); không cho adjust làm lot âm |
| D7 | UnpaidCredits | Giữ nguyên — undercharge vẫn ghi `UnpaidCredits`, không trừ lot |

## Domain model

```csharp
public sealed class CreditLot
{
    public Guid Id;
    public Guid TenantId;
    public Guid? TopupRequestId;      // null = admin grant / legacy backfill
    public decimal CreditsGranted;
    public decimal CreditsRemaining;  // giảm khi deduct/adjust tiêu thụ
    public DateTime? ExpiresAtUtc;    // null = không hết hạn
    public DateTime? ExpiredWrittenOffAtUtc; // null = chưa write-off; set = đã ghi Adjust
    public DateTime CreatedAtUtc;
}
```

## Thay đổi

### Domain (`VanAn.Metering.Domain`)
- `CreditLot.cs` mới (pure, private setters, factory `Create`/`Consume`/`Expire`).
- `CreditWallet`: thêm helper `SpendableBalance` không bắt buộc — P10 giữ `BalancePurchased`
  làm spendable đã sync; lots là source of truth chi tiết.

### Infra (`VanAn.Metering.Infra`)
- `MeteringDbContext`: `DbSet<CreditLot>` + config (index `(TenantId, ExpiresAtUtc)`,
  `(TenantId, CreatedAtUtc)`).
- `WalletService`:
  - `GrantAsync(..., DateTime? expiresAtUtc = null, Guid? lotTopupId = null)` → tạo lot +
    BalancePurchased/GrantedTotal như cũ.
  - `DeductInTransactionAsync` → sau khi tính `applied`, phân bổ trừ FEFO vào lots
    (cùng transaction; conditional per-lot UPDATE `CreditsRemaining >= take` hoặc
    in-memory cho test provider).
  - `ExpireLotsAsync(tenantId, nowUtc, ct)` — private, gọi từ Check/GetWallet/Deduct:
    lots `ExpiresAtUtc <= now && ExpiredWrittenOffAtUtc == null` → set flag +
    `Adjust(-remaining)` ledger entry reason `"expired:{lotId}"`, trừ BalancePurchased.
    Idempotent: flag chặn double write-off.
  - `AdjustAsync` → phân bổ delta âm vào lots FEFO (không hết hạn cuối cùng).
- `TopupService.ConfirmPaidCoreAsync` / `ConfirmAsync` → truyền `expiresAtUtc=null`
  cho P10 (P11 sẽ truyền package validity).
- `Program.cs`: optional `POST /v1/admin/wallets/{tenantId}/expire-sweep` (admin caller)
  để chủ động write-off tất cả tenant (ops tool).
- Schema: `EnsureP7Schema` + fresh DDL thêm bảng `CreditLots` + backfill INSERT.

### Contracts
- `WalletDto` thêm `IReadOnlyList<CreditLotDto>? Lots` (optional — ShopERP hiển thị
  "còn X credits hết hạn Y"). `CreditLotDto { CreditsRemaining, ExpiresAtUtc }`.
- Ledger entry reason cho expiry: `expired:{lotId}` — UI hiển thị "Credit hết hạn".

## Test plan (TDD — RED trước)

File mới `tests/VanAn.Metering.Tests/CreditLotTests.cs`:

1. `Grant_CreatesLot_BalanceMatches`
2. `Deduct_FEFO_ConsumesEarliestExpiryFirst` — 2 lots (exp sớm/hạn), deduct → lot sớm trừ trước
3. `Deduct_NonExpiringLot_Last` — lot `ExpiresAtUtc=null` chỉ bị trừ khi hết lot có hạn
4. `Expiry_WriteOff_RemovesRemainingBalance` — lot quá hạn → GetWallet balance giảm, ledger có Adjust âm
5. `Expiry_Idempotent_NoDoubleWriteOff` — gọi Check 2 lần → chỉ 1 Adjust entry
6. `Expiry_PartialUse_OnlyRemainingForfeited` — lot 100, dùng 30, hết hạn → forfeit 70
7. `Check_Deny_AfterExpiry` — balance đủ trước hạn, sau hạn → deny=true
8. `Adjust_NeverMakesLotNegative`
9. `ConcurrentDeduct_LotsNotOverspent` — 2 deduct song song không vượt tổng remaining
10. `Backfill_LegacyWallet_GetsNonExpiringLot` (nếu test được schema upgrade path)
11. Regression: toàn bộ test P7/P8/P9 hiện có vẫn pass (124+ tests)

## Không làm (P11 lo)

- Package catalog, `TopupRequest.PackageCode`, giá gói riêng, validity từ package.
- UI ShopERP package picker, suspend-page guidance, banner CTA.

## RV sau deploy

- Tạo topup → confirm → wallet có lot.
- Admin grant với `expiresAtUtc` quá khứ → Check → balance về 0 + ledger Adjust.
- Query `CreditLots` table trên VPS verify FEFO allocation.
