namespace VanAn.Metering.Domain;

/// <summary>
/// P10 (task_p10_credit_lots_expiry) — one grant = one lot with its own expiry.
/// Credits are consumed FEFO (earliest ExpiresAtUtc first; null = never expires,
/// consumed last). On expiry the remaining credits are forfeited via an Adjust
/// ledger entry — the lot row itself is never deleted (append-only spirit).
/// </summary>
public sealed class CreditLot
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Top-up that funded this lot; null = admin grant / legacy backfill.</summary>
    public Guid? TopupRequestId { get; private set; }

    public decimal CreditsGranted { get; private set; }
    public decimal CreditsRemaining { get; private set; }

    /// <summary>null = never expires (custom top-up / admin grant).</summary>
    public DateTime? ExpiresAtUtc { get; private set; }

    /// <summary>Set when the expiry write-off ledger entry was posted — idempotency guard.</summary>
    public DateTime? ExpiredWrittenOffAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private CreditLot() { } // EF Core

    public static CreditLot Create(Guid tenantId, decimal credits, DateTime? expiresAtUtc,
        DateTime nowUtc, Guid? topupRequestId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TopupRequestId = topupRequestId,
            CreditsGranted = credits,
            CreditsRemaining = credits,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = nowUtc
        };

    /// <summary>Consume up to <paramref name="amount"/>; returns actually consumed.</summary>
    public decimal Consume(decimal amount)
    {
        var take = Math.Min(amount, CreditsRemaining);
        CreditsRemaining -= take;
        return take;
    }

    /// <summary>FEFO ordering: expiring lots first (soonest expiry), non-expiring last.</summary>
    public static readonly Comparison<CreditLot> FefoOrder = (a, b) =>
    {
        var c = Nullable.Compare(a.ExpiresAtUtc, b.ExpiresAtUtc);
        // null sorts LAST: treat null as DateTime.MaxValue for ascending order.
        if (a.ExpiresAtUtc == null && b.ExpiresAtUtc != null) return 1;
        if (a.ExpiresAtUtc != null && b.ExpiresAtUtc == null) return -1;
        if (c != 0) return c;
        return a.CreatedAtUtc.CompareTo(b.CreatedAtUtc);
    };

    /// <summary>Expiry write-off — returns forfeited remainder, zeroes the lot.</summary>
    public decimal ForfeitRemaining()
    {
        var remaining = CreditsRemaining;
        CreditsRemaining = 0;
        return remaining;
    }

    public void MarkWrittenOff(DateTime nowUtc) => ExpiredWrittenOffAtUtc = nowUtc;
}
