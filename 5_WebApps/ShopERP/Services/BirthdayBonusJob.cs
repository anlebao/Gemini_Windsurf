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
    /// Loyalty-C WS-B: Birthday annual bonus job — runs daily, finds customers whose birthday is today
    /// (UTC month+day match), awards annual birthday bonus points via MissionService.CompleteAnnualMissionAsync
    /// (Custom mission type, one-completion-per-calendar-year enforcement), and sends a birthday push notification.
    ///
    /// Tenant context: reads Seed:TenantId from configuration (same as seeding logic in Program.cs),
    /// then calls ITenantProvider.SetTenant() to set the context for scoped repositories/services.
    ///
    /// Degraded mode: if no Custom mission is configured for the tenant, the job logs a warning and
    /// still sends birthday notifications (without points). If no customers have birthdays today, the
    /// job completes silently.
    /// </summary>
    public class BirthdayBonusJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BirthdayBonusJob> _logger;
        private readonly TimeSpan _runInterval = TimeSpan.FromHours(24);
        private readonly TimeSpan _initialDelay = TimeSpan.FromMinutes(5); // Wait 5 min after startup before first run

        public BirthdayBonusJob(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<BirthdayBonusJob> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BirthdayBonusJob started — runs every {Interval}h, first run in {Delay}min",
                _runInterval.TotalHours, _initialDelay.TotalMinutes);

            // Wait initial delay before first run (let app warm up)
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
                    await RunBirthdayBonusAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "BirthdayBonusJob: error during daily run");
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

            _logger.LogInformation("BirthdayBonusJob stopped");
        }

        private async Task RunBirthdayBonusAsync(CancellationToken cancellationToken)
        {
            // Resolve tenant ID from configuration (same key as seeding logic)
            string tenantIdStr = _configuration["Seed:TenantId"]
                ?? "00000000-0000-0000-0000-000000000001";
            if (!Guid.TryParse(tenantIdStr, out Guid tenantId))
            {
                _logger.LogWarning("BirthdayBonusJob: invalid Seed:TenantId config '{TenantIdStr}' — skipping run", tenantIdStr);
                return;
            }

            using IServiceScope scope = _serviceProvider.CreateScope();
            var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            tenantProvider.SetTenant(tenantId);

            var customerRepository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
            var missionService = scope.ServiceProvider.GetRequiredService<IMissionService>();
            var pushNotificationService = scope.ServiceProvider.GetService<PushNotificationService>();
            var shopFeatureSettingsService = scope.ServiceProvider.GetService<IShopFeatureSettingsService>();

            // Loyalty-C WS-C: Check Notify_BirthdayBonus toggle before sending notifications.
            // Points are still awarded regardless of toggle — only push is gated.
            bool notifyBirthday = true;
            if (shopFeatureSettingsService != null)
            {
                try
                {
                    var settings = await shopFeatureSettingsService.GetSettingsAsync(tenantId);
                    notifyBirthday = settings.Notify_BirthdayBonus;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "BirthdayBonusJob: failed to load Notify_BirthdayBonus toggle — defaulting to true");
                }
            }

            // Find customers with birthday today (UTC month+day match)
            var birthdayCustomers = await customerRepository.GetCustomersWithBirthdayTodayAsync();
            if (birthdayCustomers.Count == 0)
            {
                _logger.LogInformation("BirthdayBonusJob: no customers with birthday today ({Date:yyyy-MM-dd}) — skipping",
                    DateTime.UtcNow.Date);
                return;
            }

            _logger.LogInformation("BirthdayBonusJob: processing {Count} birthday customer(s) for tenant {TenantId}",
                birthdayCustomers.Count, tenantId);

            int awarded = 0;
            int notified = 0;
            int currentYear = DateTime.UtcNow.Year;

            foreach (var customer in birthdayCustomers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Award annual birthday bonus via Custom mission (one-per-year enforcement)
                string metadata = $"{{\"kind\":\"birthday_annual\",\"year\":{currentYear}}}";
                var result = await missionService.CompleteAnnualMissionAsync(customer.Id, MissionType.Custom, metadata);

                if (result.Success)
                {
                    awarded++;
                    _logger.LogInformation("BirthdayBonusJob: awarded {Points} birthday bonus points to customer {CustomerId} ({Name}) for year {Year}",
                        result.PointsAwarded, customer.Id, customer.FullName, currentYear);
                }
                else
                {
                    _logger.LogDebug("BirthdayBonusJob: no birthday bonus for customer {CustomerId} — {Error} (likely already awarded this year or no mission configured)",
                        customer.Id, result.Error);
                }

                // Send birthday push notification (best-effort — non-blocking on failure)
                // Loyalty-C WS-C: gated by Notify_BirthdayBonus toggle (checked above)
                if (pushNotificationService != null && notifyBirthday)
                {
                    try
                    {
                        int sent = await pushNotificationService.SendBirthdayNotificationAsync(customer.Id, customer.FullName, result.PointsAwarded);
                        if (sent > 0) notified++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "BirthdayBonusJob: failed to send birthday notification to customer {CustomerId}", customer.Id);
                    }
                }
            }

            _logger.LogInformation("BirthdayBonusJob complete: {Total} birthday customer(s) processed, {Awarded} awarded points, {Notified} notified",
                birthdayCustomers.Count, awarded, notified);
        }
    }
}
