using System.Text.Json;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// WS-2: Promo campaign service implementation.
    /// Creates campaigns with recipient records (segmented customers).
    /// PromoCampaignJob (HostedService) processes pending campaigns async.
    /// </summary>
    public class PromoCampaignService(
        IPromoCampaignRepository campaignRepository,
        ICustomerSegmentationService customerSegmentationService,
        ITenantProvider tenantProvider,
        ILogger<PromoCampaignService> logger) : IPromoCampaignService
    {
        private readonly IPromoCampaignRepository _campaignRepository = campaignRepository;
        private readonly ICustomerSegmentationService _customerSegmentationService = customerSegmentationService;
        private readonly ITenantProvider _tenantProvider = tenantProvider;
        private readonly ILogger<PromoCampaignService> _logger = logger;

        public async Task<IReadOnlyList<Customer>> PreviewSegmentAsync(CustomerSegmentCriteria criteria)
        {
            _logger.LogInformation("PreviewSegment: filtering customers with criteria={Criteria}", JsonSerializer.Serialize(criteria));
            return await _customerSegmentationService.GetCustomersBySegmentAsync(criteria);
        }

        public async Task<PromoCampaign> CreateCampaignAsync(
            string title, string message, string? url, CustomerSegmentCriteria criteria)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required.", nameof(title));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message is required.", nameof(message));

            // 1. Query segment to get recipient list
            var customers = await _customerSegmentationService.GetCustomersBySegmentAsync(criteria);
            if (customers.Count == 0)
                throw new InvalidOperationException("Không có khách hàng nào thỏa bộ lọc. Vui lòng nới lỏng điều kiện.");

            // 2. Snapshot criteria for audit trail
            string segmentJson = JsonSerializer.Serialize(criteria);

            // 3. Create campaign entity
            var tenantId = new TenantId(_tenantProvider.TenantId);
            var campaign = new PromoCampaign(tenantId, title, message, url, customers.Count, segmentJson);
            campaign = await _campaignRepository.AddAsync(campaign);

            // 4. Create recipient records (one per customer)
            var recipients = customers.Select(c => new PromoCampaignRecipient(tenantId, campaign.Id, c.Id)).ToList();
            await _campaignRepository.AddRecipientsAsync(recipients);

            _logger.LogInformation("CreateCampaign: campaign {CampaignId} created with {Count} recipients (title='{Title}')",
                campaign.Id, customers.Count, title);
            return campaign;
        }

        public Task<PromoCampaign?> GetCampaignAsync(Guid id)
            => _campaignRepository.GetByIdAsync(id);

        public Task<IReadOnlyList<PromoCampaign>> GetCampaignsAsync(int page, int pageSize)
            => _campaignRepository.GetAllAsync(page, pageSize);

        public async Task<bool> CancelCampaignAsync(Guid id)
        {
            var campaign = await _campaignRepository.GetByIdAsync(id);
            if (campaign == null) return false;
            if (campaign.Status is "Completed" or "Failed" or "Cancelled") return false;

            campaign.MarkCancelled();
            _ = await _campaignRepository.UpdateAsync(campaign);

            _logger.LogInformation("CancelCampaign: campaign {CampaignId} cancelled", id);
            return true;
        }

        public Task<IReadOnlyList<PromoCampaignRecipient>> GetRecipientsAsync(Guid campaignId, int page, int pageSize)
            => _campaignRepository.GetRecipientsAsync(campaignId, page, pageSize);

        public async Task<(int Pending, int Sent, int Failed)> GetRecipientStatusSummaryAsync(Guid campaignId)
        {
            int pending = await _campaignRepository.GetRecipientCountByStatusAsync(campaignId, "Pending");
            int sent = await _campaignRepository.GetRecipientCountByStatusAsync(campaignId, "Sent");
            int failed = await _campaignRepository.GetRecipientCountByStatusAsync(campaignId, "Failed");
            return (pending, sent, failed);
        }
    }
}
