using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Enhanced Journal Factory with Business Rules Engine
    /// Implements Hybrid Architecture combining Generic Template and Rule Engine
    /// </summary>
    public interface IEnhancedJournalFactory
    {
        /// <summary>
        /// Creates a journal entry from template with business rules applied
        /// </summary>
        Task<JournalEntry> CreateFromTemplateAsync(
            TenantId tenantId, 
            string templateCode, 
            decimal amount, 
            Dictionary<string, object> parameters);

        /// <summary>
        /// Validates a journal template before use
        /// </summary>
        Task<bool> ValidateTemplateAsync(
            JournalTemplate template, 
            Dictionary<string, object> parameters);

        /// <summary>
        /// Gets available template codes for a tenant
        /// </summary>
        Task<IEnumerable<string>> GetAvailableTemplatesAsync(TenantId tenantId);
    }
}
