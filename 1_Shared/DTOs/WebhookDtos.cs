using System.Text.Json.Serialization;
using VanAn.Shared.Domain;

namespace VanAn.Shared.DTOs;

/// <summary>
/// ViettelWebhookDto — Typed DTO for Viettel SInvoicer webhook callbacks.
/// Status codes: 1=Created, 2=PendingSend, 3=Approved, 4=Rejected, 5=Failed
/// Per Viettel SInvoicer API spec (2025).
/// </summary>
public sealed class ViettelWebhookDto
{
    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; init; }

    [JsonPropertyName("status")]
    public int Status { get; init; }

    [JsonPropertyName("issueDate")]
    public string? IssueDate { get; init; }

    [JsonPropertyName("buyerTaxCode")]
    public string? BuyerTaxCode { get; init; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; init; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; init; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Maps Viettel status codes to domain InvoiceStatus.
    /// 3 = TaxApproved, 4 = Rejected, else = SentToProvider
    /// </summary>
    public InvoiceStatus GetInvoiceStatus() => Status switch
    {
        3 => InvoiceStatus.TaxApproved,
        4 => InvoiceStatus.Rejected,
        5 => InvoiceStatus.Failed,
        _ => InvoiceStatus.SentToProvider
    };
}

/// <summary>
/// MisaWebhookDto — Typed DTO for MISA meInvoice webhook callbacks.
/// ProcessStatus codes: 1=Success/Approved, 2=Rejected, 3=Pending
/// Per MISA meInvoice API spec (2025).
/// </summary>
public sealed class MisaWebhookDto
{
    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; init; }

    [JsonPropertyName("invoiceCode")]
    public string? InvoiceCode { get; init; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; init; }

    [JsonPropertyName("processStatus")]
    public int ProcessStatus { get; init; }

    [JsonPropertyName("resultCode")]
    public int ResultCode { get; init; }

    [JsonPropertyName("resultMessage")]
    public string? ResultMessage { get; init; }

    [JsonPropertyName("submitDate")]
    public string? SubmitDate { get; init; }

    /// <summary>
    /// Maps MISA processStatus codes to domain InvoiceStatus.
    /// 1 = TaxApproved, 2 = Rejected, else = SentToProvider
    /// </summary>
    public InvoiceStatus GetInvoiceStatus() => ProcessStatus switch
    {
        1 => InvoiceStatus.TaxApproved,
        2 => InvoiceStatus.Rejected,
        _ => InvoiceStatus.SentToProvider
    };

    /// <summary>
    /// Returns failure reason when ProcessStatus indicates rejection.
    /// </summary>
    public string? GetFailureReason() =>
        ProcessStatus == 2 ? (ResultMessage ?? $"MISA rejection code {ResultCode}") : null;
}
