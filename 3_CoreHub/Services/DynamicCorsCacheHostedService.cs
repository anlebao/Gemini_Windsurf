using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Background service that pre-warms + refreshes the CORS origin cache every 5 minutes.
/// Loads active CustomDomains from KhachLinkInstance registry via IServiceScopeFactory
/// (singleton-safe scoped service access — same pattern as FeatureFlagService).
///
/// This separates DB access (async, background) from CORS callback (sync, read-only cache).
/// No .GetAwaiter().GetResult() in request path — no thread pool blocking.
/// </summary>
public class DynamicCorsCacheHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DynamicCorsCacheHostedService> _logger;
    private static readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);

    public DynamicCorsCacheHostedService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<DynamicCorsCacheHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Pre-warm immediately on startup (don't wait 5 min for first load)
        await RefreshCacheAsync(stoppingToken);

        using var timer = new PeriodicTimer(_refreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshCacheAsync(stoppingToken);
        }
    }

    private async Task RefreshCacheAsync(CancellationToken ct)
    {
        try
        {
            List<string> domains;
            using (var scope = _scopeFactory.CreateScope())
            {
                var instanceService = scope.ServiceProvider.GetRequiredService<IKhachLinkInstanceService>();
                // Lightweight query — SELECT CustomDomain only, WHERE IsActive = true
                domains = await instanceService.GetActiveCustomDomainsAsync(ct);
            }

            // Build snapshot: HashSet of normalized origins (https://domain)
            var snapshot = new HashSet<string>(
                domains.Select(d => $"https://{d}".ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);

            _cache.Set(DynamicCorsService.SnapshotKey, snapshot, _refreshInterval);
            _logger.LogDebug("Refreshed CORS cache: {Count} origins from registry", snapshot.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh CORS cache from DB — stale cache retained");
            // Stale cache stays (IMemoryCache TTL = 5 min, will expire naturally)
            // Next tick will retry. No blocking, no crash.
        }
    }
}
