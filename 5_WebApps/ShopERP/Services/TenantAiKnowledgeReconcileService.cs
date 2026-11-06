using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.ShopERP.Services;

/// <summary>
/// task_ai_p2 — nightly reconcile (SRS [G16]): per tenant with the flag ON, re-push
/// the full product catalog so drift between ShopERP (source of truth) and the
/// vanan-ai curated mirror self-heals. Runs 1x/day, swallowed per-tenant errors.
/// </summary>
public sealed class TenantAiKnowledgeReconcileService(
    IServiceProvider services,
    ILogger<TenantAiKnowledgeReconcileService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ReconcileAllAsync(stoppingToken); }
            catch (Exception ex) { logger.LogWarning(ex, "TenantAi reconcile pass failed"); }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ReconcileAllAsync(CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
        var tenantIds = (await db.Products.Select(p => p.TenantId).Distinct().ToListAsync(ct))
            .Select(t => t.Value);
        var sync = scope.ServiceProvider.GetRequiredService<ITenantAiKnowledgeSync>();
        foreach (var tenantId in tenantIds)
            await sync.SyncNowAsync(tenantId, ct);
    }
}
