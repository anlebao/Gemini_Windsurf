namespace VanAn.Gateway.Services;

public class MstLookupService(
    IHttpClientFactory httpClientFactory,
    ILogger<MstLookupService> logger) : IMstLookupService
{
    public async Task<BusinessLookupResult?> LookupByTaxCodeAsync(string taxCode, CancellationToken ct = default)
    {
        // TODO: Sprint 4 - Implement actual VietQR API call
        // GET https://api.vietqr.io/v2/business/{taxCode}
        logger.LogInformation("MstLookupService: lookup taxCode={TaxCode} (stub)", taxCode);
        await Task.CompletedTask;
        return new BusinessLookupResult(taxCode, "Chưa tra cứu", null, "stub");
    }
}
