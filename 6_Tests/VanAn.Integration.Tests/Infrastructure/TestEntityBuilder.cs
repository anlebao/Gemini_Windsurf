using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Domain;
using DomainLoyaltyRewards = VanAn.Shared.Domain.LoyaltyRewards;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Domain-compliant test factory - chỉ dùng cho Infrastructure Integration Test
/// KHÔNG duplicate business rule, chỉ wrap constructor hợp lệ đã tồn tại trong Domain
/// Creates test entities using proper constructors, respecting protected setters
/// Follows DDD patterns and domain integrity rules
/// </summary>
public static class TestEntityBuilder
{
    /// <summary>
    /// Default test tenant ID for all integration tests
    /// </summary>
    public static readonly TenantId TestTenantId = new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001"));

    /// <summary>
    /// Creates a new test tenant ID
    /// </summary>
    public static TenantId CreateTenantId()
    {
        return new TenantId(Guid.NewGuid());
    }
    /// <summary>
    /// Creates a test Customer with proper tenant context
    /// Uses public constructor: Customer(TenantId tenantId, string fullName, string phoneNumber, string? email = null)
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
    /// Uses public constructor: Shop(TenantId tenantId, string name, string address, string phone, string email)
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
    /// Uses public constructor: Order(TenantId tenantId, Guid? customerId, decimal totalAmount)
    /// </summary>
    public static Order CreateOrder(TenantId tenantId, Guid? customerId = null, decimal totalAmount = 100.0m)
    {
        return new Order(
            tenantId,
            customerId ?? Guid.NewGuid(),
            totalAmount
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
    /// Creates a test OrderItem with proper tenant and order context
    /// Uses public constructor: OrderItem(TenantId tenantId, Guid orderId, Guid productId, int quantity, decimal unitPrice)
    /// </summary>
    public static OrderItem CreateOrderItem(TenantId tenantId, Guid orderId, Guid? productId = null, int quantity = 1, decimal unitPrice = 25.0m)
    {
        return new OrderItem(
            tenantId,
            orderId,
            productId ?? Guid.NewGuid(),
            quantity,
            unitPrice
        );
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
    /// Creates a test LoyaltyRewards with proper tenant and customer context
    /// Uses public constructor: LoyaltyRewards(TenantId tenantId, Guid customerId)
    /// </summary>
    public static DomainLoyaltyRewards CreateLoyaltyRewards(TenantId tenantId, CustomerId customerId, int points = 100)
    {
        var rewards = new DomainLoyaltyRewards(tenantId, customerId.Value);
        // Note: PointBalance is protected, would need business method to set points
        return rewards;
    }

    /// <summary>
    /// Creates a test Product with proper tenant context
    /// Uses public constructor: Product(TenantId tenantId, string name, decimal price, string category)
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
        var order = CreateOrder(testTenantId, customer.CustomerId.Value);

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

    /// <summary>
    /// Creates a test Lead entity using reflection to bypass protected constructor
    /// Used only for integration tests where domain factory methods are not available
    /// </summary>
    public static Lead CreateLead(
        Guid? tenantId = null,
        string? fullName = null,
        string? phoneNumber = null,
        string? email = null,
        string? companyName = null,
        LeadSource source = LeadSource.Manual,
        LeadStatus status = LeadStatus.New,
        int leadScore = 0)
    {
        var lead = (Lead)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Lead));
        
        lead.Id = Guid.NewGuid();
        lead.LeadId = new LeadId(lead.Id);
        lead.TenantId = tenantId ?? Guid.Parse("12345678-1234-1234-1234-123456789abc");
        lead.FullName = fullName ?? "Test Lead";
        lead.PhoneNumber = phoneNumber ?? "1234567890";
        lead.Email = email ?? "test@example.com";
        lead.CompanyName = companyName;
        lead.Source = source;
        lead.Status = status;
        lead.LeadScore = leadScore;
        lead.CreatedAt = DateTime.UtcNow;
        lead.UpdatedAt = DateTime.UtcNow;
        lead.IsDeleted = false;
        
        return lead;
    }

    /// <summary>
    /// Creates a test FacebookLead entity using reflection to bypass protected constructor
    /// Used only for integration tests where domain factory methods are not available
    /// </summary>
    public static FacebookLead CreateFacebookLead(
        Guid? tenantId = null,
        string? facebookLeadId = null,
        string? facebookAdId = null,
        string? facebookPageId = null,
        string? facebookCampaignId = null,
        DateTime? facebookCreatedTime = null)
    {
        var facebookLead = (FacebookLead)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FacebookLead));
        
        facebookLead.Id = Guid.NewGuid();
        facebookLead.LeadId = new LeadId(facebookLead.Id);
        facebookLead.TenantId = tenantId ?? Guid.Parse("12345678-1234-1234-1234-123456789abc");
        facebookLead.FacebookLeadId = facebookLeadId ?? "test_fb_lead_id";
        facebookLead.FacebookAdId = facebookAdId ?? "test_fb_ad_id";
        facebookLead.FacebookPageId = facebookPageId ?? "test_fb_page_id";
        facebookLead.FacebookCampaignId = facebookCampaignId ?? "test_fb_campaign_id";
        facebookLead.FacebookCreatedTime = facebookCreatedTime ?? DateTime.UtcNow;
        facebookLead.FacebookFormData = "{}";
        facebookLead.IsFacebookProcessed = false;
        facebookLead.CreatedAt = DateTime.UtcNow;
        facebookLead.UpdatedAt = DateTime.UtcNow;
        facebookLead.IsDeleted = false;
        
        return facebookLead;
    }
}
