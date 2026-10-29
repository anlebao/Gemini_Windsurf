namespace VanAn.Shared.Domain.Common;

/// <summary>
/// Usage event sent to the independent vanan-metering service (GĐ-1 "Phí nền tảng").
/// Pure facts only — no business ownership state (S4).
/// </summary>
/// <param name="TenantId">Tenant facing the metered action (Guid, not the TenantId VO — HTTP boundary).</param>
/// <param name="ServiceCode">Rate-card code, e.g. "store.visit", "chat.message", "chat.bytes",
/// "directory.search", "directory.redirect", "feature.booking", "feature.financial",
/// "feature.inventory", "feature.accounting", "feature.loyalty", "feature.ocr",
/// "feature.voice-note".</param>
/// <param name="Quantity">Amount in <paramref name="Unit"/>.</param>
/// <param name="Unit">"visit" | "message" | "byte" | "search" | "redirect" | "booking" | "module-month".</param>
/// <param name="OccurredAtUtc">When the action happened (server time if not available).</param>
/// <param name="IdempotencyKey">REQUIRED — dedupe key per caller (see task_p4 formats).</param>
/// <param name="ShopInstanceId">Optional platform-side install id (opaque to metering).</param>
/// <param name="RequestedBy">Actor id (user GUID / "guest:*") — for future fraud rules.</param>
/// <param name="TaskRef">Business task/object id (booking id, message id) — for future NATS mirror.</param>
/// <param name="MetadataJson">Small JSON blob (&lt;4KB), e.g. {"slug":"x","source":"public-gateway"}.</param>
public sealed record MeteringUsageEvent(
    Guid TenantId,
    string ServiceCode,
    decimal Quantity,
    string Unit,
    DateTime OccurredAtUtc,
    string IdempotencyKey,
    Guid? ShopInstanceId = null,
    string? RequestedBy = null,
    string? TaskRef = null,
    string? MetadataJson = null);

/// <summary>Balance-check result (P7 wallet) — from GET /v1/wallets/{tenant}/check.
/// Deny=true only for Deny-enforcement rate cards when balance &lt; required.</summary>
public sealed record MeteringBalanceCheck(decimal Balance, decimal Required, bool Deny, string? AlertLevel);

/// <summary>
/// Issue #197 — caller-supplied tenant attributes that scope rate-card resolution
/// (Tenant override > TenantType > TenantStatus > Global). Sent as X-Tenant-Type /
/// X-Tenant-Status headers on check/ingest. Null attributes (or null members) → the
/// metering service resolves the Global chain only (exact GĐ-1 behaviour).
/// Values are canonical strings: TenantType = "HKD"|"Enterprise_SuperSmall"|
/// "Enterprise_SME"|"Enterprise_Large"|"HTX"; TenantStatus = "verified"|"unverified".
/// </summary>
public sealed record MeteringTenantAttributes(string? TenantType, string? TenantStatus);

/// <summary>
/// Canonical values for <see cref="MeteringTenantAttributes"/> — contract by
/// convention with the independent vanan-metering service (RateCardScopes there);
/// the two solutions do not share code (metering.md §1).
/// </summary>
public static class MeteringAttributeValues
{
    // TenantType scope values — mirror TenantType enum names.
    public const string Hkd = "HKD";
    public const string EnterpriseSuperSmall = "Enterprise_SuperSmall";
    public const string EnterpriseSme = "Enterprise_SME";
    public const string EnterpriseLarge = "Enterprise_Large";
    public const string Htx = "HTX";

    // TenantStatus scope values — "verified" = Active, "unverified" = Pending.
    public const string Verified = "verified";
    public const string Unverified = "unverified";
}

/// <summary>
/// Thin, fail-safe client into vanan-metering. Implemented per-host (ShopERP/Gateway/Directory),
/// NOT a shared DLL (D2). Contracts:
/// - NEVER throws into business code (G9) — every method swallows transport failures, logs, returns false/null.
/// - RecordUsage* returns true when the event was accepted/replayed, false when dropped.
/// - Missing Metering:BaseUrl/CallerKey config → client self-disables (no-op + one-time warning).
/// </summary>
public interface IMeteringClient
{
    Task<bool> RecordUsageAsync(MeteringUsageEvent evt, MeteringTenantAttributes? attributes = null, CancellationToken ct = default);

    /// <summary>Batch submit (≤500) — for burst sources. Partial failure is server-side per-item.</summary>
    Task<bool> RecordUsageBatchAsync(IReadOnlyList<MeteringUsageEvent> events, MeteringTenantAttributes? attributes = null, CancellationToken ct = default);

    /// <summary>P7 — wallet balance + deny flag for a service code. Null on failure /
    /// unconfigured — callers MUST treat null as "allow" (fail-open per G9).
    /// Issue #197: <paramref name="attributes"/> scope the rate resolution.</summary>
    Task<MeteringBalanceCheck?> CheckBalanceAsync(Guid tenantId, string serviceCode, MeteringTenantAttributes? attributes = null, CancellationToken ct = default);
}

/// <summary>
/// Issue #197 (D1=A) — resolves a tenant's group claims (loại hình + trạng thái
/// verify) so metering calls can scope rate-card resolution. Implemented per host
/// (ShopERP: SQLite mirror; Gateway: PostgreSQL) — the metering service itself
/// treats them as opaque claims and never validates tenant existence.
/// Null return → caller omits the headers (Global chain resolution).
/// </summary>
public interface IMeteringTenantAttributeResolver
{
    Task<MeteringTenantAttributes?> ResolveAsync(Guid tenantId, CancellationToken ct = default);
}

/// <summary>
/// Issue #197 (item 2) — thrown by server-side deny guards when the wallet check
/// resolves deny=true (Enforcement=Deny + balance &lt; required). API layers map it
/// to HTTP 402 with a friendly message. Fail-open contract: the check returns
/// deny=false on metering transport errors, so this is NEVER thrown for outages.
/// </summary>
public sealed class MeteringDeniedException(string serviceCode, decimal balance, decimal required)
    : Exception($"Hết hạn mức phí nền tảng: {serviceCode} cần {required:0.##} credit nhưng ví chỉ còn {balance:0.##}.")
{
    public string ServiceCode { get; } = serviceCode;
    public decimal Balance { get; } = balance;
    public decimal Required { get; } = required;
}

/// <summary>
/// Issue #197 (item 2) — server-side deny guard. Fail-open (G9): metering transport
/// error → check returns null → pass. Only a real deny response throws
/// <see cref="MeteringDeniedException"/> (HTTP 402 at the API layer).
/// </summary>
public static class MeteringDenyGuard
{
    public static async Task CheckOrThrowAsync(
        IMeteringClient metering,
        IMeteringTenantAttributeResolver? attributeResolver,
        Guid tenantId,
        string serviceCode,
        CancellationToken ct = default)
    {
        if (metering == null) return;
        var attrs = attributeResolver == null ? null : await attributeResolver.ResolveAsync(tenantId, ct);
        var check = await metering.CheckBalanceAsync(tenantId, serviceCode, attrs, ct);
        if (check?.Deny == true)
            throw new MeteringDeniedException(serviceCode, check.Balance, check.Required);
    }
}
