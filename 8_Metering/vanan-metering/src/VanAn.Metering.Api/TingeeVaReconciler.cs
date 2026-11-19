using Microsoft.EntityFrameworkCore;
using VanAn.Metering.Infra;

/// <summary>
/// P12 — VA transaction polling fallback. Tingee's Payment Callback webhook is
/// scoped per-VA on the BaaS portal and may not fire for dynamic-QR VA credits;
/// this worker polls transaction/get-paging on a sysadmin-configured interval
/// (TopupConfig.VaPollEnabled / VaPollIntervalMinutes) and settles matching
/// top-ups via the same ConfirmPaidByQrBillIdAsync path the webhook uses —
/// idempotent, so webhook + poller can run together safely.
/// </summary>
public sealed class TingeeVaReconciler(
    IServiceProvider services,
    ILogger<TingeeVaReconciler> logger) : BackgroundService
{
    private static readonly TimeSpan MinDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // First poll shortly after boot, then on configured interval.
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = await PollOnceAsync(stoppingToken);
            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>One poll cycle; returns how long to wait until the next one.</summary>
    private async Task<TimeSpan> PollOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<MeteringDbContext>();
            var cfg = await db.TopupConfigs.AsNoTracking().FirstOrDefaultAsync(ct);
            var intervalMin = cfg?.VaPollIntervalMinutes ?? 2;
            var delay = TimeSpan.FromMinutes(Math.Clamp(intervalMin, 1, 360));

            var tingee = sp.GetRequiredService<TingeePaymentClient>();
            var topups = sp.GetRequiredService<TopupService>();
            if (cfg == null || !cfg.VaPollEnabled || !tingee.IsEnabled)
                return delay;

            var txns = await tingee.GetVaTransactionsAsync(50, ct);
            var settled = 0;
            foreach (var t in txns)
            {
                if (!string.Equals(t.Type, "credit", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(t.BillId)) continue;
                var r = await topups.ConfirmPaidByQrBillIdAsync(
                    t.BillId, t.Amount,
                    t.TransactionId ?? t.TransactionCode,
                    DateTime.UtcNow, ct);
                if (r.Request != null) settled++;
            }
            if (settled > 0 || txns.Count > 0)
                logger.LogInformation("VA poll: {Count} txn(s), {Settled} settled", txns.Count, settled);
            return delay;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "VA poll cycle failed — retrying in {Delay}", MinDelay);
            return MinDelay;
        }
    }
}
