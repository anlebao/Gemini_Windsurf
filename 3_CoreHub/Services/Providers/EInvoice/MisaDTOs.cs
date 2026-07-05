using System.Text.Json.Serialization;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// MisaConfig — bound from appsettings MisaConfig section.
/// W6-T5 (2026-07-05): Rewritten per real MISA meInvoice API spec.
/// Sandbox: testapi.meinvoice.vn | Production: api.meinvoice.vn
/// </summary>
public record MisaConfig(
    string CompanyCode,
    string ApiKey,
    string AppId,           // W6-T5: REQUIRED for auth (was missing)
    string Username,
    string Password,
    string InvoiceSeries,
    string BaseUrl,
    string? ProductionBaseUrl,
    string? SandboxBaseUrl)
{
    /// <summary>Effective BaseUrl — falls back to testapi.meinvoice.vn if unset.</summary>
    public string EffectiveBaseUrl => string.IsNullOrWhiteSpace(BaseUrl)
        ? (SandboxBaseUrl ?? "https://testapi.meinvoice.vn/")
        : BaseUrl;
}

// ── Auth ────────────────────────────────────────────────────────────────────

/// <summary>
/// MISA meInvoice auth request (POST /api/integration/auth/token).
/// W6-T5: Added AppId (REQUIRED — was missing in stub).
/// </summary>
public class MisaAuthRequest
{
    [JsonPropertyName("company_code")]
    public string CompanyCode { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("appid")]
    public string AppId { get; set; } = string.Empty;
}

/// <summary>
/// MISA meInvoice auth response — {Success, Data, ErrorCode} structure.
/// W6-T5: Fixed from flat {access_token} to nested structure per real spec.
/// Token expiry: 15 ngày (NOT 55 phút).
/// </summary>
public class MisaAuthResponse
{
    [JsonPropertyName("Success")]
    public bool Success { get; set; }

    [JsonPropertyName("Data")]
    public MisaAuthData? Data { get; set; }

    [JsonPropertyName("ErrorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }
}

public class MisaAuthData
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public long? ExpiresIn { get; set; }
}

// ── Create Invoice: payload with SignType + line items ──────────────────────

/// <summary>
/// MISA meInvoice submission payload — POST /api/integration/invoice.
/// W6-T5: Added SignType (REQUIRED — 2 sync or 3 async), OriginalInvoiceDetail[], TaxRateInfo[].
/// </summary>
public class MisaInvoicePayload
{
    [JsonPropertyName("InvSeries")]
    public string InvSeries { get; set; } = string.Empty;

    [JsonPropertyName("InvDate")]
    public string InvDate { get; set; } = string.Empty;

    /// <summary>SignType: 2 = sync (immediate), 3 = async (pending).</summary>
    [JsonPropertyName("SignType")]
    public int SignType { get; set; } = 2;

    [JsonPropertyName("BuyerName")]
    public string BuyerName { get; set; } = string.Empty;

    [JsonPropertyName("BuyerTaxCode")]
    public string BuyerTaxCode { get; set; } = string.Empty;

    [JsonPropertyName("BuyerAddress")]
    public string BuyerAddress { get; set; } = string.Empty;

    [JsonPropertyName("AmountWithoutTax")]
    public decimal AmountWithoutTax { get; set; }

    [JsonPropertyName("VatAmount")]
    public decimal VatAmount { get; set; }

    [JsonPropertyName("TotalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("CurrencyCode")]
    public string CurrencyCode { get; set; } = "VND";

    [JsonPropertyName("PaymentType")]
    public string PaymentType { get; set; } = "CASH";

    /// <summary>Line items — OriginalInvoiceDetail[] per MISA spec.</summary>
    [JsonPropertyName("OriginalInvoiceDetail")]
    public List<MisaInvoiceDetail> OriginalInvoiceDetail { get; set; } = new();

    /// <summary>Tax breakdown by rate — TaxRateInfo[] per MISA spec.</summary>
    [JsonPropertyName("TaxRateInfo")]
    public List<MisaTaxRateInfo> TaxRateInfo { get; set; } = new();
}

public class MisaInvoiceDetail
{
    [JsonPropertyName("ItemCode")]
    public string ItemCode { get; set; } = string.Empty;

    [JsonPropertyName("ItemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("Unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("Quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("UnitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("Amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("VatRate")]
    public decimal VatRate { get; set; }

    [JsonPropertyName("VatAmount")]
    public decimal VatAmount { get; set; }
}

public class MisaTaxRateInfo
{
    [JsonPropertyName("VatRate")]
    public decimal VatRate { get; set; }

    [JsonPropertyName("TaxableAmount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("VatAmount")]
    public decimal VatAmount { get; set; }
}

// ── Create Invoice: result ───────────────────────────────────────────────────

/// <summary>
/// MISA meInvoice submission result — {Success, Data, ErrorCode} structure.
/// </summary>
public class MisaInvoiceResult
{
    [JsonPropertyName("Success")]
    public bool Success { get; set; }

    [JsonPropertyName("Data")]
    public MisaInvoiceResultData? Data { get; set; }

    [JsonPropertyName("ErrorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }
}

public class MisaInvoiceResultData
{
    [JsonPropertyName("InvNo")]
    public string? InvNo { get; set; }

    [JsonPropertyName("TransactionID")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("ReservationCode")]
    public string? ReservationCode { get; set; }
}

// ── Status: GET /api/integration/invoice/{invNo} ─────────────────────────────

/// <summary>MISA invoice status result — {Success, Data, ErrorCode}.</summary>
public class MisaStatusResult
{
    [JsonPropertyName("Success")]
    public bool Success { get; set; }

    [JsonPropertyName("Data")]
    public MisaStatusData? Data { get; set; }

    [JsonPropertyName("ErrorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }
}

public class MisaStatusData
{
    [JsonPropertyName("InvNo")]
    public string? InvNo { get; set; }

    [JsonPropertyName("InvStatus")]
    public string? InvStatus { get; set; }

    [JsonPropertyName("ApprovedDate")]
    public string? ApprovedDate { get; set; }
}

// ── Cancel: POST /api/integration/invoice/cancel ─────────────────────────────

/// <summary>MISA cancel invoice request.</summary>
public class MisaCancelRequest
{
    [JsonPropertyName("InvNo")]
    public string InvNo { get; set; } = string.Empty;

    [JsonPropertyName("CancelReason")]
    public string CancelReason { get; set; } = string.Empty;
}

/// <summary>MISA cancel invoice result — {Success, Data, ErrorCode}.</summary>
public class MisaCancelResult
{
    [JsonPropertyName("Success")]
    public bool Success { get; set; }

    [JsonPropertyName("ErrorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }
}
