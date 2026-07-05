using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 4 — Cash Flow Statement service (Mẫu B03-DN / B03-DNN).
/// Direct method (R4): for each JournalEntry touching cash accounts (111/112), classify the offsetting
/// account into Operating / Investing / Financing activities. Returns detail lines per activity
/// (no decimal totals per activity — per W2 Domain record design).
/// </summary>
public interface ICashFlowStatementService
{
    /// <summary>
    /// Generate the Cash Flow Statement (direct method) for a tenant + period + accounting standard.
    /// OpeningCash = Σ debit 111+112 where EntryDate &lt; periodStart. ClosingCash = Σ debit 111+112 where EntryDate &lt; periodEnd.
    /// NetChange = ClosingCash - OpeningCash.
    /// </summary>
    Task<CashFlowStatement> GenerateAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
}
