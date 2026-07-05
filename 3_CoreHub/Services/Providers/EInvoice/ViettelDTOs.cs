using System.Text.Json.Serialization;

namespace VanAn.CoreHub.Services.Providers.EInvoice;

/// <summary>
/// ViettelConfig — bound from appsettings ViettelConfig section.
/// W6-T4 (2026-07-05): Rewritten per real Viettel S-Invoice v2.0 API spec.
/// Sandbox: vinvoice.viettel.vn (NOT sinvoice — sinvoice is the old v1 endpoint).
/// </summary>
public record ViettelConfig(
    string Username,
    string Password,
    string TaxCode,
    string TemplateCode,
    string SerialNumber,
    string BaseUrl,
    string? ProductionBaseUrl,
    string? SandboxBaseUrl)
{
    /// <summary>Effective BaseUrl — falls back to vinvoice.viettel.vn if unset.</summary>
    public string EffectiveBaseUrl => string.IsNullOrWhiteSpace(BaseUrl)
        ? (SandboxBaseUrl ?? "https://vinvoice.viettel.vn/")
        : BaseUrl;
}

// ── Auth ────────────────────────────────────────────────────────────────────

/// <summary>Viettel S-Invoice auth request (POST /auth/login).</summary>
public record ViettelAuthRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

/// <summary>
/// Viettel S-Invoice auth response. The access_token is returned in the response body
/// AND set as a Cookie header (access_token=...). Provider uses the Cookie for subsequent calls.
/// </summary>
public record ViettelAuthResponse(
    [property: JsonPropertyName("access_token")] string? AccessToken,
    [property: JsonPropertyName("token_type")] string? TokenType,
    [property: JsonPropertyName("expires_in")] long? ExpiresIn);

// ── Create Invoice: nested payload per Viettel v2.0 spec ─────────────────────

/// <summary>
/// Viettel invoice submission payload — nested structure per real API spec.
/// Top-level: generalInvoiceInfo, buyerInfo, sellerInfo, itemInfo[], summarizeInfo, taxBreakdowns[].
/// </summary>
public class ViettelInvoicePayload
{
    [JsonPropertyName("generalInvoiceInfo")]
    public ViettelGeneralInvoiceInfo GeneralInvoiceInfo { get; set; } = new();

    [JsonPropertyName("buyerInfo")]
    public ViettelBuyerInfo BuyerInfo { get; set; } = new();

    [JsonPropertyName("sellerInfo")]
    public ViettelSellerInfo SellerInfo { get; set; } = new();

    [JsonPropertyName("itemInfo")]
    public List<ViettelItemInfo> ItemInfo { get; set; } = new();

    [JsonPropertyName("summarizeInfo")]
    public ViettelSummarizeInfo SummarizeInfo { get; set; } = new();

    [JsonPropertyName("taxBreakdowns")]
    public List<ViettelTaxBreakdown> TaxBreakdowns { get; set; } = new();

    [JsonPropertyName("metadata")]
    public List<ViettelMetadata>? Metadata { get; set; }
}

public class ViettelGeneralInvoiceInfo
{
    [JsonPropertyName("invoiceType")]
    public string InvoiceType { get; set; } = string.Empty;

    [JsonPropertyName("templateCode")]
    public string TemplateCode { get; set; } = string.Empty;

    [JsonPropertyName("invoiceSeries")]
    public string InvoiceSeries { get; set; } = string.Empty;

    /// <summary>Epoch milliseconds (Unix timestamp). Viettel requires this format.</summary>
    [JsonPropertyName("invoiceDate")]
    public long InvoiceDate { get; set; }

    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; } = "VND";

    [JsonPropertyName("adjustmentType")]
    public int AdjustmentType { get; set; } = 0; // 0 = original, 1 = replacement, 3 = cancellation

    [JsonPropertyName("paymentType")]
    public string PaymentType { get; set; } = "CASH";

    /// <summary>Idempotency key — maps from EInvoiceRequest.TransactionUuid.</summary>
    [JsonPropertyName("transactionUUID")]
    public string TransactionUuid { get; set; } = string.Empty;
}

public class ViettelBuyerInfo
{
    [JsonPropertyName("buyerName")]
    public string BuyerName { get; set; } = string.Empty;

    [JsonPropertyName("buyerTaxCode")]
    public string BuyerTaxCode { get; set; } = string.Empty;

    [JsonPropertyName("buyerAddress")]
    public string BuyerAddress { get; set; } = string.Empty;

    [JsonPropertyName("buyerBankName")]
    public string? BuyerBankName { get; set; }

    [JsonPropertyName("buyerBankAccount")]
    public string? BuyerBankAccount { get; set; }

    [JsonPropertyName("buyerEmail")]
    public string? BuyerEmail { get; set; }

    [JsonPropertyName("buyerPhone")]
    public string? BuyerPhone { get; set; }
}

public class ViettelSellerInfo
{
    [JsonPropertyName("sellerName")]
    public string? SellerName { get; set; }

    [JsonPropertyName("sellerTaxCode")]
    public string SellerTaxCode { get; set; } = string.Empty;

    [JsonPropertyName("sellerAddress")]
    public string? SellerAddress { get; set; }

    [JsonPropertyName("sellerBankName")]
    public string? SellerBankName { get; set; }

    [JsonPropertyName("sellerBankAccount")]
    public string? SellerBankAccount { get; set; }

    [JsonPropertyName("sellerEmail")]
    public string? SellerEmail { get; set; }

    [JsonPropertyName("sellerPhone")]
    public string? SellerPhone { get; set; }
}

public class ViettelItemInfo
{
    [JsonPropertyName("itemCode")]
    public string ItemCode { get; set; } = string.Empty;

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("unitName")]
    public string UnitName { get; set; } = string.Empty;

    [JsonPropertyName("qty")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("taxRate")]
    public decimal VatRate { get; set; }

    [JsonPropertyName("taxAmount")]
    public decimal VatAmount { get; set; }
}

public class ViettelSummarizeInfo
{
    [JsonPropertyName("sumAmount")]
    public decimal TotalAmountWithoutTax { get; set; }

    [JsonPropertyName("sumTaxAmount")]
    public decimal TotalVatAmount { get; set; }

    [JsonPropertyName("sumTotalAmountOf")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("discountAmount")]
    public decimal DiscountAmount { get; set; }
}

public class ViettelTaxBreakdown
{
    [JsonPropertyName("taxRate")]
    public decimal TaxRate { get; set; }

    [JsonPropertyName("taxableAmount")]
    public decimal TaxableAmount { get; set; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; set; }
}

public class ViettelMetadata
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

// ── Create Invoice: result ───────────────────────────────────────────────────

/// <summary>Viettel createInvoice result wrapper.</summary>
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
    [JsonPropertyName("supplierTaxCode")]
    public string? SupplierTaxCode { get; set; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }

    [JsonPropertyName("transactionID")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("reservationCode")]
    public string? ReservationCode { get; set; }
}

// ── Status: searchInvoiceByTransactionUuid ───────────────────────────────────

/// <summary>Request body for searchInvoiceByTransactionUuid (POST, JSON).</summary>
public record ViettelStatusRequest(
    [property: JsonPropertyName("supplierTaxCode")] string SupplierTaxCode,
    [property: JsonPropertyName("transactionUUID")] string TransactionUuid);

/// <summary>Result for searchInvoiceByTransactionUuid.</summary>
public class ViettelStatusResult
{
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("result")]
    public ViettelStatusResultData? Result { get; set; }
}

public class ViettelStatusResultData
{
    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }

    [JsonPropertyName("transactionID")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("invoiceStatus")]
    public string? InvoiceStatus { get; set; }

    [JsonPropertyName("invoiceType")]
    public string? InvoiceType { get; set; }
}

// ── Cancel: cancelTransactionInvoice (7 required fields) ─────────────────────

/// <summary>
/// Viettel cancelTransactionInvoice request — 7 required fields (form-urlencoded).
/// </summary>
public record ViettelCancelRequest(
    [property: JsonPropertyName("supplierTaxCode")] string SupplierTaxCode,
    [property: JsonPropertyName("invoiceNo")] string InvoiceNo,
    [property: JsonPropertyName("additionalReferenceDate")] string AdditionalReferenceDate,
    [property: JsonPropertyName("additionalReferenceDesc")] string AdditionalReferenceDesc,
    [property: JsonPropertyName("customFields")] string CustomFields,
    [property: JsonPropertyName("freeText")] string FreeText,
    [property: JsonPropertyName("transactionUUID")] string TransactionUuid);

/// <summary>Viettel cancelTransactionInvoice result.</summary>
public class ViettelCancelResult
{
    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

// ── GetFile: getInvoiceRepresentationFile ─────────────────────────────────────

/// <summary>Request body for getInvoiceRepresentationFile (POST, JSON).</summary>
public record ViettelGetFileRequest(
    [property: JsonPropertyName("supplierTaxCode")] string SupplierTaxCode,
    [property: JsonPropertyName("invoiceNo")] string InvoiceNo,
    [property: JsonPropertyName("fileType")] string FileType); // "PDF" or "XML"
