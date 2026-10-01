namespace VanAn.Gateway.Services;

public interface IMstLookupService
{
    /// <summary>
    /// Lookup business info by MST (tax code).
    /// Local-first: checks VanAn PG tenants (crawled/onboarded data) before calling doanhnghiep.vn.
    /// </summary>
    /// <returns>null when the MST is not found (locally or remotely).</returns>
    Task<BusinessLookupResult?> LookupByTaxCodeAsync(string taxCode, CancellationToken ct = default);
}

/// <summary>
/// Business registration info (public data per Luật Doanh nghiệp 2020).
/// Status values follow doanhnghiep.vn: active / suspended / dissolved (mapped for local tenants).
/// </summary>
public record BusinessLookupResult(
    string TaxCode,
    string BusinessName,
    string? Address,
    string? Status,
    string? LegalRepName = null,
    string? IndustryName = null,
    string? ProvinceName = null,
    string Source = "doanhnghiep.vn");
