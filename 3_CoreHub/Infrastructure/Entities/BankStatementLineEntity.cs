using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure.Entities;

/// <summary>
/// SRS v1.2 W5/F (FR-RECNC-001): 1 dòng sao kê ngân hàng import từ CSV.
/// Match thủ công vs JournalEntryLine trên TK 112x — KHÔNG auto-post.
/// </summary>
public class BankStatementLineEntity : BaseEntity
{
    public Guid BankAccountId { get; private set; }
    public DateTime TxnDate { get; private set; }
    /// <summary>Số tiền (+ ghi có / − ghi nợ theo sao kê).</summary>
    public decimal Amount { get; private set; }
    public string? Reference { get; private set; }
    public string? Description { get; private set; }
    /// <summary>JournalLineId (line.Id — int) đã match — null = chưa match.</summary>
    public int? MatchedJournalLineId { get; private set; }
    public DateTime ImportedAt { get; private set; }

    private BankStatementLineEntity() { } // EF Core

    public BankStatementLineEntity(TenantId tenantId, Guid bankAccountId, DateTime txnDate,
        decimal amount, string? reference, string? description) : base(tenantId)
    {
        BankAccountId = bankAccountId;
        TxnDate = txnDate;
        Amount = amount;
        Reference = reference;
        Description = description;
        ImportedAt = DateTime.UtcNow;
    }

    public void MarkMatched(int journalLineId) => MatchedJournalLineId = journalLineId;
    public void MarkUnmatched() => MatchedJournalLineId = null;
}
