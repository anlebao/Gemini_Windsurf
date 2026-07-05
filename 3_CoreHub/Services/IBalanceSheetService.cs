using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 4 — Balance Sheet service (Mẫu B01-DN / B01-DNN).
/// Queries JournalEntries (Pattern #1 + #5 fix) and returns a <see cref="BalanceSheet"/> Domain record.
/// Enforces the W2 invariant: TotalAssetsEnding == TotalLiabilitiesAndEquityEnding (throws if unbalanced;
/// no IsBalanced flag — unbalanced data is never stored, per W2 Domain record design).
/// </summary>
public interface IBalanceSheetService
{
    /// <summary>
    /// Generate the Balance Sheet for a tenant + period + accounting standard.
    /// Opening balances are computed cumulatively from all JournalEntry lines where EntryDate &lt; periodStart.
    /// Ending balances = Opening + Movement (EntryDate in [periodStart, periodEnd)).
    /// </summary>
    /// <param name="standard">Accounting standard (TT 99/2025, TT 133/2016) — used for account classification lookup.</param>
    /// <exception cref="InvalidOperationException">Thrown when TotalAssetsEnding != TotalLiabilitiesAndEquityEnding (W2 invariant).</exception>
    Task<BalanceSheet> GenerateAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
}
