using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// TT 99/2025/TT-BTC — Financial Statement Notes service (Mẫu B 09-DN).
/// Generates textual notes explaining BCTC indicators + accounting policies.
/// Unlike the 3 numeric reports (B 01-DN/B 02-DN/B 03-DN), B 09-DN is a textual
/// report with 5 sections (I, II, III, IV, X) per Phụ lục IV TT 99.
/// </summary>
public interface IFinancialStatementNotesService
{
    /// <summary>
    /// Generate the Financial Statement Notes (B 09-DN) for a tenant + period + accounting standard.
    /// Pulls tenant info (LegalForm, BusinessField, CharterCapital) from TenantSettings for Phần I.
    /// Phần IV (29 accounting policies) uses TT 99 standard template text.
    /// </summary>
    Task<FinancialStatementNotes> GenerateAsync(TenantId tenantId, AccountingPeriod period, AccountingStandard standard, CancellationToken ct = default);
}
