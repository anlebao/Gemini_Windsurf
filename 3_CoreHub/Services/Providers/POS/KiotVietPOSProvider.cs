using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Providers.POS;

/// <summary>
/// KiotVietPOSProvider - KiotViet POS integration
/// Stateless implementation. HTTP client injection deferred to Sprint 4 (secrets needed).
/// </summary>
[Provider("kiotviet")]
public class KiotVietPOSProvider : IPOSProvider
{
    public string ProviderId => "kiotviet";
    public string ProviderName => "KiotViet POS";

    public ProviderCapabilities Capabilities => new(
        RateLimit: 100,
        Timeout: TimeSpan.FromSeconds(30),
        MaxBatchSize: 50,
        SLA: TimeSpan.FromSeconds(5),
        ErrorPattern: @"^KV-\d{4}$");

    public Task<POSOrderResponse> SyncOrdersAsync(
        TenantId tenantId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new POSOrderResponse(
            Success: true,
            OrderId: null,
            ErrorMessage: null,
            ProcessedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, string>
            {
                ["provider"] = ProviderId,
                ["fromDate"] = fromDate.ToString("O"),
                ["toDate"] = toDate.ToString("O")
            }));
    }

    public Task<POSOrderResponse> GetOrderAsync(
        TenantId tenantId,
        OrderId orderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new POSOrderResponse(
            Success: true,
            OrderId: orderId.Value.ToString(),
            ErrorMessage: null,
            ProcessedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, string>
            {
                ["provider"] = ProviderId
            }));
    }

    public Task<POSOrderResponse> SubmitInvoiceAsync(
        POSOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(new POSOrderResponse(
            Success: true,
            OrderId: request.OrderId.Value.ToString(),
            ErrorMessage: null,
            ProcessedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, string>
            {
                ["provider"] = ProviderId,
                ["customerName"] = request.CustomerName,
                ["totalAmount"] = request.TotalAmount.ToString("F2")
            }));
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
