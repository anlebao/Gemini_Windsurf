# TASK CARD: Phase 6 — Tests TT 71

> **Master plan:** `docs/AI/plans/tt71-htx-accounting-master-plan.md`
> **Status:** PENDING

## 1. OBJECTIVE

Test đầy đủ cho chart/template/form/mapping TT 71 + không regress các suite hiện có.

## 2. TEST LIST

| # | Test | Nơi | Nội dung |
|---|---|---|---|
| T1 | `AccountChartSeederTests` (thêm vào file hiện có hoặc mới) | VanAn.Core.Tests | Seed TT 71 đủ 51 TK cấp 1 + cấp 2/3 · type đúng (214/229 contra-asset, 521 giảm trừ) · idempotent · TT 133/99 không đổi |
| T2 | `Tt71TemplatesTests` (MỚI) | VanAn.Core.Tests | B01-HTX + B02-HTX: mã số/nhãn theo spec · tách nội bộ/ngoài (511 vs 512, 611 vs 612) · công thức tính (10=01-02, 20=10-11-12, 40=31-32, 50=20+40, 60=50-51) |
| T3 | `IncomeStatementServiceTests` += TT71 case | VanAn.Core.Tests | Generate với standard TT71 → cấu trúc B02-HTX; số liệu giả đúng |
| T4 | `BalanceSheetServiceTests` += TT71 case | VanAn.Core.Tests | B01-HTX cấu trúc đúng |
| T5 | Domain test: `TenantType.HTX` + `SetTenantType` + HtxProfile → mapping | VanAn.Core.Tests | Tạo HtxProfile → tenant Type=HTX, Standard=TT71 |
| T6 | ShopERP.Tests component: RevenueEntry/ExpenseEntry HTX options | VanAn.ShopERP.Tests | Tenant HTX → thu: 511/512/558, chi: 642/658; DN/HKD giữ nguyên |
| T7 | UI standard select: TrialBalance auto-map HTX | VanAn.ShopERP.Tests | Render + selected value |

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
