using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Formula;
using VanAn.CoreHub.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace VanAn.CoreHub.Services.PreAggregation
{
    /// <summary>
    /// Smart PreAggregation Service - Dependency-driven optimization
    /// Only aggregates what's actually needed by templates
    /// </summary>
    public class SmartPreAggregationService : IPreAggregationService
    {
        private readonly VanAnDbContext _context;
        private readonly IFormulaEngine _formulaEngine;
        private readonly ILogger<SmartPreAggregationService> _logger;
        
        public SmartPreAggregationService(
            VanAnDbContext context,
            IFormulaEngine formulaEngine,
            ILogger<SmartPreAggregationService> logger)
        {
            _context = context;
            _formulaEngine = formulaEngine;
            _logger = logger;
        }
        
        public async Task<Dictionary<string, decimal>> GetAccountAggregatesAsync(
            TenantId tenantId, 
            AccountingPeriod period)
        {
            _logger.LogInformation("Starting smart pre-aggregation for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            
            // Get all templates for this tenant
            var templates = await GetTemplatesForTenantAsync(tenantId);
            
            // Extract account patterns from ALL template formulas
            var accountPatterns = ExtractAccountPatterns(templates);
            
            _logger.LogInformation("Extracted {Count} unique account patterns from {TemplateCount} templates for tenant {TenantId}", 
                accountPatterns.Count, templates.Count, tenantId.Value);
            
            var aggregates = new Dictionary<string, decimal>();
            
            // Only aggregate what's needed
            foreach (var pattern in accountPatterns)
            {
                var creditSum = await GetAccountSumAsync(tenantId, period, pattern, "Credit");
                var debitSum = await GetAccountSumAsync(tenantId, period, pattern, "Debit");
                
                aggregates[$"Account_{pattern}_Credit"] = creditSum;
                aggregates[$"Account_{pattern}_Debit"] = debitSum;
                
                _logger.LogDebug("Aggregated pattern {Pattern}: Credit={Credit}, Debit={Debit}", 
                    pattern, creditSum, debitSum);
            }
            
            _logger.LogInformation("Smart pre-aggregation completed for tenant {TenantId}: {ValueCount} values", 
                tenantId.Value, aggregates.Count);
            
            return aggregates;
        }
        
        private async Task<List<HKDBookTemplate>> GetTemplatesForTenantAsync(TenantId tenantId)
        {
            var templates = new List<HKDBookTemplate>();
            
            // For now, we'll create templates based on HKD group
            // In production, this would come from tenant configuration
            var tenantData = await GetTenantAsync(tenantId);
            
            // Extract HKDGroup from dictionary
            var hkdGroupValue = tenantData.GetValueOrDefault("HKDGroup")?.ToString();
            HKDGroup? hkdGroup = null;
            if (Enum.TryParse<HKDGroup>(hkdGroupValue, out var parsedGroup))
                hkdGroup = parsedGroup;
            
            switch (hkdGroup)
            {
                case HKDGroup.Group1:
                    templates.Add(new S1aHKDTemplate());
                    break;
                case HKDGroup.Group2:
                    templates.Add(new S2aHKDTemplate());
                    templates.Add(new S2bHKDTemplate());
                    templates.Add(new S2cHKDTemplate());
                    templates.Add(new S2dHKDTemplate());
                    templates.Add(new S2eHKDTemplate());
                    break;
                case HKDGroup.Group3:
                    templates.Add(new S3aHKDTemplate());
                    break;
            }
            
            _logger.LogDebug("Retrieved {Count} templates for tenant {TenantId} with HKD group {Group}", 
                templates.Count, tenantId.Value, hkdGroup);
            
            return templates;
        }
        
        private HashSet<string> ExtractAccountPatterns(List<HKDBookTemplate> templates)
        {
            var patterns = new HashSet<string>();
            
            foreach (var template in templates)
            {
                // Extract from fields
                if (template.Fields != null)
                {
                    foreach (var field in template.Fields)
                    {
                        if (!string.IsNullOrEmpty(field.Formula))
                        {
                            var dependencies = _formulaEngine.GetDependencies(field.Formula);
                            AddAccountPatternsFromDependencies(dependencies, patterns);
                        }
                    }
                }
                
                // Extract from calculations
                if (template.Calculations != null)
                {
                    foreach (var calculation in template.Calculations)
                    {
                        if (!string.IsNullOrEmpty(calculation.Formula))
                        {
                            var dependencies = _formulaEngine.GetDependencies(calculation.Formula);
                            AddAccountPatternsFromDependencies(dependencies, patterns);
                        }
                    }
                }
            }
            
            _logger.LogDebug("Extracted account patterns: {Patterns}", string.Join(", ", patterns));
            
            return patterns;
        }
        
        private void AddAccountPatternsFromDependencies(List<string> dependencies, HashSet<string> patterns)
        {
            foreach (var dependency in dependencies)
            {
                if (dependency.StartsWith("Account_"))
                {
                    var parts = dependency.Split('_');
                    if (parts.Length >= 3)
                    {
                        var pattern = parts[1];
                        patterns.Add(pattern);
                    }
                }
            }
        }
        
        private async Task<decimal> GetAccountSumAsync(
            TenantId tenantId, 
            AccountingPeriod period, 
            string accountPattern, 
            string side)
        {
            try
            {
                var query = _context.JournalEntries
                    .Where(e => EF.Property<Guid>(e, "TenantId") == tenantId.Value &&
                               e.Period.Year == period.Year &&
                               e.Period.Month == period.Month &&
                               e.Lines.Any(l => l.AccountNumber.StartsWith(accountPattern)));
                
                var sum = await query
                    .SelectMany(e => e.Lines)
                    .Where(l => l.AccountNumber.StartsWith(accountPattern))
                    .SumAsync(l => side.Equals("Credit", StringComparison.OrdinalIgnoreCase) ? l.CreditAmount : l.DebitAmount);
                
                _logger.LogDebug("Account sum for tenant {TenantId}, pattern {Pattern}, side {Side}: {Sum}", 
                    tenantId.Value, accountPattern, side, sum);
                
                return sum;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account sum for tenant {TenantId}, pattern {Pattern}, side {Side}", 
                    tenantId.Value, accountPattern, side);
                return 0;
            }
        }
        
        private async Task<Dictionary<string, object>> GetTenantAsync(TenantId tenantId)
        {
            var tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId);
            
            if (tenant == null)
            {
                _logger.LogWarning("Tenant {TenantId} not found", tenantId.Value);
                return new Dictionary<string, object>();
            }
            
            return new Dictionary<string, object>
            {
                ["TenantId"] = tenant.Id.Value,
                ["Name"] = tenant.Name,
                ["BusinessType"] = tenant.BusinessType.ToString(),
                ["HKDGroup"] = tenant.HKDGroup?.ToString(),
                ["CreatedAt"] = tenant.CreatedAt,
                ["IsActive"] = tenant.IsActive
            };
        }
    }
}
