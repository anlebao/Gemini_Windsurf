using System.Text.Json.Serialization;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// Webhook payload DTO from Viettel e-invoice provider callback.
/// Maps Viettel's status codes to internal InvoiceStatus.
/// </summary>
public class ViettelWebhookDto
{
    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }

    /// <summary>Viettel status code: 1=Pending, 2=Processing, 3=Approved, 4=Rejected.</summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("issueDate")]
    public string? IssueDate { get; set; }

    [JsonPropertyName("buyerTaxCode")]
    public string? BuyerTaxCode { get; set; }

    [JsonPropertyName("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("taxAmount")]
    public decimal TaxAmount { get; set; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("signedTime")]
    public DateTime? SignedTime { get; set; }

    /// <summary>Maps Viettel status int to InvoiceStatus enum.</summary>
    public InvoiceStatus? GetInvoiceStatus()
    {
        return Status switch
        {
            3 => InvoiceStatus.TaxApproved,
            4 => InvoiceStatus.Rejected,
            1 or 2 => InvoiceStatus.SentToProvider,
            _ => null
        };
    }
}

/// <summary>
/// Webhook payload DTO from MISA e-invoice provider callback.
/// Maps MISA's status codes to internal InvoiceStatus.
/// </summary>
public class MisaWebhookDto
{
    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("invoiceCode")]
    public string? InvoiceCode { get; set; }

    [JsonPropertyName("invoiceNo")]
    public string? InvoiceNo { get; set; }

    /// <summary>MISA process status: 1=Success, 2=Failed, 3=Processing.</summary>
    [JsonPropertyName("processStatus")]
    public int ProcessStatus { get; set; }

    [JsonPropertyName("resultCode")]
    public int ResultCode { get; set; }

    [JsonPropertyName("resultMessage")]
    public string? ResultMessage { get; set; }

    [JsonPropertyName("submitDate")]
    public string? SubmitDate { get; set; }

    [JsonPropertyName("invoiceStatus")]
    public int InvoiceStatus { get; set; }

    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; set; }

    [JsonPropertyName("processedAt")]
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Maps MISA status int to InvoiceStatus enum.</summary>
    public VanAn.Shared.Domain.InvoiceStatus? GetInvoiceStatus()
    {
        // ProcessStatus takes precedence if set
        if (ProcessStatus != 0)
        {
            return ProcessStatus switch
            {
                1 => VanAn.Shared.Domain.InvoiceStatus.TaxApproved,
                2 => VanAn.Shared.Domain.InvoiceStatus.Rejected,
                3 => VanAn.Shared.Domain.InvoiceStatus.SentToProvider,
                _ => null
            };
        }
        return InvoiceStatus switch
        {
            1 => VanAn.Shared.Domain.InvoiceStatus.TaxApproved,
            2 => VanAn.Shared.Domain.InvoiceStatus.Rejected,
            3 => VanAn.Shared.Domain.InvoiceStatus.SentToProvider,
            _ => null
        };
    }

    /// <summary>Returns failure reason string if present.</summary>
    public string? GetFailureReason() => FailureReason;
}
