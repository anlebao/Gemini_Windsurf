using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using VanAn.CoreHub.Services.Providers.POS;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// ViettelEInvoiceProvider — Viettel S-Invoice v2.0 integration.
/// W6-T4 (2026-07-05): Rewritten per real API spec (vinvoice.viettel.vn).
/// Auth via Cookie (NOT Bearer). Nested payload. transactionUUID idempotency.
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
    public string ProviderName => "Viettel S-Invoice";

    public ProviderCapabilities Capabilities => new(
        RateLimit: 200,
        Timeout: TimeSpan.FromSeconds(90), // W6-T4: Viettel recommends 60-90s
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
            // Auth via Cookie — Viettel sets access_token cookie, NOT Bearer header
            await EnsureAuthenticatedAsync(cancellationToken);

            var supplierTaxCode = string.IsNullOrWhiteSpace(request.SupplierTaxCode)
                ? _config.TaxCode
                : request.SupplierTaxCode;

            var payload = BuildPayload(request, supplierTaxCode);

            // Create: POST InvoiceAPI/InvoiceWS/createInvoice/{supplierTaxCode}
            var response = await _httpClient.PostAsJsonAsync(
                $"InvoiceAPI/InvoiceWS/createInvoice/{Uri.EscapeDataString(supplierTaxCode)}",
                payload,
                cancellationToken);

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
                    Metadata: new Dictionary<string, string> { ["provider"] = ProviderId },
                    TransactionUuid: result.Result.TransactionId ?? request.TransactionUuid,
                    ReservationCode: result.Result.ReservationCode);
            }

            return new EInvoiceResponse(
                Success: false,
                ProviderInvoiceNumber: null,
                TaxAuthorityInvoiceNumber: null,
                ErrorMessage: result?.Description ?? $"Viettel errorCode: {result?.ErrorCode}",
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
        await EnsureAuthenticatedAsync(cancellationToken);

        // Status: POST InvoiceAPI/InvoiceWS/searchInvoiceByTransactionUuid
        // Note: providerInvoiceNumber here is the transactionUUID (callers should pass TransactionUuid)
        var statusRequest = new ViettelStatusRequest(
            SupplierTaxCode: _config.TaxCode,
            TransactionUuid: providerInvoiceNumber);

        var response = await _httpClient.PostAsJsonAsync(
            "InvoiceAPI/InvoiceWS/searchInvoiceByTransactionUuid",
            statusRequest,
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<ViettelStatusResult>(
            cancellationToken: cancellationToken);

        var status = result?.Result?.InvoiceStatus?.ToUpperInvariant() switch
        {
            "APPROVED" or "PUBLISHED" => InvoiceStatus.TaxApproved,
            "REJECTED" or "CANCELED" => InvoiceStatus.Rejected,
            "PENDING" or "WAITING" => InvoiceStatus.PendingSend,
            _ => InvoiceStatus.PendingSend
        };

        return new InvoiceStatusResponse(
            ProviderInvoiceNumber: result?.Result?.InvoiceNo ?? providerInvoiceNumber,
            Status: status,
            ApprovedAt: status == InvoiceStatus.TaxApproved ? DateTime.UtcNow : null,
            FailureReason: status == InvoiceStatus.Rejected ? result?.Description : null,
            Metadata: new Dictionary<string, string> { ["provider"] = ProviderId });
    }

    public async Task<EInvoiceResponse> CancelInvoiceAsync(
        TenantId tenantId,
        string providerInvoiceNumber,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        // Cancel: POST InvoiceAPI/InvoiceWS/cancelTransactionInvoice with 7 required fields
        var cancelRequest = new ViettelCancelRequest(
            SupplierTaxCode: _config.TaxCode,
            InvoiceNo: providerInvoiceNumber,
            AdditionalReferenceDate: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            AdditionalReferenceDesc: reason,
            CustomFields: string.Empty,
            FreeText: reason,
            TransactionUuid: Guid.NewGuid().ToString("N"));

        var response = await _httpClient.PostAsJsonAsync(
            "InvoiceAPI/InvoiceWS/cancelTransactionInvoice",
            cancelRequest,
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<ViettelCancelResult>(
            cancellationToken: cancellationToken);

        var success = response.IsSuccessStatusCode && result?.ErrorCode == "0";

        return new EInvoiceResponse(
            Success: success,
            ProviderInvoiceNumber: providerInvoiceNumber,
            TaxAuthorityInvoiceNumber: null,
            ErrorMessage: success ? null : result?.Description ?? $"Cancel failed: {response.StatusCode}",
            ProcessedAt: DateTime.UtcNow,
            Metadata: new Dictionary<string, string> { ["provider"] = ProviderId });
    }

    public async Task<byte[]> GetInvoiceFileAsync(
        TenantId tenantId,
        string providerInvoiceNumber,
        string fileFormat,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken);

        // GetFile: POST InvoiceAPI/InvoiceUtilsWS/getInvoiceRepresentationFile
        var fileRequest = new ViettelGetFileRequest(
            SupplierTaxCode: _config.TaxCode,
            InvoiceNo: providerInvoiceNumber,
            FileType: string.IsNullOrWhiteSpace(fileFormat) ? "PDF" : fileFormat.ToUpperInvariant());

        var response = await _httpClient.PostAsJsonAsync(
            "InvoiceAPI/InvoiceUtilsWS/getInvoiceRepresentationFile",
            fileRequest,
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

    private ViettelInvoicePayload BuildPayload(EInvoiceRequest request, string supplierTaxCode)
    {
        // Epoch milliseconds — Viettel requires this format for invoiceDate
        var epochMs = new DateTimeOffset(request.InvoiceDate).ToUnixTimeMilliseconds();

        var payload = new ViettelInvoicePayload
        {
            GeneralInvoiceInfo =
            {
                InvoiceType = MapInvoiceType(request.InvoiceType),
                TemplateCode = _config.TemplateCode,
                InvoiceSeries = _config.SerialNumber,
                InvoiceDate = epochMs,
                CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "VND" : request.CurrencyCode,
                AdjustmentType = 0, // 0 = original
                PaymentType = string.IsNullOrWhiteSpace(request.PaymentType) ? "CASH" : request.PaymentType,
                TransactionUuid = request.TransactionUuid
            },
            BuyerInfo =
            {
                BuyerName = request.CustomerName,
                BuyerTaxCode = request.CustomerTaxCode,
                BuyerAddress = request.CustomerAddress
            },
            SellerInfo =
            {
                SellerTaxCode = supplierTaxCode
            },
            SummarizeInfo =
            {
                TotalAmountWithoutTax = request.Amount,
                TotalVatAmount = request.VatAmount,
                TotalAmount = request.TotalAmount,
                DiscountAmount = 0m
            }
        };

        // Map line items from EInvoiceRequest.LineItems → itemInfo[]
        foreach (var item in request.LineItems)
        {
            payload.ItemInfo.Add(new ViettelItemInfo
            {
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                UnitName = item.Unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Amount = item.Amount,
                VatRate = item.VatRate,
                VatAmount = item.VatAmount
            });
        }

        // Build taxBreakdowns from line items (group by VatRate)
        var taxBreakdowns = request.LineItems
            .GroupBy(i => i.VatRate)
            .Select(g => new ViettelTaxBreakdown
            {
                TaxRate = g.Key,
                TaxableAmount = g.Sum(i => i.Amount),
                TaxAmount = g.Sum(i => i.VatAmount)
            })
            .ToList();
        payload.TaxBreakdowns = taxBreakdowns;

        return payload;
    }

    private static string MapInvoiceType(InvoiceType invoiceType) => invoiceType switch
    {
        InvoiceType.Goods => "01GTKT", // Hóa đơn GTGT bán hàng hóa
        InvoiceType.Services => "02GTKT", // Hóa đơn GTGT dịch vụ
        InvoiceType.Mixed => "01GTKT",
        InvoiceType.HKD => "01GTKT",
        _ => "01GTKT"
    };

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
        {
            // Re-apply Cookie header for subsequent calls
            ApplyCookie(_cachedToken);
            return;
        }

        var authRequest = new ViettelAuthRequest(_config.Username, _config.Password);
        var response = await _httpClient.PostAsJsonAsync("auth/login", authRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Viettel auth failed: {response.StatusCode}");

        var authResponse = await response.Content.ReadFromJsonAsync<ViettelAuthResponse>(
            cancellationToken: cancellationToken);

        _cachedToken = authResponse?.AccessToken
            ?? throw new InvalidOperationException("Viettel auth returned null token");

        // Viettel tokens typically expire in 24h; cache for 55 min as safety
        _tokenExpiry = DateTime.UtcNow.AddMinutes(55);
        ApplyCookie(_cachedToken);
    }

    /// <summary>
    /// Viettel uses Cookie-based auth (NOT Bearer). Set the access_token cookie on the handler.
    /// </summary>
    private void ApplyCookie(string token)
    {
        // Remove existing access_token cookie, then add the fresh one
        var cookieHeader = $"access_token={token}; Path=/";
        _httpClient.DefaultRequestHeaders.Remove("Cookie");
        _httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
    }
}
