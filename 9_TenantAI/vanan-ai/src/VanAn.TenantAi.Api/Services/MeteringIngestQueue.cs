using System.Threading.Channels;
using VanAn.TenantAi.Infra;

namespace VanAn.TenantAi.Api.Services;

/// <summary>Post-response usage ingest — fire-and-forget for the chat path.</summary>
public interface IMeteringIngestQueue
{
    void Enqueue(MeteringUsageRecord evt);
}

/// <summary>
/// task_ai_p5 — light retry queue behind the chat response (card: "fire-and-forget
/// có retry queue nhẹ"). Bounded channel (512, drop-oldest) so a metering outage
/// can never grow memory or block chat; each event retries ≤3 times (1s/5s/15s)
/// then is dropped with a warning — usage is observability, never response-critical.
/// </summary>
public sealed class MeteringIngestQueue(
    IMeteringService metering,
    ILogger<MeteringIngestQueue> logger) : BackgroundService, IMeteringIngestQueue
{
    private static readonly int[] RetryDelaysMs = [1_000, 5_000, 15_000];

    private readonly Channel<MeteringUsageRecord> _queue = Channel.CreateBounded<MeteringUsageRecord>(
        new BoundedChannelOptions(512) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(MeteringUsageRecord evt)
    {
        if (!_queue.Writer.TryWrite(evt))
            logger.LogWarning("Metering ingest queue full — dropped {ServiceCode} tenant={Tenant}", evt.ServiceCode, evt.TenantId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            // Metering not configured → drop silently (fail-open, same convention as check).
            if (!metering.IsConfigured) continue;
            for (var attempt = 0; attempt <= RetryDelaysMs.Length; attempt++)
            {
                try
                {
                    if (await metering.IngestAsync(evt, stoppingToken)) break;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Metering ingest error {ServiceCode} tenant={Tenant}", evt.ServiceCode, evt.TenantId);
                    break;
                }
                if (attempt == RetryDelaysMs.Length)
                {
                    logger.LogWarning("Metering ingest dropped after retries {ServiceCode} tenant={Tenant} key={Key}",
                        evt.ServiceCode, evt.TenantId, evt.IdempotencyKey);
                    break;
                }
                try { await Task.Delay(RetryDelaysMs[attempt], stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
