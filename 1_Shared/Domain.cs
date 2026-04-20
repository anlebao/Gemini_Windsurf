using System.Collections.ObjectModel;
using VanAn.Shared.Domain.Common;

// REMOVED: EF Core dependencies violate Domain purity
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
// using System.ComponentModel.DataAnnotations;
// using System.ComponentModel.DataAnnotations.Schema;

namespace VanAn.Shared.Domain;

// Accounting Foundation - VAT 2026 Compliant
public enum AccountingEntryType
{
    Revenue = 1,        // Doanh thu
    Expense = 2,        // Chi phí
    TaxPayment = 3,     // Thuế
    Adjustment = 4      // Điều chỉnh
}

public enum AccountingBookType
{
    RevenueBook = 1,    // Sách chi doanh thu
    ExpenseBook = 2,    // Sách chi chi phí
    CashBankBook = 3,   // Sách chi tiền mặt ngân hàng
    TaxDeclarationBook = 4  // Sách chi kê khai thuế
}

public enum VatRate
{
    Exempt = 0,         // Miễn thuế
    Zero = 0,           // 0%
    Five = 5,           // 5%
    Ten = 10            // 10%
}

/// <summary>
/// Accounting Period for Household Business reporting
/// </summary>
public record AccountingPeriod(int Year, int Month)
{
    public override string ToString() => $"{Year:0000}-{Month:00}";
    
    public static AccountingPeriod FromDateTime(DateTime date) => new(date.Year, date.Month);
    
    public DateTime StartDate => new DateTime(Year, Month, 1);
    public DateTime EndDate => StartDate.AddMonths(1).AddDays(-1);
    
    // Static Create method for test files compatibility
    public static AccountingPeriod Create(int year, int month) => new(year, month);
}

/// <summary>
/// Tenant ID Value Object
/// </summary>
public record TenantId(Guid Value)
{
    public static implicit operator Guid(TenantId tenantId) => tenantId.Value;
    public static implicit operator TenantId(Guid value) => new(value);
    
    public static TenantId FromGuid(Guid value) => new(value);
    public Guid ToGuid() => Value;
}

/// <summary>
/// Accounting Entry ID Value Object
/// </summary>
public record AccountingEntryId(Guid Value)
{
    public static implicit operator Guid(AccountingEntryId entryId) => entryId.Value;
    public static implicit operator AccountingEntryId(Guid value) => new(value);
    
    public static AccountingEntryId FromGuid(Guid value) => new(value);
    public Guid ToGuid() => Value;
}

/// <summary>
/// Money Value Object
/// </summary>
public record Money(decimal Value)
{
    public static implicit operator decimal(Money money) => money.Value;
    public static implicit operator Money(decimal value) => new(value);
    
    public static Money FromDecimal(decimal value) => new(value);
    public decimal ToDecimal() => Value;
    
    // Backward compatibility constructor for test files
    public Money(decimal value, string currency) : this(value)
    {
        // Currency parameter ignored for backward compatibility
    }
}

/// <summary>
/// Accounting Entry - 100% IMMUTABLE APPEND-ONLY
/// Once created, never changed. Only reversal allowed.
/// VAT 2026 Compliance
/// </summary>
public sealed class AccountingEntry : BaseEntity
{
    public decimal Amount { get; }
    public AccountingEntryType EntryType { get; }
    public VatRate VatRate { get; }
    public DateTime TransactionDate { get; }
    public AccountingBookType AccountingBookType { get; }
    public int PeriodYear { get; }
    public int PeriodMonth { get; }
    public Guid? ReversalEntryId { get; }
    public string Description { get; } = string.Empty;
    public Guid? ReferenceId { get; }
    public string? ReferenceType { get; }

    public AccountingPeriod Period => new(PeriodYear, PeriodMonth);

    // Navigation (read-only)
    public AccountingEntry? OriginalEntry { get; }
    public IReadOnlyCollection<AccountingEntry> ReversalEntries { get; } = new List<AccountingEntry>();

    // Private constructor - EF Core & Factory only
    private AccountingEntry() { }

    // Main constructor used by factories
    private AccountingEntry(
        TenantId tenantId,
        decimal amount,
        AccountingEntryType entryType,
        VatRate vatRate,
        AccountingBookType bookType,
        int periodYear,
        int periodMonth,
        string description,
        Guid? reversalEntryId = null)
    {
        TenantId = tenantId;
        Amount = amount;
        EntryType = entryType;
        VatRate = vatRate;
        AccountingBookType = bookType;
        PeriodYear = periodYear;
        PeriodMonth = periodMonth;
        Description = description;
        ReversalEntryId = reversalEntryId;
        TransactionDate = DateTime.UtcNow;
    }

    // ====================== FACTORY METHODS ======================
    public static AccountingEntry CreateRevenue(TenantId tenantId, AccountingPeriod period, Money amount, string description)
        => new AccountingEntry(tenantId, amount.Value, AccountingEntryType.Revenue, VatRate.Zero, 
                              AccountingBookType.RevenueBook, period.Year, period.Month, description);

    public static AccountingEntry CreateExpense(TenantId tenantId, AccountingPeriod period, Money amount, string description)
        => new AccountingEntry(tenantId, amount.Value, AccountingEntryType.Expense, VatRate.Zero, 
                              AccountingBookType.ExpenseBook, period.Year, period.Month, description);

    public static AccountingEntry CreateReversal(AccountingEntry original, string reason)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason required", nameof(reason));

        return new AccountingEntry(
            original.TenantId,
            -original.Amount,
            original.EntryType,
            original.VatRate,
            original.AccountingBookType,
            original.PeriodYear,
            original.PeriodMonth,
            $"Reversal of: {original.Description} - {reason}",
            original.Id);
    }
}

public record ProductId(Guid Value);
public record IngredientId(Guid Value);
public record RecipeId(Guid Value);
public record InventoryId(Guid Value);
public record OrderId(Guid Value);
public record OrderStatusId(string Value);
public record ShopId(Guid Value);

// Identity Schema - RBAC for ShopERP
public enum UserRole
{
    None = 0,
    Owner = 1,        // Chủ quán - Full access
    StoreKeeper = 2,  // Thủ kho - Quản lý inventory
    Guard = 3,        // Bảo vệ - Check-in/out
    Staff = 4,        // Phục vụ - Order management
    Masterchef = 5    // 🆕 GOLDEN FLOW: Bếp trưởng - Kitchen operations
}

public record OrderStatusDefinition
{
    public required OrderStatusId Id { get; init; }
    public required string DisplayName { get; init; }
    public int Sequence { get; init; }
    public bool IsActive { get; init; }
    public bool RequiresInventoryDeduction { get; init; }
}

public static class OrderStatuses
{
    public static readonly OrderStatusDefinition[] Default = new[]
    {
        new OrderStatusDefinition 
        { 
            Id = new OrderStatusId("pending"), 
            DisplayName = "Chờ xác nhận", 
            Sequence = 1, 
            IsActive = true, 
            RequiresInventoryDeduction = false 
        },
        new OrderStatusDefinition 
        { 
            Id = new OrderStatusId("confirmed"), 
            DisplayName = "Đã xác nhận", 
            Sequence = 2, 
            IsActive = true, 
            RequiresInventoryDeduction = true 
        },
        new OrderStatusDefinition 
        { 
            Id = new OrderStatusId("preparing"), 
            DisplayName = "Đang pha chế", 
            Sequence = 3, 
            IsActive = true, 
            RequiresInventoryDeduction = false 
        },
        new OrderStatusDefinition 
        { 
            Id = new OrderStatusId("ready"), 
            DisplayName = "Sẵn sàng", 
            Sequence = 4, 
            IsActive = true, 
            RequiresInventoryDeduction = false 
        },
        new OrderStatusDefinition 
        { 
            Id = new OrderStatusId("completed"), 
            DisplayName = "Hoàn thành", 
            Sequence = 5, 
            IsActive = true, 
            RequiresInventoryDeduction = false 
        },
        new OrderStatusDefinition 
        { 
            Id = new OrderStatusId("cancelled"), 
            DisplayName = "Đã hủy", 
            Sequence = 6, 
            IsActive = true, 
            RequiresInventoryDeduction = false 
        }
    };
}

// Core Entities với Multi-tenancy
public class Shop : BaseEntity, IMustHaveTenant
{
    public string Name { get; protected set; } = string.Empty;
    public string Address { get; protected set; } = string.Empty;
    public string Phone { get; protected set; } = string.Empty;
    public string Email { get; protected set; } = string.Empty;
    public bool IsActive { get; protected set; } = true;
    
    // PHASE 2: Navigation Properties for Social Flywheel
    public virtual ICollection<SocialCampaign> SocialCampaigns { get; } = new Collection<SocialCampaign>();

    protected Shop() { }

    public Shop(TenantId tenantId, string name, string address, string phone, string email)
        : base(tenantId)
    {
        Name = name;
        Address = address;
        Phone = phone;
        Email = email;
    }

    // Business methods for shop management
    public void UpdateShopDetails(string name, string address, string phone, string email, bool isActive)
    {
        Name = name;
        Address = address;
        Phone = phone;
        Email = email;
        IsActive = isActive;
        UpdateAudit();
    }
}

public class Product : BaseEntity
{
    public ProductId ProductId { get; protected set; } = new ProductId(Guid.NewGuid());
    public string Name { get; protected set; } = string.Empty;
    public string Description { get; protected set; } = string.Empty;
    public decimal Price { get; protected set; }
    public string Category { get; protected set; } = string.Empty;
    public bool IsActive { get; protected set; } = true;
    public string? ImageUrl { get; protected set; }
    public decimal VatRate { get; protected set; } = 0.10m; // 10% default VAT for 2026 compliance

    public Product() { } // Public constructor for UI layer

    public Product(TenantId tenantId, string name, decimal price, string category)
        : base(tenantId)
    {
        Name = name;
        Price = price;
        Category = category;
    }

    public Product(TenantId tenantId, string name, string description, decimal price, string category, bool isActive = true, string? imageUrl = null, decimal vatRate = 0.10m)
        : base(tenantId)
    {
        Name = name;
        Description = description;
        Price = price;
        Category = category;
        IsActive = isActive;
        ImageUrl = imageUrl;
        VatRate = vatRate;
    }
}

public record CustomerId(Guid Value);

// Customer CRM Entity for Loyalty & Tier Management
public class Customer : BaseEntity, IMustHaveTenant
{
    public CustomerId CustomerId { get; protected set; } = new CustomerId(Guid.NewGuid());
    public string FullName { get; protected set; } = string.Empty;
    public string PhoneNumber { get; protected set; } = string.Empty;
    public string? Email { get; protected set; }
    public int LoyaltyPoints { get; protected set; } = 0;
    public string CustomerTier { get; protected set; } = "Bronze"; // Bronze, Silver, Gold, Platinum
    public DateTime? LastOrderDate { get; protected set; }
    public decimal TotalSpent { get; protected set; } = 0;
    public bool IsActive { get; protected set; } = true;
    
    // Device tracking for anonymous customer identification
    public Guid? DeviceId { get; protected set; }
    
    // Navigation Properties
    public virtual ICollection<Order> Orders { get; } = new Collection<Order>();

    protected Customer() { }

    public Customer(TenantId tenantId, string fullName, string phoneNumber, string? email = null)
        : base(tenantId)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Email = email;
    }

    // Business methods for customer management
    public void UpdateCustomerDetails(string fullName, string phoneNumber, string? email, string customerTier, Guid? deviceId, bool isActive)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Email = email;
        CustomerTier = customerTier;
        DeviceId = deviceId;
        IsActive = isActive;
        UpdateAudit();
    }

    public void SoftDelete()
    {
        MarkAsDeleted();
    }
}

public record OrderItemId(Guid Value);

// OrderItem for Multi-item Order Support
public class OrderItem : BaseEntity
{
    public OrderItemId OrderItemId { get; protected set; } = new OrderItemId(Guid.NewGuid());
    public Guid OrderId { get; protected set; }
    public Guid ProductId { get; protected set; }
    public int Quantity { get; protected set; }
    public decimal UnitPrice { get; protected set; }
    public decimal VatRate { get; protected set; } = 0.10m;
    public string? Notes { get; protected set; } // Customizations (size, sugar level, etc.)
    
    //  GOLDEN FLOW: Kitchen Status (Operational Only)
    public KitchenStatus KitchenStatus { get; protected set; } = KitchenStatus.Pending;
    
    //  GOLDEN FLOW: Voice Note Properties (Operational Only)
    public string? ItemNoteText { get; protected set; }
    public string? ItemNoteAudioBlob { get; protected set; }

    protected OrderItem() { }

    public OrderItem(TenantId tenantId, Guid orderId, Guid productId, int quantity, decimal unitPrice)
        : base(tenantId)
    {
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    // Business methods for order item management
    public void UpdateKitchenStatus(KitchenStatus status)
    {
        KitchenStatus = status;
        UpdateAudit();
    }

    public void UpdateItemNotes(string? noteText, string? noteAudioBlob)
    {
        ItemNoteText = noteText;
        ItemNoteAudioBlob = noteAudioBlob;
        UpdateAudit();
    }

    // Calculated Fields (🛡️ FINANCIAL PROTECTION - DO NOT MUTATE)
    public decimal SubTotal => Quantity * UnitPrice;
    public decimal VatAmount => SubTotal * VatRate;
    public decimal TotalAmount => SubTotal + VatAmount;
    
    // Navigation Properties
    public Order Order { get; protected set; } = null!;
    public Product Product { get; protected set; } = null!;
}

public class Ingredient : BaseEntity
{
    public IngredientId IngredientId { get; set; } = new IngredientId(Guid.NewGuid());
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinStockThreshold { get; set; }
    public decimal PricePerUnit { get; set; }
}

public class Recipe : BaseEntity
{
    public RecipeId RecipeId { get; set; } = new RecipeId(Guid.NewGuid());
    public Guid ProductId { get; set; } // 🛡️ PHASE 3 FIX: Use Guid instead of ProductId
    public Guid IngredientId { get; set; } // 🛡️ PHASE 3 FIX: Use Guid instead of IngredientId
    public decimal QuantityNeeded { get; set; }
    
    // Navigation properties
    // REMOVED: DataAnnotations violate Domain purity (FAIL-FAST MVP)
    // [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
    // [ForeignKey(nameof(IngredientId))]
    public Ingredient Ingredient { get; set; } = null!;
}

public class Inventory : BaseEntity
{
    public InventoryId InventoryId { get; protected set; } = new InventoryId(Guid.NewGuid());
    public Guid IngredientId { get; protected set; } // PHASE 3 FIX: Use Guid instead of IngredientId
    public decimal Quantity { get; protected set; }
    public DateTime LastUpdated { get; protected set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Ingredient Ingredient { get; protected set; } = null!;

    protected Inventory() { }

    public Inventory(TenantId tenantId, Guid ingredientId, decimal quantity)
        : base(tenantId)
    {
        IngredientId = ingredientId;
        Quantity = quantity;
        LastUpdated = DateTime.UtcNow;
    }

    // Business methods for inventory management
    public void UpdateQuantity(decimal newQuantity)
    {
        Quantity = newQuantity;
        LastUpdated = DateTime.UtcNow;
        UpdateAudit();
    }
}

public class Order : BaseEntity
{
    public OrderId OrderId { get; protected set; } = new OrderId(Guid.NewGuid());
    
    // Customer Information (CRM Integration)
    public Guid? CustomerId { get; protected set; }
    public string? CustomerDeviceId { get; protected set; } // Zero-friction identity fallback
    
    // Order Details
    public string OrderType { get; protected set; } = "DINEIN"; // DINEIN, TAKEAWAY, DELIVERY
    public OrderStatusId Status { get; protected set; } = new OrderStatusId("Draft");
    
    // Voice & Text Commands for KDS
    public string? TextCommand { get; protected set; }
    public string? VoiceCommandUrl { get; protected set; }
    
    //  GOLDEN FLOW: Voice Note Properties (Operational Only)
    public string? VoiceNoteText { get; protected set; }
    public string? VoiceNoteAudioBlob { get; protected set; }
    
    // GOLDEN FLOW: Kitchen Status (Operational Only)
    public KitchenStatus KitchenStatus { get; protected set; } = KitchenStatus.Pending;
    
    // Financial Calculations ( 2026 Tax Compliance - DO NOT MUTATE)
    public decimal SubTotal { get; protected set; } = 0;
    public decimal TotalVatAmount { get; protected set; } = 0;
    public decimal ShippingFee { get; protected set; } = 0;
    public decimal DiscountAmount { get; protected set; } = 0;
    public decimal TotalAmount { get; protected set; } = 0;
    
    // Payment Information
    public string? PaymentMethod { get; protected set; } // CASH, VIETQR, CREDIT_CARD
    public string? PaymentStatus { get; protected set; } = "Pending"; // Pending, Paid, Failed, Refunded
    public string? VietQR_TransactionId { get; protected set; }
    public string? VietQR_Payload { get; protected set; }
    
    // Timestamps
    public DateTime OrderDate { get; protected set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; protected set; }
    
    // Notes & Metadata
    public string? CustomerNotes { get; protected set; }
    public string? StaffNotes { get; protected set; }
    public string? TrackingCode { get; protected set; } // Social campaign tracking
    public bool IsSyncedToCoreHub { get; protected set; } = false;
    
    // Navigation Properties
    public Customer? Customer { get; protected set; } = null;
    public virtual ICollection<OrderItem> Items { get; protected set; } = new List<OrderItem>();
    
    // Calculated Methods (🛡️ FINANCIAL PROTECTION - DO NOT MUTATE)
    public void CalculateTotals()
    {
        // Calculate SubTotal from all items
        SubTotal = Items.Sum(item => item.SubTotal);
        
        // If there's a discount, distribute it proportionally across items
        if (DiscountAmount > 0 && SubTotal > 0)
        {
            // Calculate TotalVatAmount on discounted amounts
            TotalVatAmount = Items.Sum(item => 
            {
                // Proportionally distribute discount to this item
                var itemDiscountProportion = (item.SubTotal / SubTotal) * DiscountAmount;
                var discountedItemAmount = item.SubTotal - itemDiscountProportion;
                return discountedItemAmount * item.VatRate;
            });
        }
        else
        {
            // No discount, calculate VAT on full amounts
            TotalVatAmount = Items.Sum(item => item.VatAmount);
        }
        
        // Calculate final TotalAmount
        TotalAmount = SubTotal + TotalVatAmount + ShippingFee - DiscountAmount;
    }

    // Additional properties needed by services
    public decimal TotalPrice => TotalAmount; // Alias for compatibility
    public string? DeliveryAddress { get; protected set; }
    public string? Notes { get; protected set; }
    public DateTime? LastSyncedAt { get; protected set; }

    protected Order() { }

    public Order(TenantId tenantId, Guid? customerId, decimal totalAmount)
        : base(tenantId)
    {
        CustomerId = customerId;
        TotalAmount = totalAmount;
        OrderDate = DateTime.UtcNow;
    }

    // Business methods for order management
    public void UpdateOrderStatus(OrderStatusId status)
    {
        Status = status;
        UpdateAudit();
    }

    public void UpdateOrderDetails(OrderStatusId status, DateTime orderDate, string? deliveryAddress, string? notes)
    {
        Status = status;
        OrderDate = orderDate;
        DeliveryAddress = deliveryAddress;
        Notes = notes;
        UpdateAudit();
    }

    public void UpdateKitchenStatus(KitchenStatus status)
    {
        KitchenStatus = status;
        UpdateAudit();
    }

    public void MarkAsCompleted()
    {
        CompletedAt = DateTime.UtcNow;
        UpdateAudit();
    }

    public void UpdateVoiceNotes(string? voiceNoteText, string? voiceNoteAudioBlob)
    {
        VoiceNoteText = voiceNoteText;
        VoiceNoteAudioBlob = voiceNoteAudioBlob;
        UpdateAudit();
    }
}

// Demo User cho ShopERP với Multi-tenancy
public class DemoUser : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;
    public bool IsActive { get; set; } = true;
}

// Legacy record types cho compatibility - sẽ được migrate
public record ProductLegacy
{
    public required ProductId Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string DescriptionEn { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Category { get; init; } = string.Empty;
    public bool IsAvailable { get; init; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public record IngredientLegacy
{
    public required IngredientId Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty; // g, ml, cái, v.v.
    public decimal CostPerUnit { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public record RecipeItem
{
    public required IngredientId IngredientId { get; init; }
    public decimal Quantity { get; init; } // Số lượng định mức
}

public record RecipeLegacy
{
    public required RecipeId Id { get; init; }
    public required ProductId ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<RecipeItem> Items { get; init; } = Array.Empty<RecipeItem>();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

public record OrderLegacy
{
    public required OrderId Id { get; init; }
    public required ProductId ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal TotalPrice { get; init; }
    public DateTime OrderDate { get; init; } = DateTime.UtcNow;
    public string Status { get; init; } = "Pending"; // Pending, Completed, Cancelled
    public string? CustomerDeviceId { get; init; } // Zero-friction identity
    public string? CustomerShadowAccountId { get; init; } // Anonymous identity protection
    public bool ShouldPromptUpgrade { get; init; } // UI signal for loyalty upgrade
    
    // Workflow properties
    public OrderStatusId CurrentStatusId { get; set; } = new OrderStatusId("pending");
    public int EstimatedMinutes { get; init; } = 15; // Default 15 minutes
    public DateTime? StatusStartedAt { get; set; }
    
    // Add parameterless constructor for EF Core
    public OrderLegacy() {}
}

// Service Models
public record AudioFile
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string FileName { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddHours(24);
}

public record VietQrRequest
{
    public decimal Amount { get; init; }
    public string OrderDescription { get; init; } = string.Empty;
    public BankConfig BankConfig { get; init; } = new();
}

public record BankConfig
{
    public string BankId { get; init; } = string.Empty;
    public string AccountNo { get; init; } = string.Empty;
    public string AccountName { get; init; } = string.Empty;
}

public record VietQrResponse
{
    public Uri QrImageUrl { get; init; } = new Uri("https://example.com");
    public Uri PaymentUrl { get; init; } = new Uri("https://example.com");
    public decimal Amount { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

// Multi-Tenant UI Models
public record ShopConfig
{
    public Guid ShopId { get; init; } = Guid.TryParse("00000000-0000-0000-0000-000000000001", out Guid defaultShopId) ? defaultShopId : Guid.NewGuid();
    public string ShopName { get; init; } = "Vạn An Group"; // Default name
    public string PrimaryColor { get; init; } = "#8B4513"; // Default brown
    public string SecondaryColor { get; init; } = "#D2691E"; // Default chocolate
    public Uri LogoUrl { get; init; } = new Uri("/images/vanan-default-logo.png", UriKind.Relative);
    public string Address { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    // Social Media Links
    public string? SocialLinksFb { get; init; }
    public string? SocialLinksTiktok { get; init; }
    
    // UI Preferences
    public bool EnableDarkMode { get; init; }
    public ThemeType Theme { get; init; } = ThemeType.Classic;
    public ThemeType ActiveTheme { get; set; } = ThemeType.Classic;
    public Collection<string> Features { get; init; } = new();
    
    // Loyalty & Identity Configuration
    public LoyaltyUpgradeConfig LoyaltyConfig { get; init; } = new();
}

// Loyalty Upgrade Configuration for Identity System
public class LoyaltyUpgradeConfig
{
    public bool IsEnabled { get; set; } = true;
    public int MinOrdersForUpgrade { get; set; } = 3;
    public decimal MinTotalAmountForUpgrade { get; set; } = 500000m; // 500K VND
    public IReadOnlyCollection<string> RequiredFeatures { get; set; } = new List<string> { "loyalty", "identity" };
    public string UpgradeMessage { get; set; } = "Bảo vệ điểm của bạn và nhận thêm quà tặng!";
    public int BonusPointsOnUpgrade { get; set; } = 100;
}

// Theme Types for Dynamic Vibe Engine
public enum ThemeType
{
    Classic,      // Classic coffee shop theme
    Modern,       // Modern minimalist
    Teen,
    Lady,
    Premium
}

// 🛡️ PHASE 2: SOCIAL FLYWHEEL DOMAIN MODELS

// Social Campaign for O2O Flywheel
public class SocialCampaign : BaseEntity, IMustHaveTenant
{
    public Guid ShopId { get; protected set; }
    public string UtmSource { get; protected set; } = string.Empty;
    public string CampaignName { get; protected set; } = string.Empty;
    public string TrackingCode { get; protected set; } = string.Empty;
    public int TotalClicks { get; protected set; }
    public int ConvertedOrders { get; protected set; }
    public bool IsActive { get; protected set; } = true;
    
    // Navigation Properties
    public virtual Shop Shop { get; protected set; } = null!;

    protected SocialCampaign() { }

    public SocialCampaign(TenantId tenantId, Guid shopId, string utmSource, string campaignName, string trackingCode)
        : base(tenantId)
    {
        ShopId = shopId;
        UtmSource = utmSource;
        CampaignName = campaignName;
        TrackingCode = trackingCode;
    }

    // Business methods for campaign management
    public void IncrementClicks()
    {
        TotalClicks++;
        UpdateAudit();
    }

    public void IncrementConvertedOrders()
    {
        ConvertedOrders++;
        UpdateAudit();
    }

    public void UpdateCampaignDetails(string campaignName, string utmSource, bool isActive)
    {
        CampaignName = campaignName;
        UtmSource = utmSource;
        IsActive = isActive;
        UpdateAudit();
    }
}

// Loyalty Rewards System
public class LoyaltyRewards : BaseEntity, IMustHaveTenant
{
    public Guid CustomerId { get; protected set; }
    public int PointBalance { get; protected set; }
    public string History { get; protected set; } = string.Empty; // JSON serialized history
    public bool IsActive { get; protected set; } = true;
    
    // Navigation Properties
    public virtual DemoUser Customer { get; protected set; } = null!;

    protected LoyaltyRewards() { }

    public LoyaltyRewards(TenantId tenantId, Guid customerId)
        : base(tenantId)
    {
        CustomerId = customerId;
        PointBalance = 0;
        History = string.Empty;
        IsActive = true;
    }

    // Business methods for loyalty rewards management
    public void AddPoints(int points, string? reason = null)
    {
        PointBalance += points;
        UpdateAudit();
    }

    public void DeductPoints(int points, string? reason = null)
    {
        PointBalance = Math.Max(0, PointBalance - points);
        UpdateAudit();
    }

    public void UpdateHistory(string historyJson)
    {
        History = historyJson;
        UpdateAudit();
    }
}

// Voice Command Models
public class VoiceCommand
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CommandText { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsProcessed { get; set; }
}

public class TextCommandRequest
{
    public string CommandText { get; set; } = string.Empty;
    public string? OrderId { get; set; }
    public string? Parameters { get; set; }
}

public class TtsRequest
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = "vi-VN";
}

public class CleanupResult
{
    public bool CleanedFiles { get; set; }
    public int TotalExpired { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Error { get; set; }
}

// ====================== ACCOUNTING ENTRY FACTORY - DDD PATTERN ======================

/// <summary>
/// Factory for creating Accounting Entries - DDD Pattern
/// Updated to use decimal Amount (not Money Value Object) to match current domain model
/// Ensures business rules and immutability compliance
/// </summary>
public static class AccountingEntryFactory
{
    // Legacy factory methods removed - Using clean constructor-based approach above
}

// ====================== VALUE OBJECTS & EF CORE CONFIGURATIONS ======================

// Value Objects (LeadId thêm vào, CustomerId ã tón tai ó dòng 180)
public record LeadId(Guid Value);

// REMOVED: Value Objects must NOT use IEntityTypeConfiguration
// public class LeadIdConfiguration : IEntityTypeConfiguration<LeadId> - VIOLATION
// public class CustomerIdConfiguration : IEntityTypeConfiguration<CustomerId> - VIOLATION
