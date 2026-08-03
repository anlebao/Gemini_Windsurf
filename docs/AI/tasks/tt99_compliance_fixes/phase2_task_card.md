# TASK CARD — Phase 2: Auto-select Standard + TT58 Dropdown + Tách TrialBalance

> **Status:** 🟡 PLANNED
> **Priority:** P4 — UX improvement
> **Branch:** `feature/tt99-fix-phase2-standard-autoselect`
> **Estimated sessions:** 1
> **Mode:** IMPLEMENT
> **Domain modification:** NO (UI logic only)

## Objective
3 issues gộp:
1. **Gap #5:** Default `selectedStandard = TT133_2016` hardcoded → auto-select theo `Tenant.Type`
2. **Gap #6:** Dropdown thiếu TT58_2026 (DN siêu nhỏ)
3. **Gap #8:** TrialBalance nằm trong bộ BCTC — không thuộc TT 99, tách riêng

## Prerequisites
- [ ] Verify 4 report pages đều có `selectedStandard = AccountingStandard.TT133_2016` (line 169/117/177/117)
- [ ] Verify 4 report pages đều có dropdown với 2 options (TT133 + TT99)
- [ ] Verify `Tenant.Type` property tồn tại (TenantId VO, enum TenantType)
- [ ] Verify `ITenantProvider` có tenant context
- [ ] Verify `FinancialReports.razor` hiện 4 thẻ (BS + IS + CFS + TrialBalance)

## Files to Modify
| File | Changes |
|------|---------|
| `5_WebApps/ShopERP/Components/Pages/Accounting/BalanceSheet.razor` | Auto-select standard + thêm TT58 option |
| `5_WebApps/ShopERP/Components/Pages/Accounting/IncomeStatement.razor` | Same |
| `5_WebApps/ShopERP/Components/Pages/Accounting/CashFlowStatement.razor` | Same |
| `5_WebApps/ShopERP/Components/Pages/Accounting/TrialBalance.razor` | Same |
| `5_WebApps/ShopERP/Components/Pages/Accounting/FinancialReports.razor` | Tách TrialBalance vào section "Báo cáo hỗ trợ" |

## Detailed Changes

### Change 1: Auto-select standard theo Tenant.Type
Thêm helper method trong mỗi report page (hoặc tạo shared service):

```csharp
// Trong @code block, thay:
private AccountingStandard selectedStandard = AccountingStandard.TT133_2016;

// Bằng:
private AccountingStandard selectedStandard = AccountingStandard.TT133_2016; // default

protected override async Task OnInitializedAsync()
{
    // Auto-select standard theo Tenant.Type
    if (TenantProvider.HasTenant)
    {
        try
        {
            // Query tenant type via Gateway API hoặc cached tenant info
            // INVESTIGATE: cách lấy Tenant.Type trong Blazor page
            // Option A: inject ITenantManagementService + GetTenantByIdAsync
            // Option B: thêm TenantType vào TenantApiDto (đã có từ Phase 3 Bug 1)
            // → Option B đơn giản hơn (TenantApiDto.BusinessType đã có)
            var tenantType = await GetTenantTypeAsync();
            selectedStandard = tenantType switch
            {
                TenantType.Enterprise_Large => AccountingStandard.TT99_2025,
                TenantType.Enterprise_SuperSmall => AccountingStandard.TT58_2026,
                _ => AccountingStandard.TT133_2016 // Enterprise_SME + HKD fallback
            };
        }
        catch { /* fallback to TT133 */ }
    }
    await GenerateReport();
}
```

**INVESTIGATE:** `TenantApiDto` hiện có `BusinessType` (Company/HouseholdBusiness) nhưng KHÔNG có `TenantType` (Enterprise_Large/SME/SuperSmall/HKD). Cần:
- Option A: Thêm `TenantType? Type` vào `TenantApiDto` + `TenantDto` (Gateway controller)
- Option B: Query `ITenantManagementService.GetTenantByIdAsync` trong page (sync call)
- **Recommend Option A** — thêm field vào DTO, populate từ `tenant.Type` trong `MapToDto`

### Change 2: Thêm TT58 option trong dropdown
```razor
<!-- OLD -->
<select @bind="selectedStandard">
    <option value="@AccountingStandard.TT133_2016">TT 133/2016 (DN vừa)</option>
    <option value="@AccountingStandard.TT99_2025">TT 99/2025 (DN lớn)</option>
</select>

<!-- NEW -->
<select @bind="selectedStandard">
    <option value="@AccountingStandard.TT58_2026">TT 58/2026 (DN siêu nhỏ)</option>
    <option value="@AccountingStandard.TT133_2016">TT 133/2016 (DN vừa)</option>
    <option value="@AccountingStandard.TT99_2025">TT 99/2025 (DN lớn)</option>
</select>
```

### Change 3: Tách TrialBalance khỏi bộ BCTC
```razor
<!-- FinancialReports.razor — NEW structure -->
<VanACard Header="Bộ Báo Cáo Tài Chính năm (TT 99/2025/TT-BTC)">
    <div class="report-links-grid">
        <a href="/accounting/balance-sheet" class="report-link-card">
            📊 Báo Cáo Tình Hình Tài Chính (B 01-DN)
        </a>
        <a href="/accounting/income-statement" class="report-link-card">
            📈 Báo Cáo Kết Quả Hoạt Động Kinh Doanh (B 02-DN)
        </a>
        <a href="/accounting/cash-flow-statement" class="report-link-card">
            💰 Báo Cáo Lưu Chuyển Tiền Tệ (B 03-DN)
        </a>
        <!-- B 09-DN sẽ thêm ở Phase 5 -->
    </div>
</VanACard>

<VanACard Header="Báo cáo hỗ trợ (không thuộc bộ BCTC năm)">
    <div class="report-links-grid">
        <a href="/accounting/trial-balance" class="report-link-card">
            ⚖️ Bảng Cân Đối Số Phát Sinh
        </a>
    </div>
</VanACard>
```

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] DN lớn auto-select TT99, DN vừa auto-select TT133, DN siêu nhỏ auto-select TT58
- [ ] Dropdown có 3 options (TT58 + TT133 + TT99)
- [ ] `FinancialReports.razor` có 2 sections: "Bộ BCTC năm" + "Báo cáo hỗ trợ"
- [ ] TrialBalance không còn nằm trong bộ BCTC

## Rollback
`git revert <commit>` — UI logic only, không phá data.

---

## ANALYZE UPDATE (2026-08-03)

### Verified Accurate
- ✅ All 4 `selectedStandard = TT133_2016` at claimed lines (169/117/177/117)
- ✅ All 4 dropdowns have exactly 2 options (TT133 + TT99), no TT58
- ✅ `Tenant.Type` exists as `TenantType? Type` (nullable, private set)
- ✅ `TenantType` enum: HKD=1, Enterprise_SuperSmall=2, Enterprise_SME=3, Enterprise_Large=4
- ✅ `FinancialReports.razor` has 4 links in 1 card (needs split)

### KEY FINDING: `IVasFeatureFlagService.GetTenantTypeAsync()` ALREADY EXISTS
```csharp
// VasFeatureFlagService.cs — already implements
Task<TenantType?> GetTenantTypeAsync(TenantId tenantId, CancellationToken ct = default);
```
**Impact:** Task card's Option A (add TenantType to TenantApiDto + Gateway controller) is **NOT NEEDED**. Report pages can inject `IVasFeatureFlagService` directly (same as AccountingLayout.razor already does).

### CRITICAL: TT58 Intentionally NOT Seeded
`AccountChartSeeder.cs:12`: "TT 58/2026 NOT seeded — TT 58 'bỏ hoàn toàn hệ thống tài khoản kế toán, thay bằng sổ theo dõi đơn giản hóa' (C5)"

**Revised Change 2:** Do NOT add TT58 option to dropdown. Instead:
- Show info message: "TT 58/2026 sử dụng sổ theo dõi đơn giản hóa, không áp dụng BCTC mẫu"
- OR simply skip TT58 (enum exists but no accounts → reports return empty)

### Revised Implementation (simpler)
```csharp
// Inject IVasFeatureFlagService (already available, same as AccountingLayout)
@inject IVasFeatureFlagService FeatureFlagService

protected override async Task OnInitializedAsync()
{
    if (TenantProvider.HasTenant)
    {
        var tenantId = new TenantId(TenantProvider.TenantId);
        var tenantType = await FeatureFlagService.GetTenantTypeAsync(tenantId);
        selectedStandard = tenantType switch
        {
            TenantType.Enterprise_Large => AccountingStandard.TT99_2025,
            _ => AccountingStandard.TT133_2016  // SME + SuperSmall + HKD fallback
        };
    }
    await GenerateReport();
}
```
**No DTO change, no Gateway controller change, no TenantApiDto change.**
