using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using VanAn.CoreHub.Services.Providers.POS;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// MisaEInvoiceProvider — MISA meInvoice integration
/// HTTP client injected via named client "misa". Token cached per instance lifetime.
/// </summary>
[Provider("misa")]
public class MisaEInvoiceProvider : IEInvoiceProvider
{
    private readonly HttpClient _httpClient;
    private readonly MisaConfig _config;
    private readonly ILogger<MisaEInvoiceProvider> _logger;

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public MisaEInvoiceProvider(
        HttpClient httpClient,
        IOptions<MisaConfig> config,
        ILogger<MisaEInvoiceProvider> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
    }

    public string ProviderId => "misa";
    public string ProviderName => "MISA meInvoice";

    public ProviderCapabilities Capabilities => new(
        RateLimit: 150,
        Timeout: TimeSpan.FromSeconds(45),
        MaxBatchSize: 100,
        SLA: TimeSpan.FromSeconds(8),
        ErrorPattern: @"^MISA-\d{3}$");

    public async Task<EInvoiceResponse> SubmitInvoiceAsync(
        EInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var token = await GetAccessTokenAsync(cancellationToken);
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var payload = new MisaInvoicePayload
            {
                InvSeries = _config.InvoiceSeries,
                InvDate = request.InvoiceDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                BuyerName = request.CustomerName,
                BuyerTaxCode = request.CustomerTaxCode,
                BuyerAddress = request.CustomerAddress,
                AmountWithoutTax = request.Amount,
                VatAmount = request.VatAmount,
                TotalAmount = request.TotalAmount
            };

            var response = await _httpClient.PostAsJsonAsync(
                "einvoices", payload, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<MisaInvoiceResult>(
                cancellationToken: cancellationToken);

            if (result?.IsSuccess == true && result.InvNo != null)
            {
                return new EInvoiceResponse(
                    Success: true,
                    ProviderInvoiceNumber: result.InvNo,
                    TaxAuthorityInvoiceNumber: null,
                    ErrorMessage: null,
                    ProcessedAt: DateTime.UtcNow,
                    Metadata: new Dictionary<string, string> { ["provider"] = ProviderId });
            }

            return new EInvoiceResponse(
                Success: false,
                ProviderInvoiceNumber: null,
                TaxAuthorityInvoiceNumber: null,
                ErrorMessage: result?.ErrorMessage ?? "Unknown error from MISA",
                ProcessedAt: DateTime.UtcNow,
                Metadata: new Dictionary<string, string> { ["provider"] = ProviderId });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MISA SubmitInvoice failed for invoice {InvoiceId}", request.InvoiceId);
            return new EInvoiceResponse(
                Success: false,
                ProviderInvoiceNumber: null,
                TaxAuthorityInvoiceNumber: null,
                ErrorMessage: ex.Message,
                ProcessedAt: DateTime.UtcNow,
                Metadata: new Dictionary<string, string> { ["provider"] = ProviderId });
        }
    }

    public async Task<InvoiceStatusResponse> GetInvoiceStatusAsync(
        TenantId tenantId,
        string providerInvoiceNumber,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.GetFromJsonAsync<MisaStatusResult>(
            $"einvoices/{Uri.EscapeDataString(providerInvoiceNumber)}/status",
            cancellationToken);

        var status = response?.InvoiceStatus?.ToUpperInvariant() switch
        {
            "APPROVED" => InvoiceStatus.TaxApproved,
            "REJECTED" => InvoiceStatus.Rejected,
            _ => InvoiceStatus.PendingSend
        };

        DateTime? approvedAt = null;
        if (status == InvoiceStatus.TaxApproved && response?.ApprovedDate != null)
            _ = DateTime.TryParse(response.ApprovedDate, out var parsed)
                ? approvedAt = parsed
                : approvedAt = DateTime.UtcNow;

        return new InvoiceStatusResponse(
            ProviderInvoiceNumber: providerInvoiceNumber,
            Status: status,
            ApprovedAt: approvedAt,
            FailureReason: status == InvoiceStatus.Rejected ? response?.InvoiceStatus : null,
            Metadata: new Dictionary<string, string> { ["provider"] = ProviderId });
    }

    public async Task<EInvoiceResponse> CancelInvoiceAsync(
        TenantId tenantId,
        string providerInvoiceNumber,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = new { inv_no = providerInvoiceNumber, cancel_reason = reason };
        var response = await _httpClient.PostAsJsonAsync(
            $"einvoices/{Uri.EscapeDataString(providerInvoiceNumber)}/cancel",
            payload, cancellationToken);

        return new EInvoiceResponse(
            Success: response.IsSuccessStatusCode,
            ProviderInvoiceNumber: providerInvoiceNumber,
            TaxAuthorityInvoiceNumber: null,
            ErrorMessage: response.IsSuccessStatusCode ? null : $"Cancel failed: {response.StatusCode}",
            ProcessedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, string> { ["provider"] = ProviderId });
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;

        var authRequest = new MisaAuthRequest
        {
            CompanyCode = _config.CompanyCode,
            Username = _config.Username,
            Password = _config.Password
        };

        var response = await _httpClient.PostAsJsonAsync("auth/login", authRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"MISA auth failed: {response.StatusCode}");

        var authResponse = await response.Content.ReadFromJsonAsync<MisaAuthResponse>(
            cancellationToken: cancellationToken);

        _cachedToken = authResponse?.AccessToken
            ?? throw new InvalidOperationException("MISA auth returned null token");

        _tokenExpiry = DateTime.UtcNow.AddMinutes(55);
        return _cachedToken;
    }
}
