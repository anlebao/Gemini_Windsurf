using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Providers.POS;

/// <summary>
/// Provider Capabilities - Define provider-specific capabilities
/// Stateless configuration for normalization across providers
/// </summary>
public record ProviderCapabilities(
    int RateLimit,           // Max requests per minute
    TimeSpan Timeout,        // Request timeout
    int MaxBatchSize,        // Max items per batch
    TimeSpan SLA,            // Service Level Agreement (expected response time)
    string ErrorPattern      // Regex pattern for error detection
);

/// <summary>
/// POS Order Request - Raw request structure for POS providers
/// </summary>
public record POSOrderRequest(
    TenantId TenantId,
    OrderId OrderId,
    DateTime OrderDate,
    decimal TotalAmount,
    string CustomerName,
    string CustomerPhone,
    List<POSOrderItem> Items
);

/// <summary>
/// POS Order Item - Line item in POS order
/// </summary>
public record POSOrderItem(
    ProductId ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal
);

/// <summary>
/// POS Order Response - Raw response structure from POS providers
/// </summary>
public record POSOrderResponse(
    bool Success,
    string? OrderId,
    string? ErrorMessage,
    DateTime ProcessedAt,
    Dictionary<string, string> Metadata
);

/// <summary>
/// IPOSProvider - Stateless interface for POS provider integrations
/// Provider implementations MUST be stateless (no instance state)
/// </summary>
public interface IPOSProvider
{
    /// <summary>
    /// Provider ID - Unique identifier for this provider
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Provider Name - Human-readable name
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Provider Capabilities - Define provider-specific capabilities
    /// </summary>
    ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Sync orders from POS system
    /// </summary>
    Task<POSOrderResponse> SyncOrdersAsync(
        TenantId tenantId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get specific order from POS system
    /// </summary>
    Task<POSOrderResponse> GetOrderAsync(
        TenantId tenantId,
        OrderId orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submit invoice to POS system
    /// </summary>
    Task<POSOrderResponse> SubmitInvoiceAsync(
        POSOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Health check for provider
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
