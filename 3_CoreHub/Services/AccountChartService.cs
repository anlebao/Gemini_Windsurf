using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;
// W3: Alias to disambiguate from the legacy VanAn.CoreHub.Services.AccountType (7 values, includes OtherIncome/OtherExpense).
// The Domain AccountType (5 values, no Contra) is the W2-approved enum used by AccountChartEntry.
using DomainAccountType = VanAn.Shared.Domain.AccountType;

namespace VanAn.CoreHub.Services;

/// <summary>
/// W3: Implementation of <see cref="IAccountChartService"/>.
/// Queries the <c>AccountCharts</c> reference-data table and returns immutable
/// <see cref="AccountChartEntry"/> Domain records.
///
/// Reference data is global (NOT tenant-scoped) — no multi-tenancy filter applied
/// because <see cref="AccountChartEntity"/> does not implement IMustHaveTenant.
/// </summary>
public class AccountChartService : IAccountChartService
{
    private readonly IAccountingDbContext _dbContext;
    private readonly ILogger<AccountChartService> _logger;
    // SRS v1.2 W2/B (FR-COA-006): audit log khi thêm/sửa/deactivate tenant account — optional dep.
    private readonly IAuditTrailService? _auditTrailService;

    public AccountChartService(IAccountingDbContext dbContext, ILogger<AccountChartService> logger, IAuditTrailService? auditTrailService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _auditTrailService = auditTrailService;
    }

    /// <inheritdoc />
    public async Task<string> GetAccountNameAsync(string accountCode, AccountingStandard standard, CancellationToken ct = default)
    {
        AccountChartEntry? entry = await GetAccountAsync(accountCode, standard, ct).ConfigureAwait(false);
        return entry?.AccountName ?? $"Tài khoản {accountCode}";
    }

    /// <inheritdoc />
    public async Task<List<AccountChartEntry>> GetAccountsByTypeAsync(DomainAccountType type, AccountingStandard standard, CancellationToken ct = default)
    {
        List<AccountChartEntity> entities = await _dbContext.AccountCharts
            .AsNoTracking()
            .Where(e => e.Standard == standard && e.Type == type)
            .OrderBy(e => e.AccountCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(e => e.ToDomainRecord()).ToList();
    }

    /// <inheritdoc />
    public async Task<DomainAccountType> GetAccountTypeAsync(string accountCode, AccountingStandard standard, CancellationToken ct = default)
    {
        AccountChartEntry? entry = await GetAccountAsync(accountCode, standard, ct).ConfigureAwait(false);
        return entry?.Type ?? DomainAccountType.Expense; // safe default — W4 services should null-check via GetAccountAsync
    }

    /// <inheritdoc />
    public async Task<AccountChartEntry?> GetAccountAsync(string accountCode, AccountingStandard standard, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            return null;

        AccountChartEntity? entity = await _dbContext.AccountCharts
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Standard == standard && e.AccountCode == accountCode, ct)
            .ConfigureAwait(false);

        return entity?.ToDomainRecord();
    }

    /// <inheritdoc />
    public async Task<List<AccountChartEntry>> GetAllAccountsAsync(AccountingStandard standard, CancellationToken ct = default)
    {
        List<AccountChartEntity> entities = await _dbContext.AccountCharts
            .AsNoTracking()
            .Where(e => e.Standard == standard)
            .OrderBy(e => e.AccountCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(e => e.ToDomainRecord()).ToList();
    }

    // ===================== SRS v1.2 W2/B — TENANT CHART (G-H2, M4) =====================

    /// <inheritdoc />
    public async Task<AccountChartEntry?> GetTenantAccountAsync(TenantId tenantId, string accountCode, AccountingStandard standard, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            return null;

        AccountChartEntity? globalEntity = await _dbContext.AccountCharts
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Standard == standard && e.AccountCode == accountCode, ct)
            .ConfigureAwait(false);

        TenantAccountChartEntity? tenantEntity = await _dbContext.TenantAccountCharts
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId.Value && e.AccountCode == accountCode, ct)
            .ConfigureAwait(false);

        return MergeTenantAccount(globalEntity, tenantEntity, standard);
    }

    /// <inheritdoc />
    public async Task<List<AccountChartEntry>> GetTenantAccountsAsync(TenantId tenantId, AccountingStandard standard, CancellationToken ct = default)
    {
        List<AccountChartEntity> globals = await _dbContext.AccountCharts
            .AsNoTracking()
            .Where(e => e.Standard == standard)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        List<TenantAccountChartEntity> tenantRows = await _dbContext.TenantAccountCharts
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId.Value)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var byCode = globals.ToDictionary(e => e.AccountCode, StringComparer.Ordinal);
        var result = new List<AccountChartEntry>(globals.Count + tenantRows.Count);

        foreach (AccountChartEntity globalEntity in globals)
        {
            TenantAccountChartEntity? tenantRow = tenantRows.FirstOrDefault(t => t.AccountCode == globalEntity.AccountCode);
            AccountChartEntry? merged = MergeTenantAccount(globalEntity, tenantRow, standard);
            if (merged != null)
            {
                result.Add(merged);
            }
        }

        // Custom accounts (code không có trong global chart)
        foreach (TenantAccountChartEntity tenantRow in tenantRows.Where(t => !byCode.ContainsKey(t.AccountCode)))
        {
            AccountChartEntry? merged = MergeTenantAccount(null, tenantRow, standard);
            if (merged != null)
            {
                result.Add(merged);
            }
        }

        return result.OrderBy(e => e.AccountCode).ToList();
    }

    /// <summary>
    /// Merge tenant row lên global row. Tenant row inactive → global fallback (Override)
    /// hoặc null (Custom tắt). EffectiveFrom tương lai → coi như chưa có row.
    /// </summary>
    private static AccountChartEntry? MergeTenantAccount(AccountChartEntity? globalEntity, TenantAccountChartEntity? tenantEntity, AccountingStandard standard)
    {
        if (tenantEntity == null)
        {
            return globalEntity?.ToDomainRecord();
        }

        if (!tenantEntity.IsActive || (tenantEntity.EffectiveFrom.HasValue && tenantEntity.EffectiveFrom.Value > DateTime.UtcNow))
        {
            // Override chưa hiệu lực/tắt → global vẫn dùng; Custom chưa hiệu lực/tắt → không có TK.
            return tenantEntity.Source == TenantAccountChartSources.Override
                ? globalEntity?.ToDomainRecord()
                : null;
        }

        DomainAccountType type = tenantEntity.Type ?? globalEntity?.Type ?? DomainAccountType.Expense;
        bool isNormalCredit = tenantEntity.IsNormalCredit ?? globalEntity?.IsNormalCredit ?? (type is DomainAccountType.Liability or DomainAccountType.Equity or DomainAccountType.Revenue);
        return new AccountChartEntry(tenantEntity.AccountCode, tenantEntity.AccountName, type, standard, isNormalCredit);
    }

    /// <inheritdoc />
    public async Task<TenantAccountChartEntity> UpsertTenantAccountAsync(
        TenantId tenantId, string accountCode, string accountName, string source,
        AccountingStandard standard, DomainAccountType? type = null,
        bool? isNormalCredit = null, string? parentCode = null, DateTime? effectiveFrom = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            throw new ArgumentException("AccountCode is required.", nameof(accountCode));
        if (string.IsNullOrWhiteSpace(accountName))
            throw new ArgumentException("AccountName is required.", nameof(accountName));

        bool isCustom = source == TenantAccountChartSources.Custom;
        if (source != TenantAccountChartSources.Custom && source != TenantAccountChartSources.Override)
            throw new ArgumentException($"Source phải là '{TenantAccountChartSources.Custom}' hoặc '{TenantAccountChartSources.Override}'.", nameof(source));

        if (isCustom)
        {
            if (string.IsNullOrWhiteSpace(parentCode))
                throw new ArgumentException("ParentCode là bắt buộc với TK chi tiết (Custom).", nameof(parentCode));
            if (type == null)
                throw new ArgumentException("Type là bắt buộc với TK chi tiết (Custom) — không kế thừa từ global.", nameof(type));

            // Parent phải tồn tại trong global chart (cùng standard) HOẶC tenant chart active.
            bool parentExists = await _dbContext.AccountCharts
                    .AnyAsync(e => e.Standard == standard && e.AccountCode == parentCode, ct)
                || await _dbContext.TenantAccountCharts
                    .AnyAsync(e => e.TenantId == tenantId.Value && e.AccountCode == parentCode && e.IsActive, ct);
            if (!parentExists)
                throw new InvalidOperationException($"ParentCode '{parentCode}' không tồn tại trong chart (global {standard} hoặc tenant).");
        }
        else
        {
            // Override: TK gốc phải có trong global chart cùng standard.
            bool globalExists = await _dbContext.AccountCharts
                .AnyAsync(e => e.Standard == standard && e.AccountCode == accountCode, ct);
            if (!globalExists)
                throw new InvalidOperationException($"AccountCode '{accountCode}' không có trong global chart {standard} — dùng Source=Custom cho TK mới.");
        }

        TenantAccountChartEntity? existing = await _dbContext.TenantAccountCharts
            .FirstOrDefaultAsync(e => e.TenantId == tenantId.Value && e.AccountCode == accountCode, ct)
            .ConfigureAwait(false);

        TenantAccountChartEntity row;
        if (existing == null)
        {
            row = new TenantAccountChartEntity(tenantId.Value, accountCode, accountName, source, type, isNormalCredit, parentCode, effectiveFrom);
            _ = await _dbContext.TenantAccountCharts.AddAsync(row, ct);
        }
        else
        {
            existing.Update(accountName, type, isNormalCredit, parentCode, effectiveFrom);
            row = existing;
        }
        _ = await _dbContext.SaveChangesAsync(ct);

        if (_auditTrailService != null)
        {
            var newValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                accountCode, accountName, source, standard = standard.ToString(),
                type = type?.ToString(), isNormalCredit, parentCode, effectiveFrom,
                action = existing == null ? "create" : "update"
            });
            await _auditTrailService.LogCreateAsync(
                VanAn.Shared.Domain.Audit.AuditableEntityType.TenantAccountChart,
                row.Id, newValues, correlationId: row.Id.ToString());
        }

        return row;
    }

    /// <inheritdoc />
    public async Task DeactivateTenantAccountAsync(TenantId tenantId, string accountCode, CancellationToken ct = default)
    {
        // FR-COA-005: deactivate TK đã phát sinh vẫn cho phép (dừng dùng mới — không xóa).
        TenantAccountChartEntity? row = await _dbContext.TenantAccountCharts
            .FirstOrDefaultAsync(e => e.TenantId == tenantId.Value && e.AccountCode == accountCode, ct)
            .ConfigureAwait(false);
        if (row == null)
            throw new InvalidOperationException($"Tenant account '{accountCode}' không tồn tại.");

        row.Deactivate();
        _ = await _dbContext.SaveChangesAsync(ct);

        if (_auditTrailService != null)
        {
            var newValues = System.Text.Json.JsonSerializer.Serialize(new { accountCode, action = "deactivate" });
            await _auditTrailService.LogUpdateAsync(
                VanAn.Shared.Domain.Audit.AuditableEntityType.TenantAccountChart,
                row.Id, "{}", newValues, correlationId: row.Id.ToString());
        }
    }

    /// <inheritdoc />
    public Task DeleteTenantAccountAsync(TenantId tenantId, string accountCode, CancellationToken ct = default)
    {
        // FR-COA-005: KHÔNG BAO GIỜ xóa TK — chỉ deactivate (giữ audit trail + dữ liệu phát sinh).
        throw new InvalidOperationException(
            $"Không thể xóa tài khoản '{accountCode}' — chỉ được deactivate (FR-COA-005).");
    }
}
