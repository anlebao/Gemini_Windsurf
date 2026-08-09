using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// WS-2: Promo campaign processing job — polls for Pending campaigns and sends push notifications
    /// to each recipient (batch 50, 100ms delay between sends to avoid push provider rate limits).
    /// Outbox pattern: campaign stays Pending → Processing → Completed/Failed.
    /// Per-recipient tracking: PromoCampaignRecipient.Status (Pending/Sent/Failed).
    /// </summary>
    public class PromoCampaignJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PromoCampaignJob> _logger;
        private readonly VanAn.CoreHub.Services.IBackgroundServiceToggleService _toggleService;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);
        private readonly TimeSpan _initialDelay = TimeSpan.FromSeconds(15);
        private const int BatchSize = 50;
        private const int SendDelayMs = 100;

        public PromoCampaignJob(
            IServiceProvider serviceProvider,
            ILogger<PromoCampaignJob> logger,
            VanAn.CoreHub.Services.IBackgroundServiceToggleService toggleService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _toggleService = toggleService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PromoCampaignJob started — polls every {Interval}s, first run in {Delay}s",
                _pollInterval.TotalSeconds, _initialDelay.TotalSeconds);

            try { await Task.Delay(_initialDelay, stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // REQ-1.2: Runtime toggle — skip cycle if disabled via admin UI
                    if (await _toggleService.IsEnabledAsync("PromoCampaignJob", stoppingToken))
                        await ProcessPendingCampaignsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PromoCampaignJob: error during poll cycle");
                }

                try { await Task.Delay(_pollInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }

            _logger.LogInformation("PromoCampaignJob stopped");
        }

        private async Task ProcessPendingCampaignsAsync(CancellationToken cancellationToken)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            var campaignRepository = scope.ServiceProvider.GetRequiredService<IPromoCampaignRepository>();
            var pushNotificationService = scope.ServiceProvider.GetService<PushNotificationService>();

            var pendingCampaigns = await campaignRepository.GetPendingCampaignsAsync();
            if (pendingCampaigns.Count == 0) return;

            _logger.LogInformation("PromoCampaignJob: processing {Count} pending campaign(s)", pendingCampaigns.Count);

            foreach (var campaign in pendingCampaigns)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessOneCampaignAsync(campaign, campaignRepository, pushNotificationService, cancellationToken);
            }
        }

        private async Task ProcessOneCampaignAsync(
            PromoCampaign campaign,
            IPromoCampaignRepository campaignRepository,
            PushNotificationService? pushNotificationService,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("PromoCampaignJob: starting campaign {CampaignId} ('{Title}') with {Total} recipients",
                campaign.Id, campaign.Title, campaign.TotalRecipients);

            campaign.MarkProcessing();
            _ = await campaignRepository.UpdateAsync(campaign);

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Re-check campaign status — admin may have cancelled while processing
                    var fresh = await campaignRepository.GetByIdAsync(campaign.Id);
                    if (fresh is null || fresh.Status == "Cancelled")
                    {
                        _logger.LogInformation("PromoCampaignJob: campaign {CampaignId} was cancelled — stopping processing", campaign.Id);
                        return;
                    }

                    var batch = await campaignRepository.GetPendingRecipientsAsync(campaign.Id, BatchSize);
                    if (batch.Count == 0) break; // All recipients processed

                    foreach (var recipient in batch)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (pushNotificationService == null)
                        {
                            recipient.MarkFailed("PushNotificationService unavailable");
                            await campaignRepository.UpdateRecipientAsync(recipient);
                            campaign.IncrementFailed();
                            continue;
                        }

                        try
                        {
                            int sent = await pushNotificationService.SendPromoNotificationAsync(
                                recipient.CustomerId, campaign.Title, campaign.Message, campaign.Url);

                            if (sent > 0)
                            {
                                recipient.MarkSent();
                                campaign.IncrementSent();
                            }
                            else
                            {
                                // No active subscriptions — not an error, just no channel
                                recipient.MarkSent(); // Mark as processed (no subscription = silently skipped)
                                campaign.IncrementSent();
                            }
                        }
                        catch (Exception ex)
                        {
                            recipient.MarkFailed(ex.Message);
                            campaign.IncrementFailed();
                            _logger.LogWarning(ex, "PromoCampaignJob: failed to send to recipient {RecipientId} (customer {CustomerId})",
                                recipient.Id, recipient.CustomerId);
                        }

                        await campaignRepository.UpdateRecipientAsync(recipient);

                        // Rate limit — avoid push provider ban
                        try { await Task.Delay(SendDelayMs, cancellationToken); }
                        catch (OperationCanceledException) { throw; }
                    }

                    _ = await campaignRepository.UpdateAsync(campaign);
                }

                campaign.MarkCompleted();
                _ = await campaignRepository.UpdateAsync(campaign);
                _logger.LogInformation("PromoCampaignJob: campaign {CampaignId} completed — sent={Sent}, failed={Failed}, total={Total}",
                    campaign.Id, campaign.SentCount, campaign.FailedCount, campaign.TotalRecipients);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                campaign.MarkFailed(ex.Message);
                _ = await campaignRepository.UpdateAsync(campaign);
                _logger.LogError(ex, "PromoCampaignJob: campaign {CampaignId} failed with error: {Error}",
                    campaign.Id, ex.Message);
            }
        }
    }
}
