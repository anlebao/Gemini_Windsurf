using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;
using VanAn.CoreHub.Repositories;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Service implementation for Accounting Entry operations - Week 1 implementation
    /// Implements immutable AccountingEntry pattern with append-only operations
    /// </summary>
    public class AccountingEntryService(
        IAccountingEntryRepository repository,
        ILogger<AccountingEntryService> logger) : IAccountingService
    {
        private readonly IAccountingEntryRepository _repository = repository;
        private readonly ILogger<AccountingEntryService> _logger = logger;

        public async Task<decimal> GetTodayRevenueAsync(Guid tenantId)
        {
            try
            {
                TenantId tenantIdObj = new(tenantId);
                DateTime today = DateTime.UtcNow.Date;
                IEnumerable<CoreAccountingEntry> entries = await _repository.GetByTenantAndDateRangeAsync(
                    tenantIdObj,
                    today,
                    today.AddDays(1).AddTicks(-1),
                    CancellationToken.None);

                return entries
                    .Where(e => e.EntryType == AccountingEntryType.Revenue)
                    .Sum(e => e.Amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting today's revenue for tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<decimal> GetRevenueByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
        {
            try
            {
                TenantId tenantIdObj = new(tenantId);
                IEnumerable<CoreAccountingEntry> entries = await _repository.GetByTenantAndDateRangeAsync(
                    tenantIdObj,
                    startDate,
                    endDate,
                    CancellationToken.None);

                return entries
                    .Where(e => e.EntryType == AccountingEntryType.Revenue)
                    .Sum(e => e.Amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting revenue for tenant {TenantId} from {StartDate} to {EndDate}",
                    tenantId, startDate, endDate);
                throw;
            }
        }

        public async Task<AccountingEntryDto> CreateEntryAsync(AccountingEntryDto entry)
        {
            try
            {
                // Create immutable AccountingEntry using Factory
                AccountingPeriod period = new(entry.TransactionDate.Year, entry.TransactionDate.Month);
                TenantId tenantId = new(entry.TenantId);

                CoreAccountingEntry coreEntry = entry.EntryType == AccountingEntryType.Revenue
                    ? CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(entry.Amount), entry.Description)
                    : CoreAccountingEntry.CreateExpense(tenantId, period, new Money(entry.Amount), entry.Description);

                await _repository.AddAsync(coreEntry);

                return entry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating accounting entry for tenant {TenantId}", entry.TenantId);
                throw;
            }
        }

        public async Task<AccountingEntryDto?> GetEntryByIdAsync(Guid entryId)
        {
            try
            {
                CoreAccountingEntry? entry = await _repository.GetByIdAsync(entryId);
                return entry == null
                    ? null
                    : new AccountingEntryDto
                    {
                        Id = entry.Id,
                        TenantId = entry.TenantId.Value,
                        Amount = entry.Amount,
                        Description = entry.Description,
                        EntryType = entry.EntryType,
                        CreatedAt = entry.CreatedAt,
                        AccountingBookType = entry.AccountingBookType,
                        PeriodYear = entry.PeriodYear,
                        PeriodMonth = entry.PeriodMonth,
                        ReversalEntryId = entry.ReversalEntryId,
                        TransactionDate = entry.TransactionDate
                    };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entry by ID {EntryId}", entryId);
                throw;
            }
        }

        public async Task<AccountingEntryDto> CreateRevenueEntryAsync(TenantId tenantId, AccountingPeriod period, decimal amount, string description)
        {
            try
            {
                CoreAccountingEntry entry = CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(amount), description);
                await _repository.AddAsync(entry);

                return new AccountingEntryDto
                {
                    Id = entry.Id,
                    TenantId = entry.TenantId.Value,
                    Amount = entry.Amount,
                    Description = entry.Description,
                    EntryType = entry.EntryType,
                    CreatedAt = entry.CreatedAt,
                    AccountingBookType = entry.AccountingBookType,
                    PeriodYear = entry.PeriodYear,
                    PeriodMonth = entry.PeriodMonth,
                    ReversalEntryId = entry.ReversalEntryId,
                    TransactionDate = entry.TransactionDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating revenue entry for tenant {TenantId}", tenantId.Value);
                throw;
            }
        }

        public async Task<AccountingEntryDto> CreateExpenseEntryAsync(TenantId tenantId, AccountingPeriod period, decimal amount, string description)
        {
            try
            {
                CoreAccountingEntry entry = CoreAccountingEntry.CreateExpense(tenantId, period, new Money(amount), description);
                await _repository.AddAsync(entry);

                return new AccountingEntryDto
                {
                    Id = entry.Id,
                    TenantId = entry.TenantId.Value,
                    Amount = entry.Amount,
                    Description = entry.Description,
                    EntryType = entry.EntryType,
                    CreatedAt = entry.CreatedAt,
                    AccountingBookType = entry.AccountingBookType,
                    PeriodYear = entry.PeriodYear,
                    PeriodMonth = entry.PeriodMonth,
                    ReversalEntryId = entry.ReversalEntryId,
                    TransactionDate = entry.TransactionDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating expense entry for tenant {TenantId}", tenantId.Value);
                throw;
            }
        }

        public async Task<IEnumerable<AccountingEntryDto>> GetEntriesByTenantAsync(TenantId tenantId)
        {
            try
            {
                IEnumerable<CoreAccountingEntry> entries = await _repository.GetByTenantAsync(tenantId, CancellationToken.None);
                return entries.Select(e => new AccountingEntryDto
                {
                    Id = e.Id,
                    TenantId = e.TenantId.Value,
                    Amount = e.Amount,
                    Description = e.Description,
                    EntryType = e.EntryType,
                    CreatedAt = e.CreatedAt,
                    AccountingBookType = e.AccountingBookType,
                    PeriodYear = e.PeriodYear,
                    PeriodMonth = e.PeriodMonth,
                    ReversalEntryId = e.ReversalEntryId,
                    TransactionDate = e.TransactionDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entries for tenant {TenantId}", tenantId.Value);
                throw;
            }
        }

        public async Task<IEnumerable<AccountingEntryDto>> GetEntriesByTenantAndBookTypeAsync(TenantId tenantId, AccountingBookType bookType)
        {
            try
            {
                IEnumerable<CoreAccountingEntry> entries = await _repository.GetByTenantAndBookTypeAsync(tenantId, bookType, CancellationToken.None);
                return entries.Select(e => new AccountingEntryDto
                {
                    Id = e.Id,
                    TenantId = e.TenantId.Value,
                    Amount = e.Amount,
                    Description = e.Description,
                    EntryType = e.EntryType,
                    CreatedAt = e.CreatedAt,
                    AccountingBookType = e.AccountingBookType,
                    PeriodYear = e.PeriodYear,
                    PeriodMonth = e.PeriodMonth,
                    ReversalEntryId = e.ReversalEntryId,
                    TransactionDate = e.TransactionDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entries for tenant {TenantId} and book type {BookType}", tenantId.Value, bookType);
                throw;
            }
        }

        public async Task<IEnumerable<AccountingEntryDto>> GetEntriesByTenantAndPeriodAsync(TenantId tenantId, AccountingPeriod period)
        {
            try
            {
                IEnumerable<CoreAccountingEntry> entries = await _repository.GetByTenantAndPeriodAsync(tenantId, period, CancellationToken.None);
                return entries.Select(e => new AccountingEntryDto
                {
                    Id = e.Id,
                    TenantId = e.TenantId.Value,
                    Amount = e.Amount,
                    Description = e.Description,
                    EntryType = e.EntryType,
                    CreatedAt = e.CreatedAt,
                    AccountingBookType = e.AccountingBookType,
                    PeriodYear = e.PeriodYear,
                    PeriodMonth = e.PeriodMonth,
                    ReversalEntryId = e.ReversalEntryId,
                    TransactionDate = e.TransactionDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entries for tenant {TenantId} and period {Period}", tenantId.Value, period);
                throw;
            }
        }

        public async Task<AccountingEntryDto> CreateReversalEntryAsync(Guid originalEntryId, string reason, Guid tenantId)
        {
            try
            {
                AccountingEntryId originalId = new(originalEntryId);
                TenantId tenantIdObj = new(tenantId);

                // Get original entry
                CoreAccountingEntry? originalEntry = await _repository.GetByIdAsync(originalId, CancellationToken.None) ?? throw new InvalidOperationException($"Original entry {originalEntryId} not found");

                // Multi-tenancy check
                if (originalEntry.TenantId.Value != tenantId)
                {
                    throw new UnauthorizedAccessException($"Entry {originalEntryId} does not belong to tenant {tenantId}");
                }

                // Create reversal entry using Factory
                CoreAccountingEntry reversalEntry = CoreAccountingEntry.CreateReversal(originalEntry, reason);
                await _repository.AddAsync(reversalEntry, CancellationToken.None);

                _logger.LogInformation("Created reversal entry {ReversalId} for original entry {OriginalId}",
                    reversalEntry.Id, originalEntryId);

                // Convert to DTO for return
                return new AccountingEntryDto
                {
                    Id = reversalEntry.Id,
                    TenantId = reversalEntry.TenantId.Value,
                    Amount = reversalEntry.Amount,
                    Description = reversalEntry.Description,
                    EntryType = reversalEntry.EntryType,
                    CreatedAt = reversalEntry.CreatedAt,
                    AccountingBookType = reversalEntry.AccountingBookType,
                    PeriodYear = reversalEntry.PeriodYear,
                    PeriodMonth = reversalEntry.PeriodMonth,
                    ReversalEntryId = reversalEntry.ReversalEntryId,
                    TransactionDate = reversalEntry.TransactionDate
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reversal entry for original entry {OriginalEntryId}", originalEntryId);
                throw;
            }
        }

        public async Task<IEnumerable<AccountingEntryDto>> GetEntriesByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
        {
            try
            {
                TenantId tenantIdObj = new(tenantId);
                IEnumerable<CoreAccountingEntry> entries = await _repository.GetByTenantAndDateRangeAsync(
                    tenantIdObj,
                    startDate,
                    endDate,
                    CancellationToken.None);

                return entries.Select(e => new AccountingEntryDto
                {
                    Id = e.Id,
                    TenantId = e.TenantId.Value,
                    Amount = e.Amount,
                    Description = e.Description,
                    EntryType = e.EntryType,
                    CreatedAt = e.CreatedAt,
                    AccountingBookType = e.AccountingBookType,
                    PeriodYear = e.PeriodYear,
                    PeriodMonth = e.PeriodMonth,
                    ReversalEntryId = e.ReversalEntryId,
                    TransactionDate = e.TransactionDate
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entries for tenant {TenantId} from {StartDate} to {EndDate}",
                    tenantId, startDate, endDate);
                throw;
            }
        }

        public decimal CalculateVat(decimal revenue, VatRate vatRate)
        {
            try
            {
                // VAT calculation for Vietnamese Household Businesses (Direct Method)
                decimal vatAmount = revenue * ((decimal)vatRate / 100m);

                _logger.LogDebug("Calculated VAT amount {VatAmount} for revenue {Revenue} with rate {VatRate}%",
                    vatAmount, revenue, (decimal)vatRate);

                return vatAmount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating VAT for revenue {Revenue} with rate {VatRate}",
                    revenue, (decimal)vatRate);
                throw;
            }
        }
    }
}
