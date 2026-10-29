using VanAn.Shared.Domain.Common;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Records module-active-month feature usage (task_p4 hook d + O-3 phụ: financial/inventory
/// are metered as "module active in month", deduped server-side by the idempotency unique index).
/// Idempotency format: feature:{code}:{tenantId}:{yyyy-MM} — at most 1 event per tenant/module/month
/// no matter how often the page is opened.
/// Fire-and-forget (G9): swallows its own errors; metering outages must not affect pages.
/// Issue #197: sends tenant group claims (X-Tenant-Type/Status) so scoped rate cards resolve.
/// </summary>
public sealed class FeatureUsageRecorder(
    IMeteringClient metering,
    ITenantProvider tenantProvider,
    IMeteringTenantAttributeResolver attributes,
    ILogger<FeatureUsageRecorder> logger)
{
    /// <param name="code">"feature.financial" | "feature.inventory" | "feature.booking"
    /// | "feature.accounting" | "feature.loyalty" | "feature.ocr" | "feature.voice-note".</param>
    public Task RecordAsync(string code, CancellationToken ct = default)
    {
        try
        {
            if (!tenantProvider.HasTenant) return Task.CompletedTask;
            var tenantId = tenantProvider.TenantId;
            var key = $"feature:{code}:{tenantId}:{DateTime.UtcNow:yyyy-MM}";
            _ = RecordWithAttributesAsync(tenantId, code, key, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Feature usage record failed ({Code}) — skipped (fail-safe)", code);
        }
        return Task.CompletedTask;
    }

    private async Task RecordWithAttributesAsync(Guid tenantId, string code, string key, CancellationToken ct)
    {
        try
        {
            var attrs = await attributes.ResolveAsync(tenantId, ct);
            _ = metering.RecordUsageAsync(new MeteringUsageEvent(
                tenantId, code, 1, "module-month", DateTime.UtcNow, key), attrs, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Feature usage record failed ({Code}) — skipped (fail-safe)", code);
        }
    }
}
