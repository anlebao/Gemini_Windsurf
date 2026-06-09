using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// HKDRevenueClassificationService - HKD revenue classification implementation
/// TT152-2025/TT-BTC: 4-level revenue group classification with threshold warnings.
/// Group1: ≤500M | Group2: 500M–1B | Group3: 1B–3B | Group4: >3B
/// </summary>
public class HKDRevenueClassificationService : IHKDRevenueClassificationService
{
    private const decimal Group1Threshold = 500_000_000m;
    private const decimal Group2Threshold = 1_000_000_000m;
    private const decimal Group3Threshold = 3_000_000_000m;
    private const decimal WarningRatio = 0.90m;

    private readonly IAccountingService _accountingService;

    public HKDRevenueClassificationService(IAccountingService accountingService)
    {
        _accountingService = accountingService;
    }

    public async Task<HKDRevenueGroup> CalculateRevenueGroupAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        if (tenantId is null) throw new ArgumentNullException(nameof(tenantId));
        if (period is null) throw new ArgumentNullException(nameof(period));

        var entries = await _accountingService.GetEntriesByTenantAndPeriodAsync(tenantId, period);
        var totalRevenue = entries
            .Where(e => e.EntryType == AccountingEntryType.Revenue)
            .Sum(e => e.Amount);

        return HKDRevenueClassification.CalculateGroup(totalRevenue);
    }

    public async Task<bool> ValidateComplianceAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        if (tenantId is null || period is null)
            return false;

        var group = await CalculateRevenueGroupAsync(tenantId, period, cancellationToken);
        return group >= HKDRevenueGroup.Group1;
    }

    public async Task<List<string>> GetThresholdWarningsAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        if (tenantId is null || period is null)
            return warnings;

        var entries = await _accountingService.GetEntriesByTenantAndPeriodAsync(tenantId, period);
        var totalRevenue = entries
            .Where(e => e.EntryType == AccountingEntryType.Revenue)
            .Sum(e => e.Amount);

        if (totalRevenue > Group1Threshold * WarningRatio && totalRevenue <= Group1Threshold)
            warnings.Add($"TT152-2025 Cảnh báo: Doanh thu {totalRevenue:N0}₫ đang tiệm cận ngưỡng Nhóm 2 (500 triệu). Kiểm tra nghĩa vụ nộp thuế GTGT.");

        if (totalRevenue > Group2Threshold * WarningRatio && totalRevenue <= Group2Threshold)
            warnings.Add($"TT152-2025 Cảnh báo: Doanh thu {totalRevenue:N0}₫ đang tiệm cận ngưỡng Nhóm 3 (1 tỷ). Xem xét điều chỉnh phương pháp tính thuế.");

        if (totalRevenue > Group3Threshold * WarningRatio && totalRevenue <= Group3Threshold)
            warnings.Add($"TT152-2025 Cảnh báo: Doanh thu {totalRevenue:N0}₫ đang tiệm cận ngưỡng Nhóm 4 (3 tỷ). Bắt buộc chuyển sang phương pháp khấu trừ.");

        return warnings;
    }
}
