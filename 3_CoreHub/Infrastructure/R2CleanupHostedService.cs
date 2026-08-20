using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VanAn.CoreHub.Services;

namespace VanAn.CoreHub.Infrastructure;

/// <summary>
/// Background service that periodically cleans up expired R2 photos.
/// Runs every RunIntervalHours (default 24h), deletes photos older than RetentionDays (default 30 days).
/// </summary>
public class R2CleanupHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<R2CleanupOptions> options,
    ILogger<R2CleanupHostedService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly R2CleanupOptions _options = options.Value;
    private readonly ILogger<R2CleanupHostedService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("R2CleanupHostedService is disabled via configuration.");
            return;
        }

        _logger.LogInformation(
            "R2CleanupHostedService started: retention={RetentionDays}d, interval={IntervalHours}h",
            _options.RetentionDays, _options.RunIntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(_options.RunIntervalHours), stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<IR2CleanupService>();
                var retention = TimeSpan.FromDays(_options.RetentionDays);

                var result = await cleanupService.CleanupAllTenantsAsync(retention, stoppingToken);

                _logger.LogInformation(
                    "R2 cleanup completed: sessions={Sessions}, photos={Photos}, bytes={Bytes}, errors={ErrorCount}",
                    result.SessionsProcessed, result.PhotosDeleted, result.BytesFreed, result.Errors.Count);

                foreach (var error in result.Errors)
                {
                    _logger.LogWarning("R2 cleanup error: {Error}", error);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "R2CleanupHostedService encountered an error during cleanup.");
            }
        }
    }
}
