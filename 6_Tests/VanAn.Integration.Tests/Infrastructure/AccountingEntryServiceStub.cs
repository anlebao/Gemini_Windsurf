using VanAn.CoreHub.Services;
using VanAn.Shared.DTOs;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Stub implementation of IAccountingEntryService for integration tests
/// </summary>
public class AccountingEntryServiceStub : IAccountingEntryService
{
    private readonly List<AccountingEntryDto> _entries = new();

    public async Task<AccountingEntryDto> CreateRevenueEntryAsync(Guid tenantId, RevenueEntryDto dto)
    {
        var entry = new AccountingEntryDto
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = dto.Amount,
            AccountCode = dto.AccountCode,
            Description = dto.Description,
            Reference = dto.Reference,
            TransactionDate = dto.Date,
            EntryType = AccountingEntryType.Revenue,
            CreatedAt = DateTime.UtcNow
        };
        _entries.Add(entry);
        return await Task.FromResult(entry);
    }

    public async Task<AccountingEntryDto> CreateExpenseEntryAsync(Guid tenantId, ExpenseEntryDto dto)
    {
        var entry = new AccountingEntryDto
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Amount = dto.Amount,
            AccountCode = dto.AccountCode,
            Description = dto.Description,
            Vendor = dto.Vendor,
            Category = dto.Category,
            Reference = dto.Reference,
            TransactionDate = dto.Date,
            EntryType = AccountingEntryType.Expense,
            CreatedAt = DateTime.UtcNow
        };
        _entries.Add(entry);
        return await Task.FromResult(entry);
    }

    public async Task<List<AccountingEntryDto>> GetEntriesAsync(
        Guid tenantId,
        string? searchTerm = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        decimal? amountMin = null,
        decimal? amountMax = null,
        string? accountType = null)
    {
        var query = _entries.Where(e => e.TenantId == tenantId);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(e => e.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (startDate.HasValue)
        {
            query = query.Where(e => e.TransactionDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.TransactionDate <= endDate.Value);
        }

        if (amountMin.HasValue)
        {
            query = query.Where(e => e.Amount >= amountMin.Value);
        }

        if (amountMax.HasValue)
        {
            query = query.Where(e => e.Amount <= amountMax.Value);
        }

        if (!string.IsNullOrEmpty(accountType))
        {
            query = query.Where(e => e.AccountCode.StartsWith(accountType));
        }

        return await Task.FromResult(query.ToList());
    }

    public async Task<BalanceSummary> GetBalanceSummaryAsync(Guid tenantId)
    {
        var entries = _entries.Where(e => e.TenantId == tenantId);
        var summary = new BalanceSummary
        {
            TotalRevenue = entries.Where(e => e.EntryType == AccountingEntryType.Revenue).Sum(e => e.Amount),
            TotalExpenses = entries.Where(e => e.EntryType == AccountingEntryType.Expense).Sum(e => e.Amount),
            NetProfit = 0
        };
        summary.NetProfit = summary.TotalRevenue - summary.TotalExpenses;
        return await Task.FromResult(summary);
    }

    public async Task<BalanceSummary> GetBalanceSummaryAsync(Guid tenantId, DateTime period)
    {
        var entries = _entries.Where(e => e.TenantId == tenantId && e.TransactionDate.Year == period.Year && e.TransactionDate.Month == period.Month);
        var summary = new BalanceSummary
        {
            TotalRevenue = entries.Where(e => e.EntryType == AccountingEntryType.Revenue).Sum(e => e.Amount),
            TotalExpenses = entries.Where(e => e.EntryType == AccountingEntryType.Expense).Sum(e => e.Amount),
            NetProfit = 0
        };
        summary.NetProfit = summary.TotalRevenue - summary.TotalExpenses;
        return await Task.FromResult(summary);
    }
}
