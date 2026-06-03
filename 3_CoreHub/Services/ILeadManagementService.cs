using VanAn.CoreHub.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service interface for Lead Management
    /// Handles lead creation, updates, and status management
    /// </summary>
    public interface ILeadManagementService
    {
        /// <summary>
        /// Creates a new lead in the system
        /// </summary>
        /// <param name="lead">The lead entity to create</param>
        /// <returns>The created lead with generated ID</returns>
        Task<Lead> CreateLeadAsync(Lead lead);

        /// <summary>
        /// Updates the status of an existing lead
        /// </summary>
        /// <param name="leadId">The ID of the lead to update</param>
        /// <param name="status">The new status to set</param>
        /// <param name="staffId">Optional staff ID assigning the lead</param>
        /// <returns>The updated lead entity</returns>
        Task<Lead> UpdateLeadStatusAsync(Guid leadId, LeadStatus status, Guid? staffId = null);

        /// <summary>
        /// Gets all leads with a specific status
        /// </summary>
        /// <param name="status">The status to filter by</param>
        /// <returns>List of leads with the specified status</returns>
        Task<List<Lead>> GetLeadsByStatusAsync(LeadStatus status);
    }
}
