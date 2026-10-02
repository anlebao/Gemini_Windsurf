# TASK CARD: Phase 5 — UI chọn chuẩn kế toán + auto-map tenant (kể cả HTX)

> **Master plan:** `docs/AI/plans/tt71-htx-accounting-master-plan.md`
> **Status:** ✅ COMPLETE 2026-10-02 (`0dfb848f` — pushed + CD Multi-VPS SUCCESS + RV markers PASS)

## 1. OBJECTIVE

Các màn hình báo cáo chọn đúng chuẩn kế toán theo tenant; tenant HTX thấy đúng mẫu HTX, KHÔNG thấy B03.

## 2. HIỆN TRẠNG (mâu thuẫn)

- `TrialBalance.razor:48-50` + `IncomeStatement.razor:48-49`: options hardcode **TT 133 / TT 99** — không có TT 71; auto-map `_ => TT133_2016` (`TrialBalance.razor:129-132`) → tenant HTX (và HKD/siêu nhỏ) rơi vào TT 133 SAI
- `CashFlowStatement.razor:55-56`: hiện "B 03-DN" — TT 71 KHÔNG yêu cầu B03
- `FinancialReports.razor`: kiểm tra cách list báo cáo (ẩn/hiện theo standard)

## 3. CHANGES

| File | Change |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Accounting/TrialBalance.razor` | Options += "TT 71/2024 (HTX)"; auto-map `TenantType.HTX => TT71_2024`; `_ => TT133` giữ |
| `5_WebApps/ShopERP/Components/Pages/Accounting/IncomeStatement.razor` | Options += TT 71; auto-map HTX |
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | Options += TT 71; auto-map HTX |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | **Ẩn tab/trang khi tenant HTX** (TT 71 không có B03) — hoặc hiển thị note "TT 71 không yêu cầu LCTT" |
| `5_WebApps/ShopERP/Components/Pages/Accounting/FinancialReports.razor` | List báo cáo theo standard/tenant type |
| `5_WebApps/ShopERP/Services/Accounting/...` (helper) | `ResolveAccountingStandardAsync(tenant)` dùng chung (Phase 4 helper mở rộng) — 1 nguồn sự thật |

## 4. LƯU Ý

- Auto-map là DEFAULT — user vẫn có thể đổi select (giữ UX hiện tại)
- HTX + HKD: HKD tenant KHÔNG dùng BCTC DN (HKD dùng sổ) — kiểm tra trạng thái hiện tại (có thể HKD đang hiện BCTC sai từ lâu — ngoài scope TT 71, note lại)
- **Q4 = KHÔNG:** `TenantType.HTX` KHÔNG đổi `ShopErpMenuService`/feature flags ngoài kế toán — chỉ thêm "TT 71/2024 (HTX)" vào các select chuẩn kế toán

## 5. ACCEPTANCE

- [x] Tenant HTX auto chọn TT 71 trên TrialBalance/IncomeStatement/BalanceSheet (+ Notes) — `AccountingStandardResolver.Resolve`
- [x] CashFlowStatement ẩn/note cho HTX (VanAAlert "TT 71 KHÔNG yêu cầu B03" — từ S2) + FinancialReports hub ẨN B03 link cho HTX
- [x] Không regress tenant DN/HKD (options DN giữ nguyên; label hub "Kinh Doanh (B 02-DN)" giữ — W6-HUB-3 PASS)
- [x] ShopERP.Tests component tests PASS (116 full)

## 6. KẾT QUẢ (2026-10-02)

- `Services/Accounting/AccountingStandardResolver.cs` (mới) — 1 nguồn sự thật: Enterprise_Large→TT99 · HTX→TT71 · _→TT133; dùng cho TrialBalance/IncomeStatement/BalanceSheet/FinancialStatementNotes/CashFlow
- TrialBalance/IncomeStatement/BalanceSheet/FinancialStatementNotes: options += "TT 71/2024 (HTX)"
- FinancialStatementNotes h1 động (B 09-DN / B 09-HTX)
- FinancialReports hub: HTX → title "Bộ BCTC năm (TT 71/2024/TT-BTC)" + links B 01-HTX/B 02-HTX/B 09-HTX, ẨN B03; DN giữ nguyên
- Q4 tuân thủ: KHÔNG đụng ShopErpMenuService/feature flags ngoài kế toán
- Note (ngoài scope): HKD tenant đang auto-map TT 133 cho BCTC (thực chất HKD dùng sổ) — ghi nhận, xử lý sau

## 6. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests\VanAn.ShopERP.Tests
```
