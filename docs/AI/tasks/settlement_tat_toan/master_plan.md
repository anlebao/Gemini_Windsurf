# Master Plan — Settlement (Tất toán) Review Findings

**Created:** 2026-09-22
**Status:** REVIEW COMPLETE — findings listed, awaiting user approval per batch before IMPLEMENT
**Branch target:** `main`
**Source:** REVIEW_ONLY session 2026-09-22 — rà soát tất toán Salesman–Shipper–Owner Tenant–Platform + mức độ đổ số liệu về kế toán

## Summary

Hệ thống có **2 sổ sách tách rời không cầu nối**: `WalletTransaction` (PG — sổ tất toán giữa các role) và `AccountingEntry`/`JournalEntry` (sổ kế toán HKD). Kết quả:

- **Kế toán thiếu:** đơn COD không bao giờ có bút toán 511/3331/632 (không qua payment-confirm path); toàn bộ 13 loại giao dịch ví (COD, Settlement, Advance, Commission, PlatformFee, CommunityFund, DeliveryFee, ExternalPayment, Withdrawal, Deposit, SmsOtpFee, Reversal) không tạo bút toán nào.
- **Kế toán sai:** `SimpleAccountingEventHandler` ghi doanh thu TRÙNG 2 lần mỗi đơn, amount gross (gồm VAT+ship), không dedup, không period-guard.
- **Settlement sai:** Marketplace settlement ngược dấu so với spec; Reseller credit shipper cả tiền COD không có leg nộp về; không tồn tại remittance/payout nào.
- **Lỗ hổng tiền trực tiếp:** Reseller commission trả 2 lần; ConfirmAdvance không idempotent (Reseller = in tiền); ConfirmAdvanceReceived không check tenant (cross-tenant credit); shipper tự khai số tiền COD.

## Findings → Task Card mapping

| Card | Findings | Severity | Mode yêu cầu |
|---|---|---|---|
| [TC-01](task_card_01_cod_confirm_validation.md) | C3 (shipper tự khai amount), C9-race (double confirm commit trước khi MarkCodCollected throw), thiếu check PaymentMethod/DeliveryTask status/order status | P0 — tiền | IMPLEMENT |
| [TC-02](task_card_02_advance_idempotency_crosstenant.md) | C4 (ConfirmAdvance không idempotent, Reseller in tiền), C5 (ConfirmAdvanceReceived cross-tenant) | P0 — tiền | IMPLEMENT |
| [TC-03](task_card_03_reseller_double_commission.md) | C6 (Reseller commission trả 2 lần + bypass risk scoring + FraudReview chỉ reverse FirstOrDefault) | P0 — tiền | IMPLEMENT |
| [TC-04](task_card_04_refund_settlement_reversal.md) | C7 (cancel không đảo settlement tx, CoolingPeriodJob trả hoa hồng đơn hủy, flag default OFF, SalesReferral status stale) | P0 — tiền | IMPLEMENT (flag decision cần duyệt) |
| [TC-05](task_card_05_accounting_handler_dedup.md) | B2 (revenue ×2 gross), B3 (không idempotent, dual-subject), B6 (cash-basis sai timing) | P1 — sổ sách | IMPLEMENT + quyết định retire hay fix |
| [TC-06](task_card_06_cod_payment_accounting_trigger.md) | B1 (COD không qua GenerateAccountingEntriesAsync), B5 (PlatformAccountingTenantId chưa config → skip im lặng) | P1 — sổ sách | IMPLEMENT (domain method cần duyệt) |
| [TC-07](task_card_07_wallet_accounting_bridge.md) | B4 (wallet → 0 bút toán: commission 641, platform fee, fund, COD receivable, advance, withdrawal...) | P1 — sổ sách | ANALYZE → duyệt mapping trước |
| [TC-08](task_card_08_shipper_remittance_leg.md) | C1 (Marketplace settlement sai dấu/thiếu leg), C2 (Reseller thiếu leg nộp về platform) | P0 — mô hình | **DESIGN DECISION trước** (xem §Decisions) |
| [TC-09](task_card_09_payout_withdrawal_flow.md) | C8 (không có Withdrawal/payout/remittance — docs hứa nhưng code không có) | P1 — feature thiếu | **DESIGN DECISION trước** |
| [TC-10](task_card_10_settlement_admin_hardening.md) | D1 (isShopOwner sai), D2 (TenantId.Value LINQ Pattern #8), C10 (paymentRef/deliveryFee task), D3 (margin invariant/rounding/net profit), D4 (cooling job partial), D6 (commission display), D7 (perf), D8 (audit), D9 (first-tx race), D10 (UI Platform) | P2 | IMPLEMENT |

## Decisions cần user duyệt TRƯỚC khi implement

| # | Quyết định | Ảnh hưởng |
|---|---|---|
| **Q1** | Semantics ví shipper COD: `CODCollection +amount` nghĩa là gì — "shipper đang GIỮ tiền hộ" (phải nợ) hay "platform nợ shipper"? Hiện ledger ghi +shipper nhưng không có leg nộp → đáp án quyết định TC-08 thiết kế như thế nào. Đề xuất: thêm tx `Remittance` — Marketplace: −shipper/+shop khi nộp; Reseller: −shipper/+PlatformWallet khi nộp về Vạn An. | TC-08, domain enum mới |
| **Q2** | `SimpleAccountingEventHandler`: **retire** (OrderService path đã đủ 511/3331/632 + JournalEntry) hay **fix** (dedup + net amount)? Legacy handler đang ghi trùng gross trên PG. Đề xuất: retire hoặc gate OFF mặc định. | TC-05 |
| **Q3** | COD order khi nào ghi nhận doanh thu: lúc `MarkCodCollected` (thực thu đúng TT 152) hay lúc delivered? Đề xuất: MarkCodCollected → set PaymentStatus=Paid → trigger GenerateAccountingEntriesAsync. **Domain modification** (Order) — cần approval. | TC-06 |
| **Q4** | Payout flow: ai duyệt, theo kỳ hay tức thời, min amount, bằng chứng chi? Docs đã hứa KYC + min 500k nhưng chưa có thiết kế. Đề xuất: WithdrawalRequest entity (Pending→Approved→Paid) + admin endpoint. **Domain mới** — cần approval. | TC-09 |
| **Q5** | Wallet→Accounting bridge: map nào? Đề xuất tối thiểu: Commission→641 expense (platform tenant), PlatformFee→511 (platform tenant), CommunityFund→3388 (phải chi quỹ), DeliveryFee→641, COD shipper-held→138/131. Ghi ở tenant nào (supplier/reseller/platform)? | TC-07 |
| **Q6** | `ValcnV2_RefundReversal` flag: bật default ON sau khi TC-04 hoàn thiện reversal coverage? Hiện default OFF = silent cancel. | TC-04 |

## Thứ tự triển khai đề xuất

### Batch 0 — Decisions (user)
Chốt Q1–Q6. Không code.

### Batch 1 — Money-safety hotfixes (P0, độc lập, có thể deploy riêng)
TC-01 → TC-02 → TC-03 → TC-04. Mỗi card: fix + Core.Tests + guard-check + build.

### Batch 2 — Accounting correctness (P1)
TC-05 (handler dedup/retire — chặn nhân đôi trước) → TC-06 (COD → accounting) → TC-07 (wallet bridge, cần Q5).

### Batch 3 — Settlement lifecycle (P0 mô hình, cần Q1/Q4)
TC-08 (remittance leg) → TC-09 (payout flow).

### Batch 4 — Hardening (P2)
TC-10 (admin + misc fixes, có thể tách nhỏ theo sub-item).

## Hard stop checks

- **Domain modifications** (Order.MarkCodCollected → PaymentStatus, WalletTransactionType enum mới `Remittance`, entity `WithdrawalRequest` mới) — chỉ trong IMPLEMENT + user approval (Q1/Q3/Q4).
- **AccountingEntry immutability** — mọi correction qua Reversal entry, không sửa entry cũ.
- **Data migration** cho wallet tx đã ghi sai (shop âm, shipper dương tích lũy) — CẦN migration plan riêng trong TC-08, không âm thầm rewrite ledger (append-only → reversal entries).
- **PG duplicate revenue entries đã tồn tại** (B2) — cleanup = reversal entries hoặc SQL cleanup có duyệt, không xóa trực tiếp.
- **FIX_ONLY guard:** các card P0 không được mở rộng scope sang redesign.

## Acceptance criteria (tổng)

- [ ] Mọi wallet multi-tx flow atomic hoặc có idempotency key + dedup check
- [ ] COD amount không do client khai — server derive từ order (SellPrice+DeliveryFee / TotalAmount)
- [ ] Reseller commission trả đúng 1 lần, qua risk scoring + cooling như Marketplace
- [ ] Cancel/refund đảo TOÀN BỘ wallet tx của đơn (không chỉ Commission) + SalesReferral→Rejected
- [ ] Mỗi OrderCompleted → đúng 1 bộ bút toán, net revenue, có dedup theo CorrelationId
- [ ] Đơn COD confirm xong → có 511/3331/632 entries trên đúng tenant bookset
- [ ] Ledger shipper có leg nộp tiền → balance khép về phí giao hàng thực nhận
- [ ] Withdrawal/payout có request→approve→pay flow + audit
- [ ] Build 0 errors · guard-check.ps1 PASS · Core.Tests PASS sau mỗi card
- [ ] RV production theo runtime-verification.md sau mỗi batch deploy
