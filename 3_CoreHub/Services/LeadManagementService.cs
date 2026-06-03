using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Lead Management Service Implementation
    /// Handles lead creation, updates, and status management
    /// </summary>
    public class LeadManagementService(VanAnDbContext dbContext, ILogger<LeadManagementService> logger) : ILeadManagementService
    {
        private readonly VanAnDbContext _dbContext = dbContext;
        private readonly ILogger<LeadManagementService> _logger = logger;

        public async Task<Lead> CreateLeadAsync(Lead lead)
        {
            _logger.LogInformation("Creating new lead for tenant {TenantId}", lead.TenantId);

            _ = _dbContext.Leads.Add(lead);
            _ = await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Lead created successfully with ID {LeadId}", lead.Id);
            return lead;
        }

        public async Task<Lead> UpdateLeadStatusAsync(Guid leadId, LeadStatus status, Guid? staffId = null)
        {
            _logger.LogInformation("Updating lead {LeadId} status to {Status}", leadId, status);

            Lead? lead = await _dbContext.Leads.FindAsync(leadId);
            if (lead == null)
            {
                _logger.LogWarning("Lead {LeadId} not found", leadId);
                throw new ArgumentException($"Lead with ID {leadId} not found");
            }

            lead.Status = status;
            lead.UpdatedAt = DateTime.UtcNow;
            if (staffId.HasValue)
            {
                lead.AssignedStaffId = staffId.Value;
            }

            _ = await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Lead {LeadId} status updated successfully", leadId);
            return lead;
        }

        public async Task<List<Lead>> GetLeadsByStatusAsync(LeadStatus status)
        {
            _logger.LogInformation("Retrieving leads with status {Status}", status);

            List<Lead> leads = await _dbContext.Leads
                .Where(l => l.Status == status && !l.IsDeleted)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();

            _logger.LogInformation("Found {Count} leads with status {Status}", leads.Count, status);
            return leads;
        }
    }
}
