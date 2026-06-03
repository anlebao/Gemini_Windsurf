using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Repositories;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Service implementation for 4 HKD Books - Phase 2.3 implementation
/// Implements Vietnamese Accounting Standard (Thông tư 200/2014/TT-BTC)
/// 5-layer protection: Domain, EF Core, Repository, Service, API
/// </summary>
public class HKDBookService : IHKDBookService
{
    private readonly IAccountingEntryRepository _repository;
    private readonly IHKDBookRepository _hkdBookRepository;
    private readonly ILogger<HKDBookService> _logger;
    
    // Vietnamese Account Mapping for HKD Books
    private readonly Dictionary<string, string> _vietnameseAccounts = new()
    {
        { "111", "Tiền mặt" },
        { "112", "Tiền gửi ngân hàng" },
        { "131", "Phải thu khách hàng" },
        { "156", "Hàng hóa" },
        { "211", "Ngắn hạn vay ngân hàng" },
        { "331", "Phải trả người bán" },
        { "334", "Phải trả người lao động" },
        { "511", "Doanh thu bán hàng" },
        { "632", "Giá vốn hàng bán" },
        { "641", "Chi phí quản lý doanh nghiệp" },
        { "642", "Chi phí bán hàng" },
        { "811", "Lợi nhuận gộp về bán hàng" },
        { "821", "Chi phí tài chính" },
        { "822", "Thu nhập khác" },
        { "831", "Lợi nhuận trước thuế" },
        { "841", "Lợi nhuận sau thuế" }
    };
    
    public HKDBookService(
        IAccountingEntryRepository repository,
        IHKDBookRepository hkdBookRepository,
        ILogger<HKDBookService> logger)
    {
        _repository = repository;
        _hkdBookRepository = hkdBookRepository;
        _logger = logger;
    }
    
    public async Task<AccountingEntry> RecordRevenueAsync(
        TenantId tenantId,
        decimal amount,
        string description,
        DateTime? transactionDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var date = transactionDate ?? DateTime.UtcNow;
            
            // Create immutable revenue entry using Factory
            var period = new AccountingPeriod(date.Year, date.Month);
            var entry = CoreAccountingEntry.CreateRevenue(tenantId, period, new Money(amount), description);
            
            await _repository.AddAsync(entry, cancellationToken);
            
            _logger.LogInformation("Recorded revenue entry {EntryId} for tenant {TenantId}, amount {Amount}", 
                entry.Id, tenantId, amount);
            
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording revenue for tenant {TenantId}, amount {Amount}", 
                tenantId, amount);
            throw;
        }
    }
    
    public async Task<AccountingEntry> RecordExpenseAsync(
        TenantId tenantId,
        decimal amount,
        string description,
        DateTime? transactionDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var date = transactionDate ?? DateTime.UtcNow;
            
            // Create immutable expense entry using Factory
            var period = new AccountingPeriod(date.Year, date.Month);
            var entry = CoreAccountingEntry.CreateExpense(tenantId, period, new Money(amount), description);
            
            await _repository.AddAsync(entry, cancellationToken);
            
            _logger.LogInformation("Recorded expense entry {EntryId} for tenant {TenantId}, amount {Amount}", 
                entry.Id, tenantId, amount);
            
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording expense for tenant {TenantId}, amount {Amount}", 
                tenantId, amount);
            throw;
        }
    }
    
    public async Task<decimal> GetRevenueTotalAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _repository.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, cancellationToken);
            
            return entries
                .Where(e => e.PeriodYear == period.Year && e.PeriodMonth == period.Month)
                .Sum(e => e.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue total for tenant {TenantId}, period {Period}", 
                tenantId, period.ToString());
            throw;
        }
    }
    
    public async Task<decimal> GetExpenseTotalAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _repository.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, cancellationToken);
            
            return entries
                .Where(e => e.PeriodYear == period.Year && e.PeriodMonth == period.Month)
                .Sum(e => e.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense total for tenant {TenantId}, period {Period}", 
                tenantId, period.ToString());
            throw;
        }
    }
    
    public async Task<decimal> GetProfitAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var revenue = await GetRevenueTotalAsync(tenantId, period, cancellationToken);
            var expense = await GetExpenseTotalAsync(tenantId, period, cancellationToken);
            
            return revenue - expense;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating profit for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            throw;
        }
    }
    
    public async Task<IEnumerable<CoreAccountingEntry>> GetRevenueEntriesAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _repository.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.RevenueBook, cancellationToken);
            
            return entries.Where(e => e.PeriodYear == period.Year && e.PeriodMonth == period.Month);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue entries for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            throw;
        }
    }
    
    public async Task<IEnumerable<CoreAccountingEntry>> GetExpenseEntriesAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = await _repository.GetByTenantAndBookTypeAsync(tenantId, AccountingBookType.ExpenseBook, cancellationToken);
            
            return entries.Where(e => e.PeriodYear == period.Year && e.PeriodMonth == period.Month);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expense entries for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            throw;
        }
    }
    
    // Phase 2.3: 4 HKD Books Implementation
    
    public async Task<IEnumerable<JournalEntry>> GenerateGeneralJournalAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating General Journal for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            
            // Get all journal entries for the period
            var journalEntries = await _hkdBookRepository.GetS1aBookAsync(tenantId, period, cancellationToken);
            
            return journalEntries.OrderBy(e => e.EntryDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating General Journal for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            throw;
        }
    }
    
    public async Task<IEnumerable<GeneralLedgerEntry>> GenerateGeneralLedgerAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating General Ledger for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            
            var journalEntries = await _hkdBookRepository.GetS2bBookAsync(tenantId, period, cancellationToken);
            var ledgerEntries = new List<GeneralLedgerEntry>();
            
            // Group by account and calculate running balances
            var accountGroups = journalEntries
                .SelectMany(e => e.Lines.Select(l => new { Entry = e, Line = l }))
                .GroupBy(x => x.Line.AccountNumber)
                .ToList();
            
            foreach (var accountGroup in accountGroups)
            {
                var accountNumber = accountGroup.Key;
                var accountName = GetAccountName(accountNumber);
                decimal runningBalance = 0;
                
                foreach (var item in accountGroup.OrderBy(x => x.Entry.EntryDate))
                {
                    var debit = item.Line.DebitAmount;
                    var credit = item.Line.CreditAmount;
                    
                    // Update running balance (simplified for MVP)
                    if (accountNumber.StartsWith("1") || accountNumber.StartsWith("5")) // Asset/Revenue accounts
                        runningBalance += debit - credit;
                    else // Liability/Expense accounts
                        runningBalance += credit - debit;
                    
                    ledgerEntries.Add(new GeneralLedgerEntry(
                        accountNumber,
                        accountName,
                        item.Entry.EntryDate,
                        item.Line.Description,
                        debit,
                        credit,
                        runningBalance,
                        item.Entry.ReferenceType ?? string.Empty,
                        item.Entry.ReferenceId ?? Guid.Empty
                    ));
                }
            }
            
            return ledgerEntries.OrderBy(e => e.AccountNumber).ThenBy(e => e.TransactionDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating General Ledger for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            throw;
        }
    }
    
    public async Task<IEnumerable<DetailedLedgerEntry>> GenerateDetailedLedgerAsync(
        TenantId tenantId,
        string accountNumber,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating Detailed Ledger for account {AccountNumber}, tenant {TenantId}, period {Period}", 
                accountNumber, tenantId.Value, period.ToString());
            
            var detailedEntries = await _hkdBookRepository.GetDetailedLedgerAsync(
                tenantId, accountNumber, period, cancellationToken);
            
            var ledgerEntries = new List<DetailedLedgerEntry>();
            decimal balance = 0;
            
            foreach (var entry in detailedEntries.OrderBy(e => e.EntryDate))
            {
                var totalDebit = entry.Lines.Where(l => l.AccountNumber == accountNumber).Sum(l => l.DebitAmount);
                var totalCredit = entry.Lines.Where(l => l.AccountNumber == accountNumber).Sum(l => l.CreditAmount);
                
                // Update balance (simplified for MVP)
                if (accountNumber.StartsWith("1") || accountNumber.StartsWith("5")) // Asset/Revenue accounts
                    balance += totalDebit - totalCredit;
                else // Liability/Expense accounts
                    balance += totalCredit - totalDebit;
                
                ledgerEntries.Add(new DetailedLedgerEntry(
                    entry.EntryDate,
                    entry.Description,
                    totalDebit,
                    totalCredit,
                    balance,
                    entry.ReferenceType ?? string.Empty,
                    entry.ReferenceId ?? Guid.Empty
                ));
            }
            
            return ledgerEntries.OrderBy(e => e.TransactionDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Detailed Ledger for account {AccountNumber}, tenant {TenantId}, period {Period}", 
                accountNumber, tenantId.Value, period.ToString());
            throw;
        }
    }
    
    public async Task<TrialBalance> GenerateTrialBalanceAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating Trial Balance for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            
            var journalEntries = await _hkdBookRepository.GetS3aBookAsync(tenantId, period, cancellationToken);
            var trialBalanceAccounts = new List<TrialBalanceAccount>();
            decimal totalDebit = 0;
            decimal totalCredit = 0;
            
            // Group by account and calculate totals
            var accountGroups = journalEntries
                .SelectMany(e => e.Lines.Select(l => new { Entry = e, Line = l }))
                .GroupBy(x => x.Line.AccountNumber)
                .ToList();
            
            foreach (var accountGroup in accountGroups)
            {
                var accountNumber = accountGroup.Key;
                var accountName = GetAccountName(accountNumber);
                var debitTotal = accountGroup.Sum(x => x.Line.DebitAmount);
                var creditTotal = accountGroup.Sum(x => x.Line.CreditAmount);
                var balance = debitTotal - creditTotal;
                
                trialBalanceAccounts.Add(new TrialBalanceAccount(
                    accountNumber,
                    accountName,
                    debitTotal,
                    creditTotal,
                    balance
                ));
                
                totalDebit += debitTotal;
                totalCredit += creditTotal;
            }
            
            var isBalanced = Math.Abs(totalDebit - totalCredit) < 0.01m; // Allow for rounding
            
            return new TrialBalance(
                period,
                DateTime.UtcNow,
                trialBalanceAccounts.OrderBy(a => a.AccountNumber),
                totalDebit,
                totalCredit,
                isBalanced
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Trial Balance for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            throw;
        }
    }
    
    public async Task<HKDBooksPackage> GenerateAllHKDBooksAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating all 4 HKD Books for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            
            // Generate all books in parallel
            var generalJournalTask = GenerateGeneralJournalAsync(tenantId, period, cancellationToken);
            var generalLedgerTask = GenerateGeneralLedgerAsync(tenantId, period, cancellationToken);
            var trialBalanceTask = GenerateTrialBalanceAsync(tenantId, period, cancellationToken);
            
            await Task.WhenAll(generalJournalTask, generalLedgerTask, trialBalanceTask);
            
            var generalJournal = await generalJournalTask;
            var generalLedger = await generalLedgerTask;
            var trialBalance = await trialBalanceTask;
            
            // Generate detailed ledgers for all accounts
            var detailedLedgers = new Dictionary<string, IEnumerable<DetailedLedgerEntry>>();
            var uniqueAccounts = generalJournal
                .SelectMany(e => e.Lines)
                .Select(l => l.AccountNumber)
                .Distinct()
                .ToList();
            
            foreach (var accountNumber in uniqueAccounts)
            {
                var detailedLedger = await GenerateDetailedLedgerAsync(tenantId, accountNumber, period, cancellationToken);
                detailedLedgers[accountNumber] = detailedLedger;
            }
            
            return new HKDBooksPackage(
                tenantId,
                period,
                generalJournal,
                generalLedger,
                detailedLedgers,
                trialBalance,
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating all HKD Books for tenant {TenantId}, period {Period}", 
                tenantId.Value, period.ToString());
            throw;
        }
    }
    
    // NEW: Dynamic HKD Book Generation Methods (Phase 2.3.4.A)
    
    public async Task<GenericHKDBook> GenerateS1aBookAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating S1a-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            
            var template = new S1aHKDTemplate();
            var accountingEntries = await _repository.GetByPeriodAsync(tenantId, period, cancellationToken);
            var journalEntries = ConvertToJournalEntries(accountingEntries);
            
            var book = await template.CreateBookAsync(tenantId, period, journalEntries);
            
            _logger.LogInformation("Successfully generated S1a-HKD Book for tenant {TenantId}", tenantId.Value);
            
            return book;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating S1a-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            throw;
        }
    }
    
    public async Task<GenericHKDBook> GenerateS2aBookAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating S2a-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            
            var template = new S2aHKDTemplate();
            var accountingEntries = await _repository.GetByPeriodAsync(tenantId, period, cancellationToken);
            var journalEntries = ConvertToJournalEntries(accountingEntries);
            
            var book = await template.CreateBookAsync(tenantId, period, journalEntries);
            
            _logger.LogInformation("Successfully generated S2a-HKD Book for tenant {TenantId}", tenantId.Value);
            
            return book;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating S2a-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            throw;
        }
    }
    
    public async Task<GenericHKDBook> GenerateS2bBookAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating S2b-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            
            var template = new S2bHKDTemplate();
            var accountingEntries = await _repository.GetByPeriodAsync(tenantId, period, cancellationToken);
            var journalEntries = ConvertToJournalEntries(accountingEntries);
            
            var book = await template.CreateBookAsync(tenantId, period, journalEntries);
            
            _logger.LogInformation("Successfully generated S2b-HKD Book for tenant {TenantId}", tenantId.Value);
            
            return book;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating S2b-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            throw;
        }
    }
    
    public async Task<GenericHKDBook> GenerateS2cBookAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating S2c-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            
            var template = new S2cHKDTemplate();
            var accountingEntries = await _repository.GetByPeriodAsync(tenantId, period, cancellationToken);
            var journalEntries = ConvertToJournalEntries(accountingEntries);
            
            var book = await template.CreateBookAsync(tenantId, period, journalEntries);
            
            _logger.LogInformation("Successfully generated S2c-HKD Book for tenant {TenantId}", tenantId.Value);
            
            return book;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating S2c-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            throw;
        }
    }
    
    public async Task<GenericHKDBook> GenerateS2dBookAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating S2d-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            
            var template = new S2dHKDTemplate();
            var accountingEntries = await _repository.GetByPeriodAsync(tenantId, period, cancellationToken);
            var journalEntries = ConvertToJournalEntries(accountingEntries);
            
            var book = await template.CreateBookAsync(tenantId, period, journalEntries);
            
            _logger.LogInformation("Successfully generated S2d-HKD Book for tenant {TenantId}", tenantId.Value);
            
            return book;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating S2d-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            throw;
        }
    }
    
    public async Task<GenericHKDBook> GenerateS2eBookAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating S2e-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            
            var template = new S2eHKDTemplate();
            var accountingEntries = await _repository.GetByPeriodAsync(tenantId, period, cancellationToken);
            var journalEntries = ConvertToJournalEntries(accountingEntries);
            
            var book = await template.CreateBookAsync(tenantId, period, journalEntries);
            
            _logger.LogInformation("Successfully generated S2e-HKD Book for tenant {TenantId}", tenantId.Value);
            
            return book;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating S2e-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            throw;
        }
    }
    
    public async Task<GenericHKDBook> GenerateS3aBookAsync(
        TenantId tenantId,
        AccountingPeriod period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating S3a-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            
            var template = new S3aHKDTemplate();
            var accountingEntries = await _repository.GetByPeriodAsync(tenantId, period, cancellationToken);
            var journalEntries = ConvertToJournalEntries(accountingEntries);
            
            var book = await template.CreateBookAsync(tenantId, period, journalEntries);
            
            _logger.LogInformation("Successfully generated S3a-HKD Book for tenant {TenantId}", tenantId.Value);
            
            return book;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating S3a-HKD Book for tenant {TenantId}, period {Period}", 
                tenantId.Value, period);
            throw;
        }
    }
    
    // Business Logic Validation Methods
    public async Task<bool> ValidateHKDGroupAsync(TenantId tenantId, HKDGroup requiredGroup)
    {
        try
        {
            // For now, return true - would validate tenant's HKD group in production
            _logger.LogDebug("Validating HKD group for tenant {TenantId}, required group: {Group}", 
                tenantId.Value, requiredGroup);
            
            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating HKD group for tenant {TenantId}", tenantId.Value);
            return false;
        }
    }
    
    public async Task<List<AccountingBookType>> GetAvailableBookTypesAsync(TenantId tenantId)
    {
        try
        {
            // For now, return all HKD book types - would filter by tenant's HKD group in production
            var bookTypes = new List<AccountingBookType>
            {
                AccountingBookType.S1a_HKD,
                AccountingBookType.S2a_HKD,
                AccountingBookType.S2b_HKD,
                AccountingBookType.S2c_HKD,
                AccountingBookType.S2d_HKD,
                AccountingBookType.S2e_HKD,
                AccountingBookType.S3a_HKD
            };
            
            _logger.LogDebug("Retrieved {Count} available book types for tenant {TenantId}", 
                bookTypes.Count, tenantId.Value);
            
            return bookTypes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available book types for tenant {TenantId}", tenantId.Value);
            return new List<AccountingBookType>();
        }
    }
    
    public async Task<HKDGroup> GetTenantHKDGroupAsync(TenantId tenantId)
    {
        try
        {
            // For now, return Group2 - would get from tenant configuration in production
            _logger.LogDebug("Getting HKD group for tenant {TenantId}", tenantId.Value);
            
            return await Task.FromResult(HKDGroup.Group2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting HKD group for tenant {TenantId}", tenantId.Value);
            throw;
        }
    }
    
    private List<JournalEntry> ConvertToJournalEntries(IEnumerable<AccountingEntry> accountingEntries)
    {
        var journalEntries = new List<JournalEntry>();
        
        foreach (var ae in accountingEntries)
        {
            // Create JournalEntry using proper constructor
            var journalEntry = new JournalEntry(
                tenantId: ae.TenantId,
                entryDate: ae.CreatedAt,
                description: ae.Description ?? "Accounting Entry",
                referenceType: "AccountingEntry",
                referenceId: ae.Id
            );
            
            // AccountingEntry doesn't have Lines property - create a simple line based on amount
            // This is a simplified conversion for HKD book compatibility
            var accountNumber = ae.EntryType == AccountingEntryType.Revenue ? "511" : "611"; // Revenue vs Expense
            var amount = Math.Abs(ae.Amount);
            
            if (ae.EntryType == AccountingEntryType.Revenue)
            {
                journalEntry.AddLine(accountNumber, 0, amount, ae.Description);
            }
            else
            {
                journalEntry.AddLine(accountNumber, amount, 0, ae.Description);
            }
            
            journalEntries.Add(journalEntry);
        }
        
        return journalEntries;
    }
    
    private string GetAccountName(string accountNumber)
    {
        return _vietnameseAccounts.TryGetValue(accountNumber, out var name) ? name : $"Tài khoàn {accountNumber}";
    }
}
