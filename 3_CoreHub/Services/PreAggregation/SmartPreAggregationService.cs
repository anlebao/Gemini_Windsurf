using System.Text.RegularExpressions;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Formula;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
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
        IVanAnDbContext context,
        Lazy<IFormulaEngine> formulaEngine,
        ILogger<SmartPreAggregationService> logger) : IPreAggregationService
    {
        private readonly IVanAnDbContext _context = context;
        private readonly Lazy<IFormulaEngine> _formulaEngine = formulaEngine;
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

            // Wave 5: Also aggregate per-industry-sector for patterns used by SUM_ACCOUNT_BY_INDUSTRY.
            // Produces keys: Account_{pattern}_{side}_{sector} for each of the 4 IndustrySector values.
            // NULL IndustrySector entries are counted in the OtherBusiness bucket.
            HashSet<string> sectorPatterns = ExtractSectorAccountPatterns(templates);
            if (sectorPatterns.Count > 0)
            {
                foreach (string pattern in sectorPatterns)
                {
                    foreach (IndustrySector sector in Enum.GetValues<IndustrySector>())
                    {
                        decimal creditSum = await GetAccountSumAsync(tenantId, period, pattern, "Credit", sector);
                        decimal debitSum = await GetAccountSumAsync(tenantId, period, pattern, "Debit", sector);

                        aggregates[$"Account_{pattern}_Credit_{sector}"] = creditSum;
                        aggregates[$"Account_{pattern}_Debit_{sector}"] = debitSum;

                        _logger.LogDebug("Aggregated sector pattern {Pattern} {Sector}: Credit={Credit}, Debit={Debit}",
                            pattern, sector, creditSum, debitSum);
                    }
                }
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
                            List<string> dependencies = _formulaEngine.Value.GetDependencies(field.Formula);
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
                            List<string> dependencies = _formulaEngine.Value.GetDependencies(calculation.Formula);
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

        /// <summary>
        /// Wave 5: Extract account patterns referenced by SUM_ACCOUNT_BY_INDUSTRY formulas.
        /// These patterns need per-sector aggregation (key: Account_{pattern}_{side}_{sector}).
        /// </summary>
        private HashSet<string> ExtractSectorAccountPatterns(List<HKDBookTemplate> templates)
        {
            HashSet<string> patterns = [];

            foreach (HKDBookTemplate template in templates)
            {
                if (template.Fields != null)
                {
                    foreach (TemplateField field in template.Fields)
                    {
                        if (!string.IsNullOrEmpty(field.Formula))
                        {
                            AddSectorPatternsFromFormula(field.Formula, patterns);
                        }
                    }
                }

                if (template.Calculations != null)
                {
                    foreach (TemplateCalculation calc in template.Calculations)
                    {
                        if (!string.IsNullOrEmpty(calc.Formula))
                        {
                            AddSectorPatternsFromFormula(calc.Formula, patterns);
                        }
                    }
                }
            }

            return patterns;
        }

        private static void AddSectorPatternsFromFormula(string formula, HashSet<string> patterns)
        {
            // Match SUM_ACCOUNT_BY_INDUSTRY("pattern", "side", "sector")
            MatchCollection matches = Regex.Matches(
                formula,
                @"SUM_ACCOUNT_BY_INDUSTRY\(""([^""]*)"",\s*""([^""]*)"",\s*""([^""]*)""\)",
                RegexOptions.IgnoreCase);

            foreach (Match match in matches.Cast<Match>())
            {
                if (match.Success)
                {
                    patterns.Add(match.Groups[1].Value);
                }
            }
        }

        private async Task<decimal> GetAccountSumAsync(
            TenantId tenantId,
            AccountingPeriod period,
            string accountPattern,
            string side,
            IndustrySector? industrySector = null)
        {
            try
            {
                // Wave 2 (Option A): Query AccountingEntries directly instead of JournalEntries.Lines.
                // EntryType → side mapping: Revenue = Credit, Expense = Debit.
                // AccountCode null heuristic: Revenue entries match "5" pattern, Expense entries match "6" pattern.
                bool wantCredit = side.Equals("Credit", StringComparison.OrdinalIgnoreCase);

                // AccountingEntries is excluded from global query filter (VanAnDbContext L230),
                // so we must filter by TenantId manually. Direct e.TenantId == tenantId works
                // correctly — EF Core applies TenantIdConverter (TenantId → Guid) via convention.
                // Do NOT use EF.Property<Guid> — TenantId is stored as TEXT, not Guid.
                IQueryable<AccountingEntry> query = _context.AccountingEntries
                    .Where(e => e.TenantId == tenantId &&
                               e.PeriodYear == period.Year &&
                               e.PeriodMonth == period.Month);

                // Filter by AccountCode pattern (non-null) OR EntryType heuristic (null AccountCode)
                query = query.Where(e =>
                    (e.AccountCode != null && e.AccountCode.StartsWith(accountPattern)) ||
                    (e.AccountCode == null &&
                     ((wantCredit && e.EntryType == AccountingEntryType.Revenue && accountPattern == "5") ||
                      (!wantCredit && e.EntryType == AccountingEntryType.Expense && accountPattern == "6"))));

                // Wave 5: Industry sector filter — NULL IndustrySector counts in OtherBusiness bucket
                if (industrySector.HasValue)
                {
                    IndustrySector effectiveSector = industrySector.Value;
                    query = query.Where(e =>
                        (e.IndustrySector == effectiveSector) ||
                        (e.IndustrySector == null && effectiveSector == IndustrySector.OtherBusiness));
                }

                // Sum Amount where EntryType matches the requested side
                // Revenue → Credit, Expense → Debit, TaxPayment/Adjustment → use side filter via EntryType
                // SQLite cannot apply aggregate 'Sum' on decimal server-side — materialize Amounts and sum on client
                // to preserve decimal precision (see NotSupportedException from SqliteQueryableAggregateMethodTranslator).
                List<decimal> amounts = await query
                    .Where(e => wantCredit
                        ? e.EntryType == AccountingEntryType.Revenue
                        : e.EntryType == AccountingEntryType.Expense)
                    .Select(e => e.Amount)
                    .ToListAsync();
                decimal sum = amounts.Count == 0 ? 0m : amounts.Sum();

                _logger.LogDebug("Account sum for tenant {TenantId}, pattern {Pattern}, side {Side}, sector {Sector}: {Sum}",
                    tenantId.Value, accountPattern, side, industrySector?.ToString() ?? "ALL", sum);

                return sum;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting account sum for tenant {TenantId}, pattern {Pattern}, side {Side}, sector {Sector}",
                    tenantId.Value, accountPattern, side, industrySector?.ToString() ?? "ALL");
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
