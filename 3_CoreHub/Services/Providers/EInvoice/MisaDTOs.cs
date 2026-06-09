using System.Text.Json.Serialization;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// MisaConfig — bound from appsettings EInvoiceProviders:Misa
/// User must fill FILL_IN_* values with real sandbox credentials.
/// </summary>
public record MisaConfig(
    string CompanyCode,
    string ApiKey,
    string Username,
    string Password,
    string InvoiceSeries,
    string SandboxBaseUrl);

/// <summary>MISA meInvoice auth request</summary>
public class MisaAuthRequest
{
    [JsonPropertyName("company_code")]
    public string CompanyCode { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>MISA meInvoice auth response</summary>
public record MisaAuthResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken);

/// <summary>MISA invoice submission payload</summary>
public class MisaInvoicePayload
{
    [JsonPropertyName("inv_series")]
    public string InvSeries { get; set; } = string.Empty;

    [JsonPropertyName("inv_date")]
    public string InvDate { get; set; } = string.Empty;

    [JsonPropertyName("buyer_name")]
    public string BuyerName { get; set; } = string.Empty;

    [JsonPropertyName("buyer_tax_code")]
    public string BuyerTaxCode { get; set; } = string.Empty;

    [JsonPropertyName("buyer_address")]
    public string BuyerAddress { get; set; } = string.Empty;

    [JsonPropertyName("amount_without_tax")]
    public decimal AmountWithoutTax { get; set; }

    [JsonPropertyName("vat_amount")]
    public decimal VatAmount { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; set; }
}

/// <summary>MISA invoice submission result</summary>
public class MisaInvoiceResult
{
    [JsonPropertyName("is_success")]
    public bool IsSuccess { get; set; }

    [JsonPropertyName("inv_no")]
    public string? InvNo { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

/// <summary>MISA invoice status result</summary>
public class MisaStatusResult
{
    [JsonPropertyName("invoice_status")]
    public string? InvoiceStatus { get; set; }

    [JsonPropertyName("approved_date")]
    public string? ApprovedDate { get; set; }
}
