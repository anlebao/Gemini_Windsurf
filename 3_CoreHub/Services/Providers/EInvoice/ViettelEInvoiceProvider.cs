using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using VanAn.CoreHub.Services.Providers.POS;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// ViettelEInvoiceProvider — Viettel SInvoicer integration
/// HTTP client injected via named client "viettel". Token cached per instance lifetime.
/// </summary>
[Provider("viettel")]
public class ViettelEInvoiceProvider : IEInvoiceProvider
{
    private readonly HttpClient _httpClient;
    private readonly ViettelConfig _config;
    private readonly ILogger<ViettelEInvoiceProvider> _logger;

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public ViettelEInvoiceProvider(
        HttpClient httpClient,
        IOptions<ViettelConfig> config,
        ILogger<ViettelEInvoiceProvider> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
    }

    public string ProviderId => "viettel";
    public string ProviderName => "Viettel SInvoicer";

    public ProviderCapabilities Capabilities => new(
        RateLimit: 200,
        Timeout: TimeSpan.FromSeconds(30),
        MaxBatchSize: 50,
        SLA: TimeSpan.FromSeconds(5),
        ErrorPattern: @"^VT-\d{4}$");

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

            var payload = new ViettelInvoicePayload
            {
                InvoiceType = request.InvoiceType.ToString(),
                TemplateCode = _config.TemplateCode,
                InvoiceSeries = _config.SerialNumber,
                TaxCode = _config.TaxCode,
                BuyerName = request.CustomerName,
                BuyerTaxCode = request.CustomerTaxCode,
                BuyerAddress = request.CustomerAddress,
                TotalAmountWithoutTax = request.Amount,
                TotalVatAmount = request.VatAmount,
                TotalAmount = request.TotalAmount,
                InvoiceDate = request.InvoiceDate.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            var response = await _httpClient.PostAsJsonAsync(
                "InvoiceAPI/services/createInvoice", payload, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<ViettelInvoiceResult>(
                cancellationToken: cancellationToken);

            if (result?.ErrorCode == "0" && result.Result?.InvoiceNo != null)
            {
                return new EInvoiceResponse(
                    Success: true,
                    ProviderInvoiceNumber: result.Result.InvoiceNo,
                    TaxAuthorityInvoiceNumber: null,
                    ErrorMessage: null,
                    ProcessedAt: DateTime.UtcNow,
                    Metadata: new Dictionary<string, string> { ["provider"] = ProviderId });
            }

            return new EInvoiceResponse(
                Success: false,
                ProviderInvoiceNumber: null,
                TaxAuthorityInvoiceNumber: null,
                ErrorMessage: result?.Description ?? "Unknown error from Viettel",
                ProcessedAt: DateTime.UtcNow,
                Metadata: new Dictionary<string, string> { ["provider"] = ProviderId });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Viettel SubmitInvoice failed for invoice {InvoiceId}", request.InvoiceId);
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

        var response = await _httpClient.GetFromJsonAsync<ViettelStatusResult>(
            $"InvoiceAPI/services/getInvoiceStatus?invoiceNo={Uri.EscapeDataString(providerInvoiceNumber)}",
            cancellationToken);

        var status = response?.InvoiceStatus?.ToUpperInvariant() switch
        {
            "APPROVED" => InvoiceStatus.TaxApproved,
            "REJECTED" => InvoiceStatus.Rejected,
            _ => InvoiceStatus.PendingSend
        };

        return new InvoiceStatusResponse(
            ProviderInvoiceNumber: providerInvoiceNumber,
            Status: status,
            ApprovedAt: status == InvoiceStatus.TaxApproved ? DateTime.UtcNow : null,
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

        var payload = new { invoiceNo = providerInvoiceNumber, cancelReason = reason };
        var response = await _httpClient.PostAsJsonAsync(
            "InvoiceAPI/services/cancelInvoice", payload, cancellationToken);

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

        var authRequest = new ViettelAuthRequest(_config.Username, _config.Password);
        var response = await _httpClient.PostAsJsonAsync("auth/token", authRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Viettel auth failed: {response.StatusCode}");

        var authResponse = await response.Content.ReadFromJsonAsync<ViettelAuthResponse>(
            cancellationToken: cancellationToken);

        _cachedToken = authResponse?.AccessToken
            ?? throw new InvalidOperationException("Viettel auth returned null token");

        _tokenExpiry = DateTime.UtcNow.AddMinutes(55);
        return _cachedToken;
    }
}
