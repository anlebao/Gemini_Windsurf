using Microsoft.EntityFrameworkCore;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Api.Services;

/// <summary>
/// task_ai_p6b — [G10]/NFR-7 daily retention cleanup: delete conversation_messages
/// past expires_at, then sessions left with no live messages. expires_at is set at
/// write time (ChatOrchestrator RetentionDays, default 90d 【PO-4】).
/// Batched deletes keep the pass cheap; errors are swallowed per pass — cleanup
/// must never take the service down.
/// </summary>
public sealed class RetentionCleanupService(
    IServiceProvider services,
    IConfiguration config,
    ILogger<RetentionCleanupService> logger) : BackgroundService
{
    private const int BatchSize = 2000;
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

    private TimeSpan Interval => TimeSpan.FromHours(
        config.GetValue("Retention:CleanupIntervalHours", 24));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(StartupDelay, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CleanupPassAsync(stoppingToken); }
            catch (Exception ex) { logger.LogWarning(ex, "Retention cleanup pass failed"); }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>One cleanup pass — public for tests.</summary>
    public async Task<(int Messages, int Sessions)> CleanupPassAsync(CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantAiDbContext>();
        var now = DateTime.UtcNow;

        var messagesDeleted = 0;
        while (true)
        {
            var batch = await db.ConversationMessages
                .Where(m => m.ExpiresAtUtc < now)
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batch.Count == 0) break;
            db.ConversationMessages.RemoveRange(batch);
            messagesDeleted += batch.Count;
            await db.SaveChangesAsync(ct);
        }

        // Orphan sessions: no live message left (all expired or never persisted).
        var sessionsDeleted = 0;
        while (true)
        {
            var batch = await db.ConversationSessions
                .Where(s => !db.ConversationMessages.Any(m =>
                    m.TenantId == s.TenantId && m.SessionId == s.SessionId))
                .Take(BatchSize)
                .ToListAsync(ct);
            if (batch.Count == 0) break;
            db.ConversationSessions.RemoveRange(batch);
            sessionsDeleted += batch.Count;
            await db.SaveChangesAsync(ct);
        }

        if (messagesDeleted > 0 || sessionsDeleted > 0)
            logger.LogInformation("Retention cleanup: {Messages} messages, {Sessions} orphan sessions deleted",
                messagesDeleted, sessionsDeleted);
        return (messagesDeleted, sessionsDeleted);
    }
}
