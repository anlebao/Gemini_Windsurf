using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Contracts;
using VanAn.Metering.Domain;

namespace VanAn.Metering.Infra;

/// <summary>
/// Rate-card versioned CRUD + point-in-time resolver (task_p2_ratecard).
/// Append-only: PATCH supersedes the head version and inserts a new one;
/// DELETE soft-deactivates. Historical rows are never updated or removed.
/// Every mutation writes one AuditLog row in the same SaveChanges.
/// </summary>
public sealed class RateCardService(MeteringDbContext db)
{
    private static readonly TimeSpan PastTolerance = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions AuditJson = new() { WriteIndented = false };

    /// <summary>
    /// Resolve the rate effective at <paramref name="atUtc"/> for the GLOBAL scope only
    /// (backward-compat: callers that do not send tenant attributes).
    /// EffectiveFrom &lt;= at &lt; EffectiveTo, latest EffectiveFrom wins, IsActive only.
    /// Miss → null (unpriced) — callers decide how to treat it, never throw.
    /// </summary>
    public Task<RateCardVersion?> ResolveAsync(string serviceCode, DateTime atUtc, CancellationToken ct = default)
        => ResolveForTenantAsync(serviceCode, null, null, null, atUtc, ct);

    /// <summary>
    /// Issue #197 — resolve the rate effective at <paramref name="atUtc"/> for a tenant
    /// context (attributes are optional caller claims — absent → Global-only, i.e. the
    /// exact GĐ-1 behaviour). Precedence: Tenant override &gt; TenantType &gt; TenantStatus
    /// &gt; Global; within one scope the latest EffectiveFrom wins. Miss → null.
    /// </summary>
    public async Task<RateCardVersion?> ResolveForTenantAsync(
        string serviceCode, Guid? tenantId, string? tenantType, string? tenantStatus,
        DateTime atUtc, CancellationToken ct = default)
    {
        var versions = await db.RateCardVersions.AsNoTracking()
            .Where(v => v.ServiceCode == serviceCode && v.IsActive
                        && v.EffectiveFrom <= atUtc
                        && (v.EffectiveTo == null || v.EffectiveTo > atUtc))
            .OrderByDescending(v => v.EffectiveFrom)
            .ToListAsync(ct);
        if (versions.Count == 0) return null;

        // Precedence list — most specific first.
        foreach (var scope in new[] { RateCardScope.Tenant, RateCardScope.TenantType, RateCardScope.TenantStatus, RateCardScope.Global })
        {
            var match = versions.FirstOrDefault(v => v.Scope == scope && MatchesScope(v, tenantId, tenantType, tenantStatus));
            if (match != null) return match;
        }
        return null;
    }

    private static bool MatchesScope(RateCardVersion v, Guid? tenantId, string? tenantType, string? tenantStatus)
        => v.Scope switch
        {
            RateCardScope.Tenant => v.ScopeValue != null && Guid.TryParse(v.ScopeValue, out var id) && id == tenantId,
            RateCardScope.TenantType => !string.IsNullOrWhiteSpace(tenantType)
                && string.Equals(v.ScopeValue, tenantType.Trim(), StringComparison.OrdinalIgnoreCase),
            RateCardScope.TenantStatus => !string.IsNullOrWhiteSpace(tenantStatus)
                && string.Equals(v.ScopeValue, tenantStatus.Trim(), StringComparison.OrdinalIgnoreCase),
            _ => true
        };

    /// <summary>Default: chain heads (IsActive &amp;&amp; EffectiveTo == null) — the patchable version
    /// per service code (may be future-scheduled). includeInactive → full version history.</summary>
    public Task<List<RateCardVersion>> ListAsync(bool includeInactive, CancellationToken ct = default)
        => includeInactive
            ? db.RateCardVersions.AsNoTracking()
                .OrderBy(v => v.ServiceCode).ThenByDescending(v => v.EffectiveFrom).ToListAsync(ct)
            : db.RateCardVersions.AsNoTracking()
                .Where(v => v.IsActive && v.EffectiveTo == null)
                .OrderBy(v => v.ServiceCode).ToListAsync(ct);

    public async Task<RateCardOpResult> CreateAsync(RateCardCreateRequest req, string actor, DateTime nowUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.ServiceCode))
            return RateCardOpResult.Fail("invalid_serviceCode", "serviceCode is required.");
        if (string.IsNullOrWhiteSpace(req.MeterUnit))
            return RateCardOpResult.Fail("invalid_meterUnit", "meterUnit is required.");
        if (req.CreditRate < 0)
            return RateCardOpResult.Fail("invalid_creditRate", "creditRate must be >= 0.");
        if (!TryParseEnforcement(req.Enforcement, out var enforcement))
            return RateCardOpResult.Fail("invalid_enforcement", "enforcement must be MeterOnly or Deny.");
        // Issue #197 — scope validation: Global (default) | TenantType | TenantStatus | Tenant.
        if (!RateCardScopes.TryParseScope(req.Scope, out var scope))
            return RateCardOpResult.Fail("invalid_scope", "scope must be Global, TenantType, TenantStatus or Tenant.");
        if (!RateCardScopes.IsValidScopeValue(scope, req.ScopeValue))
            return RateCardOpResult.Fail("invalid_scopeValue",
                scope == RateCardScope.TenantType
                    ? $"scopeValue must be one of: {string.Join(", ", RateCardScopes.TenantTypeValues)}."
                    : scope == RateCardScope.TenantStatus
                        ? $"scopeValue must be \"verified\" or \"unverified\"."
                        : scope == RateCardScope.Tenant
                            ? "scopeValue must be a non-empty tenantId (Guid)."
                            : "scopeValue must be empty for Global scope.");
        var from = req.EffectiveFrom ?? nowUtc;
        if (req.EffectiveTo.HasValue && from >= req.EffectiveTo.Value)
            return RateCardOpResult.Fail("invalid_effective_range", "effectiveFrom must be < effectiveTo.");

        var serviceCode = req.ServiceCode.Trim();
        var scopeValue = RateCardScopes.Normalize(scope, req.ScopeValue);
        var hasActive = await db.RateCardVersions.AnyAsync(
            v => v.ServiceCode == serviceCode && v.IsActive && v.Scope == scope && v.ScopeValue == scopeValue, ct);
        if (hasActive)
            return RateCardOpResult.Fail("serviceCode_active_exists",
                $"An active rate-card chain already exists for this serviceCode+scope — use PATCH.");

        var version = RateCardVersion.Create(
            serviceCode, req.MeterUnit.Trim(), req.CreditRate, enforcement,
            from, actor, req.Label, req.EffectiveTo, req.AuditNote, scope, scopeValue);
        db.RateCardVersions.Add(version);
        db.AuditLogs.Add(AuditLog.Create(actor, "create", nameof(RateCardVersion), version.Id,
            null, JsonSerializer.Serialize(version, AuditJson), nowUtc));
        await db.SaveChangesAsync(ct);
        return RateCardOpResult.Success(version);
    }

    /// <summary>Only the chain head (IsActive &amp;&amp; EffectiveTo == null) is patchable (D3).
    /// Creates the next version at <paramref name="req"/>.EffectiveFrom (or now) and
    /// closes the head at the same instant — scheduled pricing changes allowed (D4).</summary>
    public async Task<RateCardOpResult> PatchAsync(Guid id, RateCardPatchRequest req, string actor, DateTime nowUtc, CancellationToken ct = default)
    {
        var head = await db.RateCardVersions.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (head == null)
            return RateCardOpResult.Fail("not_found", "Rate-card version not found.");
        if (!head.IsActive || head.EffectiveTo != null)
            return RateCardOpResult.Fail("version_not_current",
                "Only the current head version is patchable — patch the latest version or create a new one.");

        if (req.CreditRate is < 0)
            return RateCardOpResult.Fail("invalid_creditRate", "creditRate must be >= 0.");
        // PATCH null enforcement = keep current value (not the MeterOnly default).
        var enforcement = head.Enforcement;
        if (req.Enforcement != null && !TryParseEnforcement(req.Enforcement, out enforcement))
            return RateCardOpResult.Fail("invalid_enforcement", "enforcement must be MeterOnly or Deny.");

        var at = req.EffectiveFrom ?? nowUtc;
        if (at <= head.EffectiveFrom)
            return RateCardOpResult.Fail("invalid_effectiveFrom",
                "effectiveFrom must be later than the current version's effectiveFrom.");
        if (at < nowUtc - PastTolerance)
            return RateCardOpResult.Fail("effectiveFrom_in_past",
                "effectiveFrom may not be more than 5 minutes in the past.");

        var oldJson = JsonSerializer.Serialize(head, AuditJson);
        var next = RateCardVersion.Create(
            head.ServiceCode,
            req.MeterUnit ?? head.MeterUnit,
            req.CreditRate ?? head.CreditRate,
            enforcement,
            at, actor, req.Label ?? head.Label, null, req.AuditNote,
            // Issue #197 — the chain keeps its scope across versions.
            head.Scope, head.ScopeValue);
        head.SupersedeAt(at);
        db.RateCardVersions.Add(next);
        db.AuditLogs.Add(AuditLog.Create(actor, "patch", nameof(RateCardVersion), next.Id,
            oldJson, JsonSerializer.Serialize(next, AuditJson), nowUtc));
        await db.SaveChangesAsync(ct);
        return RateCardOpResult.Success(next);
    }

    /// <summary>Soft-deactivate. Idempotent: already-inactive → Ok with IdempotentNoOp.</summary>
    public async Task<RateCardOpResult> DeactivateAsync(Guid id, string actor, DateTime nowUtc, CancellationToken ct = default)
    {
        var version = await db.RateCardVersions.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (version == null)
            return RateCardOpResult.Fail("not_found", "Rate-card version not found.");
        if (!version.IsActive)
            return RateCardOpResult.Success(version, idempotentNoOp: true);

        var oldJson = JsonSerializer.Serialize(version, AuditJson);
        version.Close(nowUtc);
        db.AuditLogs.Add(AuditLog.Create(actor, "deactivate", nameof(RateCardVersion), version.Id,
            oldJson, JsonSerializer.Serialize(version, AuditJson), nowUtc));
        await db.SaveChangesAsync(ct);
        return RateCardOpResult.Success(version);
    }

    private static bool TryParseEnforcement(string? raw, out MeteringEnforcement enforcement)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            enforcement = MeteringEnforcement.MeterOnly;
            return true;
        }
        return Enum.TryParse(raw.Trim(), ignoreCase: true, out enforcement);
    }
}

/// <summary>Business result — services report errors via ErrorCode instead of throwing.</summary>
public sealed record RateCardOpResult(RateCardVersion? Version, string? ErrorCode, string? ErrorMessage, bool IdempotentNoOp)
{
    public bool Ok => ErrorCode == null;
    public static RateCardOpResult Success(RateCardVersion version, bool idempotentNoOp = false)
        => new(version, null, null, idempotentNoOp);
    public static RateCardOpResult Fail(string code, string message)
        => new(null, code, message, false);
}
