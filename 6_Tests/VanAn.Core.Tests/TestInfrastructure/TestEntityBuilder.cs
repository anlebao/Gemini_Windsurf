using VanAn.Shared.Domain;
using VanAn.CoreHub.Domain;

namespace VanAn.Core.Tests.TestInfrastructure;

/// <summary>
/// Domain-compliant test factory for VanAn.Core.Tests
/// Creates test entities using proper constructors, respecting protected setters
/// Follows DDD patterns and domain integrity rules
/// </summary>
public static class TestEntityBuilder
{
    /// <summary>
    /// Creates a test Customer with proper tenant context
    /// </summary>
    public static Customer CreateCustomer(TenantId tenantId, string? fullName = null, string? phoneNumber = null, string? email = null)
    {
        return new Customer(
            tenantId,
            fullName ?? "Test Customer",
            phoneNumber ?? "1234567890",
            email ?? "test@example.com"
        );
    }

    /// <summary>
    /// Creates a test Shop with proper tenant context
    /// </summary>
    public static Shop CreateShop(TenantId tenantId, string? name = null, string? address = null, string? phone = null, string? email = null)
    {
        return new Shop(
            tenantId,
            name ?? "Test Shop",
            address ?? "123 Test Street",
            phone ?? "1234567890",
            email ?? "shop@example.com"
        );
    }

    /// <summary>
    /// Creates a test Order with proper tenant context
    /// </summary>
    public static Order CreateOrder(TenantId tenantId, decimal totalAmount = 100.0m, Guid? customerId = null)
    {
        return new Order(
            tenantId,
            customerId ?? Guid.NewGuid(),
            totalAmount
        );
    }

    /// <summary>
    /// Creates a test JournalTemplate with proper tenant context
    /// </summary>
    public static JournalTemplate CreateJournalTemplate(TenantId tenantId, string code, string description)
    {
        return new JournalTemplate(
            tenantId,
            code,
            description
        );
    }

    /// <summary>
    /// Creates a test JournalEntry with proper tenant context
    /// </summary>
    public static JournalEntry CreateJournalEntry(TenantId tenantId, AccountingPeriod period, string description)
    {
        return new JournalEntry(
            tenantId,
            period.ToDateTime(),
            description
        );
    }

    /// <summary>
    /// Creates a test TenantId for testing
    /// </summary>
    public static TenantId CreateTenantId(Guid? guid = null)
    {
        return new TenantId(guid ?? Guid.Parse("12345678-1234-1234-1234-123456789abc"));
    }

    /// <summary>
    /// Creates a test CustomerId for testing
    /// </summary>
    public static CustomerId CreateCustomerId(Guid? guid = null)
    {
        return new CustomerId(guid ?? Guid.NewGuid());
    }

    /// <summary>
    /// Creates a test ProductId for testing
    /// </summary>
    public static ProductId CreateProductId(Guid? guid = null)
    {
        return new ProductId(guid ?? Guid.NewGuid());
    }

    /// <summary>
    /// Creates a test AccountingEntry with proper tenant context
    /// Uses factory method: AccountingEntry.CreateRevenue() or CreateExpense()
    /// IMPORTANT: Respects AccountingEntry immutability - 100% append-only pattern
    /// </summary>
    public static AccountingEntry CreateAccountingEntry(TenantId tenantId, AccountingEntryType type, Money amount, AccountingPeriod? period = null)
    {
        // Use factory methods to respect immutability
        var description = type == AccountingEntryType.Revenue ? "Test Revenue Entry" : "Test Expense Entry";
        
        if (type == AccountingEntryType.Revenue)
        {
            return AccountingEntry.CreateRevenue(tenantId, period ?? AccountingPeriod.FromDateTime(DateTime.UtcNow), amount, description);
        }
        else
        {
            return AccountingEntry.CreateExpense(tenantId, period ?? AccountingPeriod.FromDateTime(DateTime.UtcNow), amount, description);
        }
    }

    /// <summary>
    /// Creates a test Product with proper tenant context
    /// </summary>
    public static Product CreateProduct(TenantId tenantId, string name, decimal price, string category = "General")
    {
        return new Product(tenantId, name, price, category);
    }

    /// <summary>
    /// Creates a test CartItem. CartItem is a record — all 6 required properties must be set.
    /// ProductId = FK to Product catalog (distinct from Id = cart line PK).
    /// </summary>
    public static CartItem CreateCartItem(
        Guid? productId = null,
        string productName = "Test Product",
        string description = "Test Description",
        int quantity = 1,
        decimal unitPrice = 25000m)
    {
        return new CartItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId ?? Guid.NewGuid(),
            ProductName = productName,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
    }

    /// <summary>
    /// Creates a complete test scenario with Customer, Shop, and Order
    /// </summary>
    public static (Customer customer, Shop shop, Order order) CreateTestScenario(TenantId? tenantId = null)
    {
        var testTenantId = tenantId ?? CreateTenantId();
        var customer = CreateCustomer(testTenantId);
        var shop = CreateShop(testTenantId);
        var order = CreateOrder(testTenantId, 100.0m, customer.CustomerId.Value);

        return (customer, shop, order);
    }

    /// <summary>
    /// Creates a complete accounting test scenario with original and reversal entries
    /// Tests core AccountingEntry immutability feature
    /// </summary>
    public static (AccountingEntry original, AccountingEntry reversal) CreateAccountingReversalScenario(TenantId tenantId, Money amount)
    {
        var originalEntry = CreateAccountingEntry(tenantId, AccountingEntryType.Revenue, amount);
        var reversalEntry = CreateAccountingEntry(tenantId, AccountingEntryType.Expense, new Money(-amount.ToDecimal()), originalEntry.Period);
        
        return (originalEntry, reversalEntry);
    }
}
