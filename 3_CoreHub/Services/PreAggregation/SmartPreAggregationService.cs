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
    public class SmartPreAggregationService(
        VanAnDbContext context,
        IFormulaEngine formulaEngine,
        ILogger<SmartPreAggregationService> logger) : IPreAggregationService
    {
        private readonly VanAnDbContext _context = context;
        private readonly IFormulaEngine _formulaEngine = formulaEngine;
        private readonly ILogger<SmartPreAggregationService> _logger = logger;

        public async Task<Dictionary<string, decimal>> GetAccountAggregatesAsync(
            TenantId tenantId,
            AccountingPeriod period)
        {
            _logger.LogInformation("Starting smart pre-aggregation for tenant {TenantId}, period {Period}",
                tenantId.Value, period);

            // Get all templates for this tenant
            List<HKDBookTemplate> templates = await GetTemplatesForTenantAsync(tenantId);

            // Extract account patterns from ALL template formulas
            HashSet<string> accountPatterns = ExtractAccountPatterns(templates);

            _logger.LogInformation("Extracted {Count} unique account patterns from {TemplateCount} templates for tenant {TenantId}",
                accountPatterns.Count, templates.Count, tenantId.Value);

            Dictionary<string, decimal> aggregates = [];

            // Only aggregate what's needed
            foreach (string pattern in accountPatterns)
            {
                decimal creditSum = await GetAccountSumAsync(tenantId, period, pattern, "Credit");
                decimal debitSum = await GetAccountSumAsync(tenantId, period, pattern, "Debit");

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
            List<HKDBookTemplate> templates = [];

            // For now, we'll create templates based on HKD group
            // In production, this would come from tenant configuration
            Dictionary<string, object> tenantData = await GetTenantAsync(tenantId);

            // Extract HKDGroup from dictionary
            string? hkdGroupValue = tenantData.GetValueOrDefault("HKDGroup")?.ToString();
            HKDGroup? hkdGroup = null;
            if (Enum.TryParse(hkdGroupValue, out HKDGroup parsedGroup))
            {
                hkdGroup = parsedGroup;
            }

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
                default:
                    break;
            }

            _logger.LogDebug("Retrieved {Count} templates for tenant {TenantId} with HKD group {Group}",
                templates.Count, tenantId.Value, hkdGroup);

            return templates;
        }

        private HashSet<string> ExtractAccountPatterns(List<HKDBookTemplate> templates)
        {
            HashSet<string> patterns = [];

            foreach (HKDBookTemplate template in templates)
            {
                // Extract from fields
                if (template.Fields != null)
                {
                    foreach (TemplateField field in template.Fields)
                    {
                        if (!string.IsNullOrEmpty(field.Formula))
                        {
                            List<string> dependencies = _formulaEngine.GetDependencies(field.Formula);
                            AddAccountPatternsFromDependencies(dependencies, patterns);
                        }
                    }
                }

                // Extract from calculations
                if (template.Calculations != null)
                {
                    foreach (TemplateCalculation calculation in template.Calculations)
                    {
                        if (!string.IsNullOrEmpty(calculation.Formula))
                        {
                            List<string> dependencies = _formulaEngine.GetDependencies(calculation.Formula);
                            AddAccountPatternsFromDependencies(dependencies, patterns);
                        }
                    }
                }
            }

            _logger.LogDebug("Extracted account patterns: {Patterns}", string.Join(", ", patterns));

            return patterns;
        }

        private static void AddAccountPatternsFromDependencies(List<string> dependencies, HashSet<string> patterns)
        {
            foreach (string dependency in dependencies)
            {
                if (dependency.StartsWith("Account_"))
                {
                    string[] parts = dependency.Split('_');
                    if (parts.Length >= 3)
                    {
                        string pattern = parts[1];
                        _ = patterns.Add(pattern);
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
                IQueryable<JournalEntry> query = _context.JournalEntries
                    .Where(e => EF.Property<Guid>(e, "TenantId") == tenantId.Value &&
                               e.Period.Year == period.Year &&
                               e.Period.Month == period.Month &&
                               e.Lines.Any(l => l.AccountNumber.StartsWith(accountPattern)));

                decimal sum = await query
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
            Tenant? tenant = await _context.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null)
            {
                _logger.LogWarning("Tenant {TenantId} not found", tenantId.Value);
                return [];
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
