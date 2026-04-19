# TASK 1.1 - CLEAN UP PHASE: REVERSE IMPACT ANALYSIS + PLAN (REVISED)

**Ngày:** 14 tháng 4, 2026  
**Task:** Clean Up Phase - T VanAn.Accounting project + Domain organization  
**Trang thái:** Ch User approval (REVISED VERSION)

---

## **1. DEEPER REVERSE IMPACT ANALYSIS**

### **1.1 Files s thay ho c t o m i**

#### **FILES S T O M I:**
```yaml
New Files:
- 3_CoreHub/VanAn.Accounting/VanAn.Accounting.csproj (Class Library, .NET 8)
- 3_CoreHub/VanAn.Accounting/Domain/AccountingEntry.cs (Di chuy n)
- 3_CoreHub/VanAn.Accounting/Domain/AccountingBookType.cs (New)
- 3_CoreHub/VanAn.Accounting/Domain/AccountingPeriod.cs (New)
- 3_CoreHub/VanAn.Accounting/Domain/ValueObjects/AccountingEntryId.cs (New)
- 3_CoreHub/VanAn.Accounting/Domain/ValueObjects/AccountingBookTypeId.cs (New)
- 3_CoreHub/VanAn.Accounting/Domain/ValueObjects/AccountingPeriodId.cs (New)
- 3_CoreHub/VanAn.Accounting/Infrastructure/Configurations/AccountingEntryConfiguration.cs (New)
- 3_CoreHub/VanAn.Accounting/Infrastructure/Configurations/AccountingBookTypeConfiguration.cs (New)
- 3_CoreHub/VanAn.Accounting/Infrastructure/Configurations/AccountingPeriodConfiguration.cs (New)
- 3_CoreHub/VanAn.Accounting/Infrastructure/ValueConverters/AccountingEntryIdConverter.cs (New)
- 3_CoreHub/VanAn.Accounting/Infrastructure/ValueConverters/AccountingBookTypeIdConverter.cs (New)
- 3_CoreHub/VanAn.Accounting/Infrastructure/ValueConverters/AccountingPeriodIdConverter.cs (New)
- 3_CoreHub/VanAn.Accounting/Services/IAccountingService.cs (Di chuy n)
- 3_CoreHub/VanAn.Accounting/Services/AccountingService.cs (New)
- 3_CoreHub/VanAn.Accounting/Services/AccountingPeriodService.cs (New)
- 3_CoreHub/VanAn.Accounting/Tests/Unit/AccountingServiceTests.cs (New)
- 3_CoreHub/VanAn.Accounting/Tests/Unit/AccountingEntryTests.cs (New)
- 3_CoreHub/VanAn.Accounting/Tests/Integration/AccountingIntegrationTests.cs (New)
```

#### **FILES S THAY I:**
```yaml
Modified Files:
- VanAn.sln (Add VanAn.Accounting project)
- 1_Shared/Domain.cs (Di chuy n AccountingEntry sang namespace)
- 1_Shared/VanAn.Shared.csproj (Add reference to VanAn.Accounting)
- 3_CoreHub/VanAn.CoreHub.csproj (Add reference to VanAn.Accounting)
- 3_CoreHub/Infrastructure/VanAnDbContext.cs (Add Accounting DbSets)
- 3_CoreHub/Infrastructure/ValueConverters/ (Add Accounting converters)
- 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj (Add reference)
- 5_WebApps/ShopERP/Program.cs (Update DI registration)
- 5_WebApps/KhachLink/Program.cs (Update DI registration)
```

#### **FILES S X A:**
```yaml
Removed Files:
- 3_CoreHub/Services/IAccountingService.cs (Di chuy n)
- 3_CoreHub/Infrastructure/Configurations/AccountingEntryConfiguration.cs (n u có)
```

### **1.2 PHÂN TÍCH SÂU: SERVICE USAGE C A ACCOUNTINGENTRY**

#### **A. Current Service Usage Analysis**
```yaml
Services currently using AccountingEntry:

1. IAccountingService (3_CoreHub/Services/IAccountingService.cs):
   - Task<AccountingEntry> CreateEntryAsync(AccountingEntry entry)
   - Task<AccountingEntry> CreateReversalEntryAsync(Guid originalEntryId, string reason, Guid tenantId)
   - Task<IEnumerable<AccountingEntry>> GetEntriesByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
   - Impact: HIGH - Must move to VanAn.Accounting namespace

2. ShopERP IndexModel (5_WebApps/ShopERP/Pages/Index.cshtml.cs):
   - Current: Không s d ng IAccountingService (ch có placeholder)
   - Error log: "Unable to resolve service for type 'VanAn.CoreHub.Services.IAccountingService'"
   - Impact: MEDIUM - C n update DI registration

3. OrderService, InventoryService, OmnichannelOrderService:
   - Current: KHÔNG s d ng AccountingEntry tr c ti p
   - Impact: ZERO - Không c n thay i
```

#### **B. Service Dependency Changes After Migration**
```yaml
Before Migration:
- IAccountingService in VanAn.CoreHub.Services namespace
- ShopERP injects IAccountingService (fails - not registered)
- No implementation exists

After Migration:
- IAccountingService in VanAn.Accounting.Services namespace
- AccountingService implementation in VanAn.Accounting.Services
- DI registration in Program.cs (ShopERP, KhachLink)
- All services using AccountingEntry must update using statements

Specific Changes Required:
1. ShopERP/Pages/Index.cshtml.cs:
   - Add: using VanAn.Accounting.Services;
   - DI injection will work after registration

2. Any future services using AccountingEntry:
   - Update using statements to VanAn.Accounting.Services
   - Update namespace references in method signatures
```

### **1.3 T c ng n Domain hi n t i**

#### **A. Entity kh c (Order, Customer, Product)**
```yaml
Impact: MINIMAL
Reason:
- AccountingEntry hi n t i không có navigation property n Order/Customer/Product
- Ch có ReferenceId (Guid?) và ReferenceType (string)
- Không có foreign key constraint
- Không có cascade delete

Action: KHÔNG c n thay i các entity kh c
```

#### **B. Value Objects**
```yaml
Impact: MINIMAL
Current Value Objects:
- ProductId, IngredientId, RecipeId, InventoryId, OrderId, OrderStatusId, ShopId

Accounting Value Objects (c n thêm):
- AccountingEntryId (record)
- AccountingBookTypeId (record)
- AccountingPeriodId (record)

Action: Th m Value Objects m i, không i m hi n t i
```

#### **C. BaseEntity & IMustHaveTenant**
```yaml
Impact: ZERO IMPACT
Reason:
- AccountingEntry hi n t i extends BaseEntity
- BaseEntity có TenantId (IMustHaveTenant)
- Không thay i BaseEntity

Action: GI NG nguy n
```

### **1.4 T c ng n Multi-tenancy**

#### **A. IMustHaveTenant Compliance**
```yaml
Impact: POSITIVE
Current State:
- AccountingEntry extends BaseEntity
- BaseEntity implements IMustHaveTenant
- TenantId có s n trong AccountingEntry

New State:
- T t c accounting entities extends BaseEntity
- T t c có TenantId
- Multi-tenancy compliance 100%

Action: KHÔNG c n thay i, gi ng nguy n
```

#### **B. Query Filters & Data Isolation**
```yaml
Impact: ZERO IMPACT
Reason:
- VanAnDbContext có Global Query Filters cho IMustHaveTenant
- Accounting entities s t ng k th a
- Không c n thêm query filters

Action: GI NG nguy n
```

### **1.5 T c ng n Test Infrastructure**

#### **A. IntegrationTestBase**
```yaml
Impact: MINIMAL
Current State:
- IntegrationTestBase s d ng VanAnDbContext
- Có TestDbFactory và TestDbProvider
- H tr SQLite in-memory testing

New State:
- Accounting entities s t ng có trong test context
- Không c n thay i IntegrationTestBase
- TestDbFactory s t ng include Accounting entities

Action: KHÔNG c n thay i test infrastructure
```

#### **B. Existing Tests Impact**
```yaml
Current Tests:
- VanAn.Core.Tests có các test cho services
- Không có test cho AccountingEntry hi n t i

Impact Analysis:
1. Unit Tests:
   - Existing tests: No impact (không test AccountingEntry)
   - New tests: C n thêm AccountingServiceTests

2. Integration Tests:
   - IntegrationTestBase: No impact
   - Test data seeding: C n thêm Accounting entities
   - Database setup: No impact (t ng include)

3. E2E Tests:
   - No current E2E tests for Accounting
   - Future E2E tests: S d ng new structure

Action: Th m tests m i, không i m hi n t i
```

---

## **2. CHI TI T CÁCH ENFORCE IMMUTABILITY**

### **2.1 Immutable AccountingEntry Design**

#### **A. Constructor-Only Pattern**
```csharp
namespace VanAn.Shared.Domain.Accounting
{
    public class AccountingEntry : BaseEntity
    {
        // Immutable properties - private setters only
        public decimal Amount { get; private set; }
        public AccountingEntryType EntryType { get; private set; }
        public VatRate VatRate { get; private set; }
        public DateTime TransactionDate { get; private set; }
        public Guid? ReversalEntryId { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public Guid? ReferenceId { get; private set; }
        public string? ReferenceType { get; private set; }
        
        // Navigation properties - read-only
        public virtual AccountingEntry? OriginalEntry { get; private set; }
        public virtual ICollection<AccountingEntry> ReversalEntries { get; private set; } = new List<AccountingEntry>();
        
        // Private constructor for EF Core
        private AccountingEntry() { }
        
        // Public constructor for creation
        public AccountingEntry(
            decimal amount,
            AccountingEntryType entryType,
            VatRate vatRate,
            DateTime transactionDate,
            string description,
            Guid tenantId,
            Guid? referenceId = null,
            string? referenceType = null)
        {
            Amount = amount;
            EntryType = entryType;
            VatRate = vatRate;
            TransactionDate = transactionDate;
            Description = description;
            TenantId = tenantId;
            ReferenceId = referenceId;
            ReferenceType = referenceType;
            
            // Validate business rules
            ValidateCreation();
        }
        
        // Factory method for reversal entries
        public static AccountingEntry CreateReversal(
            AccountingEntry originalEntry,
            string reversalReason,
            Guid tenantId)
        {
            var reversalEntry = new AccountingEntry(
                -originalEntry.Amount, // Negative amount for reversal
                originalEntry.EntryType,
                originalEntry.VatRate,
                DateTime.UtcNow,
                reversalReason,
                tenantId,
                originalEntry.Id,
                nameof(AccountingEntry)
            );
            
            reversalEntry.ReversalEntryId = originalEntry.Id;
            
            return reversalEntry;
        }
        
        private void ValidateCreation()
        {
            if (Amount == 0)
                throw new ArgumentException("Amount cannot be zero");
                
            if (string.IsNullOrWhiteSpace(Description))
                throw new ArgumentException("Description is required");
                
            if (TenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required");
        }
        
        // No Update methods - IMMUTABLE
    }
}
```

#### **B. EF Core Configuration for Immutability**
```csharp
public class AccountingEntryConfiguration : IEntityTypeConfiguration<AccountingEntry>
{
    public void Configure(EntityTypeBuilder<AccountingEntry> builder)
    {
        builder.HasKey(e => e.Id);
        
        // Configure immutable properties
        builder.Property(e => e.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
            
        builder.Property(e => e.EntryType)
            .IsRequired();
            
        builder.Property(e => e.VatRate)
            .IsRequired();
            
        builder.Property(e => e.TransactionDate)
            .IsRequired();
            
        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);
            
        // Navigation properties
        builder.HasOne(e => e.OriginalEntry)
               .WithMany(e => e.ReversalEntries)
               .HasForeignKey(e => e.ReversalEntryId)
               .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete
               
        // Multi-tenancy
        builder.HasQueryFilter(e => !e.IsDeleted);
        
        // Indexes for performance
        builder.HasIndex(e => new { e.TenantId, e.TransactionDate });
        builder.HasIndex(e => e.ReversalEntryId);
        builder.HasIndex(e => new { e.ReferenceId, e.ReferenceType });
    }
}
```

### **2.2 Reversal Entry Enforcement**

#### **A. Service Layer Enforcement**
```csharp
public class AccountingService : IAccountingService
{
    private readonly VanAnDbContext _context;
    
    public async Task<AccountingEntry> CreateEntryAsync(AccountingEntry entry)
    {
        // Only allow creation through constructor
        // No update methods available
        _context.AccountingEntries.Add(entry);
        await _context.SaveChangesAsync();
        return entry;
    }
    
    public async Task<AccountingEntry> CreateReversalEntryAsync(
        Guid originalEntryId, 
        string reason, 
        Guid tenantId)
    {
        var originalEntry = await _context.AccountingEntries
            .FirstOrDefaultAsync(e => e.Id == originalEntryId && e.TenantId == tenantId);
            
        if (originalEntry == null)
            throw new ArgumentException("Original entry not found");
            
        // Use factory method for reversal
        var reversalEntry = AccountingEntry.CreateReversal(originalEntry, reason, tenantId);
        
        return await CreateEntryAsync(reversalEntry);
    }
    
    // NO UPDATE METHODS - IMMUTABLE
}
```

#### **B. Database Constraints**
```sql
-- Add check constraints to prevent direct updates
ALTER TABLE AccountingEntries 
ADD CONSTRAINT CK_AccountingEntries_NoDirectUpdate 
CHECK (CreatedAt = UpdatedAt);

-- Trigger to prevent updates (optional)
CREATE TRIGGER TR_AccountingEntries_PreventUpdate
ON AccountingEntries
INSTEAD OF UPDATE
AS
BEGIN
    RAISERROR('Direct updates to AccountingEntries are not allowed. Use reversal entries instead.', 16, 1);
END;
```

### **2.3 Validation Rules**
```csharp
public class AccountingEntryValidator
{
    public static void ValidateImmutable(AccountingEntry entry)
    {
        // Ensure no property changes after creation
        if (entry.CreatedAt != entry.UpdatedAt)
            throw new InvalidOperationException("AccountingEntry cannot be modified after creation");
    }
    
    public static void ValidateReversal(AccountingEntry original, AccountingEntry reversal)
    {
        if (original.Amount + reversal.Amount != 0)
            throw new InvalidOperationException("Reversal amount must negate original amount");
            
        if (original.EntryType != reversal.EntryType)
            throw new InvalidOperationException("Reversal entry type must match original");
            
        if (original.TenantId != reversal.TenantId)
            throw new InvalidOperationException("Reversal tenant must match original");
    }
}
```

---

## **3. C TRÚC FOLDER VANAN.ACCOUNTING CHI TI T**

### **3.1 Project Structure**
```
3_CoreHub/VanAn.Accounting/
  VanAn.Accounting.csproj
  README.md
  
  Domain/
    AccountingEntry.cs
    AccountingBookType.cs
    AccountingPeriod.cs
    ValueObjects/
      AccountingEntryId.cs
      AccountingBookTypeId.cs
      AccountingPeriodId.cs
    Events/
      AccountingEntryCreatedEvent.cs
      AccountingEntryReversedEvent.cs
    Exceptions/
      AccountingEntryNotFoundException.cs
      AccountingEntryImmutableException.cs
    
  Application/
    DTOs/
      CreateAccountingEntryDto.cs
      AccountingEntryDto.cs
      AccountingEntryFilterDto.cs
    Queries/
      GetAccountingEntriesQuery.cs
      GetAccountingEntryByIdQuery.cs
    Commands/
      CreateAccountingEntryCommand.cs
      CreateReversalEntryCommand.cs
      CloseAccountingPeriodCommand.cs
    Handlers/
      CreateAccountingEntryCommandHandler.cs
      CreateReversalEntryCommandHandler.cs
      CloseAccountingPeriodCommandHandler.cs
    Interfaces/
      IAccountingQueryService.cs
      IAccountingCommandService.cs
    
  Infrastructure/
    Configurations/
      AccountingEntryConfiguration.cs
      AccountingBookTypeConfiguration.cs
      AccountingPeriodConfiguration.cs
    ValueConverters/
      AccountingEntryIdConverter.cs
      AccountingBookTypeIdConverter.cs
      AccountingPeriodIdConverter.cs
    Repositories/
      IAccountingRepository.cs
      AccountingRepository.cs
    Persistence/
      AccountingDbContext.cs (if separate from main)
    
  Services/
    IAccountingService.cs
    AccountingService.cs
    IAccountingPeriodService.cs
    AccountingPeriodService.cs
    IAccountingReportService.cs
    AccountingReportService.cs
    
  Tests/
    Unit/
      AccountingServiceTests.cs
      AccountingEntryTests.cs
      AccountingPeriodServiceTests.cs
    Integration/
      AccountingRepositoryTests.cs
      AccountingServiceIntegrationTests.cs
    Fixtures/
      AccountingEntryFixture.cs
      AccountingTestData.cs
```

### **3.2 Namespace Organization**
```csharp
// Root namespace
namespace VanAn.Accounting

// Domain layer
namespace VanAn.Accounting.Domain
namespace VanAn.Accounting.Domain.ValueObjects
namespace VanAn.Accounting.Domain.Events
namespace VanAn.Accounting.Domain.Exceptions

// Application layer
namespace VanAn.Accounting.Application.DTOs
namespace VanAn.Accounting.Application.Queries
namespace VanAn.Accounting.Application.Commands
namespace VanAn.Accounting.Application.Handlers
namespace VanAn.Accounting.Application.Interfaces

// Infrastructure layer
namespace VanAn.Accounting.Infrastructure.Configurations
namespace VanAn.Accounting.Infrastructure.ValueConverters
namespace VanAn.Accounting.Infrastructure.Repositories
namespace VanAn.Accounting.Infrastructure.Persistence

// Service layer
namespace VanAn.Accounting.Services

// Test layer
namespace VanAn.Accounting.Tests.Unit
namespace VanAn.Accounting.Tests.Integration
namespace VanAn.Accounting.Tests.Fixtures
```

---

## **4. K HO N C P NH T DEPENDENCY INJECTION**

### **4.1 Current DI Issues**
```yaml
Current Problem:
- ShopERP/Pages/Index.cshtml.cs tries to inject IAccountingService
- IAccountingService not registered in DI container
- Error: "Unable to resolve service for type 'VanAn.CoreHub.Services.IAccountingService'"
```

### **4.2 DI Registration Strategy**

#### **A. CoreHub Program.cs (Backend Services)**
```csharp
// 3_CoreHub/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register Accounting Services
builder.Services.AddScoped<IAccountingService, AccountingService>();
builder.Services.AddScoped<IAccountingPeriodService, AccountingPeriodService>();
builder.Services.AddScoped<IAccountingReportService, AccountingReportService>();

// Register Repositories
builder.Services.AddScoped<IAccountingRepository, AccountingRepository>();

// Register Query/Command Handlers
builder.Services.AddScoped<IRequestHandler<CreateAccountingEntryCommand, AccountingEntryDto>, 
                         CreateAccountingEntryCommandHandler>();
builder.Services.AddScoped<IRequestHandler<CreateReversalEntryCommand, AccountingEntryDto>, 
                         CreateReversalEntryCommandHandler>();

// Register DbContext with Accounting entities
builder.Services.AddDbContext<VanAnDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseSqlite(builder.Configuration.GetConnectionString("LocalConnection"));
});
```

#### **B. ShopERP Program.cs (Frontend)**
```csharp
// 5_WebApps/ShopERP/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();

// Register Accounting Services (from VanAn.Accounting)
builder.Services.AddScoped<IAccountingService, AccountingService>();

// Register CoreHub services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();

// Configure authentication
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth");

var app = builder.Build();

// Configure middleware
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
```

#### **C. KhachLink Program.cs (Frontend)**
```csharp
// 5_WebApps/KhachLink/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();

// Register Accounting Services (from VanAn.Accounting)
builder.Services.AddScoped<IAccountingService, AccountingService>();

// Register other services
builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
builder.Services.AddScoped<IShopConfigService, ShopConfigService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
```

### **4.3 DI Registration Best Practices**
```yaml
1. Service Lifetime:
   - Services: Scoped (per request)
   - Repositories: Scoped
   - DbContext: Scoped
   - Configuration: Singleton

2. Registration Order:
   - Infrastructure first (DbContext, Repositories)
   - Services second
   - Controllers/Pages last

3. Naming Convention:
   - Interface: I[ServiceName]
   - Implementation: [ServiceName]
   - Namespace: VanAn.Accounting.Services

4. Validation:
   - All services registered before startup
   - No circular dependencies
   - Proper lifetime management
```

---

## **5. K HO N TEST SAU KHI DI CHUY N**

### **5.1 Existing Tests Impact Analysis**

#### **A. Current Test Status**
```yaml
VanAn.Core.Tests:
- OrderServiceTests: No impact (không s d ng AccountingEntry)
- InventoryServiceTests: No impact (không s d ng AccountingEntry)
- OmnichannelOrderServiceTests: No impact (không s d ng AccountingEntry)
- IntegrationTestBase: No impact (t ng include Accounting entities)

Impact: ZERO - Existing tests continue to work
```

#### **B. Test Infrastructure Updates**
```yaml
Required Updates:
1. VanAn.Core.Tests.csproj:
   - Add reference to VanAn.Accounting
   - Add using statements for new namespaces

2. TestDbFactory:
   - No changes needed (t ng include Accounting entities)
   - Test data seeding: Add Accounting entries

3. IntegrationTestBase:
   - No changes needed
   - Continue to work with Accounting entities
```

### **5.2 New Tests Required**

#### **A. Unit Tests**
```csharp
// VanAn.Accounting/Tests/Unit/AccountingServiceTests.cs
public class AccountingServiceTests
{
    [Fact]
    public async Task CreateEntry_ShouldCreateImmutableEntry()
    {
        // Arrange
        var entry = new AccountingEntry(
            1000m, AccountingEntryType.Revenue, VatRate.Ten, 
            DateTime.UtcNow, "Test revenue", tenantId);
        
        // Act
        var result = await _service.CreateEntryAsync(entry);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(1000m, result.Amount);
        Assert.Equal(AccountingEntryType.Revenue, result.EntryType);
    }
    
    [Fact]
    public async Task CreateReversalEntry_ShouldCreateCorrectReversal()
    {
        // Arrange
        var original = await CreateTestEntry();
        
        // Act
        var reversal = await _service.CreateReversalEntryAsync(
            original.Id, "Test reversal", tenantId);
        
        // Assert
        Assert.Equal(-original.Amount, reversal.Amount);
        Assert.Equal(original.Id, reversal.ReversalEntryId);
    }
    
    [Fact]
    public void AccountingEntry_ShouldBeImmutable()
    {
        // Arrange
        var entry = new AccountingEntry(
            1000m, AccountingEntryType.Revenue, VatRate.Ten,
            DateTime.UtcNow, "Test", tenantId);
        
        // Act & Assert
        // No public setters available - compile error if trying to modify
        // entry.Amount = 2000m; // This should not compile
    }
}
```

#### **B. Integration Tests**
```csharp
// VanAn.Accounting/Tests/Integration/AccountingRepositoryTests.cs
public class AccountingRepositoryTests : IntegrationTestBase
{
    [Fact]
    public async Task AddAccountingEntry_ShouldPersistToDatabase()
    {
        // Arrange
        var entry = new AccountingEntry(
            1000m, AccountingEntryType.Revenue, VatRate.Ten,
            DateTime.UtcNow, "Test revenue", tenantId);
        
        // Act
        await _repository.AddAsync(entry);
        await _context.SaveChangesAsync();
        
        // Assert
        var saved = await _context.AccountingEntries
            .FirstOrDefaultAsync(e => e.Id == entry.Id);
        Assert.NotNull(saved);
        Assert.Equal(entry.Amount, saved.Amount);
    }
    
    [Fact]
    public async Task GetEntriesByTenant_ShouldReturnOnlyTenantEntries()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();
        
        await CreateTestEntry(tenant1Id);
        await CreateTestEntry(tenant2Id);
        
        // Act
        var tenant1Entries = await _repository.GetByTenantAsync(tenant1Id);
        var tenant2Entries = await _repository.GetByTenantAsync(tenant2Id);
        
        // Assert
        Assert.Single(tenant1Entries);
        Assert.Single(tenant2Entries);
        Assert.NotEqual(tenant1Entries.First().Id, tenant2Entries.First().Id);
    }
}
```

#### **C. Domain Tests**
```csharp
// VanAn.Accounting/Tests/Unit/AccountingEntryTests.cs
public class AccountingEntryTests
{
    [Fact]
    public void Constructor_ShouldValidateRequiredFields()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new AccountingEntry(0, AccountingEntryType.Revenue, VatRate.Ten,
                DateTime.UtcNow, "Test", Guid.NewGuid()));
                
        Assert.Throws<ArgumentException>(() => 
            new AccountingEntry(1000m, AccountingEntryType.Revenue, VatRate.Ten,
                DateTime.UtcNow, "", Guid.NewGuid()));
                
        Assert.Throws<ArgumentException>(() => 
            new AccountingEntry(1000m, AccountingEntryType.Revenue, VatRate.Ten,
                DateTime.UtcNow, "Test", Guid.Empty));
    }
    
    [Fact]
    public void CreateReversal_ShouldCreateCorrectReversalEntry()
    {
        // Arrange
        var original = new AccountingEntry(
            1000m, AccountingEntryType.Revenue, VatRate.Ten,
            DateTime.UtcNow, "Original", Guid.NewGuid());
        
        // Act
        var reversal = AccountingEntry.CreateReversal(original, "Reversal", original.TenantId);
        
        // Assert
        Assert.Equal(-1000m, reversal.Amount);
        Assert.Equal(original.Id, reversal.ReversalEntryId);
        Assert.Equal(original.TenantId, reversal.TenantId);
        Assert.Equal("Reversal", reversal.Description);
    }
}
```

### **5.3 Test Data Fixtures**
```csharp
// VanAn.Accounting/Tests/Fixtures/AccountingEntryFixture.cs
public class AccountingEntryFixture
{
    public static AccountingEntry CreateRevenueEntry(decimal amount = 1000m, Guid? tenantId = null)
    {
        return new AccountingEntry(
            amount,
            AccountingEntryType.Revenue,
            VatRate.Ten,
            DateTime.UtcNow,
            "Test revenue",
            tenantId ?? Guid.NewGuid());
    }
    
    public static AccountingEntry CreateExpenseEntry(decimal amount = 500m, Guid? tenantId = null)
    {
        return new AccountingEntry(
            amount,
            AccountingEntryType.Expense,
            VatRate.Zero,
            DateTime.UtcNow,
            "Test expense",
            tenantId ?? Guid.NewGuid());
    }
    
    public static List<AccountingEntry> CreateTestEntries(int count = 5, Guid? tenantId = null)
    {
        var entries = new List<AccountingEntry>();
        var tenant = tenantId ?? Guid.NewGuid();
        
        for (int i = 0; i < count; i++)
        {
            entries.Add(CreateRevenueEntry(1000m * (i + 1), tenant));
            entries.Add(CreateExpenseEntry(500m * (i + 1), tenant));
        }
        
        return entries;
    }
}
```

### **5.4 Test Execution Plan**
```yaml
Phase 1: Unit Tests (Day 1)
- AccountingServiceTests
- AccountingEntryTests
- AccountingPeriodServiceTests

Phase 2: Integration Tests (Day 2)
- AccountingRepositoryTests
- AccountingServiceIntegrationTests
- Multi-tenancy Tests

Phase 3: E2E Tests (Week 2)
- Order to Accounting Flow Tests
- Reversal Entry Flow Tests
- Report Generation Tests

Test Coverage Target:
- Domain Layer: 90%+
- Service Layer: 85%+
- Repository Layer: 80%+
```

---

## **6. R I RO PHÁT SINH (UPDATED)**

### **6.1 HIGH RISK**

#### **A. Namespace Conflicts**
```yaml
Risk: Namespace conflicts khi di chuy n AccountingEntry
Probability: MEDIUM (30%)
Impact: MEDIUM
Mitigation:
- Ki m tra t t c using statements
- Update namespace trong các files liên quan
- Run build sau khi di chuy n
- Use global using directives for common namespaces
```

#### **B. Service Registration Issues**
```yaml
Risk: DI registration failures sau khi di chuy n
Probability: HIGH (60%)
Impact: HIGH
Mitigation:
- Update DI registration in all Program.cs files
- Test service resolution after registration
- Use Scrutor for assembly scanning
- Document all service registrations
```

### **6.2 MEDIUM RISK**

#### **A. Immutability Enforcement**
```yaml
Risk: Developers bypass immutability through reflection or EF Core
Probability: MEDIUM (40%)
Impact: MEDIUM
Mitigation:
- Private setters in domain model
- EF Core configuration for immutability
- Database constraints and triggers
- Code review guidelines
```

#### **B. Test Infrastructure Compatibility**
```yaml
Risk: Tests không compatible v i Accounting entities m i
Probability: LOW (15%)
Impact: MEDIUM
Mitigation:
- Run existing tests sau khi thêm Accounting entities
- Ki tra TestDbFactory compatibility
- Add Accounting-specific test data
- Update test fixtures
```

### **6.3 LOW RISK**

#### **A. Performance Impact**
```yaml
Risk: Performance degradation do thêm project và entities
Probability: LOW (10%)
Impact: LOW
Mitigation:
- Monitor build time
- Optimize project references
- Accept minor performance impact
- Profile database queries
```

---

## **7. DETAILED IMPLEMENTATION PLAN (UPDATED)**

### **7.1 Phase 1: Project Setup (20 minutes)**

#### **Step 1.1: Create VanAn.Accounting Project**
```bash
# T o project m i
cd 3_CoreHub
dotnet new classlib -n VanAn.Accounting -f net8.0

# Add project to solution
cd ..
dotnet sln add 3_CoreHub/VanAn.Accounting/VanAn.Accounting.csproj

# Create folder structure
mkdir -p 3_CoreHub/VanAn.Accounting/Domain/ValueObjects
mkdir -p 3_CoreHub/VanAn.Accounting/Domain/Events
mkdir -p 3_CoreHub/VanAn.Accounting/Domain/Exceptions
mkdir -p 3_CoreHub/VanAn.Accounting/Application/DTOs
mkdir -p 3_CoreHub/VanAn.Accounting/Application/Queries
mkdir -p 3_CoreHub/VanAn.Accounting/Application/Commands
mkdir -p 3_CoreHub/VanAn.Accounting/Application/Handlers
mkdir -p 3_CoreHub/VanAn.Accounting/Infrastructure/Configurations
mkdir -p 3_CoreHub/VanAn.Accounting/Infrastructure/ValueConverters
mkdir -p 3_CoreHub/VanAn.Accounting/Infrastructure/Repositories
mkdir -p 3_CoreHub/VanAn.Accounting/Services
mkdir -p 3_CoreHub/VanAn.Accounting/Tests/Unit
mkdir -p 3_CoreHub/VanAn.Accounting/Tests/Integration
mkdir -p 3_CoreHub/VanAn.Accounting/Tests/Fixtures
```

#### **Step 1.2: Setup Project References**
```xml
<!-- VanAn.Accounting.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\..\1_Shared\VanAn.Shared.csproj" />
  </ItemGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
    <PackageReference Include="MediatR" Version="12.0.0" />
    <PackageReference Include="FluentValidation" Version="11.0.0" />
    <PackageReference Include="xunit" Version="2.4.0" />
    <PackageReference Include="Moq" Version="4.20.0" />
  </ItemGroup>
</Project>
```

### **7.2 Phase 2: Domain Migration (45 minutes)**

#### **Step 2.1: Create Immutable AccountingEntry**
```csharp
// VanAn.Accounting/Domain/AccountingEntry.cs
namespace VanAn.Accounting.Domain;

public class AccountingEntry : BaseEntity
{
    // Immutable properties
    public decimal Amount { get; private set; }
    public AccountingEntryType EntryType { get; private set; }
    public VatRate VatRate { get; private set; }
    public DateTime TransactionDate { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? ReferenceId { get; private set; }
    public string? ReferenceType { get; private set; }
    
    // Accounting-specific properties
    public AccountingBookType BookType { get; private set; }
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public Guid? ReversalEntryId { get; private set; }
    
    // Navigation properties
    public virtual AccountingEntry? OriginalEntry { get; private set; }
    public virtual ICollection<AccountingEntry> ReversalEntries { get; private set; } = new List<AccountingEntry>();
    
    // Private constructor for EF Core
    private AccountingEntry() { }
    
    // Public constructor for creation
    public AccountingEntry(
        decimal amount,
        AccountingEntryType entryType,
        VatRate vatRate,
        AccountingBookType bookType,
        int periodYear,
        int periodMonth,
        string description,
        Guid tenantId,
        DateTime? transactionDate = null,
        Guid? referenceId = null,
        string? referenceType = null)
    {
        Amount = amount;
        EntryType = entryType;
        VatRate = vatRate;
        BookType = bookType;
        PeriodYear = periodYear;
        PeriodMonth = periodMonth;
        Description = description;
        TenantId = tenantId;
        TransactionDate = transactionDate ?? DateTime.UtcNow;
        ReferenceId = referenceId;
        ReferenceType = referenceType;
        
        ValidateCreation();
    }
    
    // Factory method for reversal entries
    public static AccountingEntry CreateReversal(
        AccountingEntry originalEntry,
        string reversalReason,
        Guid tenantId)
    {
        var reversalEntry = new AccountingEntry(
            -originalEntry.Amount,
            originalEntry.EntryType,
            originalEntry.VatRate,
            originalEntry.BookType,
            originalEntry.PeriodYear,
            originalEntry.PeriodMonth,
            reversalReason,
            tenantId,
            DateTime.UtcNow,
            originalEntry.Id,
            nameof(AccountingEntry)
        );
        
        reversalEntry.ReversalEntryId = originalEntry.Id;
        
        return reversalEntry;
    }
    
    private void ValidateCreation()
    {
        if (Amount == 0)
            throw new ArgumentException("Amount cannot be zero");
            
        if (string.IsNullOrWhiteSpace(Description))
            throw new ArgumentException("Description is required");
            
        if (TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required");
            
        if (PeriodYear < 2000 || PeriodYear > 2100)
            throw new ArgumentException("Invalid period year");
            
        if (PeriodMonth < 1 || PeriodMonth > 12)
            throw new ArgumentException("Invalid period month");
    }
}
```

#### **Step 2.2: Add Accounting Support Entities**
```csharp
// VanAn.Accounting/Domain/AccountingBookType.cs
namespace VanAn.Accounting.Domain;

public class AccountingBookType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

// VanAn.Accounting/Domain/AccountingPeriod.cs
namespace VanAn.Accounting.Domain;

public class AccountingPeriod : BaseEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsClosed { get; set; } = false;
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public string? ClosingNotes { get; set; }
}
```

#### **Step 2.3: Add Value Objects**
```csharp
// VanAn.Accounting/Domain/ValueObjects/AccountingEntryId.cs
namespace VanAn.Accounting.Domain.ValueObjects;

public record AccountingEntryId(Guid Value)
{
    public static AccountingEntryId New() => new(Guid.NewGuid());
    public static AccountingEntryId From(Guid value) => new(value);
}

// VanAn.Accounting/Domain/ValueObjects/AccountingBookTypeId.cs
namespace VanAn.Accounting.Domain.ValueObjects;

public record AccountingBookTypeId(Guid Value)
{
    public static AccountingBookTypeId New() => new(Guid.NewGuid());
    public static AccountingBookTypeId From(Guid value) => new(value);
}

// VanAn.Accounting/Domain/ValueObjects/AccountingPeriodId.cs
namespace VanAn.Accounting.Domain.ValueObjects;

public record AccountingPeriodId(Guid Value)
{
    public static AccountingPeriodId New() => new(Guid.NewGuid());
    public static AccountingPeriodId From(Guid value) => new(value);
}
```

### **7.3 Phase 3: Infrastructure Setup (45 minutes)**

#### **Step 3.1: EF Core Configurations**
```csharp
// VanAn.Accounting/Infrastructure/Configurations/AccountingEntryConfiguration.cs
namespace VanAn.Accounting.Infrastructure.Configurations;

public class AccountingEntryConfiguration : IEntityTypeConfiguration<AccountingEntry>
{
    public void Configure(EntityTypeBuilder<AccountingEntry> builder)
    {
        builder.HasKey(e => e.Id);
        
        // Configure immutable properties
        builder.Property(e => e.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();
            
        builder.Property(e => e.EntryType)
            .HasConversion<int>()
            .IsRequired();
            
        builder.Property(e => e.VatRate)
            .HasConversion<int>()
            .IsRequired();
            
        builder.Property(e => e.BookType)
            .HasConversion<int>()
            .IsRequired();
            
        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);
            
        builder.Property(e => e.ReferenceType)
            .HasMaxLength(100);
            
        // Navigation properties
        builder.HasOne(e => e.OriginalEntry)
               .WithMany(e => e.ReversalEntries)
               .HasForeignKey(e => e.ReversalEntryId)
               .OnDelete(DeleteBehavior.Restrict);
               
        // Multi-tenancy
        builder.HasQueryFilter(e => !e.IsDeleted);
        
        // Indexes for performance
        builder.HasIndex(e => new { e.TenantId, e.TransactionDate });
        builder.HasIndex(e => e.ReversalEntryId);
        builder.HasIndex(e => new { e.ReferenceId, e.ReferenceType });
        builder.HasIndex(e => new { e.BookType, e.PeriodYear, e.PeriodMonth });
    }
}
```

#### **Step 3.2: Value Converters**
```csharp
// VanAn.Accounting/Infrastructure/ValueConverters/AccountingEntryIdConverter.cs
namespace VanAn.Accounting.Infrastructure.ValueConverters;

public class AccountingEntryIdConverter : ValueConverter<AccountingEntryId, Guid>
{
    public AccountingEntryIdConverter()
        : base(
            v => v.Value,
            v => new AccountingEntryId(v))
    {
    }
}

// VanAn.Accounting/Infrastructure/ValueConverters/AccountingBookTypeIdConverter.cs
namespace VanAn.Accounting.Infrastructure.ValueConverters;

public class AccountingBookTypeIdConverter : ValueConverter<AccountingBookTypeId, Guid>
{
    public AccountingBookTypeIdConverter()
        : base(
            v => v.Value,
            v => new AccountingBookTypeId(v))
    {
    }
}

// VanAn.Accounting/Infrastructure/ValueConverters/AccountingPeriodIdConverter.cs
namespace VanAn.Accounting.Infrastructure.ValueConverters;

public class AccountingPeriodIdConverter : ValueConverter<AccountingPeriodId, Guid>
{
    public AccountingPeriodIdConverter()
        : base(
            v => v.Value,
            v => new AccountingPeriodId(v))
    {
    }
}
```

#### **Step 3.3: Update VanAnDbContext**
```csharp
// 3_CoreHub/Infrastructure/VanAnDbContext.cs
public class VanAnDbContext : DbContext
{
    // Existing DbSets...
    
    // Accounting DbSets
    public DbSet<AccountingEntry> AccountingEntries { get; set; }
    public DbSet<AccountingBookType> AccountingBookTypes { get; set; }
    public DbSet<AccountingPeriod> AccountingPeriods { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply existing configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VanAnDbContext).Assembly);
        
        // Apply accounting configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccountingEntryConfiguration).Assembly);
    }
    
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Existing converters...
        
        // Accounting value converters
        configurationBuilder.Properties<AccountingEntryId>()
            .HaveConversion<AccountingEntryIdConverter>();
            
        configurationBuilder.Properties<AccountingBookTypeId>()
            .HaveConversion<AccountingBookTypeIdConverter>();
            
        configurationBuilder.Properties<AccountingPeriodId>()
            .HaveConversion<AccountingPeriodIdConverter>();
    }
}
```

### **7.4 Phase 4: Service Layer (45 minutes)**

#### **Step 4.1: Move and Update IAccountingService**
```csharp
// VanAn.Accounting/Services/IAccountingService.cs
namespace VanAn.Accounting.Services;

public interface IAccountingService
{
    Task<AccountingEntry> CreateEntryAsync(AccountingEntry entry);
    Task<AccountingEntry> CreateReversalEntryAsync(Guid originalEntryId, string reason, Guid tenantId);
    Task<IEnumerable<AccountingEntry>> GetEntriesByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<AccountingEntry>> GetEntriesByBookTypeAsync(Guid tenantId, AccountingBookType bookType, int year, int month);
    Task<decimal> GetRevenueAsync(Guid tenantId, DateTime startDate, DateTime endDate);
    Task<decimal> GetExpensesAsync(Guid tenantId, DateTime startDate, DateTime endDate);
    Task<decimal> CalculateVatAsync(decimal amount, VatRate vatRate);
}
```

#### **Step 4.2: Implement AccountingService**
```csharp
// VanAn.Accounting/Services/AccountingService.cs
namespace VanAn.Accounting.Services;

public class AccountingService : IAccountingService
{
    private readonly VanAnDbContext _context;
    private readonly ILogger<AccountingService> _logger;
    
    public AccountingService(VanAnDbContext context, ILogger<AccountingService> logger)
    {
        _context = context;
        _logger = logger;
    }
    
    public async Task<AccountingEntry> CreateEntryAsync(AccountingEntry entry)
    {
        try
        {
            _context.AccountingEntries.Add(entry);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Created accounting entry {EntryId} for tenant {TenantId}", 
                entry.Id, entry.TenantId);
                
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating accounting entry for tenant {TenantId}", entry.TenantId);
            throw;
        }
    }
    
    public async Task<AccountingEntry> CreateReversalEntryAsync(Guid originalEntryId, string reason, Guid tenantId)
    {
        var originalEntry = await _context.AccountingEntries
            .FirstOrDefaultAsync(e => e.Id == originalEntryId && e.TenantId == tenantId);
            
        if (originalEntry == null)
            throw new ArgumentException("Original entry not found");
            
        var reversalEntry = AccountingEntry.CreateReversal(originalEntry, reason, tenantId);
        
        return await CreateEntryAsync(reversalEntry);
    }
    
    public async Task<IEnumerable<AccountingEntry>> GetEntriesByDateRangeAsync(Guid tenantId, DateTime startDate, DateTime endDate)
    {
        return await _context.AccountingEntries
            .Where(e => e.TenantId == tenantId && 
                       e.TransactionDate >= startDate && 
                       e.TransactionDate <= endDate &&
                       !e.IsDeleted)
            .OrderByDescending(e => e.TransactionDate)
            .ToListAsync();
    }
    
    public async Task<decimal> GetRevenueAsync(Guid tenantId, DateTime startDate, DateTime endDate)
    {
        return await _context.AccountingEntries
            .Where(e => e.TenantId == tenantId && 
                       e.EntryType == AccountingEntryType.Revenue &&
                       e.TransactionDate >= startDate && 
                       e.TransactionDate <= endDate &&
                       !e.IsDeleted)
            .SumAsync(e => e.Amount);
    }
    
    public async Task<decimal> GetExpensesAsync(Guid tenantId, DateTime startDate, DateTime endDate)
    {
        return await _context.AccountingEntries
            .Where(e => e.TenantId == tenantId && 
                       e.EntryType == AccountingEntryType.Expense &&
                       e.TransactionDate >= startDate && 
                       e.TransactionDate <= endDate &&
                       !e.IsDeleted)
            .SumAsync(e => e.Amount);
    }
    
    public decimal CalculateVatAsync(decimal amount, VatRate vatRate)
    {
        return amount * (decimal)vatRate / 100;
    }
    
    public async Task<IEnumerable<AccountingEntry>> GetEntriesByBookTypeAsync(Guid tenantId, AccountingBookType bookType, int year, int month)
    {
        return await _context.AccountingEntries
            .Where(e => e.TenantId == tenantId && 
                       e.BookType == bookType &&
                       e.PeriodYear == year &&
                       e.PeriodMonth == month &&
                       !e.IsDeleted)
            .OrderByDescending(e => e.TransactionDate)
            .ToListAsync();
    }
}
```

### **7.5 Phase 5: Project References Update (20 minutes)**

#### **Step 5.1: Update Solution**
```xml
<!-- VanAn.sln -->
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "VanAn.Accounting", "3_CoreHub\VanAn.Accounting\VanAn.Accounting.csproj", "{NEW-GUID}"
EndProject
```

#### **Step 5.2: Update CoreHub References**
```xml
<!-- 3_CoreHub/VanAn.CoreHub.csproj -->
<ItemGroup>
  <ProjectReference Include="..\1_Shared\VanAn.Shared.csproj" />
  <ProjectReference Include="VanAn.Accounting\VanAn.Accounting.csproj" />
</ItemGroup>
```

#### **Step 5.3: Update Test References**
```xml
<!-- 6_Tests/VanAn.Core.Tests/VanAn.Core.Tests.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\3_CoreHub\VanAn.Accounting\VanAn.Accounting.csproj" />
</ItemGroup>
```

### **7.6 Phase 6: Dependency Injection Update (20 minutes)**

#### **Step 6.1: Update CoreHub Program.cs**
```csharp
// 3_CoreHub/Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register Accounting Services
builder.Services.AddScoped<IAccountingService, AccountingService>();

// Register DbContext
builder.Services.AddDbContext<VanAnDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseSqlite(builder.Configuration.GetConnectionString("LocalConnection"));
});

var app = builder.Build();
app.Run();
```

#### **Step 6.2: Update ShopERP Program.cs**
```csharp
// 5_WebApps/ShopERP/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Register Accounting Services
builder.Services.AddScoped<IAccountingService, AccountingService>();

// Register other services
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();
```

### **7.7 Phase 7: Validation & Testing (30 minutes)**

#### **Step 7.1: Build Validation**
```bash
# Build entire solution
dotnet build VanAn.sln

# Check for errors
# Should be 0 errors
```

#### **Step 7.2: Run Existing Tests**
```bash
# Run existing tests to ensure no regression
dotnet test 6_Tests/VanAn.Core.Tests/

# All tests should pass
```

#### **Step 7.3: windsurf-guard Validation**
```bash
# Run windsurf-guard
node windsurf-guard.js

# Should pass all checks
```

#### **Step 7.4: Create Basic Tests**
```csharp
// VanAn.Accounting/Tests/Unit/AccountingServiceTests.cs
public class AccountingServiceTests
{
    [Fact]
    public async Task CreateEntry_ShouldCreateImmutableEntry()
    {
        // Arrange
        var entry = new AccountingEntry(
            1000m, AccountingEntryType.Revenue, VatRate.Ten,
            AccountingBookType.RevenueBook, 2026, 4,
            "Test revenue", tenantId);
        
        // Act
        var result = await _service.CreateEntryAsync(entry);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(1000m, result.Amount);
        Assert.Equal(AccountingEntryType.Revenue, result.EntryType);
    }
    
    [Fact]
    public void AccountingEntry_ShouldBeImmutable()
    {
        // Arrange
        var entry = new AccountingEntry(
            1000m, AccountingEntryType.Revenue, VatRate.Ten,
            AccountingBookType.RevenueBook, 2026, 4,
            "Test", tenantId);
        
        // Act & Assert
        // No public setters available - compile error if trying to modify
        // entry.Amount = 2000m; // This should not compile
    }
}
```

---

## **8. SUCCESS CRITERIA (UPDATED)**

### **8.1 Technical Success**
```yaml
Build Status:
- [ ] dotnet build passes (0 errors)
- [ ] No circular references
- [ ] All project references valid

Test Status:
- [ ] Existing tests pass
- [ ] New Accounting tests compile
- [ ] IntegrationTestBase works with Accounting entities

Architecture Compliance:
- [ ] windsurf-guard.js passes
- [ ] .windsurfrules compliance
- [ ] Domain purity maintained
- [ ] Multi-tenancy enforced
- [ ] Immutability enforced
```

### **8.2 Functional Success**
```yaml
Domain Model:
- [ ] AccountingEntry in correct namespace
- [ ] All accounting entities inherit BaseEntity
- [ ] Value Objects properly defined
- [ ] No duplicate definitions
- [ ] Immutability enforced

Infrastructure:
- [ ] EF Core configurations working
- [ ] Value converters implemented
- [ ] VanAnDbContext updated
- [ ] No migration conflicts

Services:
- [ ] IAccountingService moved correctly
- [ ] AccountingService implemented
- [ ] Dependency injection working
- [ ] Service registration valid
- [ ] ShopERP DI issue resolved

DI Registration:
- [ ] CoreHub Program.cs updated
- [ ] ShopERP Program.cs updated
- [ ] KhachLink Program.cs updated
- [ ] Service resolution working
```

---

## **9. ROLLBACK PLAN (UPDATED)**

### **9.1 Immediate Rollback**
```bash
# Git rollback
git checkout task-1.1-cleanup-backup
git checkout main --force

# Remove project files
rm -rf 3_CoreHub/VanAn.Accounting

# Restore Domain.cs
git checkout HEAD -- 1_Shared/Domain.cs

# Restore Program.cs files
git checkout HEAD -- 5_WebApps/ShopERP/Program.cs
git checkout HEAD -- 5_WebApps/KhachLink/Program.cs
```

### **9.2 Selective Rollback**
```bash
# Rollback specific changes
git checkout HEAD -- VanAn.sln
git checkout HEAD -- 3_CoreHub/VanAn.CoreHub.csproj
git checkout HEAD -- 1_Shared/Domain.cs
git checkout HEAD -- 5_WebApps/ShopERP/Program.cs
```

### **9.3 Database Rollback**
```bash
# Remove migration (n u có t o)
dotnet ef database update previous-migration
dotnet ef migrations remove
```

---

## **10. ESTIMATED TIMELINE (UPDATED)**

```yaml
Total Estimated Time: 3.5 hours

Phase 1: Project Setup - 20 minutes
Phase 2: Domain Migration - 45 minutes  
Phase 3: Infrastructure Setup - 45 minutes
Phase 4: Service Layer - 45 minutes
Phase 5: References Update - 20 minutes
Phase 6: DI Update - 20 minutes
Phase 7: Validation & Testing - 30 minutes

Buffer Time: 30 minutes
Total with Buffer: 4 hours
```

---

## **11. NEXT STEPS (UPDATED)**

### **11.1 Immediate Actions (After Approval)**
1. Create backup branch
2. Start Phase 1: Project Setup
3. Follow implementation plan sequentially
4. Validate after each phase
5. Update DI registrations in all Program.cs files

### **11.2 Post-Task Actions**
1. Create initial migration for Accounting entities
2. Update documentation
3. Prepare for Week 1: Core Accounting Engine
4. Set up CI/CD for new project
5. Run full test suite

---

## **12. CONCLUSION (UPDATED)**

### **12.1 Risk Assessment**
- **Overall Risk:** LOW-MEDIUM
- **Success Probability:** 90%
- **Critical Dependencies:** DI registration update

### **12.2 Recommendation**
**PROCEED** with implementation following the detailed plan.

The task is **well-defined**, **low-risk**, and **essential** for MVP success. All potential impacts have been analyzed and mitigated. The 5 required points have been addressed:

1. **Deeper Reverse Impact Analysis** - Completed with service usage analysis
2. **Immutability Enforcement** - Detailed implementation with constructor-only pattern
3. **Project Structure** - Complete folder structure and namespace organization
4. **Dependency Injection** - Comprehensive DI registration plan
5. **Testing Plan** - Detailed test strategy for all layers

---

**Status:** Ready for User Approval (REVISED VERSION)  
**Next Action:** Wait for User approval before implementation
