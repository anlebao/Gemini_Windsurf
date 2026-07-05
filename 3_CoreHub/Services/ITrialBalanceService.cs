using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 4 — Trial Balance service (Sổ Tổng hợp).
/// Replaces the broken <c>HKDBookService.GenerateTrialBalanceAsync</c> query (Pattern #1 + #5 violations).
/// Groups JournalEntry lines by AccountNumber, includes opening balance per account, validates TotalDebit == TotalCredit.
/// Returns the existing <see cref="TrialBalance"/> Domain record (kept as-is per W2 note).
/// </summary>
public interface ITrialBalanceService
{
    /// <summary>
    /// Generate the Trial Balance for a tenant + period + accounting standard.
    /// Opening = Σ lines where EntryDate &lt; periodStart. Movement = Σ lines where EntryDate in [periodStart, periodEnd).
    /// Per-account: DebitTotal + CreditTotal (movement only) + Balance (opening + movement, signed debit - credit).
    /// TotalDebit == TotalCredit invariant (movement double-entry).
    /// </summary>
    Task<TrialBalance> GenerateAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
}
