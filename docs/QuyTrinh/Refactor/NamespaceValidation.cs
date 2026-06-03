// NAMESPACE VALIDATION TEST
// Pre-implementation build test for 18 conflicts refactor
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Infrastructure.Configurations;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using CoreTenantId = VanAn.Shared.Domain.TenantId;
using CoreMoney = VanAn.Shared.Domain.Money;

namespace Test.Validation;

/// <summary>
/// Namespace validation test - Ensures 100% immutability and namespace resolution
/// </summary>
public class NamespaceValidationTest
{
    public void TestNamespaceResolution()
    {
        // Arrange
        var tenantId = CoreTenantId.FromGuid(Guid.NewGuid());
        var period = AccountingPeriod.Create(2024, 1);
        var amount = new CoreMoney(1000m);
        
        // Act - Factory method only
        var entry = CoreAccountingEntry.CreateRevenue(tenantId, period, amount, "Test");
        
        // Assert - Namespace resolution
        entry.Should().NotBeNull();
        entry.TenantId.Should().Be(tenantId);
        entry.Amount.Should().Be(1000m);
        entry.Id.Should().NotBe(Guid.Empty);
        
        // Test immutability - should not allow external modification
        var entryType = entry.GetType();
        var amountProperty = entryType.GetProperty("Amount");
        amountProperty?.CanWrite.Should().BeFalse(); // Read-only
        
        // Test sealed class - should not allow inheritance
        entryType.IsSealed.Should().BeTrue();
    }
    
    public void TestValueObjectConverters()
    {
        // Arrange
        var tenantConverter = new TenantIdConverter();
        var moneyConverter = new MoneyConverter();
        var periodConverter = new AccountingPeriodConverter();
        
        var tenantId = CoreTenantId.FromGuid(Guid.NewGuid());
        var money = new CoreMoney(1500.50m);
        var period = AccountingPeriod.Create(2024, 1);
        
        // Act
        var tenantDbValue = tenantConverter.ConvertToProvider(tenantId);
        var moneyDbValue = moneyConverter.ConvertToProvider(money);
        var periodDbValue = periodConverter.ConvertToProvider(period);
        
        // Assert
        tenantDbValue.Should().Be(tenantId.Value);
        moneyDbValue.Should().Be(1500.50m);
        periodDbValue.Should().Be("2024-01");
    }
    
    public void TestBaseEntityInterfaces()
    {
        // Arrange
        var tenantId = CoreTenantId.FromGuid(Guid.NewGuid());
        var entry = CoreAccountingEntry.CreateRevenue(
            tenantId,
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(1000m),
            "Test");
        
        // Assert - Interface implementation
        entry.Should().BeAssignableTo<IMustHaveTenant>();
        entry.Should().BeAssignableTo<IAuditableEntity>();
        
        // Test interface properties
        var mustHaveTenant = (IMustHaveTenant)entry;
        var auditableEntity = (IAuditableEntity)entry;
        
        mustHaveTenant.TenantId.Should().Be(tenantId);
        auditableEntity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        auditableEntity.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
    
    public void TestReadOnlyCollection()
    {
        // Arrange
        var entry = CoreAccountingEntry.CreateRevenue(
            CoreTenantId.FromGuid(Guid.NewGuid()),
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(1000m),
            "Test");
        
        // Assert - IReadOnlyCollection
        entry.ReversalEntries.Should().BeAssignableTo<IReadOnlyCollection<CoreAccountingEntry>>();
        entry.ReversalEntries.Should().BeEmpty();
    }
    
    public void TestMultiTenancyEnforcement()
    {
        // Arrange
        var tenant1Id = CoreTenantId.FromGuid(Guid.NewGuid());
        var tenant2Id = CoreTenantId.FromGuid(Guid.NewGuid());
        
        var entry1 = CoreAccountingEntry.CreateRevenue(
            tenant1Id,
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(1000m),
            "Tenant1 Entry");
        
        var entry2 = CoreAccountingEntry.CreateRevenue(
            tenant2Id,
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(2000m),
            "Tenant2 Entry");
        
        // Assert - Tenant isolation
        entry1.TenantId.Should().Be(tenant1Id);
        entry2.TenantId.Should().Be(tenant2Id);
        entry1.TenantId.Should().NotBe(tenant2Id);
        entry2.TenantId.Should().NotBe(tenant1Id);
    }
    
    public void TestReversalPattern()
    {
        // Arrange
        var originalEntry = CoreAccountingEntry.CreateRevenue(
            CoreTenantId.FromGuid(Guid.NewGuid()),
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(1000m),
            "Original transaction");
        
        // Act
        var reversalEntry = CoreAccountingEntry.CreateReversal(originalEntry, "Correction needed");
        
        // Assert - Reversal pattern compliance
        reversalEntry.Should().NotBeNull();
        reversalEntry.Amount.Should().Be(-1000m); // Negative amount
        reversalEntry.ReversalEntryId.Should().Be(originalEntry.Id);
        reversalEntry.EntryType.Should().Be(originalEntry.EntryType);
        reversalEntry.Description.Should().Contain("Reversal of:");
        reversalEntry.Description.Should().Contain("Correction needed");
        
        // Original entry unchanged
        originalEntry.Amount.Should().Be(1000m);
        originalEntry.ReversalEntryId.Should().BeNull();
    }
    
    public void TestPeriodProperty()
    {
        // Arrange
        var entry = CoreAccountingEntry.CreateRevenue(
            CoreTenantId.FromGuid(Guid.NewGuid()),
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(1000m),
            "Test");
        
        // Act
        var period = entry.Period;
        
        // Assert - Computed period property
        period.Year.Should().Be(2024);
        period.Month.Should().Be(1);
        period.StartDate.Should().Be(new DateTime(2024, 1, 1));
        period.EndDate.Should().Be(new DateTime(2024, 1, 31));
        period.ToString().Should().Be("2024-01");
    }
    
    public void TestAccountingBookType()
    {
        // Arrange
        var revenueEntry = CoreAccountingEntry.CreateRevenue(
            CoreTenantId.FromGuid(Guid.NewGuid()),
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(1000m),
            "Revenue Test");
        
        var expenseEntry = CoreAccountingEntry.CreateExpense(
            CoreTenantId.FromGuid(Guid.NewGuid()),
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(500m),
            "Expense Test");
        
        // Assert - AccountingBookType assignment
        revenueEntry.AccountingBookType.Should().Be(AccountingBookType.RevenueBook);
        expenseEntry.AccountingBookType.Should().Be(AccountingBookType.ExpenseBook);
    }
    
    public void TestVatRate()
    {
        // Arrange
        var entry = CoreAccountingEntry.CreateRevenue(
            CoreTenantId.FromGuid(Guid.NewGuid()),
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(1000m),
            "Test");
        
        // Assert - Default VAT rate
        entry.VatRate.Should().Be(VatRate.Zero);
    }
    
    public void TestTransactionDate()
    {
        // Arrange & Act
        var beforeCreate = DateTime.UtcNow;
        var entry = CoreAccountingEntry.CreateRevenue(
            CoreTenantId.FromGuid(Guid.NewGuid()),
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(1000m),
            "Test");
        var afterCreate = DateTime.UtcNow;
        
        // Assert - Transaction date set at creation
        entry.TransactionDate.Should().BeOnOrAfter(beforeCreate);
        entry.TransactionDate.Should().BeOnOrBefore(afterCreate);
    }
    
    public void TestDescription()
    {
        // Arrange
        var description = "Test accounting entry description";
        
        // Act
        var entry = CoreAccountingEntry.CreateRevenue(
            CoreTenantId.FromGuid(Guid.NewGuid()),
            AccountingPeriod.Create(2024, 1),
            new CoreMoney(1000m),
            description);
        
        // Assert - Description preserved
        entry.Description.Should().Be(description);
    }
    
    public void TestBackwardCompatibility()
    {
        // Arrange
        var tenantId = CoreTenantId.FromGuid(Guid.NewGuid());
        var period = AccountingPeriod.Create(2024, 1);
        
        // Act - Test deprecated methods
        var revenueEntry = CoreAccountingEntry.CreateRevenueEntry(tenantId, period, 1000m, "Test");
        var expenseEntry = CoreAccountingEntry.CreateExpenseEntry(tenantId, period, 500m, "Test");
        
        // Assert - Backward compatibility maintained
        revenueEntry.Should().NotBeNull();
        revenueEntry.Amount.Should().Be(1000m);
        revenueEntry.EntryType.Should().Be(AccountingEntryType.Revenue);
        
        expenseEntry.Should().NotBeNull();
        expenseEntry.Amount.Should().Be(500m);
        expenseEntry.EntryType.Should().Be(AccountingEntryType.Expense);
    }
}
