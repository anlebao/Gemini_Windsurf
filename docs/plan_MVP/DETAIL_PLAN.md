# DETAIL PLAN - VÃN AN ACCOUNTING MVP
## WEEK-BY-WEEK IMPLEMENTATION BREAKDOWN

---

## WEEK 1: CORE ACCOUNTING ENGINE (FOUNDATION)
**Objective: Build immutable accounting foundation**

### Day 1-2: Domain Design & Value Objects

#### AccountingEntry Immutable Pattern (5-Layer Protection)
```csharp
// Layer 1: Domain - Private readonly fields
public sealed class AccountingEntry
{
    private readonly AccountingEntryId _id;
    private readonly AccountingBookType _bookType;
    private readonly AccountingPeriod _period;
    private readonly Money _amount;
    private readonly string _description;
    private readonly AccountingEntryId? _reversalEntryId;
    private readonly TenantId _tenantId;
    private readonly DateTime _createdAt;
}

// Layer 2: EF Core - No Update/Delete methods
public class VanAnDbContext : DbContext
{
    public DbSet<AccountingEntry> AccountingEntries { get; set; }
    
    // Only AddAsync, no Update/Delete methods exposed
    public async Task AddEntryAsync(AccountingEntry entry) { }
    
    // Remove Update/Delete methods or make them private
    private void UpdateEntry(AccountingEntry entry) { }
    private void DeleteEntry(AccountingEntry entry) { }
}

// Layer 3: Repository - Only Add and Get operations
public interface IAccountingEntryRepository
{
    Task AddAsync(AccountingEntry entry);
    Task<AccountingEntry?> GetByIdAsync(AccountingEntryId id);
    Task<IEnumerable<AccountingEntry>> GetByTenantAsync(TenantId tenantId);
    // No Update/Delete methods
}

// Layer 4: Application Service - Only Create operations
public class AccountingEntryService
{
    public async Task<AccountingEntry> CreateRevenueEntryAsync(CreateRevenueEntryCommand command);
    public async Task<AccountingEntry> CreateExpenseEntryAsync(CreateExpenseEntryCommand command);
    public async Task<AccountingEntry> CreateReversalEntryAsync(CreateReversalEntryCommand command);
    // No Update/Delete methods
}

// Layer 5: API - Only POST endpoints
[ApiController]
[Route("api/[controller]")]
public class AccountingEntriesController : ControllerBase
{
    [HttpPost("revenue")]
    public async Task<ActionResult<AccountingEntryDto>> CreateRevenueEntry(CreateRevenueEntryRequest request);
    
    [HttpPost("expense")]
    public async Task<ActionResult<AccountingEntryDto>> CreateExpenseEntry(CreateExpenseEntryRequest request);
    
    [HttpPost("reversal")]
    public async Task<ActionResult<AccountingEntryDto>> CreateReversalEntry(CreateReversalEntryRequest request);
    // No PUT/DELETE endpoints
}
```

#### Value Objects Implementation
```csharp
// 1_Shared/Domain.cs
public sealed record AccountingEntryId(Guid Value);
public sealed record TenantId(Guid Value);

public sealed class AccountingBookType
{
    public static readonly AccountingBookType RevenueBook = new("RevenueBook");
    public static readonly AccountingBookType ExpenseBook = new("ExpenseBook");
    public static readonly AccountingBookType CashBankBook = new("CashBankBook");
    public static readonly AccountingBookType TaxDeclarationBook = new("TaxDeclarationBook");
    
    private AccountingBookType(string value) => Value = value;
    public string Value { get; }
}

public sealed record AccountingPeriod(int Year, int Month)
{
    public static AccountingPeriod Create(int year, int month)
    {
        if (year < 2020 || year > 2030) throw new ArgumentException("Invalid year");
        if (month < 1 || month > 12) throw new ArgumentException("Invalid month");
        return new AccountingPeriod(year, month);
    }
}

public sealed record Money(decimal Amount, string Currency)
{
    public static Money Zero => new(0, "VND");
    
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency) 
            throw new InvalidOperationException("Currency mismatch");
        return new Money(left.Amount + right.Amount, left.Currency);
    }
}
```

### Day 3: EF Core Configuration & Value Converters

#### Entity Configuration
```csharp
// 3_CoreHub/Infrastructure/Configurations/AccountingEntryConfiguration.cs
public class AccountingEntryConfiguration : IEntityTypeConfiguration<AccountingEntry>
{
    public void Configure(EntityTypeBuilder<AccountingEntry> builder)
    {
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                value => new AccountingEntryId(value));
            
        builder.Property(e => e.BookType)
            .HasConversion(
                type => type.Value,
                value => new AccountingBookType(value));
            
        builder.Property(e => e.Period)
            .HasConversion(
                period => $"{period.Year}-{period.Month:D2}",
                value => 
                {
                    var parts = value.Split('-');
                    return new AccountingPeriod(int.Parse(parts[0]), int.Parse(parts[1]));
                });
            
        builder.Property(e => e.Amount)
            .HasConversion(
                money => $"{money.Amount}:{money.Currency}",
                value => 
                {
                    var parts = value.Split(':');
                    return new Money(decimal.Parse(parts[0]), parts[1]);
                });
            
        builder.Property(e => e.TenantId)
            .HasConversion(
                id => id.Value,
                value => new TenantId(value));
            
        // Global Query Filter for Multi-tenancy
        builder.HasQueryFilter(e => e.TenantId == _currentTenantId);
    }
}
```

#### Multi-tenancy Implementation
```csharp
// 1_Shared/Domain.cs
public interface IMustHaveTenant
{
    TenantId TenantId { get; }
}

// Base entity for all entities
public abstract class Entity : IMustHaveTenant
{
    public TenantId TenantId { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    
    protected Entity(TenantId tenantId)
    {
        TenantId = tenantId;
        CreatedAt = DateTime.UtcNow;
    }
}

// DbContext with multi-tenancy
public class VanAnDbContext : DbContext
{
    private readonly TenantId _currentTenantId;
    
    public VanAnDbContext(DbContextOptions<VanAnDbContext> options, TenantId currentTenantId)
        : base(options)
    {
        _currentTenantId = currentTenantId;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply global query filter for all entities implementing IMustHaveTenant
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(
                    CreateTenantFilter(_currentTenantId));
            }
        }
    }
    
    private static LambdaExpression CreateTenantFilter(TenantId tenantId)
    {
        var parameter = Expression.Parameter(typeof(IMustHaveTenant), "e");
        var property = Expression.Property(parameter, nameof(IMustHaveTenant.TenantId));
        var constant = Expression.Constant(tenantId, typeof(TenantId));
        var equal = Expression.Equal(property, constant);
        return Expression.Lambda(equal, parameter);
    }
}
```

### Day 4-5: 4 HKD Books Implementation

#### HKD Books per TT152
```csharp
// 3_CoreHub/Services/HKDBookService.cs
public class HKDBookService
{
    public async Task GenerateHKDBooksAsync(AccountingEntry entry)
    {
        switch (entry.BookType.Value)
        {
            case "RevenueBook":
                await GenerateRevenueBookEntryAsync(entry);
                await GenerateCashBankBookEntryAsync(entry);
                break;
                
            case "ExpenseBook":
                await GenerateExpenseBookEntryAsync(entry);
                await GenerateCashBankBookEntryAsync(entry);
                break;
                
            case "CashBankBook":
                // Already handled in above cases
                break;
                
            case "TaxDeclarationBook":
                await GenerateTaxDeclarationEntryAsync(entry);
                break;
        }
    }
    
    private async Task GenerateRevenueBookEntryAsync(AccountingEntry entry)
    {
        // TT152 Revenue Book requirements
        var revenueBookEntry = new AccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.RevenueBook,
            entry.Period,
            entry.Amount,
            $"Doanh thu: {entry.Description}",
            null, // No reversal for revenue book
            entry.TenantId);
            
        await _repository.AddAsync(revenueBookEntry);
    }
    
    private async Task GenerateExpenseBookEntryAsync(AccountingEntry entry)
    {
        // TT152 Expense Book requirements
        var expenseBookEntry = new AccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.ExpenseBook,
            entry.Period,
            entry.Amount,
            $"Chi phí: {entry.Description}",
            null, // No reversal for expense book
            entry.TenantId);
            
        await _repository.AddAsync(expenseBookEntry);
    }
    
    private async Task GenerateCashBankBookEntryAsync(AccountingEntry entry)
    {
        // TT152 Cash Book requirements
        var cashBookEntry = new AccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.CashBankBook,
            entry.Period,
            entry.Amount,
            $"Tiên mâtt/Ngân hàng: {entry.Description}",
            null, // No reversal for cash book
            entry.TenantId);
            
        await _repository.AddAsync(cashBookEntry);
    }
    
    private async Task GenerateTaxDeclarationEntryAsync(AccountingEntry entry)
    {
        // TT152 Tax Declaration Book requirements
        var taxEntry = new AccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.TaxDeclarationBook,
            entry.Period,
            entry.Amount,
            $"Kê khai thuê: {entry.Description}",
            null, // No reversal for tax book
            entry.TenantId);
            
        await _repository.AddAsync(taxEntry);
    }
}
```

### Day 6-7: Reversal Pattern & Factory Methods

#### Factory Pattern Implementation
```csharp
// 3_CoreHub/Domain/AccountingEntryFactory.cs
public static class AccountingEntryFactory
{
    public static AccountingEntry CreateRevenueEntry(
        TenantId tenantId,
        AccountingPeriod period,
        Money amount,
        string description)
    {
        return new AccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.RevenueBook,
            period,
            amount,
            description,
            null, // No reversal for original entry
            tenantId);
    }
    
    public static AccountingEntry CreateExpenseEntry(
        TenantId tenantId,
        AccountingPeriod period,
        Money amount,
        string description)
    {
        return new AccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.ExpenseBook,
            period,
            amount,
            description,
            null, // No reversal for original entry
            tenantId);
    }
    
    public static AccountingEntry CreateReversalEntry(
        AccountingEntry originalEntry,
        string reason)
    {
        if (originalEntry.ReversalEntryId != null)
            throw new InvalidOperationException("Entry already reversed");
            
        return new AccountingEntry(
            AccountingEntryId.New(),
            originalEntry.BookType,
            originalEntry.Period,
            new Money(-originalEntry.Amount.Amount, originalEntry.Amount.Currency),
            $"Dâo bût toán: {originalEntry.Description} - Lý do: {reason}",
            originalEntry.Id,
            originalEntry.TenantId);
    }
}
```

#### Reversal Service
```csharp
// 3_CoreHub/Services/ReversalService.cs
public class ReversalService
{
    public async Task<AccountingEntry> ReverseEntryAsync(
        AccountingEntryId entryId,
        string reason,
        TenantId tenantId)
    {
        var originalEntry = await _repository.GetByIdAsync(entryId);
        if (originalEntry == null)
            throw new NotFoundException($"Entry {entryId} not found");
            
        if (originalEntry.TenantId != tenantId)
            throw new UnauthorizedAccessException("Access denied");
            
        if (originalEntry.ReversalEntryId != null)
            throw new InvalidOperationException("Entry already reversed");
            
        var reversalEntry = AccountingEntryFactory.CreateReversalEntry(originalEntry, reason);
        
        // Add reversal entry
        await _repository.AddAsync(reversalEntry);
        
        // Update original entry to reference reversal
        // Note: This is the ONLY place where we modify an existing entry
        // and it should be done through a specialized method
        await _repository.SetReversalEntryIdAsync(originalEntry.Id, reversalEntry.Id);
        
        return reversalEntry;
    }
}
```

---

## WEEK 2: ORDER FLOW + HKD ACCOUNTING
**Objective: Integrate accounting into sales flow**

### Day 8-9: Order to Accounting Integration

#### Order Service with Accounting Integration
```csharp
// 3_CoreHub/Services/OrderService.cs
public class OrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IAccountingEntryService _accountingService;
    private readonly IInventoryService _inventoryService;
    private readonly IHKDBookService _hkdBookService;
    
    public async Task<OrderDto> CreateOrderAsync(CreateOrderCommand command)
    {
        // 1. Create order
        var order = new Order(
            OrderId.New(),
            command.CustomerId,
            command.TenantId,
            command.Items);
            
        await _orderRepository.AddAsync(order);
        
        // 2. Deduct inventory
        foreach (var item in order.Items)
        {
            await _inventoryService.DeductStockAsync(
                item.ProductId,
                item.Quantity,
                command.TenantId);
        }
        
        // 3. Generate accounting entries
        await GenerateAccountingEntriesAsync(order);
        
        return order.ToDto();
    }
    
    private async Task GenerateAccountingEntriesAsync(Order order)
    {
        var period = AccountingPeriod.Create(
            DateTime.UtcNow.Year,
            DateTime.UtcNow.Month);
        
        // Revenue entry
        var revenueEntry = AccountingEntryFactory.CreateRevenueEntry(
            order.TenantId,
            period,
            order.TotalAmount,
            $"Doanh thu bán hàng #{order.Id.Value}");
            
        await _accountingService.CreateRevenueEntryAsync(revenueEntry);
        
        // Generate HKD books
        await _hkdBookService.GenerateHKDBooksAsync(revenueEntry);
        
        // Cost of goods sold entry
        var cogsAmount = await CalculateCostOfGoodsSoldAsync(order);
        var cogsEntry = AccountingEntryFactory.CreateExpenseEntry(
            order.TenantId,
            period,
            cogsAmount,
            $"Giá vô hàng bán #{order.Id.Value}");
            
        await _accountingService.CreateExpenseEntryAsync(cogsEntry);
        await _hkdBookService.GenerateHKDBooksAsync(cogsEntry);
    }
}
```

### Day 10-11: Offline SQLite Sync Implementation

#### Offline Database Setup
```csharp
// 5_WebApps/KhachLink/Data/OfflineDbContext.cs
public class OfflineDbContext : DbContext
{
    private readonly string _databasePath;
    
    public OfflineDbContext(string databasePath)
    {
        _databasePath = databasePath;
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={_databasePath}");
    }
    
    public DbSet<OfflineOrder> OfflineOrders { get; set; }
    public DbSet<OfflineAccountingEntry> OfflineAccountingEntries { get; set; }
    public DbSet<SyncLog> SyncLogs { get; set; }
}

// Offline entities
public class OfflineOrder
{
    public Guid Id { get; set; }
    public string JsonData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SyncedAt { get; set; }
    public SyncStatus Status { get; set; }
}

public enum SyncStatus
{
    Pending,
    Syncing,
    Synced,
    Failed
}
```

#### Sync Service Implementation
```csharp
// 3_CoreHub/Services/SyncService.cs
public class SyncService
{
    private readonly IOfflineRepository _offlineRepository;
    private readonly IOnlineRepository _onlineRepository;
    private readonly ILogger<SyncService> _logger;
    
    public async Task<SyncResult> SyncPendingDataAsync(TenantId tenantId)
    {
        var result = new SyncResult();
        
        try
        {
            // 1. Get pending offline orders
            var pendingOrders = await _offlineRepository.GetPendingOrdersAsync(tenantId);
            
            foreach (var offlineOrder in pendingOrders)
            {
                try
                {
                    // 2. Sync to online database
                    var order = JsonSerializer.Deserialize<Order>(offlineOrder.JsonData);
                    await _onlineRepository.AddOrderAsync(order);
                    
                    // 3. Update sync status
                    offlineOrder.Status = SyncStatus.Synced;
                    offlineOrder.SyncedAt = DateTime.UtcNow;
                    await _offlineRepository.UpdateOfflineOrderAsync(offlineOrder);
                    
                    result.SuccessfulOrders++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync order {OrderId}", offlineOrder.Id);
                    offlineOrder.Status = SyncStatus.Failed;
                    await _offlineRepository.UpdateOfflineOrderAsync(offlineOrder);
                    result.FailedOrders++;
                }
            }
            
            // 4. Sync accounting entries
            var pendingEntries = await _offlineRepository.GetPendingAccountingEntriesAsync(tenantId);
            foreach (var offlineEntry in pendingEntries)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<AccountingEntry>(offlineEntry.JsonData);
                    await _onlineRepository.AddAccountingEntryAsync(entry);
                    
                    offlineEntry.Status = SyncStatus.Synced;
                    offlineEntry.SyncedAt = DateTime.UtcNow;
                    await _offlineRepository.UpdateOfflineAccountingEntryAsync(offlineEntry);
                    
                    result.SuccessfulEntries++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync accounting entry {EntryId}", offlineEntry.Id);
                    offlineEntry.Status = SyncStatus.Failed;
                    await _offlineRepository.UpdateOfflineAccountingEntryAsync(offlineEntry);
                    result.FailedEntries++;
                }
            }
            
            result.IsSuccessful = result.FailedOrders == 0 && result.FailedEntries == 0;
            
            // 5. Log sync result
            await LogSyncResultAsync(tenantId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for tenant {TenantId}", tenantId);
            result.IsSuccessful = false;
        }
        
        return result;
    }
    
    private async Task LogSyncResultAsync(TenantId tenantId, SyncResult result)
    {
        var log = new SyncLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId.Value,
            SyncTime = DateTime.UtcNow,
            SuccessfulOrders = result.SuccessfulOrders,
            FailedOrders = result.FailedOrders,
            SuccessfulEntries = result.SuccessfulEntries,
            FailedEntries = result.FailedEntries,
            IsSuccessful = result.IsSuccessful
        };
        
        await _offlineRepository.AddSyncLogAsync(log);
    }
}
```

### Day 12-13: SignalR Real-time Implementation

#### KitchenHub SignalR Hub
```csharp
// 2_Gateway/Hubs/KitchenHub.cs
[Authorize]
public class KitchenHub : Hub
{
    private readonly ITenantService _tenantService;
    
    public KitchenHub(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }
    
    public async Task JoinKitchenGroup(string tenantId)
    {
        var currentTenantId = await _tenantService.GetCurrentTenantIdAsync();
        if (currentTenantId.Value.ToString() != tenantId)
            throw new UnauthorizedAccessException();
            
        await Groups.AddToGroupAsync(Context.ConnectionId, $"kitchen_{tenantId}");
    }
    
    public async Task LeaveKitchenGroup(string tenantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"kitchen_{tenantId}");
    }
    
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        // Remove from all kitchen groups
        // Implementation depends on your tracking strategy
        await base.OnDisconnectedAsync(exception);
    }
}

// Order service with SignalR integration
public class OrderService
{
    private readonly IHubContext<KitchenHub> _kitchenHub;
    
    public async Task<OrderDto> CreateOrderAsync(CreateOrderCommand command)
    {
        // ... existing order creation logic ...
        
        // Notify kitchen staff
        await _kitchenHub.Clients.Group($"kitchen_{command.TenantId.Value}")
            .SendAsync("NewOrderReceived", new
            {
                OrderId = order.Id.Value,
                CustomerName = order.CustomerName,
                Items = order.Items.Select(i => new
                {
                    i.ProductName,
                    i.Quantity,
                    i.Price
                }),
                TotalAmount = order.TotalAmount.Amount,
                CreatedAt = order.CreatedAt
            });
            
        return order.ToDto();
    }
    
    public async Task UpdateOrderStatusAsync(OrderId orderId, OrderStatus status)
    {
        // ... existing status update logic ...
        
        // Notify all clients
        await _kitchenHub.Clients.All
            .SendAsync("OrderStatusUpdated", new
            {
                OrderId = orderId.Value,
                Status = status.ToString(),
                UpdatedAt = DateTime.UtcNow
            });
    }
}
```

### Day 14: HKD Tax Reports

#### Tax Report Service
```csharp
// 3_CoreHub/Services/TaxReportService.cs
public class TaxReportService
{
    private readonly IAccountingEntryRepository _repository;
    private readonly IExcelExportService _excelExport;
    private readonly IPdfExportService _pdfExport;
    
    public async Task<byte[]> GenerateVATReportAsync(
        TenantId tenantId,
        AccountingPeriod period)
    {
        // 1. Get revenue entries for the period
        var revenueEntries = await _repository.GetByTenantAndBookTypeAsync(
            tenantId,
            AccountingBookType.RevenueBook,
            period);
            
        // 2. Get expense entries for the period
        var expenseEntries = await _repository.GetByTenantAndBookTypeAsync(
            tenantId,
            AccountingBookType.ExpenseBook,
            period);
            
        // 3. Calculate VAT
        var totalRevenue = revenueEntries.Sum(e => e.Amount.Amount);
        var totalExpenses = expenseEntries.Sum(e => e.Amount.Amount);
        var vatAmount = (totalRevenue - totalExpenses) * 0.1m; // 10% VAT
        
        // 4. Generate report data
        var reportData = new VATReportData
        {
            TenantId = tenantId.Value,
            Period = period,
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            VATAmount = vatAmount,
            RevenueEntries = revenueEntries.Select(e => new VATReportEntry
            {
                Date = e.CreatedAt,
                Description = e.Description,
                Amount = e.Amount.Amount,
                VAT = e.Amount.Amount * 0.1m
            }).ToList(),
            ExpenseEntries = expenseEntries.Select(e => new VATReportEntry
            {
                Date = e.CreatedAt,
                Description = e.Description,
                Amount = e.Amount.Amount,
                VAT = e.Amount.Amount * 0.1m
            }).ToList()
        };
        
        // 5. Export to Excel
        return await _excelExport.ExportVATReportAsync(reportData);
    }
    
    public async Task<byte[]> GeneratePersonalIncomeTaxReportAsync(
        TenantId tenantId,
        AccountingPeriod period)
    {
        // Similar implementation for personal income tax
        // Following Vietnamese tax regulations
        var reportData = new PersonalIncomeTaxReportData
        {
            TenantId = tenantId.Value,
            Period = period,
            // ... tax calculation logic ...
        };
        
        return await _pdfExport.ExportPersonalIncomeTaxReportAsync(reportData);
    }
}
```

---

## WEEK 3: TRADING/SERVICE COMPANY LITE
**Objective: Extend accounting for companies**

### Day 15-16: Extended AccountingEntry for Accrual Basis

#### Extended AccountingEntry for Companies
```csharp
// 1_Shared/Domain.cs - Extended Value Objects
public sealed record AccountNumber(string Value)
{
    public static readonly AccountNumber Cash = new("111");
    public static readonly AccountNumber Bank = new("112");
    public static readonly AccountNumber AccountsReceivable = new("131");
    public static readonly AccountNumber AccountsPayable = new("331");
    public static readonly AccountNumber Revenue = new("511");
    public static readonly AccountNumber CostOfGoodsSold = new("632");
    public static readonly AccountNumber Expenses = new("642");
    
    public static AccountNumber Create(string value)
    {
        // Validate account number format
        if (string.IsNullOrWhiteSpace(value) || value.Length != 3)
            throw new ArgumentException("Invalid account number");
            
        return new AccountNumber(value);
    }
}

public sealed record AccountingEntryType
{
    public static readonly AccountingEntryType Debit = new("Debit");
    public static readonly AccountingEntryType Credit = new("Credit");
    
    private AccountingEntryType(string value) => Value = value;
    public string Value { get; }
}

// Extended AccountingEntry for companies
public sealed class CompanyAccountingEntry : AccountingEntry
{
    public AccountNumber AccountNumber { get; }
    public AccountingEntryType EntryType { get; }
    public string? ReferenceNumber { get; }
    public string? CustomerId { get; }
    public string? SupplierId { get; }
    public DateTime? DueDate { get; }
    
    public CompanyAccountingEntry(
        AccountingEntryId id,
        AccountingBookType bookType,
        AccountingPeriod period,
        AccountNumber accountNumber,
        AccountingEntryType entryType,
        Money amount,
        string description,
        TenantId tenantId,
        string? referenceNumber = null,
        string? customerId = null,
        string? supplierId = null,
        DateTime? dueDate = null,
        AccountingEntryId? reversalEntryId = null)
        : base(id, bookType, period, amount, description, reversalEntryId, tenantId)
    {
        AccountNumber = accountNumber;
        EntryType = entryType;
        ReferenceNumber = referenceNumber;
        CustomerId = customerId;
        SupplierId = supplierId;
        DueDate = dueDate;
    }
}
```

### Day 17-18: Payables/Receivables Implementation

#### Accounts Receivable Service
```csharp
// 3_CoreHub/Services/AccountsReceivableService.cs
public class AccountsReceivableService
{
    private readonly ICompanyAccountingEntryRepository _repository;
    private readonly ICustomerRepository _customerRepository;
    
    public async Task<AccountsReceivableEntry> CreateReceivableAsync(
        CreateReceivableCommand command)
    {
        // 1. Validate customer
        var customer = await _customerRepository.GetByIdAsync(command.CustomerId);
        if (customer == null)
            throw new NotFoundException("Customer not found");
            
        // 2. Create receivable entry
        var receivableEntry = new CompanyAccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.RevenueBook,
            command.Period,
            AccountNumber.AccountsReceivable,
            AccountingEntryType.Debit,
            command.Amount,
            $"Phâi thu khách hàng {customer.Name}: {command.Description}",
            command.TenantId,
            referenceNumber: command.InvoiceNumber,
            customerId: command.CustomerId.Value,
            dueDate: command.DueDate);
            
        await _repository.AddAsync(receivableEntry);
        
        // 3. Create corresponding revenue entry (double-entry)
        var revenueEntry = new CompanyAccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.RevenueBook,
            command.Period,
            AccountNumber.Revenue,
            AccountingEntryType.Credit,
            command.Amount,
            $"Doanh thu bán hàng {customer.Name}: {command.Description}",
            command.TenantId,
            referenceNumber: command.InvoiceNumber,
            customerId: command.CustomerId.Value);
            
        await _repository.AddAsync(revenueEntry);
        
        return new AccountsReceivableEntry(receivableEntry, revenueEntry);
    }
    
    public async Task<PaymentEntry> RecordPaymentAsync(
        RecordPaymentCommand command)
    {
        // 1. Get receivable entry
        var receivableEntry = await _repository.GetByIdAsync(command.ReceivableEntryId);
        if (receivableEntry == null)
            throw new NotFoundException("Receivable entry not found");
            
        // 2. Create cash/bank entry
        var cashEntry = new CompanyAccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.CashBankBook,
            AccountingPeriod.Create(DateTime.UtcNow.Year, DateTime.UtcNow.Month),
            command.PaymentMethod == PaymentMethod.Cash ? AccountNumber.Cash : AccountNumber.Bank,
            AccountingEntryType.Debit,
            command.Amount,
            $"Thu tiên khách hàng: {receivableEntry.Description}",
            receivableEntry.TenantId,
            referenceNumber: command.ReceiptNumber,
            customerId: receivableEntry.CustomerId);
            
        await _repository.AddAsync(cashEntry);
        
        // 3. Create receivable reduction entry
        var receivableReduction = new CompanyAccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.RevenueBook,
            receivableEntry.Period,
            AccountNumber.AccountsReceivable,
            AccountingEntryType.Credit,
            command.Amount,
            $"Giâm trâ công nô: {receivableEntry.Description}",
            receivableEntry.TenantId,
            referenceNumber: command.ReceiptNumber,
            customerId: receivableEntry.CustomerId);
            
        await _repository.AddAsync(receivableReduction);
        
        return new PaymentEntry(cashEntry, receivableReduction);
    }
}
```

#### Accounts Payable Service
```csharp
// 3_CoreHub/Services/AccountsPayableService.cs
public class AccountsPayableService
{
    private readonly ICompanyAccountingEntryRepository _repository;
    private readonly ISupplierRepository _supplierRepository;
    
    public async Task<AccountsPayableEntry> CreatePayableAsync(
        CreatePayableCommand command)
    {
        // 1. Validate supplier
        var supplier = await _supplierRepository.GetByIdAsync(command.SupplierId);
        if (supplier == null)
            throw new NotFoundException("Supplier not found");
            
        // 2. Create expense entry
        var expenseEntry = new CompanyAccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.ExpenseBook,
            command.Period,
            command.AccountNumber, // 632, 642, etc.
            AccountingEntryType.Debit,
            command.Amount,
            $"Chi phí {supplier.Name}: {command.Description}",
            command.TenantId,
            referenceNumber: command.InvoiceNumber,
            supplierId: command.SupplierId.Value);
            
        await _repository.AddAsync(expenseEntry);
        
        // 3. Create payable entry
        var payableEntry = new CompanyAccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.ExpenseBook,
            command.Period,
            AccountNumber.AccountsPayable,
            AccountingEntryType.Credit,
            command.Amount,
            $"Phâi trâ nhà cung câp {supplier.Name}: {command.Description}",
            command.TenantId,
            referenceNumber: command.InvoiceNumber,
            supplierId: command.SupplierId.Value,
            dueDate: command.DueDate);
            
        await _repository.AddAsync(payableEntry);
        
        return new AccountsPayableEntry(expenseEntry, payableEntry);
    }
    
    public async Task<PaymentEntry> RecordPaymentAsync(
        RecordSupplierPaymentCommand command)
    {
        // 1. Get payable entry
        var payableEntry = await _repository.GetByIdAsync(command.PayableEntryId);
        if (payableEntry == null)
            throw new NotFoundException("Payable entry not found");
            
        // 2. Create payable reduction entry
        var payableReduction = new CompanyAccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.ExpenseBook,
            payableEntry.Period,
            AccountNumber.AccountsPayable,
            AccountingEntryType.Debit,
            command.Amount,
            $"Trâ tiên nhà cung câp: {payableEntry.Description}",
            payableEntry.TenantId,
            referenceNumber: command.PaymentNumber,
            supplierId: payableEntry.SupplierId);
            
        await _repository.AddAsync(payableReduction);
        
        // 3. Create cash/bank entry
        var cashEntry = new CompanyAccountingEntry(
            AccountingEntryId.New(),
            AccountingBookType.CashBankBook,
            AccountingPeriod.Create(DateTime.UtcNow.Year, DateTime.UtcNow.Month),
            command.PaymentMethod == PaymentMethod.Cash ? AccountNumber.Cash : AccountNumber.Bank,
            AccountingEntryType.Credit,
            command.Amount,
            $"Trâ tiên nhà cung câp: {payableEntry.Description}",
            payableEntry.TenantId,
            referenceNumber: command.PaymentNumber,
            supplierId: payableEntry.SupplierId);
            
        await _repository.AddAsync(cashEntry);
        
        return new PaymentEntry(payableReduction, cashEntry);
    }
}
```

### Day 19-20: Double-Entry Bookkeeping

#### Double-Entry Validation Service
```csharp
// 3_CoreHub/Services/DoubleEntryValidationService.cs
public class DoubleEntryValidationService
{
    private readonly ICompanyAccountingEntryRepository _repository;
    
    public async Task<ValidationResult> ValidateDoubleEntryAsync(
        AccountingEntryId entryId,
        TenantId tenantId)
    {
        var entry = await _repository.GetByIdAsync(entryId);
        if (entry == null)
            return ValidationResult.Failure("Entry not found");
            
        // 1. Find corresponding entry
        var correspondingEntry = await FindCorrespondingEntryAsync(entry);
        if (correspondingEntry == null)
            return ValidationResult.Failure("No corresponding entry found");
            
        // 2. Validate amounts
        if (entry.Amount.Amount != correspondingEntry.Amount.Amount)
            return ValidationResult.Failure("Amounts do not match");
            
        // 3. Validate entry types (debit vs credit)
        if (entry.EntryType.Value == correspondingEntry.EntryType.Value)
            return ValidationResult.Failure("Both entries have same type");
            
        // 4. Validate account numbers
        if (!IsValidAccountPair(entry.AccountNumber.Value, correspondingEntry.AccountNumber.Value))
            return ValidationResult.Failure("Invalid account pair");
            
        return ValidationResult.Success();
    }
    
    private async Task<CompanyAccountingEntry?> FindCorrespondingEntryAsync(
        CompanyAccountingEntry entry)
    {
        // Find entry with same reference number, amount, but opposite entry type
        var correspondingEntries = await _repository.GetByReferenceNumberAsync(
            entry.ReferenceNumber,
            entry.TenantId);
            
        return correspondingEntries.FirstOrDefault(e =>
            e.Id != entry.Id &&
            e.Amount.Amount == entry.Amount.Amount &&
            e.EntryType.Value != entry.EntryType.Value);
    }
    
    private bool IsValidAccountPair(string debitAccount, string creditAccount)
    {
        // Define valid account pairs for double-entry
        var validPairs = new Dictionary<string, List<string>>
        {
            ["111"] = ["511", "632", "642"], // Cash -> Revenue, COGS, Expenses
            ["112"] = ["511", "632", "642"], // Bank -> Revenue, COGS, Expenses
            ["131"] = ["511"], // Receivables -> Revenue
            ["331"] = ["632", "642"], // Payables -> COGS, Expenses
        };
        
        if (validPairs.TryGetValue(debitAccount, out var validCreditAccounts))
            return validCreditAccounts.Contains(creditAccount);
            
        return false;
    }
}
```

### Day 21: Basic Financial Reports

#### Financial Reports Service
```csharp
// 3_CoreHub/Services/FinancialReportsService.cs
public class FinancialReportsService
{
    private readonly ICompanyAccountingEntryRepository _repository;
    
    public async Task<BalanceSheetDto> GenerateBalanceSheetAsync(
        TenantId tenantId,
        AccountingPeriod period)
    {
        // 1. Get all entries for the period
        var entries = await _repository.GetByTenantAndPeriodAsync(tenantId, period);
        
        // 2. Calculate account balances
        var accountBalances = CalculateAccountBalances(entries);
        
        // 3. Build balance sheet
        var assets = new List<BalanceSheetItem>
        {
            new("Tiên mâtt", accountBalances.GetValueOrDefault("111", 0)),
            new("Tiên gûi ngân hàng", accountBalances.GetValueOrDefault("112", 0)),
            new("Phâi thu khách hàng", accountBalances.GetValueOrDefault("131", 0)),
            new("Hàng ton kho", accountBalances.GetValueOrDefault("156", 0))
        };
        
        var liabilities = new List<BalanceSheetItem>
        {
            new("Phâi trâ nhà cung câp", accountBalances.GetValueOrDefault("331", 0)),
            new("Vay và nû thuê tài chình", accountBalances.GetValueOrDefault("338", 0))
        };
        
        var equity = new List<BalanceSheetItem>
        {
            new("Vôn dâu tu", accountBalances.GetValueOrDefault("411", 0)),
            new("Lîu nhuân sau thuê", CalculateRetainedEarnings(accountBalances))
        };
        
        return new BalanceSheetDto
        {
            TenantId = tenantId.Value,
            Period = period,
            Assets = assets,
            Liabilities = liabilities,
            Equity = equity,
            TotalAssets = assets.Sum(a => a.Amount),
            TotalLiabilitiesAndEquity = liabilities.Sum(l => l.Amount) + equity.Sum(e => e.Amount)
        };
    }
    
    public async Task<IncomeStatementDto> GenerateIncomeStatementAsync(
        TenantId tenantId,
        AccountingPeriod period)
    {
        var entries = await _repository.GetByTenantAndPeriodAsync(tenantId, period);
        
        var revenues = CalculateAccountBalance(entries, "511");
        var costOfGoodsSold = CalculateAccountBalance(entries, "632");
        var operatingExpenses = CalculateAccountBalance(entries, "642");
        
        var grossProfit = revenues - costOfGoodsSold;
        var operatingIncome = grossProfit - operatingExpenses;
        
        return new IncomeStatementDto
        {
            TenantId = tenantId.Value,
            Period = period,
            Revenues = revenues,
            CostOfGoodsSold = costOfGoodsSold,
            GrossProfit = grossProfit,
            OperatingExpenses = operatingExpenses,
            OperatingIncome = operatingIncome
        };
    }
    
    public async Task<CashFlowStatementDto> GenerateCashFlowStatementAsync(
        TenantId tenantId,
        AccountingPeriod period)
    {
        var entries = await _repository.GetByTenantAndPeriodAsync(tenantId, period);
        
        var cashFromOperations = CalculateCashFromOperations(entries);
        var cashFromInvesting = CalculateCashFromInvesting(entries);
        var cashFromFinancing = CalculateCashFromFinancing(entries);
        
        var netCashFlow = cashFromOperations + cashFromInvesting + cashFromFinancing;
        
        return new CashFlowStatementDto
        {
            TenantId = tenantId.Value,
            Period = period,
            CashFromOperations = cashFromOperations,
            CashFromInvesting = cashFromInvesting,
            CashFromFinancing = cashFromFinancing,
            NetCashFlow = netCashFlow
        };
    }
    
    private Dictionary<string, decimal> CalculateAccountBalances(
        IEnumerable<CompanyAccountingEntry> entries)
    {
        var balances = new Dictionary<string, decimal>();
        
        foreach (var entry in entries)
        {
            var account = entry.AccountNumber.Value;
            var amount = entry.Amount.Amount;
            
            if (!balances.ContainsKey(account))
                balances[account] = 0;
                
            // Debit increases balance, Credit decreases balance
            if (entry.EntryType.Value == "Debit")
                balances[account] += amount;
            else
                balances[account] -= amount;
        }
        
        return balances;
    }
    
    private decimal CalculateAccountBalance(
        IEnumerable<CompanyAccountingEntry> entries,
        string accountNumber)
    {
        return entries
            .Where(e => e.AccountNumber.Value == accountNumber)
            .Sum(e => e.EntryType.Value == "Debit" ? e.Amount.Amount : -e.Amount.Amount);
    }
    
    private decimal CalculateRetainedEarnings(Dictionary<string, decimal> accountBalances)
    {
        // Simplified calculation - in real implementation would be more complex
        return accountBalances.GetValueOrDefault("421", 0);
    }
    
    private decimal CalculateCashFromOperations(IEnumerable<CompanyAccountingEntry> entries)
    {
        // Calculate cash flow from operating activities
        return entries
            .Where(e => e.AccountNumber.Value == "111" || e.AccountNumber.Value == "112")
            .Where(e => e.BookType.Value == "CashBankBook")
            .Sum(e => e.EntryType.Value == "Debit" ? e.Amount.Amount : -e.Amount.Amount);
    }
    
    private decimal CalculateCashFromInvesting(IEnumerable<CompanyAccountingEntry> entries)
    {
        // Calculate cash flow from investing activities
        return 0; // Simplified for MVP
    }
    
    private decimal CalculateCashFromFinancing(IEnumerable<CompanyAccountingEntry> entries)
    {
        // Calculate cash flow from financing activities
        return 0; // Simplified for MVP
    }
}
```

---

## WEEK 4: PACKAGING & GO-TO-MARKET
**Objective: Prepare for deployment and sales**

### Day 22-23: Docker Compose Setup

#### Docker Compose Configuration
```yaml
# docker-compose.yml
version: '3.8'

services:
  # PostgreSQL Database
  postgres:
    image: postgres:15
    container_name: vanan-postgres
    environment:
      POSTGRES_DB: VanAnAccounting
      POSTGRES_USER: vanan
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./scripts/init-db.sql:/docker-entrypoint-initdb.d/init-db.sql
    ports:
      - "5432:5432"
    networks:
      - vanan-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U vanan"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Redis Cache
  redis:
    image: redis:7-alpine
    container_name: vanan-redis
    ports:
      - "6379:6379"
    networks:
      - vanan-network
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Central Hub API
  centralhub:
    build:
      context: ./2_Gateway
      dockerfile: Dockerfile
    container_name: vanan-centralhub
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Host=postgres;Database=VanAnAccounting;Username=vanan;Password=${POSTGRES_PASSWORD}
      - Redis__ConnectionString=redis:6379
    ports:
      - "5001:80"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    networks:
      - vanan-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  # ShopERP Web App
  shoperp:
    build:
      context: ./5_WebApps/ShopERP
      dockerfile: Dockerfile
    container_name: vanan-shoperp
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ApiBaseUrl=http://centralhub
    ports:
      - "5002:80"
    depends_on:
      centralhub:
        condition: service_healthy
    networks:
      - vanan-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  # KhachLink Web App
  khachlink:
    build:
      context: ./5_WebApps/KhachLink
      dockerfile: Dockerfile
    container_name: vanan-khachlink
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ApiBaseUrl=http://centralhub
    ports:
      - "5003:80"
    depends_on:
      centralhub:
        condition: service_healthy
    networks:
      - vanan-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Nginx Reverse Proxy
  nginx:
    image: nginx:alpine
    container_name: vanan-nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf
      - ./nginx/ssl:/etc/nginx/ssl
    depends_on:
      - centralhub
      - shoperp
      - khachlink
    networks:
      - vanan-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  postgres_data:

networks:
  vanan-network:
    driver: bridge
```

#### Dockerfile for Each Service
```dockerfile
# 2_Gateway/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["VanAn.Gateway.csproj", "."]
RUN dotnet restore "./VanAn.Gateway.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "VanAn.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VanAn.Gateway.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VanAn.Gateway.dll"]
```

### Day 24-25: Enhanced guard-check.ps1 & Roslyn Analyzers

#### Enhanced Guard Check Script
```powershell
# guard-check.ps1
param(
    [switch]$SkipAnalyzer,
    [switch]$SkipTests,
    [switch]$SkipBuild
)

Write-Host "Running VÃN AN Strict Guard v8.0..." -ForegroundColor Green

# 1. Code Quality Check
if (-not $SkipBuild) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    $buildResult = dotnet build --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "BUILD FAILED" -ForegroundColor Red
        exit 1
    }
    Write-Host "BUILD SUCCEEDED" -ForegroundColor Green
}

# 2. Roslyn Analyzer Check
if (-not $SkipAnalyzer) {
    Write-Host "Running Roslyn Analyzers..." -ForegroundColor Yellow
    $analyzerResult = dotnet build --configuration Release --no-restore --verbosity quiet
    if ($analyzerResult -match "error VA") {
        Write-Host "ANALYZER ERRORS FOUND" -ForegroundColor Red
        Write-Host $analyzerResult
        exit 1
    }
    Write-Host "ANALYZERS PASSED" -ForegroundColor Green
}

# 3. Test Coverage Check
if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Yellow
    $testResult = dotnet test --configuration Release --no-build --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "TESTS FAILED" -ForegroundColor Red
        exit 1
    }
    Write-Host "TESTS PASSED" -ForegroundColor Green
}

# 4. Security Check
Write-Host "Running security checks..." -ForegroundColor Yellow
# Check for hardcoded secrets
$secrets = Select-String -Path "*.cs", "*.json", "*.js" -Pattern "password|secret|key" -CaseSensitive
if ($secrets) {
    Write-Host "POTENTIAL SECRETS FOUND" -ForegroundColor Red
    $secrets | ForEach-Object { Write-Host "  $($_.Path):$($_.LineNumber)" }
    exit 1
}

# 5. Performance Check
Write-Host "Running performance checks..." -ForegroundColor Yellow
# Check for common performance anti-patterns
$antiPatterns = Select-String -Path "*.cs" -Pattern "TODO|FIXME|HACK" -CaseSensitive
if ($antiPatterns) {
    Write-Host "ANTI-PATTERNS FOUND" -ForegroundColor Yellow
    $antiPatterns | ForEach-Object { Write-Host "  $($_.Path):$($_.LineNumber)" }
}

# 6. Documentation Check
Write-Host "Checking documentation..." -ForegroundColor Yellow
$readmeExists = Test-Path "README.md"
if (-not $readmeExists) {
    Write-Host "README.md missing" -ForegroundColor Red
    exit 1
}

Write-Host "ALL CHECKS PASSED - Ready for deployment" -ForegroundColor Green
```

#### Enhanced Roslyn Analyzers
```csharp
// VanAn.Accounting.Analyzers/ImmutableAccountingAnalyzer.cs - Enhanced
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ImmutableAccountingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "VA0001";
    private const string Category = "Design";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzePropertyAssignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(AnalyzeMethodCall, SyntaxKind.InvocationExpression);
    }

    private void AnalyzePropertyAssignment(SyntaxNodeAnalysisContext context)
    {
        if (AnalyzerHelpers.ShouldSkipAnalysis(context)) return;

        var assignment = (AssignmentExpressionSyntax)context.Node;
        
        // Check for assignment to AccountingEntry properties
        var propertyAccess = assignment.Left as MemberAccessExpressionSyntax;
        if (propertyAccess?.Expression is IdentifierNameSyntax identifier)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(identifier).Symbol;
            if (symbol?.Name.Contains("AccountingEntry") == true)
            {
                // Check if it's a mutation (not initialization)
                if (IsMutation(context, assignment))
                {
                    var diagnostic = Diagnostic.Create(
                        Rule,
                        assignment.GetLocation(),
                        symbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private void AnalyzeMethodCall(SyntaxNodeAnalysisContext context)
    {
        if (AnalyzerHelpers.ShouldSkipAnalysis(context)) return;

        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        
        if (methodSymbol?.Name == "Update" || methodSymbol?.Name == "Delete")
        {
            var receiverType = methodSymbol.ReceiverType?.Name;
            if (receiverType?.Contains("AccountingEntry") == true)
            {
                var diagnostic = Diagnostic.Create(
                    Rule,
                    invocation.GetLocation(),
                    methodSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsMutation(SyntaxNodeAnalysisContext context, AssignmentExpressionSyntax assignment)
    {
        // Check if this is a mutation (not initialization)
        // Simplified logic - in real implementation would be more sophisticated
        return assignment.Parent is not VariableDeclaratorSyntax;
    }
}
```

### Day 26-27: User Manuals & Documentation

#### User Manual Structure
```markdown
# VÃN AN ACCOUNTING - USER MANUAL

## Table of Contents
1. [Getting Started](#getting-started)
2. [Household Business Setup](#household-business-setup)
3. [Daily Operations](#daily-operations)
4. [Tax Reporting](#tax-reporting)
5. [Company Accounting](#company-accounting)
6. [Troubleshooting](#troubleshooting)

## Getting Started

### System Requirements
- Modern browser (Chrome, Firefox, Safari, Edge)
- Internet connection (optional for offline mode)
- 5GB available disk space

### Installation
1. Contact VÃN AN team for deployment
2. Receive login credentials
3. Access system via provided URL
4. Complete initial setup

### First Login
1. Enter username and password
2. Complete business profile
3. Configure tax information
4. Set up chart of accounts

## Household Business Setup

### Business Information
- Business name
- Tax code (Mã thuê)
- Address
- Phone number
- Email

### Tax Configuration
- Tax type (GTGT, TNCN)
- Tax rates
- Reporting periods
- Bank information

### Chart of Accounts
- Default HKD accounts
- Custom accounts (if needed)
- Account numbering
- Opening balances

## Daily Operations

### Recording Sales
1. Navigate to Sales > New Sale
2. Select customer
3. Add items
4. Enter payment method
5. Save transaction

### Recording Expenses
1. Navigate to Expenses > New Expense
2. Select supplier
3. Enter amount
4. Select expense category
5. Attach receipt (optional)
6. Save transaction

### Inventory Management
1. Navigate to Inventory > Stock
2. View current stock levels
3. Add new products
4. Update stock levels
5. Generate inventory reports

## Tax Reporting

### Monthly VAT Report
1. Navigate to Reports > Tax > VAT
2. Select month/year
3. Review report data
4. Export to Excel/PDF
5. Submit to tax authority

### Personal Income Tax
1. Navigate to Reports > Tax > Personal Income Tax
2. Select period
3. Review calculations
4. Export report
5. Submit to tax authority

## Company Accounting

### Accounts Receivable
1. Navigate to Accounting > Receivables
2. Create invoice
3. Set payment terms
4. Track payments
5. Generate aging report

### Accounts Payable
1. Navigate to Accounting > Payables
2. Record bills
3. Schedule payments
4. Track expenses
5. Generate cash flow report

### Financial Statements
1. Navigate to Reports > Financial
2. Generate Balance Sheet
3. Generate Income Statement
4. Generate Cash Flow Statement
5. Export reports

## Troubleshooting

### Common Issues
- Login problems
- Data sync issues
- Report generation errors
- Performance issues

### Support Contact
- Email: support@vanan.vn
- Phone: 1900-xxxx
- Online chat: Available 9AM-6PM
```

### Day 28-30: Sales Demo Kit & Final Preparation

#### Demo Script
```markdown
# VÃN AN ACCOUNTING - SALES DEMO SCRIPT

## Demo Duration: 15 Minutes

### Introduction (2 minutes)
- Welcome and introduction
- VÃN AN company overview
- Pain points of current accounting systems
- Our solution overview

### Problem Statement (1 minute)
- Manual accounting errors
- Tax compliance complexity
- Offline access issues
- High cost of existing solutions

### Solution Demo (8 minutes)

#### Part 1: Household Business Demo (4 minutes)
1. **Quick Setup**
   - Show 5-minute setup process
   - Business registration
   - Tax configuration

2. **Daily Operations**
   - Record a sale (cafe example)
   - Record expenses (rent, utilities)
   - Show real-time dashboard

3. **Tax Reports**
   - Generate monthly VAT report
   - Show TT152 compliance
   - Export to Excel

4. **Offline Mode**
   - Disconnect from internet
   - Continue recording transactions
   - Reconnect and sync

#### Part 2: Company Demo (4 minutes)
1. **Advanced Features**
   - Accounts receivable management
   - Double-entry bookkeeping
   - Financial statements

2. **Multi-tenant Architecture**
   - Show multiple companies
   - Data isolation
   - Scalability

### Value Proposition (2 minutes)
- 50% cost reduction
- 100% tax compliance
- Offline-first capability
- Mobile-friendly interface
- Vietnamese localization

### Pricing & Next Steps (2 minutes)
- Pricing tiers
- Free trial offer
- Implementation timeline
- Q&A session

## Demo Requirements
- Stable internet connection
- Backup internet (4G)
- Laptop with demo environment
- Printed handouts
- Business cards

## Follow-up Actions
1. Send demo summary email
2. Schedule technical deep-dive
3. Provide trial access
4. Follow-up call in 3 days
5. Proposal preparation
```

---

## SUCCESS CRITERIA & DELIVERABLES

### Week 1 Deliverables
- [ ] Immutable AccountingEntry implemented
- [ ] 4 HKD books auto-generated
- [ ] Multi-tenancy working
- [ ] Basic unit tests (80%+ coverage)
- [ ] Guard check passing

### Week 2 Deliverables
- [ ] Order flow integrated with accounting
- [ ] Offline SQLite sync working
- [ ] SignalR real-time features
- [ ] HKD tax reports (Excel/PDF)
- [ ] Integration tests passing

### Week 3 Deliverables
- [ ] Company accounting features
- [ ] Payables/Receivables working
- [ ] Double-entry bookkeeping
- [ ] Financial statements generated
- [ ] Enhanced sync capabilities

### Week 4 Deliverables
- [ ] Docker deployment ready
- [ ] Complete user manuals
- [ ] Sales demo kit prepared
- [ ] Production monitoring setup
- [ ] First customer signed

---

## VERSION HISTORY

| Version | Date | Changes | Author |
|---------|------|---------|--------|
| 1.0 | 16/04/2026 | Initial Detail Plan | Windsurf + User |
| 1.1 | - | - | - |

---

*This Detail Plan provides the complete implementation roadmap for the VÃN AN Accounting MVP. All team members must understand their responsibilities and timelines.*
