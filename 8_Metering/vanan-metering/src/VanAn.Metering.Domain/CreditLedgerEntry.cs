namespace VanAn.Metering.Domain;

/// <summary>
/// P7 — append-only wallet ledger. Every balance mutation is one entry;
/// corrections are reversal entries (Type=Adjust), never updates to a row.
/// Credits is SIGNED: Grant/Adjust-credit positive, Deduct/Adjust-debit negative.
/// Wallet balance at any point == Σ Credits up to that point.
/// </summary>
public sealed class CreditLedgerEntry
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public LedgerEntryType Type { get; private set; }
    public LedgerEntrySource Source { get; private set; }

    /// <summary>Signed credit delta applied to the wallet.</summary>
    public decimal Credits { get; private set; }

    /// <summary>Balance right after this entry was applied (informational).</summary>
    public decimal BalanceAfter { get; private set; }

    /// <summary>Set when this entry came from a usage-event deduction.</summary>
    public Guid? UsageEventId { get; private set; }

    /// <summary>Set when this entry came from a confirmed top-up.</summary>
    public Guid? TopupRequestId { get; private set; }

    public string Reason { get; private set; } = string.Empty;
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    private CreditLedgerEntry() { } // EF Core

    public static CreditLedgerEntry Create(
        Guid tenantId,
        LedgerEntryType type,
        LedgerEntrySource source,
        decimal credits,
        decimal balanceAfter,
        string reason,
        string createdBy,
        DateTime nowUtc,
        Guid? usageEventId = null,
        Guid? topupRequestId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Type = type,
            Source = source,
            Credits = credits,
            BalanceAfter = balanceAfter,
            UsageEventId = usageEventId,
            TopupRequestId = topupRequestId,
            Reason = reason,
            CreatedBy = createdBy,
            CreatedAtUtc = nowUtc
        };
}

public enum LedgerEntryType
{
    Grant = 0,
    Deduct = 1,
    Adjust = 2
}

/// <summary>GĐ-1 only Purchased; Pool/Earned reserved for GĐ-2+.</summary>
public enum LedgerEntrySource
{
    Purchased = 0
}
