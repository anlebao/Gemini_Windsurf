namespace VanAn.Metering.Domain;

/// <summary>
/// P7 D6/D9 — manual top-up request: tenant asks for credits, transfers money
/// with the ReferenceCode in the transfer note, admin confirms → Grant credits.
/// VND amount is optional information only — the credits granted are decided
/// by the admin at confirm time (D8, no fixed VND↔credit rate).
/// Status transitions: Pending → Confirmed | Rejected | Expired (lazy-expire at
/// read time past TTL — no background job in GĐ-1).
/// </summary>
public sealed class TopupRequest
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>"TOPUP-" + 8 chars — short enough to type into a bank transfer note.</summary>
    public string ReferenceCode { get; private set; } = string.Empty;

    /// <summary>Optional declared amount — informational only (D8).</summary>
    public decimal? AmountVnd { get; private set; }

    /// <summary>Credits granted by admin at confirm (null until then).</summary>
    public decimal? CreditsGranted { get; private set; }

    public TopupStatus Status { get; private set; }
    public string? ConfirmedBy { get; private set; }
    public DateTime? ConfirmedAtUtc { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Caller + idempotency pair — replayed POST returns the same request.</summary>
    public string CallerId { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;

    private TopupRequest() { } // EF Core

    public static TopupRequest Create(
        Guid tenantId, string referenceCode, decimal? amountVnd,
        string callerId, string idempotencyKey, DateTime nowUtc, TimeSpan ttl)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ReferenceCode = referenceCode,
            AmountVnd = amountVnd,
            Status = TopupStatus.Pending,
            CreatedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc + ttl,
            CallerId = callerId,
            IdempotencyKey = idempotencyKey
        };

    /// <summary>Idempotent: already-Confirmed returns true without re-granting.</summary>
    public bool Confirm(decimal credits, string adminActor, string? note, DateTime nowUtc)
    {
        if (Status == TopupStatus.Confirmed) return true;
        if (Status != TopupStatus.Pending) return false;
        Status = TopupStatus.Confirmed;
        CreditsGranted = credits;
        ConfirmedBy = adminActor;
        ConfirmedAtUtc = nowUtc;
        if (!string.IsNullOrWhiteSpace(note)) Note = note;
        return true;
    }

    public bool Reject(string adminActor, string? reason, DateTime nowUtc)
    {
        if (Status != TopupStatus.Pending) return false;
        Status = TopupStatus.Rejected;
        ConfirmedBy = adminActor;
        ConfirmedAtUtc = nowUtc;
        Note = reason;
        return true;
    }

    /// <summary>D9 — lazy expiry: mark expired when read past TTL.</summary>
    public bool ExpireIfStale(DateTime nowUtc)
    {
        if (Status != TopupStatus.Pending || nowUtc < ExpiresAtUtc) return false;
        Status = TopupStatus.Expired;
        return true;
    }
}

public enum TopupStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2,
    Expired = 3
}
