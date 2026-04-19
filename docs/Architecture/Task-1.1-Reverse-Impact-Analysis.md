# TASK 1.1 - CLEAN UP PHASE: REVERSE IMPACT ANALYSIS + PLAN

**Ngày:** 14 tháng 4, 2026  
**Task:** Clean Up Phase - T VanAn.Accounting project + Domain organization  
**Trang thái:** Ch User approval

---

## **1. REVERSE IMPACT ANALYSIS**

### **1.1 Files s thay ho c t o m i**

#### **FILES S T O M I:**
```yaml
New Files:
- 3_CoreHub/VanAn.Accounting/VanAn.Accounting.csproj (Class Library, .NET 8)
- 3_CoreHub/VanAn.Accounting/Domain/AccountingEntry.cs (Di chuy n)
- 3_CoreHub/VanAn.Accounting/Domain/AccountingBookType.cs (New)
- 3_CoreHub/VanAn.Accounting/Domain/AccountingPeriod.cs (New)
- 3_CoreHub/VanAn.Accounting/Infrastructure/Configurations/AccountingEntryConfiguration.cs (New)
- 3_CoreHub/VanAn.Accounting/Infrastructure/Configurations/AccountingBookTypeConfiguration.cs (New)
- 3_CoreHub/VanAn.Accounting/Infrastructure/Configurations/AccountingPeriodConfiguration.cs (New)
- 3_CoreHub/VanAn.Accounting/Services/IAccountingService.cs (Di chuy n)
- 3_CoreHub/VanAn.Accounting/Services/AccountingService.cs (New)
- 3_CoreHub/VanAn.Accounting/Tests/AccountingServiceTests.cs (New)
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
```

#### **FILES S X A:**
```yaml
Removed Files:
- 3_CoreHub/Services/IAccountingService.cs (Di chuy n)
- 3_CoreHub/Infrastructure/Configurations/AccountingEntryConfiguration.cs (n u có)
```

### **1.2 T c ng n Domain hi n t i**

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

### **1.3 T c ng n Multi-tenancy**

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

### **1.4 T c ng n Test Infrastructure**

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

#### **B. Existing Tests**
```yaml
Impact: MINIMAL
Current Tests:
- VanAn.Core.Tests có các test cho services
- Không có test cho AccountingEntry hi n t i

New Tests:
- C n thêm AccountingServiceTests
- Có th reuse existing test patterns

Action: Th m tests m i, không i m hi n t i
```

### **1.5 EF Core Configuration Impact**

#### **A. VanAnDbContext**
```yaml
Impact: CONTROLLED
Current State:
- Không có DbSet<AccountingEntry>
- Không có Accounting configurations

New State:
- Th m DbSet<AccountingEntry>, DbSet<AccountingBookType>, DbSet<AccountingPeriod>
- Th m ApplyConfigurationsFromAssembly cho Accounting entities
- Th m ValueConverters cho Accounting Value Objects

Action: C p nh t VanAnDbContext m i
```

#### **B. Migrations**
```yaml
Impact: LOW
Current State:
- Không có Accounting tables trong database

New State:
- C n t o migration m i cho Accounting entities
- Không impact existing tables

Action: T o migration m i sau khi hoàn thành
```

---

## **2. R I RO PHÁT SINH**

### **2.1 HIGH RISK**

#### **A. Namespace Conflicts**
```yaml
Risk: Namespace conflicts khi di chuy n AccountingEntry
Probability: MEDIUM (30%)
Impact: MEDIUM
Mitigation:
- Ki m tra t t c using statements
- Update namespace trong các files liên quan
- Run build sau khi di chuy n
```

#### **B. Project Reference Issues**
```yaml
Risk: Circular reference ho c missing reference
Probability: MEDIUM (40%)
Impact: HIGH
Mitigation:
- Ki tra dependency graph tr c khi add reference
- Test build sau khi m i reference
- S d ng dependency visualization tools
```

### **2.2 MEDIUM RISK**

#### **A. EF Core Configuration Conflicts**
```yaml
Risk: Configuration conflicts gi a existing và new entities
Probability: LOW (20%)
Impact: MEDIUM
Mitigation:
- S d ng ApplyConfigurationsFromAssembly (auto-discovery)
- Test migration tr c khi apply
- Review existing configurations
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
```

### **2.3 LOW RISK**

#### **A. Build Performance**
```yaml
Risk: Build time increase do thêm project
Probability: LOW (10%)
Impact: LOW
Mitigation:
- Monitor build time
- Optimize project references
- Accept minor performance impact
```

#### **B. Git History**
```yaml
Risk: Git history fragmentation do file moves
Probability: LOW (5%)
Impact: LOW
Mitigation:
- S d ng git move (--cached)
- Commit logical grouped changes
- Document changes in commit messages
```

---

## **3. MITIGATION STRATEGIES**

### **3.1 Pre-Implementation Checks**
```yaml
1. Backup current state:
   - Git branch: task-1.1-cleanup-backup
   - Database backup (n u có)
   - Document current structure

2. Dependency Analysis:
   - Map t t c dependencies
   - Identify potential circular references
   - Visualize current architecture

3. Test Baseline:
   - Run t t c existing tests
   - Document test results
   - Ensure build passes (0 errors)
```

### **3.2 Implementation Safeguards**
```yaml
1. Incremental Changes:
   - T o project m i tr c
   - Di chuy n entities t p t p
   - Build và test sau m i step

2. Rollback Plan:
   - Git branch strategy
   - Database rollback scripts
   - Configuration backup

3. Validation Gates:
   - Build passes (0 errors)
   - Existing tests pass
   - New entities compile
   - No circular references
```

### **3.3 Post-Implementation Verification**
```yaml
1. Full System Test:
   - Build entire solution
   - Run t t c tests
   - Check project references

2. Architecture Compliance:
   - windsurf-guard.js validation
   - .windsurfrules compliance
   - Domain purity check

3. Documentation Update:
   - Update architecture diagrams
   - Update README files
   - Document changes
```

---

## **4. DETAILED IMPLEMENTATION PLAN**

### **4.1 Phase 1: Project Setup (15 minutes)**

#### **Step 1.1: Create VanAn.Accounting Project**
```bash
# T o project m i
cd 3_CoreHub
dotnet new classlib -n VanAn.Accounting -f net8.0

# Add project to solution
cd ..
dotnet sln add 3_CoreHub/VanAn.Accounting/VanAn.Accounting.csproj
```

#### **Step 1.2: Setup Project Structure**
```
3_CoreHub/VanAn.Accounting/
  Domain/
    AccountingEntry.cs
    AccountingBookType.cs
    AccountingPeriod.cs
  Infrastructure/
    Configurations/
      AccountingEntryConfiguration.cs
      AccountingBookTypeConfiguration.cs
      AccountingPeriodConfiguration.cs
    ValueConverters/
      AccountingEntryIdConverter.cs
      AccountingBookTypeIdConverter.cs
      AccountingPeriodIdConverter.cs
  Services/
    IAccountingService.cs
    AccountingService.cs
  Tests/
    AccountingServiceTests.cs
```

#### **Step 1.3: Add Project References**
```xml
<!-- VanAn.Accounting.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\1_Shared\VanAn.Shared.csproj" />
</ItemGroup>
```

### **4.2 Phase 2: Domain Migration (30 minutes)**

#### **Step 2.1: Create Accounting Namespace**
```csharp
// 1_Shared/Domain.cs - Th m namespace
namespace VanAn.Shared.Domain.Accounting
{
    // Accounting entities
}
```

#### **Step 2.2: Move AccountingEntry**
```csharp
// Di chuy n AccountingEntry sang namespace m i
namespace VanAn.Shared.Domain.Accounting
{
    public class AccountingEntry : BaseEntity
    {
        // Existing properties + new properties
        public AccountingBookType BookType { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        // ... existing properties
    }
}
```

#### **Step 2.3: Add New Accounting Entities**
```csharp
// AccountingBookType
public class AccountingBookType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// AccountingPeriod
public class AccountingPeriod : BaseEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsClosed { get; set; } = false;
    public DateTime? ClosedAt { get; set; }
}
```

#### **Step 2.4: Add Accounting Value Objects**
```csharp
// Accounting Value Objects
public record AccountingEntryId(Guid Value);
public record AccountingBookTypeId(Guid Value);
public record AccountingPeriodId(Guid Value);
```

### **4.3 Phase 3: Infrastructure Setup (30 minutes)**

#### **Step 3.1: EF Core Configurations**
```csharp
// AccountingEntryConfiguration
public class AccountingEntryConfiguration : IEntityTypeConfiguration<AccountingEntry>
{
    public void Configure(EntityTypeBuilder<AccountingEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)");
        builder.HasOne(e => e.OriginalEntry)
               .WithMany(e => e.ReversalEntries)
               .HasForeignKey(e => e.ReversalEntryId);
        
        // Multi-tenancy
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
```

#### **Step 3.2: Value Converters**
```csharp
// AccountingEntryIdConverter
public class AccountingEntryIdConverter : ValueConverter<AccountingEntryId, Guid>
{
    public AccountingEntryIdConverter()
        : base(
            v => v.Value,
            v => new AccountingEntryId(v))
    {
    }
}
```

#### **Step 3.3: Update VanAnDbContext**
```csharp
// Add DbSets
public DbSet<AccountingEntry> AccountingEntries { get; set; }
public DbSet<AccountingBookType> AccountingBookTypes { get; set; }
public DbSet<AccountingPeriod> AccountingPeriods { get; set; }

// Add ConfigureConventions
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    // Existing converters + Accounting converters
    configurationBuilder.Properties<AccountingEntryId>()
        .HaveConversion<AccountingEntryIdConverter>();
}
```

### **4.4 Phase 4: Service Layer (30 minutes)**

#### **Step 4.1: Move IAccountingService**
```csharp
// VanAn.Accounting/Services/IAccountingService.cs
namespace VanAn.Accounting.Services
{
    public interface IAccountingService
    {
        Task<AccountingEntry> CreateEntryAsync(AccountingEntry entry);
        Task<AccountingEntry> CreateReversalEntryAsync(Guid originalEntryId, string reason, Guid tenantId);
        // ... existing methods
    }
}
```

#### **Step 4.2: Implement AccountingService**
```csharp
// VanAn.Accounting/Services/AccountingService.cs
namespace VanAn.Accounting.Services
{
    public class AccountingService : IAccountingService
    {
        private readonly VanAnDbContext _context;
        
        public AccountingService(VanAnDbContext context)
        {
            _context = context;
        }
        
        // Implementation following Modular Monolith rules
    }
}
```

### **4.5 Phase 5: Project References Update (15 minutes)**

#### **Step 5.1: Update Solution**
```xml
<!-- VanAn.sln -->
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "VanAn.Accounting", "3_CoreHub\VanAn.Accounting\VanAn.Accounting.csproj", "{NEW-GUID}"
EndProject
```

#### **Step 5.2: Update CoreHub References**
```xml
<!-- VanAn.CoreHub.csproj -->
<ItemGroup>
  <ProjectReference Include="..\1_Shared\VanAn.Shared.csproj" />
  <ProjectReference Include="VanAn.Accounting\VanAn.Accounting.csproj" />
</ItemGroup>
```

#### **Step 5.3: Update Test References**
```xml
<!-- VanAn.Core.Tests.csproj -->
<ItemGroup>
  <ProjectReference Include="..\..\3_CoreHub\VanAn.Accounting\VanAn.Accounting.csproj" />
</ItemGroup>
```

### **4.6 Phase 6: Validation & Testing (30 minutes)**

#### **Step 6.1: Build Validation**
```bash
# Build entire solution
dotnet build VanAn.sln

# Check for errors
# Should be 0 errors
```

#### **Step 6.2: Run Existing Tests**
```bash
# Run existing tests to ensure no regression
dotnet test 6_Tests/VanAn.Core.Tests/

# All tests should pass
```

#### **Step 6.3: windsurf-guard Validation**
```bash
# Run windsurf-guard
node windsurf-guard.js

# Should pass all checks
```

#### **Step 6.4: Create Basic Tests**
```csharp
// VanAn.Accounting/Tests/AccountingServiceTests.cs
public class AccountingServiceTests
{
    [Fact]
    public async Task CreateEntry_ShouldCreateImmutableEntry()
    {
        // Test implementation
    }
}
```

---

## **5. SUCCESS CRITERIA**

### **5.1 Technical Success**
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
```

### **5.2 Functional Success**
```yaml
Domain Model:
- [ ] AccountingEntry in correct namespace
- [ ] All accounting entities inherit BaseEntity
- [ ] Value Objects properly defined
- [ ] No duplicate definitions

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
```

---

## **6. ROLLBACK PLAN**

### **6.1 Immediate Rollback**
```bash
# Git rollback
git checkout task-1.1-cleanup-backup
git checkout main --force

# Remove project files
rm -rf 3_CoreHub/VanAn.Accounting

# Restore Domain.cs
git checkout HEAD -- 1_Shared/Domain.cs
```

### **6.2 Selective Rollback**
```bash
# Rollback specific changes
git checkout HEAD -- VanAn.sln
git checkout HEAD -- 3_CoreHub/VanAn.CoreHub.csproj
git checkout HEAD -- 1_Shared/Domain.cs
```

### **6.3 Database Rollback**
```bash
# Remove migration (n u có t o)
dotnet ef database update previous-migration
dotnet ef migrations remove
```

---

## **7. ESTIMATED TIMELINE**

```yaml
Total Estimated Time: 2.5 hours

Phase 1: Project Setup - 15 minutes
Phase 2: Domain Migration - 30 minutes  
Phase 3: Infrastructure Setup - 30 minutes
Phase 4: Service Layer - 30 minutes
Phase 5: References Update - 15 minutes
Phase 6: Validation & Testing - 30 minutes

Buffer Time: 30 minutes
Total with Buffer: 3 hours
```

---

## **8. NEXT STEPS**

### **8.1 Immediate Actions (After Approval)**
1. Create backup branch
2. Start Phase 1: Project Setup
3. Follow implementation plan sequentially
4. Validate after each phase

### **8.2 Post-Task Actions**
1. Create initial migration for Accounting entities
2. Update documentation
3. Prepare for Week 1: Core Accounting Engine
4. Set up CI/CD for new project

---

## **9. CONCLUSION**

### **9.1 Risk Assessment**
- **Overall Risk:** LOW-MEDIUM
- **Success Probability:** 85%
- **Critical Dependencies:** Minimal

### **9.2 Recommendation**
**PROCEED** with implementation following the detailed plan.

The task is **well-defined**, **low-risk**, and **essential** for MVP success. All potential impacts have been analyzed and mitigated.

---

**Status:** Ready for User Approval  
**Next Action:** Wait for User approval before implementation
