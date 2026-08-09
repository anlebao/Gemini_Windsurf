using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 Phase 3 — Loyalty Budget Daily Reset Job.
/// Runs daily at 00:00 UTC. Resets PointsIssuedToday to 0 for ALL tenants.
/// Registered in Gateway Program.cs (PG is source of truth for LoyaltyTenantConfigs).
/// Toggleable via BackgroundServiceToggleService (default enabled, can be disabled via admin UI).
/// </summary>
public class LoyaltyBudgetDailyResetJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LoyaltyBudgetDailyResetJob> _logger;
    private readonly IBackgroundServiceToggleService _toggleService;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);  // Wait 2 min after startup

    public LoyaltyBudgetDailyResetJob(
        IServiceProvider serviceProvider,
        ILogger<LoyaltyBudgetDailyResetJob> logger,
        IBackgroundServiceToggleService toggleService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _toggleService = toggleService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LoyaltyBudgetDailyResetJob started — runs daily at 00:00 UTC");

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // REQ-1.2: Runtime toggle — skip cycle if disabled via admin UI
                if (await _toggleService.IsEnabledAsync("LoyaltyBudgetDailyResetJob", stoppingToken))
                {
                    // Calculate delay until next 00:00 UTC
                    var nowUtc = DateTime.UtcNow;
                    var nextRun = nowUtc.Date.AddDays(1);  // Next midnight UTC
                    var delay = nextRun - nowUtc;

                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    if (stoppingToken.IsCancellationRequested) break;

                    // Check toggle again after delay (may have been toggled off during wait)
                    if (await _toggleService.IsEnabledAsync("LoyaltyBudgetDailyResetJob", stoppingToken))
                        await RunResetAsync(stoppingToken);
                }
                else
                {
                    // Toggle OFF — wait 5 min before re-checking (avoid tight loop)
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoyaltyBudgetDailyResetJob error during daily run");
                // Wait 1 min before retrying to avoid tight error loop
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("LoyaltyBudgetDailyResetJob stopped");
    }

    internal async Task RunResetAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var budgetService = scope.ServiceProvider.GetRequiredService<ILoyaltyBudgetService>();
        await budgetService.ResetAllDailyCountersAsync(ct);
    }
}
