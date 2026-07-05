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
/// <remarks>
/// W6-T3 (2026-07-05): Added SupplierTaxCode, LineItems, CurrencyCode, PaymentType
/// to support real Viettel/MISA API payloads (nested itemInfo[] / OriginalInvoiceDetail[]).
/// TransactionUuid derives from InvoiceId.Value.ToString("N") — providers map it to
/// Viettel transactionUuid / MISA idempotency key.
/// </remarks>
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
    Dictionary<string, string> AdditionalData,
    string SupplierTaxCode,
    IReadOnlyList<InvoiceItem> LineItems,
    string CurrencyCode,
    string PaymentType
)
{
    /// <summary>
    /// Idempotency key derived from InvoiceId (Viettel transactionUUID / MISA idempotency key).
    /// Format: 32-char hex (N format) of the InvoiceId Guid.
    /// </summary>
    public string TransactionUuid => InvoiceId.Value.ToString("N");
}

/// <summary>
/// E-Invoice Response - Raw response structure from E-Invoice providers
/// </summary>
/// <remarks>
/// W6-T3 (2026-07-05): Added TransactionUuid, ReservationCode for Viettel result fields.
/// </remarks>
public record EInvoiceResponse(
    bool Success,
    string? ProviderInvoiceNumber,
    string? TaxAuthorityInvoiceNumber,
    string? ErrorMessage,
    DateTime ProcessedAt,
    Dictionary<string, string> Metadata,
    string? TransactionUuid,
    string? ReservationCode
)
{
    /// <summary>
    /// Back-compat constructor for callers that do not yet populate TransactionUuid/ReservationCode.
    /// </summary>
    public EInvoiceResponse(
        bool Success,
        string? ProviderInvoiceNumber,
        string? TaxAuthorityInvoiceNumber,
        string? ErrorMessage,
        DateTime ProcessedAt,
        Dictionary<string, string> Metadata)
        : this(Success, ProviderInvoiceNumber, TaxAuthorityInvoiceNumber,
            ErrorMessage, ProcessedAt, Metadata, TransactionUuid: null, ReservationCode: null)
    { }
}

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
    /// Get invoice file (PDF/XML) from provider.
    /// W6-T3 (2026-07-05): Added for Viettel getInvoiceRepresentationFile / MISA download endpoints.
    /// </summary>
    /// <param name="tenantId">Tenant scope.</param>
    /// <param name="providerInvoiceNumber">Provider-issued invoice number.</param>
    /// <param name="fileFormat">"pdf" or "xml" (provider-specific).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File bytes (non-empty on success).</returns>
    Task<byte[]> GetInvoiceFileAsync(
        TenantId tenantId,
        string providerInvoiceNumber,
        string fileFormat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Health check for provider
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
