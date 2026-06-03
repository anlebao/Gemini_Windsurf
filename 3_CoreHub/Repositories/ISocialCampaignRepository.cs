using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository interface for Social Campaign entities
    /// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public interface ISocialCampaignRepository
    {
        /// <summary>
        /// Gets a campaign by ID
        /// </summary>
        Task<SocialCampaign?> GetByIdAsync(Guid campaignId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets campaigns by tenant ID
        /// </summary>
        Task<IEnumerable<SocialCampaign>> GetByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets active campaigns by tenant ID
        /// </summary>
        Task<IEnumerable<SocialCampaign>> GetActiveByTenantIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);
        Task<IEnumerable<SocialCampaign>> GetActiveAsync(CancellationToken cancellationToken = default);

        Task<SocialCampaign> AddAsync(SocialCampaign campaign, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing campaign
        /// </summary>
        Task<SocialCampaign> UpdateAsync(SocialCampaign campaign, CancellationToken cancellationToken = default);
    }
}
