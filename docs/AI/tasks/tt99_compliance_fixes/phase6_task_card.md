# TASK CARD — Phase 6: B 03-DN Chỉ Tiêu "Lãi/Lỗ Bán BĐSĐT"

> **Status:** 🟡 PLANNED
> **Priority:** P2 — Feature gap (TT 99 new indicator)
> **Branch:** `feature/tt99-fix-phase6-bdsdt-indicator`
> **Estimated sessions:** 1
> **Mode:** IMPLEMENT
> **Domain modification:** NO (service logic only — CashFlowStatement record đã có OperatingActivities section)

## Objective
TT 99/2025/TT-BTC bổ sung chỉ tiêu mới cho B 03-DN (Báo cáo lưu chuyển tiền tệ):
- **"Lãi/lỗ của hoạt động bán, thanh lý BĐS đầu tư"** — trình bày theo số thuần
- Doanh thu bán hàng & giá vốn hàng bán **không bao gồm** doanh thu & giá vốn bán BĐSĐT

Codebase hiện không có chỉ tiêu này trong `CashFlowStatementService`.

## Prerequisites
- [ ] Verify `CashFlowStatementService.cs` hiện không có chỉ tiêu BĐSĐT
- [ ] Verify TK 217 (BĐS đầu tư) tồn tại trong `AccountChartSeeder.cs` (line 223)
- [ ] Verify TK 511 (Doanh thu bán hàng) + TK 632 (Giá vốn hàng bán) — cần tách BĐSĐT ra
- [ ] **Phase 3 (indirect method) nên COMPLETE** — cùng modify CashFlowStatementService

## Files to Modify
| File | Changes |
|------|---------|
| `3_CoreHub/Services/CashFlowStatementService.cs` | Add BĐSĐT indicator in OperatingActivities |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | Display BĐSĐT line (auto via FinancialStatementLine) |

## Detailed Changes

### Change 1: Service — Add BĐSĐT indicator
```csharp
// CashFlowStatementService.cs — in GenerateAsync (direct method)
// After calculating OperatingActivities, add BĐSĐT line:

// TK 217: BĐS đầu tư (Asset)
// TK 5117: Doanh thu bán BĐSĐT (Revenue — sub-account of 511)
// TK 6327: Giá vốn BĐSĐT (Expense — sub-account of 632)
// INVESTIGATE: verify sub-account codes exist in AccountChartSeeder

decimal bdsdtRevenue = GetAccountBalance("5117");  // or 511 + sub-account logic
decimal bdsdtCost = GetAccountBalance("6327");
decimal bdsdtNet = bdsdtRevenue - bdsdtCost;

if (Math.Abs(bdsdtNet) > 0.005m)
{
    operatingLines.Add(new FinancialStatementLine(
        ReportItemCode: "75",  // TT 99 Mã số for BĐSĐT (verify from template)
        ReportItemName: "Lãi/lỗ của hoạt động bán, thanh lý BĐS đầu tư",
        EndingAmount: bdsdtNet,
        OpeningAmount: 0,  // or calculate opening period
        Level: 2,
        IsNormalNegative: bdsdtNet < 0
    ));
}

// Also: exclude BĐSĐT from "Doanh thu bán hàng" line to avoid double-count
// → Adjust TK 511 total by subtracting TK 5117
// → Adjust TK 632 total by subtracting TK 6327
```

### Change 2: UI — auto-display
`CashFlowStatement.razor` đã render `OperatingActivities` qua `VanAnDataGrid` → chỉ tiêu mới tự động hiển thị nếu có dữ liệu. Không cần change UI.

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] Khi có bút toán BĐSĐT (TK 5117/6327), báo cáo hiển thị chỉ tiêu "Lãi/lỗ bán BĐSĐT"
- [ ] Khi không có bút toán BĐSĐT, chỉ tiêu không hiển thị (hoặc = 0)
- [ ] Doanh thu bán hàng (TK 511) không double-count BĐSĐT

## Rollback
`git revert <commit>` — service logic only.

## Notes
- **INVESTIGATE:** Verify TK 5117 + 6327 có trong `AccountChartSeeder.cs` không. Nếu chưa có, cần thêm.
- **INVESTIGATE:** Verify Mã số TT 99 cho chỉ tiêu BĐSĐT (task card dùng "75" — cần confirm từ Phụ lục IV).
