# RV REPORT — Phiếu chứng từ kế toán (2026-11-01)

- **Script:** `6_Testing/rv/rv-phieu-chung-tu.sh`
- **VPS:** vanan-shop-a (asia-southeast1-b, project vanan-prod)
- **Target:** https://app2.khachvip.online
- **Image deployed:** `ghcr.io/anlebao/vanan-shoperp:f91c4b30eec9fd53f284ee32ef88bae92c2b59a7`
- **Kết quả:** **PASS 52 / FAIL 0**

## A. Routes (302 = route tồn tại, redirect login) — 20/20 PASS

```
PASS /accounting
PASS /accounting/revenue
PASS /accounting/expenses
PASS /accounting/journal-voucher
PASS /accounting/purchase-receipts
PASS /accounting/opening-balance
PASS /accounting/cong-no
PASS /accounting/history
PASS /accounting/mapping-rules
PASS /accounting/cash-book
PASS /accounting/bank-book
PASS /accounting/journal
PASS /accounting/ledger
PASS /accounting/sub-ledger
PASS /accounting/balance
PASS /accounting/import
PASS /accounting/period-closing
PASS /accounting/cash-payment-review
PASS /accounting/financial-reports
PASS /accounting/hkd-books
```

## B. Markers VanAn.ShopERP.dll — 20/20 PASS

```
PASS JournalVoucher            (13)
PASS JournalVoucherPrint       (2)
PASS Tt71ReceiptVoucher        (2)
PASS Tt71PaymentVoucher        (2)
PASS RevenueEntry              (9)
PASS ExpenseEntry              (10)
PASS TransactionHistory        (5)
PASS PurchaseReceipts          (20)
PASS OpeningBalance            (12)
PASS MappingRules              (5)
PASS CashBook                  (4)
PASS BankBook                  (10)
PASS JournalReport             (5)
PASS CongNoReport              (7)
PASS menu link /accounting/journal-voucher (1)
PASS menu link /accounting/mapping-rules   (1)
PASS menu link /accounting/cash-book       (1)
PASS menu link /accounting/bank-book       (1)
PASS menu link /accounting/revenue         (1)
PASS menu link /accounting/expenses        (1)
```

## C. Markers VanAn.CoreHub.dll — 12/12 PASS

```
PASS ManualJournalVoucherService (3)
PASS CongNoService               (12)
PASS PurchaseReceiptService      (20)
PASS OpeningBalanceService       (4)
PASS InternalTransferService     (11)
PASS VoucherNumberService        (2)
PASS ManualEntryJournalBridge    (9)
PASS voucher const TypeJournalVoucher   (1)   → CTGS
PASS voucher const TypePurchaseReceipt  (1)   → NM
PASS voucher const TypeReceiptCongNo    (1)   → PT-CN
PASS voucher const TypePaymentCongNo    (1)   → PC-CN
PASS voucher const TypeTransfer         (1)   → CT
```

## Kết luận

| Nhóm phiếu | Trạng thái production |
|---|---|
| 01-TT (thu doanh thu / góp vốn / thu nợ 131) | ✅ route + print component + service live |
| 02-TT (chi phí / trả nợ 331) | ✅ route + print component + service live |
| CTGS phiếu hạch toán | ✅ route + `ManualJournalVoucherService` + `TypeJournalVoucher` live |
| Phiếu nhập mua NM | ✅ route + `PurchaseReceiptService` + const live |
| Phiếu công nợ PT-CN / PC-CN | ✅ `CongNoService` + consts live (non-cash, không in — by design) |
| Số dư đầu kỳ | ✅ route + `OpeningBalanceService` live |
| Phiếu chuyển tiền CT | ⚠️ `InternalTransferService` + `TypeTransfer` trong DLL nhưng KHÔNG có trang UI |
| Menu links mới (revenue/expenses/journal-voucher/mapping-rules/cash-book/bank-book) | ✅ đã deploy (commit `f91c4b30`) |

**Scope:** script verify L1 (markers/image) + L2 (routes). Behavior-level đã có E2E production `accounting-issue198.spec.ts` 7/7 PASS cùng ngày (journal-voucher render + in 01-TT trên tenant DN non-HTX). L3/L5 (tạo phiếu thật + PG verify `ReferenceType='JournalVoucher'`) thực hiện ở phiếu đầu tiên của user.
