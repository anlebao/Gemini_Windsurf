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

    // Issue #197 (2026-10-29): rate-card scope — Global (default, backward compat) or
    // scoped to a tenant group. Resolution precedence: Tenant > TenantType > TenantStatus > Global.
    public RateCardScope Scope { get; private set; } = RateCardScope.Global;
    public string? ScopeValue { get; private set; }

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
        string? auditNote = null,
        RateCardScope scope = RateCardScope.Global,
        string? scopeValue = null)
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
            AuditNote = auditNote,
            Scope = scope,
            ScopeValue = scopeValue
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

/// <summary>
/// Issue #197 — scope of a rate-card chain. A tenant's effective rate resolves by
/// precedence: Tenant override → TenantType → TenantStatus → Global. A chain is
/// uniquely identified by (ServiceCode, Scope, ScopeValue) — the same service code
/// may carry one chain per scope.
/// </summary>
public enum RateCardScope
{
    Global = 0,
    TenantType = 1,
    TenantStatus = 2,
    Tenant = 3
}

/// <summary>
/// Issue #197 — canonical ScopeValue strings + parsing/validation helpers.
/// Values are caller claims (like tenantId) — matched against the X-Tenant-Type /
/// X-Tenant-Status headers callers send on check/ingest (D1=A, no registry sync).
/// </summary>
public static class RateCardScopes
{
    // TenantType scope values — mirror TenantType enum names (1_Shared Domain).
    public const string Hkd = "HKD";
    public const string EnterpriseSuperSmall = "Enterprise_SuperSmall";
    public const string EnterpriseSme = "Enterprise_SME";
    public const string EnterpriseLarge = "Enterprise_Large";
    public const string Htx = "HTX";

    public static readonly IReadOnlyList<string> TenantTypeValues =
        [Hkd, EnterpriseSuperSmall, EnterpriseSme, EnterpriseLarge, Htx];

    // TenantStatus scope values — "verified" = TenantStatus.Active, "unverified" = Pending (user chốt).
    public const string Verified = "verified";
    public const string Unverified = "unverified";

    public static readonly IReadOnlyList<string> TenantStatusValues = [Verified, Unverified];

    /// <summary>Parse "Global"|"TenantType"|"TenantStatus"|"Tenant" (case-insensitive).</summary>
    public static bool TryParseScope(string? raw, out RateCardScope scope)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            scope = RateCardScope.Global;
            return true;
        }
        return Enum.TryParse(raw.Trim(), ignoreCase: true, out scope);
    }

    /// <summary>
    /// Validate ScopeValue against the scope: Global → must be null/empty;
    /// TenantType → one of the 5 canonical values; TenantStatus → verified|unverified;
    /// Tenant → a valid non-empty Guid. Case-insensitive (resolve matching is
    /// OrdinalIgnoreCase too — header claims are not case-canonical).
    /// </summary>
    public static bool IsValidScopeValue(RateCardScope scope, string? value)
        => scope switch
        {
            RateCardScope.Global => string.IsNullOrWhiteSpace(value),
            RateCardScope.TenantType => !string.IsNullOrWhiteSpace(value) && TenantTypeValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase),
            RateCardScope.TenantStatus => !string.IsNullOrWhiteSpace(value) && TenantStatusValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase),
            RateCardScope.Tenant => Guid.TryParse(value, out var id) && id != Guid.Empty,
            _ => false
        };

    /// <summary>Normalize the value (trim) — null stays null.</summary>
    public static string? Normalize(RateCardScope scope, string? value)
        => scope == RateCardScope.Global ? null : string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
