using VanAn.Shared.Domain;

namespace VanAn.Shared.DTOs;

/// <summary>
/// Data Transfer Object for Accounting Entry
/// Used for API responses and service layer communication
/// </summary>
public class AccountingEntryDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public AccountingEntryType EntryType { get; set; }
    public DateTime CreatedAt { get; set; }
    public AccountingBookType AccountingBookType { get; set; }
    public int PeriodYear { get; set; }
    public int PeriodMonth { get; set; }
    public Guid? ReversalEntryId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Vendor { get; set; }
    public string? Category { get; set; }
    public string? Reference { get; set; }

    /// <summary>
    /// Alias for TransactionDate — used in form submission
    /// </summary>
    public DateTime Date
    {
        get => TransactionDate;
        set => TransactionDate = value;
    }
}
