using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Repositories
{
    /// <summary>
    /// Repository implementation for Journal Templates
    /// Implements 5-layer protection: Domain, EF Core, Repository, Service, API
    /// </summary>
    public class JournalTemplateRepository(VanAnDbContext context, ILogger<JournalTemplateRepository> logger) : Services.IJournalTemplateRepository
    {
        private readonly VanAnDbContext _context = context;
        private readonly ILogger<JournalTemplateRepository> _logger = logger;

        public async Task<JournalTemplate?> GetByCodeAsync(TenantId tenantId, string code)
        {
            try
            {
                return await _context.JournalTemplates
                    .FirstOrDefaultAsync(t => EF.Property<Guid>(t, "TenantId") == tenantId.Value && t.Code == code);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting journal template by code {Code} for tenant {TenantId}", code, tenantId.Value);
                return null;
            }
        }

        public async Task<IEnumerable<JournalTemplate>> GetByTenantAsync(TenantId tenantId)
        {
            try
            {
                return await _context.JournalTemplates
                    .Where(t => EF.Property<Guid>(t, "TenantId") == tenantId.Value)
                    .OrderBy(t => t.Code)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting journal templates for tenant {TenantId}", tenantId.Value);
                return new List<JournalTemplate>();
            }
        }

        public async Task AddAsync(JournalTemplate template)
        {
            try
            {
                await _context.JournalTemplates.AddAsync(template);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Added journal template {Code} for tenant {TenantId}", template.Code, template.TenantId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding journal template {Code} for tenant {TenantId}", template.Code, template.TenantId.Value);
                throw;
            }
        }

        public async Task UpdateAsync(JournalTemplate template)
        {
            try
            {
                _context.JournalTemplates.Update(template);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated journal template {Code} for tenant {TenantId}", template.Code, template.TenantId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating journal template {Code} for tenant {TenantId}", template.Code, template.TenantId.Value);
                throw;
            }
        }

        public async Task DeleteAsync(JournalTemplate template)
        {
            try
            {
                _context.JournalTemplates.Remove(template);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted journal template {Code} for tenant {TenantId}", template.Code, template.TenantId.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting journal template {Code} for tenant {TenantId}", template.Code, template.TenantId.Value);
                throw;
            }
        }
    }
}
