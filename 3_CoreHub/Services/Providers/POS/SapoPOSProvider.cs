using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Providers.POS;

/// <summary>
/// SapoPOSProvider - Sapo POS integration
/// Stateless implementation. HTTP client injection deferred to Sprint 4 (secrets needed).
/// </summary>
[Provider("sapo")]
public class SapoPOSProvider : IPOSProvider
{
    public string ProviderId => "sapo";
    public string ProviderName => "Sapo POS";

    public ProviderCapabilities Capabilities => new(
        RateLimit: 60,
        Timeout: TimeSpan.FromSeconds(45),
        MaxBatchSize: 100,
        SLA: TimeSpan.FromSeconds(8),
        ErrorPattern: @"^SAPO-\d{3}$");

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
