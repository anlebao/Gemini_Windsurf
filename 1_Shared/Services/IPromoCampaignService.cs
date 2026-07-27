using VanAn.Shared.Domain;

namespace VanAn.Shared.Services
{
    /// <summary>
    /// WS-2: Promo campaign service contract — create + track bulk marketing push campaigns.
    /// Used by ShopERP admin UI + PromoCampaignJob (HostedService).
    /// </summary>
    public interface IPromoCampaignService
    {
        /// <summary>Preview segment (dry-run filter) — returns matching customers without creating a campaign.</summary>
        Task<IReadOnlyList<Customer>> PreviewSegmentAsync(CustomerSegmentCriteria criteria);

        /// <summary>Create a new campaign + recipient records. Returns campaign with TotalRecipients count.</summary>
        Task<PromoCampaign> CreateCampaignAsync(
            string title, string message, string? url, CustomerSegmentCriteria criteria);

        /// <summary>
        /// AF-P2-T1/T2: Create a new campaign targeting an explicit list of customer IDs
        /// (per-row "Gửi" + bulk select). Recipients are resolved by ICustomerRepository.GetByIdAsync
        /// (tenant-scoped, active, non-deleted). Unknown/inactive IDs are silently skipped.
        /// SegmentSnapshotJson captures the explicit ID list for audit trail.
        /// </summary>
        Task<PromoCampaign> CreateCampaignAsync(
            string title, string message, string? url, IReadOnlyList<Guid> selectedCustomerIds);

        /// <summary>Get campaign by ID.</summary>
        Task<PromoCampaign?> GetCampaignAsync(Guid id);

        /// <summary>List campaigns (paginated, newest first).</summary>
        Task<IReadOnlyList<PromoCampaign>> GetCampaignsAsync(int page, int pageSize);

        /// <summary>Cancel a pending/processing campaign. Returns false if campaign cannot be cancelled.</summary>
        Task<bool> CancelCampaignAsync(Guid id);

        /// <summary>Get recipients for a campaign (paginated).</summary>
        Task<IReadOnlyList<PromoCampaignRecipient>> GetRecipientsAsync(Guid campaignId, int page, int pageSize);

        /// <summary>Get recipient status summary (sent/failed/pending counts).</summary>
        Task<(int Pending, int Sent, int Failed)> GetRecipientStatusSummaryAsync(Guid campaignId);
    }
}
