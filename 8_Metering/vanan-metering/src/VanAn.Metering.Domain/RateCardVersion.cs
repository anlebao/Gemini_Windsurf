namespace VanAn.Metering.Domain;

/// <summary>
/// SRS [G7]/[G20] — one version of a rate-card item. Edits never mutate a row:
/// PATCH closes the current version (EffectiveTo) and inserts a new one.
/// Enforcement decides whether the caller may deny service when the wallet is empty.
/// </summary>
public sealed class RateCardVersion
{
    public Guid Id { get; private set; }
    public string ServiceCode { get; private set; } = string.Empty;
    public string MeterUnit { get; private set; } = string.Empty;
    public decimal CreditRate { get; private set; }
    public MeteringEnforcement Enforcement { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; }
    public string? Label { get; private set; }
    public string ChangedBy { get; private set; } = string.Empty;
    public DateTime ChangedAtUtc { get; private set; }
    public string? AuditNote { get; private set; }

    private RateCardVersion() { } // EF Core

    public static RateCardVersion Create(
        string serviceCode,
        string meterUnit,
        decimal creditRate,
        MeteringEnforcement enforcement,
        DateTime effectiveFrom,
        string changedBy,
        string? label = null,
        DateTime? effectiveTo = null,
        string? auditNote = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ServiceCode = serviceCode,
            MeterUnit = meterUnit,
            CreditRate = creditRate,
            Enforcement = enforcement,
            EffectiveFrom = effectiveFrom,
            EffectiveTo = effectiveTo,
            IsActive = true,
            Label = label,
            ChangedBy = changedBy,
            ChangedAtUtc = DateTime.UtcNow,
            AuditNote = auditNote
        };

    /// <summary>A newer version takes over at <paramref name="atUtc"/> — this version stays
    /// resolvable until that instant (audit trail preserved, IsActive untouched).</summary>
    public void SupersedeAt(DateTime atUtc) => EffectiveTo = atUtc;

    /// <summary>Deactivate: close at <paramref name="atUtc"/> and mark inactive (soft-delete).</summary>
    public void Close(DateTime atUtc)
    {
        EffectiveTo ??= atUtc;
        IsActive = false;
    }
}

/// <summary>SRS [G20]/O-4 — MeterOnly = đo nhưng không chặn; Deny = caller được chặn khi hết ví.</summary>
public enum MeteringEnforcement
{
    MeterOnly = 0,
    Deny = 1
}
