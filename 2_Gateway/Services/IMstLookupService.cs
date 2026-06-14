namespace VanAn.Gateway.Services;

public interface IMstLookupService
{
    /// <summary>Lookup business info by MST (tax code) via VietQR API.</summary>
    Task<BusinessLookupResult?> LookupByTaxCodeAsync(string taxCode, CancellationToken ct = default);
}

public record BusinessLookupResult(
    string TaxCode,
    string BusinessName,
    string? Address,
    string? Status
);
