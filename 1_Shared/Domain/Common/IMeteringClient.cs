namespace VanAn.Shared.Domain.Common;

/// <summary>
/// Usage event sent to the independent vanan-metering service (GĐ-1 "Phí nền tảng").
/// Pure facts only — no business ownership state (S4).
/// </summary>
/// <param name="TenantId">Tenant facing the metered action (Guid, not the TenantId VO — HTTP boundary).</param>
/// <param name="ServiceCode">Rate-card code, e.g. "store.visit", "chat.message", "chat.bytes",
/// "directory.search", "directory.redirect", "feature.booking", "feature.financial", "feature.inventory".</param>
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

/// <summary>Balance-check result — populated in P7 (wallet). Always null in GĐ-1 pre-P7.</summary>
public sealed record MeteringBalanceCheck(decimal Balance, bool Deny, string AlertLevel);

/// <summary>
/// Thin, fail-safe client into vanan-metering. Implemented per-host (ShopERP/Gateway/Directory),
/// NOT a shared DLL (D2). Contracts:
/// - NEVER throws into business code (G9) — every method swallows transport failures, logs, returns false/null.
/// - RecordUsage* returns true when the event was accepted/replayed, false when dropped.
/// - Missing Metering:BaseUrl/CallerKey config → client self-disables (no-op + one-time warning).
/// </summary>
public interface IMeteringClient
{
    Task<bool> RecordUsageAsync(MeteringUsageEvent evt, CancellationToken ct = default);

    /// <summary>Batch submit (≤500) — for burst sources. Partial failure is server-side per-item.</summary>
    Task<bool> RecordUsageBatchAsync(IReadOnlyList<MeteringUsageEvent> events, CancellationToken ct = default);

    /// <summary>P7 stub — wallet balance + deny flag. GĐ-1: returns null, callers must tolerate null.</summary>
    Task<MeteringBalanceCheck?> CheckBalanceAsync(Guid tenantId, string serviceCode, CancellationToken ct = default);
}
