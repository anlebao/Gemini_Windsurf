using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

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
/// E-Invoice Request - Raw request structure for E-Invoice providers
/// </summary>
public record EInvoiceRequest(
    TenantId TenantId,
    ElectronicInvoiceId InvoiceId,
    OrderId OrderId,
    InvoiceType InvoiceType,
    decimal Amount,
    decimal VatAmount,
    decimal TotalAmount,
    string CustomerName,
    string CustomerTaxCode,
    string CustomerAddress,
    DateTime InvoiceDate,
    Dictionary<string, string> AdditionalData
);

/// <summary>
/// E-Invoice Response - Raw response structure from E-Invoice providers
/// </summary>
public record EInvoiceResponse(
    bool Success,
    string? ProviderInvoiceNumber,
    string? TaxAuthorityInvoiceNumber,
    string? ErrorMessage,
    DateTime ProcessedAt,
    Dictionary<string, string> Metadata
);

/// <summary>
/// Invoice Status Response - Response for invoice status query
/// </summary>
public record InvoiceStatusResponse(
    string ProviderInvoiceNumber,
    InvoiceStatus Status,
    DateTime? ApprovedAt,
    string? FailureReason,
    Dictionary<string, string> Metadata
);

/// <summary>
/// IEInvoiceProvider - Stateless interface for E-Invoice provider integrations
/// Provider implementations MUST be stateless (no instance state)
/// </summary>
public interface IEInvoiceProvider
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
    /// Submit invoice to E-Invoice provider
    /// </summary>
    Task<EInvoiceResponse> SubmitInvoiceAsync(
        EInvoiceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get invoice status from provider
    /// </summary>
    Task<InvoiceStatusResponse> GetInvoiceStatusAsync(
        TenantId tenantId,
        string providerInvoiceNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel invoice at provider
    /// </summary>
    Task<EInvoiceResponse> CancelInvoiceAsync(
        TenantId tenantId,
        string providerInvoiceNumber,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Health check for provider
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
