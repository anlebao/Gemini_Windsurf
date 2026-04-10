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

public enum VatRate
{
    Exempt = 0,         // Miễn thuế
    Zero = 0,           // 0%
    Five = 5,           // 5%
    Ten = 10            // 10%
}

/// <summary>
/// Accounting Entry - Append-Only (Immutable) for VAT 2026 Compliance
/// Direct Method for Household Businesses as per Vietnamese Tax Law 2026
/// </summary>
public class AccountingEntry : BaseEntity
{
    public decimal Amount { get; set; }
    public AccountingEntryType EntryType { get; set; }
    public VatRate VatRate { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// For reversal entries (Bút toán đảo) - VAT 2026 Compliance
    /// Direct updates/deletes are prohibited
    /// </summary>
    public Guid? ReversalEntryId { get; set; }
    
    /// <summary>
    /// Navigation to original entry if this is a reversal
    /// </summary>
    public virtual AccountingEntry? OriginalEntry { get; set; }
    
    /// <summary>
    /// Navigation to reversal entries if this has been reversed
    /// </summary>
    public virtual ICollection<AccountingEntry> ReversalEntries { get; set; } = new List<AccountingEntry>();
    
    /// <summary>
    /// Description for audit trail
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Reference to related entity (Order, Invoice, etc.)
    /// </summary>
    public Guid? ReferenceId { get; set; }
    
    /// <summary>
    /// Type of reference entity
    /// </summary>
    public string? ReferenceType { get; set; }
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
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    // 🛡️ PHASE 2: Navigation Properties for Social Flywheel
    public virtual ICollection<SocialCampaign> SocialCampaigns { get; } = new Collection<SocialCampaign>();
}

public class Product : BaseEntity
{
    public ProductId ProductId { get; set; } = new ProductId(Guid.NewGuid());
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }
    public decimal VatRate { get; set; } = 0.10m; // 10% default VAT for 2026 compliance
}

public record CustomerId(Guid Value);

// Customer CRM Entity for Loyalty & Tier Management
public class Customer : BaseEntity, IMustHaveTenant
{
    public CustomerId CustomerId { get; set; } = new CustomerId(Guid.NewGuid());
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int LoyaltyPoints { get; set; } = 0;
    public string CustomerTier { get; set; } = "Bronze"; // Bronze, Silver, Gold, Platinum
    public DateTime? LastOrderDate { get; set; }
    public decimal TotalSpent { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    
    // Device tracking for anonymous customer identification
    public Guid? DeviceId { get; set; }
    
    // Navigation Properties
    public virtual ICollection<Order> Orders { get; } = new Collection<Order>();
}

public record OrderItemId(Guid Value);

// OrderItem for Multi-item Order Support
public class OrderItem : BaseEntity
{
    public OrderItemId OrderItemId { get; set; } = new OrderItemId(Guid.NewGuid());
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; } = 0.10m;
    public string? Notes { get; set; } // Customizations (size, sugar level, etc.)
    
    // 🆕 GOLDEN FLOW: Kitchen Status (Operational Only)
    public KitchenStatus KitchenStatus { get; set; } = KitchenStatus.Pending;
    
    // 🆕 GOLDEN FLOW: Voice Note Properties (Operational Only)
    // REMOVED: DataAnnotations violate Domain purity (FAIL-FAST MVP)
    // [MaxLength(500)]  // 🛡️ DEFENSIVE: Max 500 chars
    public string? ItemNoteText { get; set; }
    
    // [MaxLength(150000)] // 🛡️ DEFENSIVE: Max ~110KB Base64
    public string? ItemNoteAudioBlob { get; set; }
    
    // Calculated Fields (🛡️ FINANCIAL PROTECTION - DO NOT MUTATE)
    public decimal SubTotal => Quantity * UnitPrice;
    public decimal VatAmount => SubTotal * VatRate;
    public decimal TotalAmount => SubTotal + VatAmount;
    
    // Navigation Properties
    // REMOVED: DataAnnotations violate Domain purity (FAIL-FAST MVP)
    // [ForeignKey(nameof(OrderId))]
    public Order Order { get; set; } = null!;
    // [ForeignKey(nameof(ProductId))]
    public Product Product { get; set; } = null!;
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
    public InventoryId InventoryId { get; set; } = new InventoryId(Guid.NewGuid());
    public Guid IngredientId { get; set; } // 🛡️ PHASE 3 FIX: Use Guid instead of IngredientId
    public decimal Quantity { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    // REMOVED: DataAnnotations violate Domain purity (FAIL-FAST MVP)
    // [ForeignKey(nameof(IngredientId))]
    public Ingredient Ingredient { get; set; } = null!;
}

public class Order : BaseEntity
{
    public OrderId OrderId { get; set; } = new OrderId(Guid.NewGuid());
    
    // Customer Information (CRM Integration)
    public Guid? CustomerId { get; set; }
    public string? CustomerDeviceId { get; set; } // Zero-friction identity fallback
    
    // Order Details
    public string OrderType { get; set; } = "DINEIN"; // DINEIN, TAKEAWAY, DELIVERY
    public OrderStatusId Status { get; set; } = new OrderStatusId("Draft");
    
    // Voice & Text Commands for KDS
    public string? TextCommand { get; set; }
    public string? VoiceCommandUrl { get; set; }
    
    // 🆕 GOLDEN FLOW: Voice Note Properties (Operational Only)
    // REMOVED: DataAnnotations violate Domain purity (FAIL-FAST MVP)
    // [MaxLength(500)]  // 🛡️ DEFENSIVE: Max 500 chars
    public string? VoiceNoteText { get; set; }
    
    // [MaxLength(150000)] // 🛡️ DEFENSIVE: Max ~110KB Base64
    public string? VoiceNoteAudioBlob { get; set; }
    
    // 🆕 GOLDEN FLOW: Kitchen Status (Operational Only)
    public KitchenStatus KitchenStatus { get; set; } = KitchenStatus.Pending;
    
    // Financial Calculations (🛡️ 2026 Tax Compliance - DO NOT MUTATE)
    public decimal SubTotal { get; set; } = 0;
    public decimal TotalVatAmount { get; set; } = 0;
    public decimal ShippingFee { get; set; } = 0;
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TotalAmount { get; set; } = 0;
    
    // Payment Information
    public string? PaymentMethod { get; set; } // CASH, VIETQR, CREDIT_CARD
    public string? PaymentStatus { get; set; } = "Pending"; // Pending, Paid, Failed, Refunded
    public string? VietQR_TransactionId { get; set; }
    public string? VietQR_Payload { get; set; }
    
    // Timestamps
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    // Notes & Metadata
    public string? CustomerNotes { get; set; }
    public string? StaffNotes { get; set; }
    public string? TrackingCode { get; set; } // Social campaign tracking
    public bool IsSyncedToCoreHub { get; set; } = false;
    
    // Navigation Properties
    // REMOVED: DataAnnotations violate Domain purity (FAIL-FAST MVP)
    // [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; } = null;
    public virtual ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    
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
    public Guid ShopId { get; set; }
    public string UtmSource { get; set; } = string.Empty;
    public string CampaignName { get; set; } = string.Empty;
    public string TrackingCode { get; set; } = string.Empty;
    public int TotalClicks { get; set; }
    public int ConvertedOrders { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation Properties
    public virtual Shop Shop { get; set; } = null!;
}

// Loyalty Rewards System
public class LoyaltyRewards : BaseEntity, IMustHaveTenant
{
    public Guid CustomerId { get; set; }
    public int PointBalance { get; set; }
    public string History { get; set; } = string.Empty; // JSON serialized history
    public bool IsActive { get; set; } = true;
    
    // Navigation Properties
    public virtual DemoUser Customer { get; set; } = null!;
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

// ====================== VALUE OBJECTS & EF CORE CONFIGURATIONS ======================

// Value Objects (LeadId thêm vào, CustomerId ã tón tai ó dòng 180)
public record LeadId(Guid Value);

// REMOVED: Value Objects must NOT use IEntityTypeConfiguration
// public class LeadIdConfiguration : IEntityTypeConfiguration<LeadId> - VIOLATION
// public class CustomerIdConfiguration : IEntityTypeConfiguration<CustomerId> - VIOLATION
