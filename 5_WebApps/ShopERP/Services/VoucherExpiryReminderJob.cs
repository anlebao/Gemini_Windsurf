using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// Loyalty-C WS-C: Voucher expiry reminder job — runs daily, finds active vouchers expiring within
    /// N days (configurable via LoyaltyC:VoucherExpiryReminderDays, default 3), and sends a push notification
    /// reminder to each voucher's customer.
    ///
    /// Tenant context: reads Seed:TenantId from configuration (same as BirthdayBonusJob),
    /// then calls ITenantProvider.SetTenant() to set the context for scoped repositories/services.
    ///
    /// Degraded mode: if no vouchers are expiring, the job completes silently. If PushNotificationService
    /// is unavailable, the job logs a warning but continues (notifications are best-effort).
    /// </summary>
    public class VoucherExpiryReminderJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<VoucherExpiryReminderJob> _logger;
        private readonly TimeSpan _runInterval = TimeSpan.FromHours(24);
        private readonly TimeSpan _initialDelay = TimeSpan.FromMinutes(8); // Offset from BirthdayBonusJob to avoid simultaneous scope creation

        public VoucherExpiryReminderJob(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<VoucherExpiryReminderJob> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("VoucherExpiryReminderJob started — runs every {Interval}h, first run in {Delay}min",
                _runInterval.TotalHours, _initialDelay.TotalMinutes);

            try
            {
                await Task.Delay(_initialDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunExpiryRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "VoucherExpiryReminderJob: error during daily run");
                }

                try
                {
                    await Task.Delay(_runInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("VoucherExpiryReminderJob stopped");
        }

        private async Task RunExpiryRemindersAsync(CancellationToken cancellationToken)
        {
            // Resolve tenant ID from configuration
            string tenantIdStr = _configuration["Seed:TenantId"]
                ?? "00000000-0000-0000-0000-000000000001";
            if (!Guid.TryParse(tenantIdStr, out Guid tenantId))
            {
                _logger.LogWarning("VoucherExpiryReminderJob: invalid Seed:TenantId config '{TenantIdStr}' — skipping run", tenantIdStr);
                return;
            }

            // Resolve reminder window from configuration (default 3 days)
            int reminderDays = _configuration.GetValue<int>("LoyaltyC:VoucherExpiryReminderDays");
            if (reminderDays <= 0) reminderDays = 3;

            using IServiceScope scope = _serviceProvider.CreateScope();
            var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            tenantProvider.SetTenant(tenantId);

            var redemptionRepository = scope.ServiceProvider.GetRequiredService<IRedemptionRepository>();
            var pushNotificationService = scope.ServiceProvider.GetService<PushNotificationService>();
            var shopFeatureSettingsService = scope.ServiceProvider.GetService<IShopFeatureSettingsService>();

            // Loyalty-C WS-C: Check Notify_VoucherExpiringSoon toggle before sending notifications.
            // Also honor VoucherExpiryNotifyHours (per-tenant override of reminderDays).
            bool notifyExpiry = true;
            if (shopFeatureSettingsService != null)
            {
                try
                {
                    var settings = await shopFeatureSettingsService.GetSettingsAsync(tenantId);
                    notifyExpiry = settings.Notify_VoucherExpiringSoon;
                    // Per-tenant override: convert hours → days (round up)
                    if (settings.VoucherExpiryNotifyHours > 0)
                        reminderDays = (int)Math.Ceiling(settings.VoucherExpiryNotifyHours / 24.0);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "VoucherExpiryReminderJob: failed to load Notify_VoucherExpiringSoon toggle — defaulting to true");
                }
            }

            // Find active vouchers expiring within the reminder window
            var expiringVouchers = await redemptionRepository.GetVouchersExpiringWithinAsync(reminderDays);
            if (expiringVouchers.Count == 0)
            {
                _logger.LogInformation("VoucherExpiryReminderJob: no vouchers expiring within {Days} days — skipping", reminderDays);
                return;
            }

            // Skip notification work entirely if toggle is off (still log for audit)
            if (!notifyExpiry)
            {
                _logger.LogInformation("VoucherExpiryReminderJob: {Count} voucher(s) expiring within {Days} days but Notify_VoucherExpiringSoon=false — skipping notifications",
                    expiringVouchers.Count, reminderDays);
                return;
            }

            _logger.LogInformation("VoucherExpiryReminderJob: processing {Count} expiring voucher(s) for tenant {TenantId}",
                expiringVouchers.Count, tenantId);

            int notified = 0;
            DateTime now = DateTime.UtcNow;

            foreach (var voucher in expiringVouchers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Calculate days remaining (rounded up — a voucher expiring in 6 hours shows "1 day")
                int daysRemaining = (int)Math.Ceiling((voucher.ExpiresAt - now).TotalDays);
                if (daysRemaining < 0) daysRemaining = 0;

                // Look up product name via RedemptionRecord → CatalogItem (best-effort, non-blocking)
                string? productName = null;
                try
                {
                    var record = await redemptionRepository.GetRecordByIdAsync(voucher.RedemptionRecordId);
                    if (record != null)
                    {
                        var catalogItem = await redemptionRepository.GetCatalogItemByIdAsync(record.CatalogItemId);
                        productName = catalogItem?.ProductName;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "VoucherExpiryReminderJob: could not resolve product name for voucher {VoucherCode} — using default", voucher.VoucherCode);
                }

                // Send push notification (best-effort — non-blocking on failure)
                if (pushNotificationService != null)
                {
                    try
                    {
                        int sent = await pushNotificationService.SendVoucherExpiryReminderAsync(
                            voucher.CustomerId,
                            voucher.VoucherCode,
                            productName,
                            voucher.ExpiresAt,
                            daysRemaining);
                        if (sent > 0) notified++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "VoucherExpiryReminderJob: failed to send expiry reminder for voucher {VoucherCode}", voucher.VoucherCode);
                    }
                }
            }

            _logger.LogInformation("VoucherExpiryReminderJob complete: {Total} expiring voucher(s) processed, {Notified} notified",
                expiringVouchers.Count, notified);
        }
    }
}
