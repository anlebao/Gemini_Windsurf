using System.Text.Json.Serialization;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// ViettelConfig — bound from appsettings EInvoiceProviders:Viettel
/// User must fill FILL_IN_* values with real sandbox credentials.
/// </summary>
public record ViettelConfig(
    string Username,
    string Password,
    string TaxCode,
    string TemplateCode,
    string SerialNumber,
    string SandboxBaseUrl);

/// <summary>Viettel SInvoicer auth request</summary>
public record ViettelAuthRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

/// <summary>Viettel SInvoicer auth response</summary>
public record ViettelAuthResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken);

/// <summary>Viettel invoice submission payload</summary>
public class ViettelInvoicePayload
{
    [JsonPropertyName("invoiceType")]
    public string InvoiceType { get; set; } = string.Empty;

    [JsonPropertyName("templateCode")]
    public string TemplateCode { get; set; } = string.Empty;

    [JsonPropertyName("invoiceSeries")]
    public string InvoiceSeries { get; set; } = string.Empty;

    [JsonPropertyName("taxCode")]
    public string TaxCode { get; set; } = string.Empty;

    [JsonPropertyName("buyerName")]
    public string BuyerName { get; set; } = string.Empty;

    [JsonPropertyName("buyerTaxCode")]
    public string BuyerTaxCode { get; set; } = string.Empty;

    [JsonPropertyName("buyerAddress")]
    public string BuyerAddress { get; set; } = string.Empty;

    [JsonPropertyName("totalAmountWithoutTax")]
    public decimal TotalAmountWithoutTax { get; set; }

    [JsonPropertyName("totalVatAmount")]
    public decimal TotalVatAmount { get; set; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("invoiceDate")]
    public string InvoiceDate { get; set; } = string.Empty;
}

/// <summary>Viettel invoice result wrapper</summary>
public class ViettelInvoiceResult
{
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("result")]
    public ViettelInvoiceResultData? Result { get; set; }
}

public class ViettelInvoiceResultData
{
    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }
}

/// <summary>Viettel invoice status result</summary>
public class ViettelStatusResult
{
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("invoiceStatus")]
    public string? InvoiceStatus { get; set; }
}
