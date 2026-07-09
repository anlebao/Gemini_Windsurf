using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VAS Wave 8 — D9 HKD→DN Conversion Service implementation.
/// Transactional conversion: creates new DN tenant, migrates opening balance,
/// marks HKD as Converted (read-only historical).
/// </summary>
public class TenantConversionService(
    IVanAnDbContext dbContext,
    IAccountingDbContext accountingContext,
    IHkdToEnterpriseAccountMapper accountMapper,
    ILogger<TenantConversionService> logger) : ITenantConversionService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IAccountingDbContext _accountingContext = accountingContext;
    private readonly IHkdToEnterpriseAccountMapper _accountMapper = accountMapper;
    private readonly ILogger<TenantConversionService> _logger = logger;

    /// <inheritdoc />
    public async Task<Tenant> ConvertHkdToEnterpriseAsync(
        Guid hkdTenantId,
        TenantType newType,
        AccountingStandard standard,
        string newName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        if (newType == TenantType.HKD)
            throw new ArgumentException("Conversion target type must be an Enterprise type, not HKD.", nameof(newType));

        var hkdId = new TenantId(hkdTenantId);

        // 1. Validate HKD tenant (IgnoreQueryFilters — cross-tenant query for conversion)
        Tenant? hkdTenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == hkdId, ct)
            .ConfigureAwait(false);

        if (hkdTenant is null)
            throw new InvalidOperationException($"HKD tenant {hkdTenantId} not found.");
        if (hkdTenant.Type != TenantType.HKD)
            throw new InvalidOperationException($"Tenant {hkdTenantId} is not an HKD tenant (Type={hkdTenant.Type}).");
        if (hkdTenant.IsConverted())
            throw new InvalidOperationException($"HKD tenant {hkdTenantId} is already converted (SuccessorTenantId={hkdTenant.SuccessorTenantId}).");
        if (hkdTenant.Status == TenantStatus.Inactive)
            throw new InvalidOperationException($"Cannot convert inactive tenant {hkdTenantId}.");

        _logger.LogInformation("W8: Starting conversion of HKD tenant {HkdId} → DN ({Type}, {Standard}, Name={Name})",
            hkdTenantId, newType, standard, newName);

        // 2. Create new DN tenant via W2 factory
        // Copy settings values (NOT the owned entity instance — EF Core tracks owned entities by reference,
        // sharing the same TenantSettings instance across two tenants causes key conflict).
        var copiedSettings = new TenantSettings(
            hkdTenant.Settings.ContactEmail,
            hkdTenant.Settings.ContactPhone,
            hkdTenant.Settings.Address,
            hkdTenant.Settings.LogoUrl,
            hkdTenant.Settings.TaxCode);

        var newTenantId = new TenantId(Guid.NewGuid());
        Tenant newTenant = Tenant.CreateFromConversion(
            newTenantId,
            newName,
            newType,
            hkdId,
            standard,
            copiedSettings);

        _dbContext.Tenants.Add(newTenant);

        // 3. Mark HKD as converted (sets Status=Converted, SuccessorTenantId, ConvertedAt)
        hkdTenant.MarkConvertedTo(newTenantId);

        // 4. Save tenants (transactional — opening balance migration is separate step)
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("W8: Conversion complete — HKD {HkdId} → DN {NewId} (Status=Converted, Predecessor link set)",
            hkdTenantId, newTenantId.Value);

        return newTenant;
    }

    /// <inheritdoc />
    public async Task<Tenant?> GetPredecessorAsync(Guid enterpriseTenantId, CancellationToken ct = default)
    {
        var dnId = new TenantId(enterpriseTenantId);
        Tenant? dnTenant = await _dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == dnId, ct)
            .ConfigureAwait(false);

        if (dnTenant?.PredecessorTenantId is null)
            return null;

        return await _dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == dnTenant.PredecessorTenantId, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<Tenant?> GetSuccessorAsync(Guid hkdTenantId, CancellationToken ct = default)
    {
        var hkdId = new TenantId(hkdTenantId);
        Tenant? hkdTenant = await _dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == hkdId, ct)
            .ConfigureAwait(false);

        if (hkdTenant?.SuccessorTenantId is null)
            return null;

        return await _dbContext.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == hkdTenant.SuccessorTenantId, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Best-effort migration: queries HKD AccountingEntries, groups by AccountCode/Category,
    /// maps to DN account codes via IHkdToEnterpriseAccountMapper (W3).
    /// Returns summary for verification — actual OpeningBalance persistence deferred (manual review per D9).
    /// </remarks>
    public async Task<(int MappingsCount, decimal TotalDebit, decimal TotalCredit)> MigrateOpeningBalanceAsync(
        Guid hkdTenantId,
        Guid newEnterpriseTenantId,
        AccountingStandard standard,
        CancellationToken ct = default)
    {
        var hkdId = new TenantId(hkdTenantId);

        // Query all HKD accounting entries (closing balance = cumulative, cross-tenant)
        List<AccountingEntry> hkdEntries = await _accountingContext.AccountingEntries
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == hkdId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (hkdEntries.Count == 0)
        {
            _logger.LogInformation("W8: No HKD entries to migrate for tenant {HkdId}", hkdTenantId);
            return (0, 0m, 0m);
        }

        // Group by AccountCode (HKD uses synthetic keys via AccountCode field)
        // Map to DN account codes via W3 mapper
        var mappings = _accountMapper.GetMappings(standard);
        var balanceByAccount = new Dictionary<string, decimal>();
        foreach (AccountingEntry entry in hkdEntries)
        {
            string hkdKey = entry.AccountCode ?? entry.EntryType.ToString();
            decimal signedAmount = entry.EntryType == AccountingEntryType.Revenue ? -entry.Amount : entry.Amount;
            balanceByAccount[hkdKey] = balanceByAccount.GetValueOrDefault(hkdKey) + signedAmount;
        }

        decimal totalDebit = 0m;
        decimal totalCredit = 0m;
        int mappedCount = 0;

        foreach (var (hkdKey, balance) in balanceByAccount)
        {
            if (!mappings.TryGetValue(hkdKey, out string? dnAccount))
            {
                _logger.LogWarning("W8: No mapping for HKD account key '{Key}' under {Standard} — skipped", hkdKey, standard);
                continue;
            }

            if (balance > 0)
                totalDebit += balance;
            else
                totalCredit += -balance;
            mappedCount++;
        }

        _logger.LogInformation("W8: Opening balance migration summary — HKD {HkdId} → DN {DnId}: {Mapped}/{Total} accounts mapped, Debit={Debit}, Credit={Credit}",
            hkdTenantId, newEnterpriseTenantId, mappedCount, balanceByAccount.Count, totalDebit, totalCredit);

        // D9 decision: actual OpeningBalance persistence requires manual review — return summary only.
        return (mappedCount, totalDebit, totalCredit);
    }
}
