using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 (Sprint 4 v1.2): Cooling Period Job — hourly HostedService.
/// Auto-approves SalesReferral + AppInstallAttribution with RiskScore<60 after 24h cooling period.
/// Creates WalletTransaction via IWalletService (Commission for SalesReferral, Commission for AppInstallBonus).
/// </summary>
public class CoolingPeriodJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CoolingPeriodJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public CoolingPeriodJob(IServiceProvider serviceProvider, ILogger<CoolingPeriodJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CoolingPeriodJob started — interval: {Interval}", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCoolingPeriodAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CoolingPeriodJob error");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessCoolingPeriodAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
        var walletService = scope.ServiceProvider.GetRequiredService<IWalletService>();

        var cutoff = DateTime.UtcNow.AddHours(-24);

        // Process SalesReferrals with RiskScore<60, Pending status, created >24h ago
        var pendingReferrals = await dbContext.SalesReferrals
            .IgnoreQueryFilters()
            .Where(r => r.CommissionStatus == CommissionStatus.Pending
                && r.RiskScore < 60
                && r.CreatedAt < cutoff)
            .ToListAsync(ct);

        foreach (var referral in pendingReferrals)
        {
            try
            {
                // Create WalletTransaction for commission payout
                await walletService.CreateTransactionAsync(
                    referral.SalesmanId,
                    WalletTransactionType.Commission,
                    referral.CommissionAmount,
                    $"Commission payout for order {referral.OrderId}",
                    referral.OrderId);

                referral.MarkCommissionPaid();
                await dbContext.SaveChangesAsync(ct);

                _logger.LogInformation("CoolingPeriodJob: SalesReferral {Id} approved + paid after 24h cooling", referral.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CoolingPeriodJob: Error processing SalesReferral {Id}", referral.Id);
            }
        }

        // Process AppInstallAttributions with RiskScore<60, Pending status, installed >24h ago
        var pendingAttributions = await dbContext.AppInstallAttributions
            .IgnoreQueryFilters()
            .Where(a => a.AttributionStatus == AttributionStatus.Pending
                && a.RiskScore < 60
                && a.InstalledAt < cutoff)
            .ToListAsync(ct);

        foreach (var attribution in pendingAttributions)
        {
            try
            {
                if (attribution.BonusAmount > 0)
                {
                    // Create WalletTransaction for app-install bonus payout
                    var txn = await walletService.CreateTransactionAsync(
                        attribution.SalesmanId,
                        WalletTransactionType.Commission,
                        attribution.BonusAmount,
                        $"App-install bonus for product {attribution.ProductId}");

                    attribution.MarkPaid(txn.Id);
                }
                else
                {
                    // No bonus — just mark as paid (no transaction)
                    attribution.MarkPaid(Guid.Empty);
                }

                await dbContext.SaveChangesAsync(ct);

                _logger.LogInformation("CoolingPeriodJob: AppInstallAttribution {Id} approved + paid after 24h cooling", attribution.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CoolingPeriodJob: Error processing AppInstallAttribution {Id}", attribution.Id);
            }
        }

        if (pendingReferrals.Count > 0 || pendingAttributions.Count > 0)
        {
            _logger.LogInformation("CoolingPeriodJob: Processed {Referrals} referrals + {Attributions} attributions",
                pendingReferrals.Count, pendingAttributions.Count);
        }
    }
}

/// <summary>
/// CC-S4 (Sprint 4 v1.2): Held Timeout Job — hourly HostedService.
/// Auto-rejects SalesReferral + AppInstallAttribution with Held status after 48h if admin hasn't reviewed.
/// </summary>
public class HeldTimeoutJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HeldTimeoutJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    public HeldTimeoutJob(IServiceProvider serviceProvider, ILogger<HeldTimeoutJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeldTimeoutJob started — interval: {Interval}", Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessHeldTimeoutAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HeldTimeoutJob error");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessHeldTimeoutAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();

        var now = DateTime.UtcNow;

        // Auto-reject SalesReferrals with Held status, HoldUntil < now
        var heldReferrals = await dbContext.SalesReferrals
            .IgnoreQueryFilters()
            .Where(r => r.CommissionStatus == CommissionStatus.Held && r.HoldUntil < now)
            .ToListAsync(ct);

        foreach (var referral in heldReferrals)
        {
            referral.MarkRejected("Auto-rejected: 48h hold timeout");
            _logger.LogInformation("HeldTimeoutJob: SalesReferral {Id} auto-rejected (48h timeout)", referral.Id);
        }

        // Auto-reject AppInstallAttributions with Held status, HoldUntil < now
        var heldAttributions = await dbContext.AppInstallAttributions
            .IgnoreQueryFilters()
            .Where(a => a.AttributionStatus == AttributionStatus.Held && a.HoldUntil < now)
            .ToListAsync(ct);

        foreach (var attribution in heldAttributions)
        {
            attribution.MarkRejected("Auto-rejected: 48h hold timeout");
            _logger.LogInformation("HeldTimeoutJob: AppInstallAttribution {Id} auto-rejected (48h timeout)", attribution.Id);
        }

        if (heldReferrals.Count > 0 || heldAttributions.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("HeldTimeoutJob: Auto-rejected {Referrals} referrals + {Attributions} attributions",
                heldReferrals.Count, heldAttributions.Count);
        }
    }
}
