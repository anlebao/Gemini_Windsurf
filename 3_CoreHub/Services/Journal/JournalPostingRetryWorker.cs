using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// SRS v1.2 Phase A (R2): retry worker cho entries có PostingStatus=Failed/Pending.
    /// Bridge fail-safe giữ phiếu lưu được nhưng JE có thể thiếu — worker vá divergence:
    /// mỗi 60s quét Failed/Pending (Attempts&lt;5, stale &gt;30s) → gọi lại bridge.
    /// Reconcile R-checks vẫn là lớp phát hiện cuối.
    /// </summary>
    public sealed class JournalPostingRetryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<JournalPostingRetryWorker> logger) : BackgroundService
    {
        private const int PollSeconds = 60;
        private const int MinAgeSeconds = 30;
        private const int MaxAttempts = 5;
        private const int BatchSize = 50;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Delay khởi động — tránh đua với migrations/seed lúc app start.
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "JournalPostingRetryWorker: batch lỗi — sẽ thử lại chu kỳ sau");
                }
                await Task.Delay(TimeSpan.FromSeconds(PollSeconds), stoppingToken);
            }
        }

        private async Task ProcessBatchAsync(CancellationToken ct)
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            IHKDBookRepository hkdRepo = scope.ServiceProvider.GetRequiredService<IHKDBookRepository>();
            IAccountingEntryRepository entryRepo = scope.ServiceProvider.GetRequiredService<IAccountingEntryRepository>();
            IManualEntryJournalBridge bridge = scope.ServiceProvider.GetRequiredService<IManualEntryJournalBridge>();

            List<EntryPostingStatusEntity> retryable = await hkdRepo.GetRetryableStatusesAsync(MaxAttempts, MinAgeSeconds, BatchSize, ct);
            if (retryable.Count == 0)
            {
                return;
            }

            foreach (EntryPostingStatusEntity status in retryable)
            {
                ct.ThrowIfCancellationRequested();
                CoreAccountingEntry? entry = await entryRepo.GetByIdAsync(status.EntryId);
                if (entry == null)
                {
                    continue;
                }
                bool ok = await bridge.CreateForAccountingEntryAsync(entry, ct);
                if (!ok && status.Attempts + 1 >= MaxAttempts)
                {
                    logger.LogWarning("JournalPostingRetryWorker: entry {EntryId} đạt max attempts ({Max}) — để Failed, reconcile sẽ bắt",
                        status.EntryId, MaxAttempts);
                }
            }

            logger.LogInformation("JournalPostingRetryWorker: xử lý {Count} entries retryable", retryable.Count);
        }
    }
}
