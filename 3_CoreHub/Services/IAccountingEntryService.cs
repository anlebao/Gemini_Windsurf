using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service interface for accounting entry operations
/// </summary>
public interface IAccountingEntryService
{
    Task<AccountingEntryDto> CreateRevenueEntryAsync(Guid tenantId, RevenueEntryDto dto);
    Task<AccountingEntryDto> CreateExpenseEntryAsync(Guid tenantId, ExpenseEntryDto dto);
    Task<List<AccountingEntryDto>> GetEntriesAsync(Guid tenantId, string? searchTerm, DateTime? startDate, DateTime? endDate, decimal? amountMin = null, decimal? amountMax = null, string? accountType = null);
    Task<BalanceSummary> GetBalanceSummaryAsync(Guid tenantId);
    Task<BalanceSummary> GetBalanceSummaryAsync(Guid tenantId, DateTime period);
}
