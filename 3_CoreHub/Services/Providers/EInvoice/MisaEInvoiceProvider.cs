using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using VanAn.CoreHub.Services.Providers.POS;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// MisaEInvoiceProvider — MISA meInvoice integration.
/// W6-T5 (2026-07-05): Rewritten per real MISA meInvoice API spec.
/// Auth via /api/integration/auth/token with appid. Bearer token (15-day expiry).
/// Create via /api/integration/invoice with SignType + line items.
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

            var payload = BuildPayload(request);

            // Create: POST /api/integration/invoice
            var response = await _httpClient.PostAsJsonAsync(
                "api/integration/invoice", payload, cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<MisaInvoiceResult>(
                cancellationToken: cancellationToken);

            if (result?.Success == true && result.Data?.InvNo != null)
            {
                return new EInvoiceResponse(
                    Success: true,
                    ProviderInvoiceNumber: result.Data.InvNo,
                    TaxAuthorityInvoiceNumber: null,
                    ErrorMessage: null,
                    ProcessedAt: DateTime.UtcNow,
                    Metadata: new Dictionary<string, string> { ["provider"] = ProviderId },
                    TransactionUuid: result.Data.TransactionId ?? request.TransactionUuid,
                    ReservationCode: result.Data.ReservationCode);
            }

            return new EInvoiceResponse(
                Success: false,
                ProviderInvoiceNumber: null,
                TaxAuthorityInvoiceNumber: null,
                ErrorMessage: result?.ErrorMessage ?? $"MISA ErrorCode: {result?.ErrorCode}",
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

        // Status: GET /api/integration/invoice/{invNo}
        var response = await _httpClient.GetFromJsonAsync<MisaStatusResult>(
            $"api/integration/invoice/{Uri.EscapeDataString(providerInvoiceNumber)}",
            cancellationToken);

        var status = response?.Data?.InvStatus?.ToUpperInvariant() switch
        {
            "APPROVED" or "PUBLISHED" => InvoiceStatus.TaxApproved,
            "REJECTED" or "CANCELED" => InvoiceStatus.Rejected,
            "PENDING" or "WAITING" => InvoiceStatus.PendingSend,
            _ => InvoiceStatus.PendingSend
        };

        DateTime? approvedAt = null;
        if (status == InvoiceStatus.TaxApproved && response?.Data?.ApprovedDate != null)
            _ = DateTime.TryParse(response.Data.ApprovedDate, out var parsed)
                ? approvedAt = parsed
                : approvedAt = DateTime.UtcNow;

        return new InvoiceStatusResponse(
            ProviderInvoiceNumber: response?.Data?.InvNo ?? providerInvoiceNumber,
            Status: status,
            ApprovedAt: approvedAt,
            FailureReason: status == InvoiceStatus.Rejected ? response?.ErrorMessage : null,
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

        // Cancel: POST /api/integration/invoice/cancel
        var cancelRequest = new MisaCancelRequest
        {
            InvNo = providerInvoiceNumber,
            CancelReason = reason
        };

        var response = await _httpClient.PostAsJsonAsync(
            "api/integration/invoice/cancel", cancelRequest, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<MisaCancelResult>(
            cancellationToken: cancellationToken);

        var success = response.IsSuccessStatusCode && result?.Success == true;

        return new EInvoiceResponse(
            Success: success,
            ProviderInvoiceNumber: providerInvoiceNumber,
            TaxAuthorityInvoiceNumber: null,
            ErrorMessage: success ? null : result?.ErrorMessage ?? $"Cancel failed: {response.StatusCode}",
            ProcessedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, string> { ["provider"] = ProviderId });
    }

    public async Task<byte[]> GetInvoiceFileAsync(
        TenantId tenantId,
        string providerInvoiceNumber,
        string fileFormat,
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // GetFile: GET /api/integration/invoice/{invNo}/file?format={format}
        var fmt = string.IsNullOrWhiteSpace(fileFormat) ? "pdf" : fileFormat.ToLowerInvariant();
        var response = await _httpClient.GetAsync(
            $"api/integration/invoice/{Uri.EscapeDataString(providerInvoiceNumber)}/file?format={fmt}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private MisaInvoicePayload BuildPayload(EInvoiceRequest request)
    {
        var payload = new MisaInvoicePayload
        {
            InvSeries = _config.InvoiceSeries,
            InvDate = request.InvoiceDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            SignType = 2, // 2 = sync (immediate signing)
            BuyerName = request.CustomerName,
            BuyerTaxCode = request.CustomerTaxCode,
            BuyerAddress = request.CustomerAddress,
            AmountWithoutTax = request.Amount,
            VatAmount = request.VatAmount,
            TotalAmount = request.TotalAmount,
            CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "VND" : request.CurrencyCode,
            PaymentType = string.IsNullOrWhiteSpace(request.PaymentType) ? "CASH" : request.PaymentType
        };

        // Map line items → OriginalInvoiceDetail[]
        foreach (var item in request.LineItems)
        {
            payload.OriginalInvoiceDetail.Add(new MisaInvoiceDetail
            {
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                Unit = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Amount = item.Amount,
                VatRate = item.VatRate,
                VatAmount = item.VatAmount
            });
        }

        // Build TaxRateInfo[] from line items (group by VatRate)
        var taxRateInfos = request.LineItems
            .GroupBy(i => i.VatRate)
            .Select(g => new MisaTaxRateInfo
            {
                VatRate = g.Key,
                TaxableAmount = g.Sum(i => i.Amount),
                VatAmount = g.Sum(i => i.VatAmount)
            })
            .ToList();
        payload.TaxRateInfo = taxRateInfos;

        return payload;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;

        // Auth: POST /api/integration/auth/token with appid
        var authRequest = new MisaAuthRequest
        {
            CompanyCode = _config.CompanyCode,
            Username = _config.Username,
            Password = _config.Password,
            AppId = _config.AppId
        };

        var response = await _httpClient.PostAsJsonAsync(
            "api/integration/auth/token", authRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"MISA auth failed: {response.StatusCode}");

        var authResponse = await response.Content.ReadFromJsonAsync<MisaAuthResponse>(
            cancellationToken: cancellationToken);

        if (authResponse?.Success != true || authResponse.Data?.AccessToken == null)
            throw new InvalidOperationException(
                $"MISA auth failed: {authResponse?.ErrorMessage ?? authResponse?.ErrorCode ?? "Unknown"}");

        _cachedToken = authResponse.Data.AccessToken;

        // W6-T5: MISA token expiry = 15 days (NOT 55 minutes)
        // Cache for 14 days as safety margin
        _tokenExpiry = DateTime.UtcNow.AddDays(14);
        return _cachedToken;
    }
}
