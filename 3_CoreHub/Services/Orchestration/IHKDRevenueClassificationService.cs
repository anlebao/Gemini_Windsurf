using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// IHKDRevenueClassificationService - HKD revenue classification per TT152-2025/TT-BTC
/// 4-level revenue group classification
/// </summary>
public interface IHKDRevenueClassificationService
{
    /// <summary>
    /// Calculate revenue group for tenant in period
    /// </summary>
    Task<HKDRevenueGroup> CalculateRevenueGroupAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate revenue classification compliance
    /// </summary>
    Task<bool> ValidateComplianceAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get revenue threshold warnings
    /// </summary>
    Task<List<string>> GetThresholdWarningsAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default);
}
