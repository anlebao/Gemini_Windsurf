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

    /// <summary>
    /// Generate the Cash Flow Statement (indirect method) — TT 99 B 03-DN.
    /// Starts from NetProfit (B 02-DN Mã 50), adjusts for non-cash items (depreciation, provisions),
    /// then adjusts for working capital changes (Δ receivables, Δ inventory, Δ payables).
    /// Investing + Financing sections are same as direct method.
    /// </summary>
    Task<CashFlowStatement> GenerateIndirectAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
}
