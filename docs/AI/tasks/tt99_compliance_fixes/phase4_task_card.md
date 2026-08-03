# TASK CARD — Phase 4: TT 99 Template Structure (Mã Số 100/110/120...)

> **Status:** 🟡 PLANNED
> **Priority:** P3 — Large refactor
> **Branch:** `feature/tt99-fix-phase4-template-structure`
> **Estimated sessions:** 2-3 (large refactor)
> **Mode:** IMPLEMENT
> **Domain modification:** YES — add template mapping records

## Objective
Codebase hiện generate báo cáo theo **flat account list** (mỗi TK 111, 112, 131... là 1 dòng riêng). TT 99 yêu cầu **template structure** với Mã số phân cấp (Mã 100 "Tài sản ngắn hạn", Mã 110 "Tiền và tương đương tiền", Mã 111 "Tiền mặt"...). Cần refactor services để group accounts thành chỉ tiêu TT 99.

**Ví dụ cấu trúc B 01-DN theo TT 99:**
```
Mã 100 — TÀI SẢN NGẮN HẠN                    (sum of 110-150)
  Mã 110 — Tiền và tương đương tiền           (sum of 111-113)
    Mã 111 — Tiền mặt                          (TK 111)
    Mã 112 — Tiền gửi không kỳ hạn             (TK 112)
    Mã 113 — Tiền đang chuyển                  (TK 113)
  Mã 120 — Đầu tư tài chính ngắn hạn          (sum of 121-129)
    Mã 121 — Chứng khoán kinh doanh            (TK 121)
    Mã 128 — Đầu tư nắm giữ đến ngày đáo hạn  (TK 128)
  Mã 130 — Các khoản phải thu ngắn hạn       (sum of 131-139)
    Mã 131 — Phải thu khách hàng               (TK 131)
    ...
  Mã 150 — Tài sản ngắn hạn khác              (sum of 151-159)
Mã 200 — TÀI SẢN DÀI HẠN                      (sum of 210-270)
  Mã 210 — Tài sản cố định                    (sum of 211-215)
    Mã 211 — TSCĐ hữu hình                     (TK 211)
    Mã 215 — Tài sản sinh học                  (TK 215, NEW TT 99)
  Mã 230 — Đầu tư tài chính dài hạn
  Mã 250 — Tài sản dài hạn khác
Mã 300 — TỔNG CỘNG TÀI SẢN                     (100 + 200)
```

## Prerequisites
- [ ] Verify `BalanceSheetService.cs` hiện aggregate flat (line 61-112)
- [ ] Verify `IncomeStatementService.cs` cấu trúc tương tự
- [ ] Verify `CashFlowStatementService.cs` cấu trúc
- [ ] Verify `AccountChartSeeder.cs` có TK → AccountType mapping
- [ ] **Phase 1 + Phase 2 phải COMPLETE** (naming + standard selection)

## Files to Modify
| File | Changes |
|------|---------|
| `1_Shared/Domain.cs` | Add `Tt99ReportTemplate` record (mapping Mã số → TK list) |
| `3_CoreHub/Services/Data/Tt99ReportTemplate.cs` (NEW) | Template definitions: B 01-DN, B 02-DN, B 03-DN |
| `3_CoreHub/Services/BalanceSheetService.cs` | Refactor to use template |
| `3_CoreHub/Services/IncomeStatementService.cs` | Refactor to use template |
| `3_CoreHub/Services/CashFlowStatementService.cs` | Refactor to use template |
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | UI hiển thị Mã số + Level hierarchy |
| `5_WebApps/ShopERP/Components/Pages/Accounting/IncomeStatement.razor` | Same |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | Same |

## Detailed Changes

### Change 1: Domain — Tt99ReportTemplate record
```csharp
// Domain.cs
/// <summary>
/// TT 99 template line: maps Mã số chỉ tiêu → list of account codes.
/// Level 1 = section header (TÀI SẢN NGẮN HẠN), Level 2 = group (Tiền), Level 3 = account.
/// </summary>
public record Tt99TemplateLine(
    string ReportItemCode,       // "100", "110", "111"
    string ReportItemName,       // "Tài sản ngắn hạn", "Tiền và tương đương tiền"
    int Level,                   // 1 = section, 2 = group, 3 = account
    string[] AccountCodes,       // TK codes that roll up into this line (empty for section/group)
    bool IsCalculated,           // true = sum of children, false = direct from accounts
    bool IsNormalNegative        // display convention
);

public record Tt99ReportTemplate(
    AccountingStandard Standard,
    string ReportForm,           // "B01-DN", "B02-DN", "B03-DN"
    IReadOnlyList<Tt99TemplateLine> Lines
);
```

### Change 2: Tt99ReportTemplate.cs — Template definitions
```csharp
// 3_CoreHub/Services/Data/Tt99ReportTemplate.cs (NEW)
public static class Tt99Templates
{
    public static Tt99ReportTemplate BalanceSheetTt99 => new(
        AccountingStandard.TT99_2025, "B01-DN",
        new List<Tt99TemplateLine>
        {
            // TÀI SẢN NGẮN HẠN
            new("100", "TÀI SẢN NGẮN HẠN", 1, Array.Empty<string>(), true, false),
            new("110", "Tiền và tương đương tiền", 2, Array.Empty<string>(), true, false),
            new("111", "Tiền mặt", 3, new[]{"111"}, false, false),
            new("112", "Tiền gửi không kỳ hạn", 3, new[]{"112"}, false, false),
            new("113", "Tiền đang chuyển", 3, new[]{"113"}, false, false),
            new("120", "Đầu tư tài chính ngắn hạn", 2, Array.Empty<string>(), true, false),
            new("121", "Chứng khoán kinh doanh", 3, new[]{"121"}, false, false),
            new("128", "Đầu tư nắm giữ đến ngày đáo hạn", 3, new[]{"128"}, false, false),
            new("130", "Các khoản phải thu ngắn hạn", 2, Array.Empty<string>(), true, false),
            new("131", "Phải thu khách hàng", 3, new[]{"131"}, false, false),
            new("133", "Thuế GTGT được khấu trừ", 3, new[]{"133"}, false, false),
            new("136", "Phải thu nội bộ", 3, new[]{"136"}, false, false),
            new("138", "Phải thu khác", 3, new[]{"138"}, false, false),
            new("140", "Hàng tồn kho", 2, Array.Empty<string>(), true, false),
            new("141", "Tạm ứng", 3, new[]{"141"}, false, false),
            new("152", "Nguyên liệu, vật liệu", 3, new[]{"152"}, false, false),
            new("153", "Công cụ, dụng cụ", 3, new[]{"153"}, false, false),
            new("155", "Sản phẩm", 3, new[]{"155"}, false, false),
            new("156", "Hàng hóa", 3, new[]{"156"}, false, false),
            new("150", "Tài sản ngắn hạn khác", 2, Array.Empty<string>(), true, false),
            new("151", "Hàng mua đang đi đường", 3, new[]{"151"}, false, false),
            new("154", "Chi phí SXKD dở dang", 3, new[]{"154"}, false, false),
            new("158", "Nguyên liệu tại kho bảo thuế", 3, new[]{"158"}, false, false),
            // TÀI SẢN DÀI HẠN
            new("200", "TÀI SẢN DÀI HẠN", 1, Array.Empty<string>(), true, false),
            new("210", "Tài sản cố định", 2, Array.Empty<string>(), true, false),
            new("211", "TSCĐ hữu hình", 3, new[]{"211"}, false, false),
            new("212", "TSCĐ thuê tài chính", 3, new[]{"212"}, false, false),
            new("213", "TSCĐ vô hình", 3, new[]{"213"}, false, false),
            new("215", "Tài sản sinh học", 3, new[]{"215"}, false, false), // NEW TT 99
            new("220", "Đầu tư tài chính dài hạn", 2, Array.Empty<string>(), true, false),
            new("221", "Đầu tư vào công ty con", 3, new[]{"221"}, false, false),
            new("222", "Đầu tư vào công ty liên kết", 3, new[]{"222"}, false, false),
            new("228", "Đầu tư khác", 3, new[]{"228"}, false, false),
            new("230", "Tài sản dài hạn khác", 2, Array.Empty<string>(), true, false),
            new("241", "XDCB dở dang", 3, new[]{"241"}, false, false),
            new("242", "Chi phí chờ phân bổ", 3, new[]{"242"}, false, false),
            new("243", "TS thuế TNDN hoãn lại", 3, new[]{"243"}, false, false),
            new("244", "Ký quỹ, ký cược", 3, new[]{"244"}, false, false),
            new("270", "Tài sản dài hạn khác", 2, Array.Empty<string>(), true, false),
            new("300", "TỔNG CỘNG TÀI SẢN", 1, Array.Empty<string>(), true, false),
            // NỢ PHẢI TRẢ
            new("310", "NỢ PHẢI TRẢ", 1, Array.Empty<string>(), true, false),
            new("330", "Nợ ngắn hạn", 2, Array.Empty<string>(), true, false),
            new("331", "Phải trả người bán", 3, new[]{"331"}, false, true),
            new("332", "Phải trả cổ tức, lợi nhuận", 3, new[]{"332"}, false, true), // NEW TT 99
            new("333", "Thuế & các khoản phải nộp NSNN", 3, new[]{"333","3331"}, false, true),
            new("334", "Phải trả NLĐ", 3, new[]{"334"}, false, true),
            new("335", "Chi phí phải trả", 3, new[]{"335"}, false, true),
            new("338", "Phải trả, phải nộp khác", 3, new[]{"338"}, false, true),
            new("341", "Vay & nợ thuê tài chính", 3, new[]{"341"}, false, true),
            new("350", "Nợ dài hạn", 2, Array.Empty<string>(), true, true),
            new("343", "Trái phiếu phát hành", 3, new[]{"343"}, false, true),
            new("344", "Nhận ký quỹ, ký cược", 3, new[]{"344"}, false, true),
            new("347", "Thuế TNDN hoãn lại phải trả", 3, new[]{"347"}, false, true),
            new("400", "TỔNG CỘNG NGUỒN VỐN", 1, Array.Empty<string>(), true, false),
            // VỐN CHỦ SỞ HỮU
            new("410", "VỐN CHỦ SỞ HỮU", 1, Array.Empty<string>(), true, false),
            new("411", "Vốn đầu tư của CSH", 3, new[]{"411"}, false, true),
            new("412", "Chênh lệch đánh giá lại TS", 3, new[]{"412"}, false, true),
            new("413", "Chênh lệch tỷ giá hối đoái", 3, new[]{"413"}, false, true),
            new("414", "Quỹ đầu tư phát triển", 3, new[]{"414"}, false, true),
            new("418", "Các quỹ khác thuộc VCSH", 3, new[]{"418"}, false, true),
            new("419", "Cổ phiếu mua lại", 3, new[]{"419"}, false, true),
            new("421", "LN sau thuế chưa phân phối", 3, new[]{"421"}, false, true),
            new("430", "Vốn khác của CSH", 2, Array.Empty<string>(), true, true), // TT 99: gom Quỹ hỗ trợ, Nguồn vốn XDCB
        });

    // Similar for IncomeStatementTt99, CashFlowStatementTt99
    // ... (to be detailed in INVESTIGATE phase)
}
```

### Change 3: Refactor BalanceSheetService
```csharp
// BalanceSheetService.cs — refactor GenerateAsync
public async Task<BalanceSheet> GenerateAsync(...)
{
    var template = Tt99Templates.BalanceSheetTt99;
    var accountBalances = await GetAccountBalancesAsync(tenantId, period, ct);

    var lines = new List<FinancialStatementLine>();
    foreach (var templateLine in template.Lines)
    {
        decimal ending, opening;
        if (templateLine.IsCalculated)
        {
            // Sum of children (lines with higher Level that follow until next sibling)
            // OR sum of all Level 3 lines under this section
            (ending, opening) = CalculateFromChildren(templateLine, template, accountBalances);
        }
        else
        {
            // Direct from accounts
            (ending, opening) = SumAccounts(templateLine.AccountCodes, accountBalances);
        }
        lines.Add(new(templateLine.ReportItemCode, templateLine.ReportItemName,
            ending, opening, templateLine.Level, templateLine.IsNormalNegative));
    }

    // Split lines into Assets / Liabilities / Equity sections
    // (based on template section markers)
    ...
}
```

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] B 01-DN hiển thị đúng cấu trúc Mã số TT 99 (100/110/111/112/120/130/140/150/200/210/300/310/330/350/400/410/430)
- [ ] Level hierarchy hiển thị đúng (Level 1 bold, Level 2 indent, Level 3 indent more)
- [ ] Totals khớp: Mã 300 = Mã 100 + Mã 200, Mã 400 = Mã 310 + Mã 410
- [ ] TK 215 (Tài sản sinh học) hiển thị dưới Mã 210
- [ ] TK 332 (Phải trả cổ tức) hiển thị dưới Mã 330
- [ ] Existing tests pass (cập nhật expected structure)

## Rollback
`git revert <commit>` — large refactor, verify existing reports vẫn hoạt động trước khi merge.
Tách thành 3 commits (BS + IS + CFS) để rollback granular.

## Notes
- **INVESTIGATE trước khi code:** cần verify chính xác cấu trúc Mã số TT 99 từ file Excel mẫu chính thức (Phụ lục IV TT 99). Task card này liệt kê cấu trúc dựa trên MISA + Grant Thornton, nhưng có thể có sai sót.
- **Nên download file Excel mẫu** từ thuvienphapluat.vn hoặc MISA để verify chính xác trước khi implement.

---

## ANALYZE UPDATE (2026-08-03)

### Verified Accurate
- ✅ All 3 services use **flat account list** (not TT99 template) — confirmed
- ✅ `BalanceSheetService`: flat per TK, AccountChart classification (lines 61-112)
- ✅ `IncomeStatementService`: flat per TK, AccountChart classification (lines 65-126)
- ✅ `CashFlowStatementService`: flat per offset account, hardcoded prefix rules (lines 134-155)
- ✅ TK 215, 332, 128 all seeded in TT99 accounts
- ✅ No `Tt99Template` or template mapping exists anywhere in codebase
- ✅ TT58 intentionally NOT seeded (correct — TT58 abandons account system)

### Reverse Impact: Callers of GenerateAsync
- 3 UI pages: BalanceSheet.razor:190, IncomeStatement.razor:138, CashFlowStatement.razor:198
- 3 API controllers: BalanceSheetsController:54, IncomeStatementsController:52, CashFlowStatementsController:51
- `FinancialReportExportService` does NOT call services directly (receives data from UI)

### Test Inventory (WILL NEED UPDATES — 33 tests total)
| Service | Test File | Tests |
|---------|-----------|-------|
| BalanceSheet | `BalanceSheetServiceTests.cs` | 11 (W4_BS1-6, W7_BS1-5) |
| IncomeStatement | `IncomeStatementServiceTests.cs` | 11 (W4_IS1-6, W7_IS1-5) |
| CashFlow | `CashFlowStatementServiceTests.cs` | 11 (W4_CF1-6, W7_CF1-5) |
| Multi-tenant | `VasMultiTenantTests.cs` | covers all 3 |
| Architecture | `ArchitectureRulesTests.cs:307-309` | references all 3 |
| UI pages | 3 page test files | BalanceSheet/IncomeStatement/CashFlow page tests |

**Warning:** W7 tests have specific value assertions (e.g., `TotalAssetsEnding_HasSpecificValue`, `Account111_EndingBalance_MatchesExpected`). Template refactor changes line structure but totals should remain. W7 tests asserting line COUNT or specific account codes WILL BREAK and need updates.
