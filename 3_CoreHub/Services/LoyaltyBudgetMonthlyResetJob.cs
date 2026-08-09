using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 Phase 3 — Loyalty Budget Monthly Reset Job.
/// Runs on 1st of each month at 00:00 UTC. Resets PointsIssuedThisMonth to 0 for ALL tenants.
/// Registered in Gateway Program.cs (PG is source of truth for LoyaltyTenantConfigs).
/// Toggleable via BackgroundServiceToggleService (default enabled, can be disabled via admin UI).
/// </summary>
public class LoyaltyBudgetMonthlyResetJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LoyaltyBudgetMonthlyResetJob> _logger;
    private readonly IBackgroundServiceToggleService _toggleService;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(3);  // Wait 3 min after startup

    public LoyaltyBudgetMonthlyResetJob(
        IServiceProvider serviceProvider,
        ILogger<LoyaltyBudgetMonthlyResetJob> logger,
        IBackgroundServiceToggleService toggleService)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _toggleService = toggleService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LoyaltyBudgetMonthlyResetJob started — runs on 1st of each month at 00:00 UTC");

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
                if (await _toggleService.IsEnabledAsync("LoyaltyBudgetMonthlyResetJob", stoppingToken))
                {
                    // Calculate delay until next 1st-of-month 00:00 UTC
                    var nowUtc = DateTime.UtcNow;
                    var nextRun = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    // If we're past the 1st of this month, target next month
                    if (nowUtc.Day > 1 || (nowUtc.Day == 1 && nowUtc.TimeOfDay > TimeSpan.Zero))
                    {
                        nextRun = nextRun.AddMonths(1);
                    }
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

                    // Check toggle again after delay
                    if (await _toggleService.IsEnabledAsync("LoyaltyBudgetMonthlyResetJob", stoppingToken))
                        await RunResetAsync(stoppingToken);
                }
                else
                {
                    // Toggle OFF — wait 10 min before re-checking (monthly job, no rush)
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoyaltyBudgetMonthlyResetJob error during monthly run");
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

        _logger.LogInformation("LoyaltyBudgetMonthlyResetJob stopped");
    }

    internal async Task RunResetAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var budgetService = scope.ServiceProvider.GetRequiredService<ILoyaltyBudgetService>();
        await budgetService.ResetAllMonthlyCountersAsync(ct);
    }
}
