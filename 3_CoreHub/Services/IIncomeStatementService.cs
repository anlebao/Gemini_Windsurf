using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 4 — Income Statement service (Mẫu B02-DN / B02-DNN).
/// Returns a 2-column comparative <see cref="IncomeStatement"/> (Ending = current period, Opening = same period last year).
/// Revenue/COGS/OpEx/OtherIncome/OtherExpense grouped via IAccountChartService.
/// </summary>
public interface IIncomeStatementService
{
    /// <summary>
    /// Generate the Income Statement for a tenant + period + accounting standard.
    /// Ending column = current period movement; Opening column = same month prior year movement.
    /// NetProfit = Revenue - COGS - OpEx + OtherIncome - OtherExpense.
    /// </summary>
    Task<IncomeStatement> GenerateAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
}
