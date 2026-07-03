using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// HKDRevenueClassificationService - HKD revenue classification implementation
/// 2026 Regulatory Compliance (Wave 5c): 4-level revenue group per Luật Thuế GTGT/TNCN sửa đổi 2025 +
/// ND 117/2025/NĐ-CP + Nghị quyết 198/2025/QH15 (áp dụng từ 01/01/2026).
///   Group1: ≤1B (không chịu thuế GTGT + TNCN)
///   Group2: >1B - ≤3B (GTGT + TNCN theo tỷ lệ ngành nghề)
///   Group3: >3B - ≤50B (TNCN bắt buộc theo lợi nhuận 17%)
///   Group4: >50B (TNCN bắt buộc theo lợi nhuận 20%)
/// 2026 changes: thuế khoán BÃI BỎ (NQ 198/2025/QH15), lệ phí môn bài BÃI BỎ (Điều 10 NQ 198/2025/QH15).
/// </summary>
public class HKDRevenueClassificationService : IHKDRevenueClassificationService
{
    // Wave 5c (2026-07-03): thresholds updated per 2026 regulatory compliance.
    //   Old (pre-2026): 500M / 1B / 3B  →  New (2026): 1B / 3B / 50B
    private const decimal Group1Threshold = 1_000_000_000m;    // ≤ 1B → Group1
    private const decimal Group2Threshold = 3_000_000_000m;    // > 1B - ≤ 3B → Group2
    private const decimal Group3Threshold = 50_000_000_000m;   // > 3B - ≤ 50B → Group3
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
            warnings.Add($"2026 Cảnh báo: Doanh thu {totalRevenue:N0}₫ đang tiệm cận ngưỡng Nhóm 2 (1 tỷ). Vượt ngưỡng → phải nộp thuế GTGT + TNCN theo tỷ lệ ngành nghề (ND 117/2025).");

        if (totalRevenue > Group2Threshold * WarningRatio && totalRevenue <= Group2Threshold)
            warnings.Add($"2026 Cảnh báo: Doanh thu {totalRevenue:N0}₫ đang tiệm cận ngưỡng Nhóm 3 (3 tỷ). Vượt ngưỡng → TNCN bắt buộc theo lợi nhuận (17%).");

        if (totalRevenue > Group3Threshold * WarningRatio && totalRevenue <= Group3Threshold)
            warnings.Add($"2026 Cảnh báo: Doanh thu {totalRevenue:N0}₫ đang tiệm cận ngưỡng Nhóm 4 (50 tỷ). Vượt ngưỡng → TNCN bắt buộc theo lợi nhuận (20%).");

        return warnings;
    }
}
