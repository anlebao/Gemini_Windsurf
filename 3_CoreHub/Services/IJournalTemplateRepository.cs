using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Repository interface for Journal Templates
    /// </summary>
    public interface IJournalTemplateRepository
    {
        /// <summary>
        /// Gets a template by code and tenant
        /// </summary>
        Task<JournalTemplate?> GetByCodeAsync(TenantId tenantId, string code);

        /// <summary>
        /// Gets all templates for a tenant
        /// </summary>
        Task<IEnumerable<JournalTemplate>> GetByTenantAsync(TenantId tenantId);

        /// <summary>
        /// Adds a new template
        /// </summary>
        Task AddAsync(JournalTemplate template);

        /// <summary>
        /// Updates an existing template
        /// </summary>
        Task UpdateAsync(JournalTemplate template);

        /// <summary>
        /// Deletes a template
        /// </summary>
        Task DeleteAsync(JournalTemplate template);
    }
}
