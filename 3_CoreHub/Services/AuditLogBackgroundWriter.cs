using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain.Audit;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Sprint 3 EXPANDED: Background writer — drain AuditLogQueue mỗi 5s (hoặc khi đủ 100/batch),
/// batch INSERT qua IAuditLogRepository (scoped, resolve qua IServiceScopeFactory).
/// Graceful shutdown: flush residual batch sau khi stoppingToken cancel.
/// Best-effort: batch fail (DB lỗi) → log error + drop (không re-enqueue — tránh vòng lặp khi DB down).
/// </summary>
public class AuditLogBackgroundWriter : BackgroundService
{
    private readonly AuditLogQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogBackgroundWriter> _logger;
    private const int BatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    public AuditLogBackgroundWriter(
        AuditLogQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AuditLogBackgroundWriter> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await FlushAsync();
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — flush residual bên dưới
        }

        // Residual flush — không truyền stoppingToken (đã cancel)
        await FlushAsync();
    }

    // internal for VanAn.Core.Tests (InternalsVisibleTo) — Sprint 10A flush tests
    internal async Task FlushAsync()
    {
        while (true)
        {
            var batch = new List<AuditLog>();
            while (_queue.TryDequeue(out var log))
            {
                batch.Add(log);
                if (batch.Count >= BatchSize) break;
            }
            if (batch.Count == 0) return;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
                foreach (var log in batch)
                {
                    await repository.AddAsync(log);
                }
                _logger.LogDebug("AuditLogBackgroundWriter: flushed {Count} audit logs", batch.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AuditLogBackgroundWriter: flush failed — {Count} audit logs dropped (best-effort)",
                    batch.Count);
            }

            if (batch.Count < BatchSize) return;  // queue đã rỗng
        }
    }
}
