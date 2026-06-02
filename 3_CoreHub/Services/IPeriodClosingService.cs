using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service for accounting period closing operations.
/// AccountingEntry immutability is enforced — period reopening uses Reversal Entry pattern.
/// </summary>
public interface IPeriodClosingService
{
    Task<PeriodClosingCheckResult> ValidatePeriodAsync(
        AccountingPeriod period,
        TenantId tenantId,
        CancellationToken cancellationToken = default);

    Task<ClosingEntry> ClosePeriodAsync(
        AccountingPeriod period,
        TenantId tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task ReopenPeriodAsync(
        AccountingPeriod period,
        TenantId tenantId,
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<PeriodClosingStatus> GetPeriodStatusAsync(
        AccountingPeriod period,
        TenantId tenantId,
        CancellationToken cancellationToken = default);
}
