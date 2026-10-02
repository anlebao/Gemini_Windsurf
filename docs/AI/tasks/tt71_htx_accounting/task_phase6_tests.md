# TASK CARD: Phase 6 — Tests TT 71

> **Master plan:** `docs/AI/plans/tt71-htx-accounting-master-plan.md`
> **Status:** ✅ COMPLETE 2026-10-02 (T1-T4 S1/S2 · T6+T8 S3 · T5+T7 S4 — toàn bộ PASS)

## 1. OBJECTIVE

Test đầy đủ cho chart/template/form/mapping TT 71 + không regress các suite hiện có.

## 2. TEST LIST

| # | Test | Nơi | Nội dung | Status |
|---|---|---|---|---|
| T1 | `AccountChartSeederTests` | VanAn.Core.Tests | Seed TT 71 (89 TK) · type đúng (214/229 contra, 521 giảm trừ) · idempotent · TT 133/99 không đổi | ✅ S1 |
| T2 | `Tt71TemplatesTests` (MỚI) | VanAn.Core.Tests | B01-HTX + B02-HTX: mã số/nhãn · tách nội/ngoài (511 vs 512, 611 vs 612) · B09 sections | ✅ S2 |
| T3 | `IncomeStatementServiceTests` += TT71 | VanAn.Core.Tests | B02-HTX cấu trúc + số liệu giả khớp spec (01a=10M/01b=5M/02a=1M/.../60=1.5M) | ✅ S2 |
| T4 | `BalanceSheetServiceTests` += TT71 | VanAn.Core.Tests | B01-HTX cấu trúc + W2 (200==500, plug 420) | ✅ S2 |
| T5 | Domain: `TenantType.HTX` + `SetTenantType` + HtxProfile → mapping | VanAn.Core.Tests | `MarkAsHtx` 4 tests (null/HKD/Enterprise, idempotent, Inactive throw) + `HtxProfileServiceTt71Tests` 3 (hook Type=HTX + Standard=TT71, idempotent, từ HKD) | ✅ S1 |
| T6 | ShopERP.Tests component: RevenueEntry/ExpenseEntry HTX options | VanAn.ShopERP.Tests | HTX → thu 511/512/558 (không 515/711), chi 642/658 (không 621/622/627/641); DN giữ nguyên (4 tests) | ✅ S3 |
| T7 | UI standard select auto-map | VanAn.ShopERP.Tests | `AccountingStandardUiTests` (4): TrialBalance HTX auto TT71 (option selected + service verify) · DN giữ TT133 · FinancialReports hub HTX ẩn B03 + DN giữ B03 | ✅ S4 |
| T8 | Voucher print: Tt71ReceiptVoucher/Tt71PaymentVoucher render | VanAn.ShopERP.Tests | `Tt71VoucherTests` (13): 01-TT/02-TT khuôn + chữ ký + `VietnameseCurrencyText` 11 case | ✅ S3 |

**Full suite cuối (S4): Core.Tests 1872 PASS · ShopERP.Tests 120 PASS (+21 tổng TT 71) · Architecture 41 PASS · guard ALL PASSED · build 0 errors**

## 3. ACCEPTANCE

- [ ] Tất cả test mới PASS
- [ ] Core.Tests full + ShopERP.Tests full + Architecture không regress
- [ ] guard-check.ps1 PASS

## 4. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests\VanAn.Core.Tests
dotnet test 6_Tests\VanAn.ShopERP.Tests
dotnet test 6_Tests\VanAn.Architecture.Tests
powershell -ExecutionPolicy Bypass -File guard-check.ps1
```
