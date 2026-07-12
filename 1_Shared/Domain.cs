using System.Collections.ObjectModel;
using VanAn.Shared.Domain.Common;

// REMOVED: EF Core dependencies violate Domain purity
// using Microsoft.EntityFrameworkCore;
// using Microsoft.EntityFrameworkCore.Metadata.Builders;
// using System.ComponentModel.DataAnnotations;
// using System.ComponentModel.DataAnnotations.Schema;

namespace VanAn.Shared.Domain
{
    // Accounting Foundation - VAT 2026 Compliant
    public enum AccountingEntryType
    {
        Revenue = 1,        // Doanh thu
        Expense = 2,        // Chi phí
        TaxPayment = 3,     // Thuế
        Adjustment = 4      // Điều chỉnh
    }

    // Financial Safety Infrastructure
    public enum EventStatus
    {
        Pending = 1,
        Processed = 2,
        Failed = 3
    }

    public enum OperationPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Business Type Classification - Company vs Household Business
    /// </summary>
    public enum BusinessType
    {
        Company = 1,           // Doanh nghiệp (Company)
        HouseholdBusiness = 2  // Hộ kinh doanh (Household Business)
    }

    /// <summary>
    /// Household Business Groups - 3 sub-groups per Vietnamese regulations
    /// </summary>
    public enum HKDGroup
    {
        Group1 = 1,  // S1a-HKD: Không chịu thuế GTGT, không nộp thuế TNCN
        Group2 = 2,  // S2a-HKD, S2b-HKD, S2c-HKD, S2d-HKD, S2e-HKD: Nộp thuế GTGT và TNCN
        Group3 = 3   // S3a-HKD: Hộ kinh doanh có hoạt động thuộc diện chịu các loại thuế khác
    }

    /// <summary>
    /// Accounting Book Types - Company vs HKD (7 types for HKD per Thông tư 152/2025/TT-BTC)
    /// </summary>
    public enum AccountingBookType
    {
        // COMPANY BOOKS
        RevenueBook = 1,    // Sách chi doanh thu
        ExpenseBook = 2,    // Sách chi chi phí
        CashBankBook = 3,   // Sách chi tiền mặt ngân hàng
        TaxDeclarationBook = 4,  // Sách chi kê khai thuế

        // HKD BOOKS - 7 types per Thông tư 152/2025/TT-BTC
        S1a_HKD = 5,        // Sổ theo dõi hàng hóa, dịch vụ cung ứng (không chịu thuế GTGT)
        S2a_HKD = 6,        // Sổ theo dõi hàng hóa, dịch vụ cung ứng (nộp thuế GTGT theo tỷ lệ %)
        S2b_HKD = 7,        // Sổ doanh thu bán hàng hóa, dịch vụ
        S2c_HKD = 8,        // Sổ chi tiết doanh thu, chi phí
        S2d_HKD = 9,        // Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa
        S2e_HKD = 10,       // Sổ chi tiết tiền
        S3a_HKD = 11        // Sổ theo dõi hoạt động thuộc diện chịu các loại thuế khác
    }

    public enum VatRate
    {
        Exempt = -1,        // Miễn thuế
        Zero = 0,           // 0%
        Five = 5,           // 5%
        Ten = 10            // 10%
    }

    /// <summary>
    /// 4 industry sector groups per Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025.
    /// Determines VAT + PIT rate for HKD Group 2 businesses (TT 152/2025/TT-BTC S2a/S2b layout).
    /// TT 152 allows up to 5 industry groups; the 5th group maps to <see cref="OtherBusiness"/>.
    /// </summary>
    public enum IndustrySector
    {
        /// <summary>Phân phối, cung cấp hàng hóa — GTGT 1%, TNCN 0.5%.</summary>
        Distribution = 1,
        /// <summary>Sản xuất, vận tải, dịch vụ gắn hàng hóa, xây dựng bao thầu NVL — GTGT 3%, TNCN 1.5%.</summary>
        ProductionTransport = 2,
        /// <summary>Dịch vụ, xây dựng không bao thầu NVL — GTGT 5%, TNCN 2%.</summary>
        Service = 3,
        /// <summary>Hoạt động kinh doanh khác — GTGT 2%, TNCN 1%. Also the fallback bucket for entries with NULL IndustrySector (ensures TotalRevenue = SUM(all sector revenues) always holds).</summary>
        OtherBusiness = 4
    }

    /// <summary>
    /// Accounting Period for Household Business reporting
    /// </summary>
    public record AccountingPeriod(int Year, int Month)
    {
        public override string ToString()
        {
            return $"{Year:0000}-{Month:00}";
        }

        public static AccountingPeriod FromDateTime(DateTime date)
        {
            return new(date.Year, date.Month);
        }

        public DateTime ToDateTime()
        {
            return new(Year, Month, 1);
        }

        public DateTime StartDate => new(Year, Month, 1);
        public DateTime EndDate => StartDate.AddMonths(1).AddTicks(-1);

        // Static Create method for test files compatibility
        public static AccountingPeriod Create(int year, int month)
        {
            return new(year, month);
        }
    }

    /// <summary>
    /// Tenant ID Value Object with Business Context
    /// </summary>
    public record TenantId(Guid Value)
    {
        public static TenantId Empty { get; } = new TenantId(Guid.Empty);

        public static implicit operator Guid(TenantId tenantId)
        {
            return tenantId.Value;
        }

        public static implicit operator TenantId(Guid value)
        {
            return new(value);
        }

        public static TenantId FromGuid(Guid value)
        {
            return new(value);
        }

        public Guid ToGuid()
        {
            return Value;
        }

        public bool IsEmpty()
        {
            return Value == Guid.Empty;
        }

        public bool IsNotEmpty()
        {
            return Value != Guid.Empty;
        }
    }

    /// <summary>
    /// Tenant with Business Type and HKD Classification
    /// </summary>
    /// <remarks>
    /// [Wave 5] OBSOLETE — use <see cref="VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant"/> (Rich Domain class).
    /// This record is kept to avoid breaking existing EF mappings until TenantConfiguration.cs is migrated.
    /// </remarks>
    [Obsolete("Use VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant instead. Will be removed in Wave 6.")]
    public record Tenant
    {
        public TenantId Id { get; init; } = null!;
        public string Name { get; init; } = string.Empty;
        public BusinessType BusinessType { get; init; }
        public HKDGroup? HKDGroup { get; init; } // Only for Household Business
        public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public bool IsActive { get; init; } = true;

        // Wave 5 (approved 2026-07-03): Default industry sector for HKD Group 2 tenants.
        // Nullable — existing tenants get NULL, must be set before generating S2a/S2b.
        // Used as fallback when Order.IndustrySector is not set.
        public IndustrySector? DefaultIndustrySector { get; init; }

        public static Tenant CreateCompany(TenantId id, string name)
        {
            return new Tenant
            {
                Id = id,
                Name = name,
                BusinessType = BusinessType.Company
            };
        }

        public static Tenant CreateHouseholdBusiness(TenantId id, string name, HKDGroup hkdGroup)
        {
            return new Tenant
            {
                Id = id,
                Name = name,
                BusinessType = BusinessType.HouseholdBusiness,
                HKDGroup = hkdGroup
            };
        }

        public bool IsHouseholdBusiness()
        {
            return BusinessType == BusinessType.HouseholdBusiness;
        }

        public bool IsCompany()
        {
            return BusinessType == BusinessType.Company;
        }
    }

    /// <summary>
    /// Accounting Entry ID Value Object
    /// </summary>
    public record AccountingEntryId(Guid Value)
    {
        public static implicit operator Guid(AccountingEntryId entryId)
        {
            return entryId.Value;
        }

        public static implicit operator AccountingEntryId(Guid value)
        {
            return new(value);
        }

        public static AccountingEntryId FromGuid(Guid value)
        {
            return new(value);
        }

        public Guid ToGuid()
        {
            return Value;
        }
    }

    /// <summary>
    /// Money Value Object
    /// </summary>
    public record Money(decimal Value)
    {
        public static implicit operator decimal(Money money)
        {
            return money.Value;
        }

        public static implicit operator Money(decimal value)
        {
            return new(value);
        }

        public static Money FromDecimal(decimal value)
        {
            return new(value);
        }

        public decimal ToDecimal()
        {
            return Value;
        }

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

        // DMD-1 fix (approved 2026-06-20): Accounting classification fields
        // AccountCode: mã tài khoản kế toán (511, 621, 642...) — bắt buộc TT 152/2025/TT-BTC
        // Vendor: nhà cung cấp (dùng cho bút toán chi phí)
        // Category: danh mục chi phí/doanh thu (materials, utilities, services...)
        // Reference: số hóa đơn/chứng từ tham chiếu
        public string? AccountCode { get; }
        public string? Vendor { get; }
        public string? Category { get; }
        public string? Reference { get; }

        // Wave 5 (approved 2026-07-03): Industry sector for TT 152 S2a/S2b industry-group split.
        // Nullable for backward compatibility (existing entries get NULL → counted in OtherBusiness group).
        // Immutable after creation (no setter) — preserves AccountingEntry immutability.
        public IndustrySector? IndustrySector { get; }

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
            Guid? reversalEntryId = null,
            string? accountCode = null,
            string? vendor = null,
            string? category = null,
            string? reference = null,
            IndustrySector? industrySector = null)
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
            AccountCode = accountCode;
            Vendor = vendor;
            Category = category;
            Reference = reference;
            IndustrySector = industrySector;
        }

        // ====================== FACTORY METHODS ======================
        public static AccountingEntry CreateRevenue(TenantId tenantId, AccountingPeriod period, Money amount, string description,
            string? accountCode = null, string? reference = null, IndustrySector? industrySector = null)
        {
            return new(tenantId, amount.Value, AccountingEntryType.Revenue, VatRate.Zero,
                AccountingBookType.RevenueBook, period.Year, period.Month, description,
                reversalEntryId: null, accountCode: accountCode, reference: reference, industrySector: industrySector);
        }

        public static AccountingEntry CreateExpense(TenantId tenantId, AccountingPeriod period, Money amount, string description,
            string? accountCode = null, string? vendor = null, string? category = null, string? reference = null,
            IndustrySector? industrySector = null)
        {
            return new(tenantId, amount.Value, AccountingEntryType.Expense, VatRate.Zero,
                AccountingBookType.ExpenseBook, period.Year, period.Month, description,
                reversalEntryId: null, accountCode: accountCode, vendor: vendor, category: category, reference: reference,
                industrySector: industrySector);
        }

        public static AccountingEntry CreateReversal(AccountingEntry original, string reason)
        {
            ArgumentNullException.ThrowIfNull(original);
            return string.IsNullOrWhiteSpace(reason)
                ? throw new ArgumentException("Reason required", nameof(reason))
                : new AccountingEntry(
                original.TenantId,
                -original.Amount,
                original.EntryType,
                original.VatRate,
                original.AccountingBookType,
                original.PeriodYear,
                original.PeriodMonth,
                $"Reversal of: {original.Description} - {reason}",
                original.Id,
                accountCode: original.AccountCode,
                industrySector: original.IndustrySector);
        }

        public static AccountingEntry CreateReversalWithId(AccountingEntry original, string reason, Guid originalEntryId)
        {
            ArgumentNullException.ThrowIfNull(original);
            return string.IsNullOrWhiteSpace(reason)
                ? throw new ArgumentException("Reason required", nameof(reason))
                : new AccountingEntry(
                original.TenantId,
                -original.Amount,
                original.EntryType,
                original.VatRate,
                original.AccountingBookType,
                original.PeriodYear,
                original.PeriodMonth,
                $"Reversal of: {original.Description} - {reason}",
                originalEntryId,
                accountCode: original.AccountCode,
                industrySector: original.IndustrySector);
        }
    }

    public record ProductId(Guid Value);
    public record IngredientId(Guid Value);
    public record RecipeId(Guid Value);
    public record InventoryId(Guid Value);
    public record OrderId(Guid Value);
    public record OrderStatusId(string Value)
    {
        // ✅ FIXED: Add static properties for UI compatibility
        public static readonly OrderStatusId Pending = new("pending");
        public static readonly OrderStatusId Confirmed = new("confirmed");
        public static readonly OrderStatusId Preparing = new("preparing");
        public static readonly OrderStatusId Ready = new("ready");
        public static readonly OrderStatusId Delivering = new("delivering");
        public static readonly OrderStatusId Completed = new("completed");
        public static readonly OrderStatusId Cancelled = new("cancelled");
        public static readonly OrderStatusId Processing = new("preparing"); // Alias for compatibility
    };
    public record ShopId(Guid Value);

    // Identity Schema - RBAC for ShopERP
    [Obsolete("Use VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole instead.")]
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
        public static readonly OrderStatusDefinition[] Default =
        [
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
        ];
    }

    // Core Entities với Multi-tenancy
    public class Shop : BaseEntity, IMustHaveTenant
    {
        public string Name { get; protected set; } = string.Empty;
        public string Address { get; protected set; } = string.Empty;
        public string Phone { get; protected set; } = string.Empty;
        public string Email { get; protected set; } = string.Empty;
        public bool IsActive { get; protected set; } = true;

        // W17-T5: Store Finder — geographic coordinates (approved Domain change)
        public double? Latitude { get; protected set; }
        public double? Longitude { get; protected set; }

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

        // W17-T5: Set geographic coordinates for Store Finder
        public void SetCoordinates(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
            UpdateAudit();
        }
    }

    public class Product : BaseEntity
    {
        public ProductId ProductId { get; protected set; } = new ProductId(Guid.NewGuid());
        public string Name { get; protected set; } = string.Empty;
        public string Description { get; protected set; } = string.Empty;
        public decimal Price { get; protected set; }
        public decimal CostPrice { get; protected set; } = 0m; // Giá vốn (cost of goods) — DMD-2 fix
        public string Category { get; protected set; } = string.Empty;
        public bool IsActive { get; protected set; } = true;
        public string? ImageUrl { get; protected set; }
        public decimal VatRate { get; protected set; } = 0.10m; // 10% default VAT for 2026 compliance

        public Product() { } // Public constructor for UI layer

        public Product(TenantId tenantId, string name, decimal price, string category, decimal costPrice = 0m)
            : base(tenantId)
        {
            Name = name;
            Price = price;
            Category = category;
            CostPrice = costPrice;
        }

        public Product(TenantId tenantId, string name, string description, decimal price, string category, bool isActive = true, string? imageUrl = null, decimal vatRate = 0.10m, decimal costPrice = 0m)
            : base(tenantId)
        {
            Name = name;
            Description = description;
            Price = price;
            Category = category;
            IsActive = isActive;
            ImageUrl = imageUrl;
            VatRate = vatRate;
            CostPrice = costPrice;
        }

        /// <summary>
        /// Update the cost price of the product (used by admin/procurement workflows).
        /// </summary>
        public void UpdateCostPrice(decimal costPrice)
        {
            if (costPrice < 0) throw new ArgumentException("CostPrice cannot be negative.", nameof(costPrice));
            CostPrice = costPrice;
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
        public int LoyaltyPoints { get; protected set; }
        public string CustomerTier { get; protected set; } = "Bronze"; // Bronze, Silver, Gold, Platinum
        public DateTime? LastOrderDate { get; protected set; }
        public decimal TotalSpent { get; protected set; } = 0;
        public bool IsActive { get; protected set; } = true;

        // Device tracking for anonymous customer identification
        public Guid? DeviceId { get; protected set; }

        // Navigation Properties
        public virtual ICollection<Order> Orders { get; } = new Collection<Order>();
        public virtual LoyaltyRewards LoyaltyRewards { get; protected set; } = null!;

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

    // Wave 9: Push Subscription Entity for Web Push Notifications
    // Separate table (per user decision) to avoid Domain layer changes
    public class PushSubscription : BaseEntity, IMustHaveTenant
    {
        public Guid PushSubscriptionId { get; protected set; } = Guid.NewGuid();
        public Guid CustomerId { get; protected set; }
        public string SubscriptionJson { get; protected set; } = string.Empty;
        public string? UserAgent { get; protected set; } // Browser/device info
        public bool IsActive { get; protected set; } = true;
        public DateTime? LastUsedAt { get; protected set; }
        public DateTime? ExpiresAt { get; protected set; } // Subscription expiration

        protected PushSubscription() { }

        public PushSubscription(TenantId tenantId, Guid customerId, string subscriptionJson, string? userAgent = null)
            : base(tenantId)
        {
            if (string.IsNullOrWhiteSpace(subscriptionJson))
                throw new ArgumentException("Subscription JSON cannot be empty", nameof(subscriptionJson));

            CustomerId = customerId;
            SubscriptionJson = subscriptionJson;
            UserAgent = userAgent;
            LastUsedAt = DateTime.UtcNow;
            // Push subscriptions typically expire after subscription renewal period (e.g., 24 hours)
            ExpiresAt = DateTime.UtcNow.AddDays(1);
        }

        public void UpdateSubscription(string subscriptionJson, string? userAgent = null)
        {
            if (string.IsNullOrWhiteSpace(subscriptionJson))
                throw new ArgumentException("Subscription JSON cannot be empty", nameof(subscriptionJson));

            SubscriptionJson = subscriptionJson;
            UserAgent = userAgent;
            LastUsedAt = DateTime.UtcNow;
            ExpiresAt = DateTime.UtcNow.AddDays(1);
            UpdateAudit();
        }

        public void MarkAsInactive()
        {
            IsActive = false;
            UpdateAudit();
        }

        public void Renew()
        {
            ExpiresAt = DateTime.UtcNow.AddDays(1);
            LastUsedAt = DateTime.UtcNow;
            UpdateAudit();
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

        // ✅ FIXED: Add missing properties for UI compatibility
        public string ProductName { get; protected set; } = string.Empty;
        public decimal TotalPrice => TotalAmount; // Alias for UI compatibility

        //  GOLDEN FLOW: Kitchen Status (Operational Only)
        public KitchenStatus KitchenStatus { get; protected set; } = KitchenStatus.Pending;

        //  GOLDEN FLOW: Voice Note Properties (Operational Only)
        public string? ItemNoteText { get; protected set; }
        [Obsolete("Audio storage removed per requirements v1.2 — STT only. TTS reads text at kitchen.")]
        public string? ItemNoteAudioBlob { get; protected set; }

        protected OrderItem() { }

        public OrderItem(TenantId tenantId, Guid orderId, Guid productId, int quantity, decimal unitPrice, string productName = "")
            : base(tenantId)
        {
            OrderId = orderId;
            ProductId = productId;
            Quantity = quantity;
            UnitPrice = unitPrice;
            ProductName = productName;
        }

        /// <summary>
        /// DDD Compliant Static Factory Method - Domain-Driven Design
        /// Phase 2.5.4: Unified API Integration - Single Backend Service
        /// Creates a new OrderItem entity with proper domain encapsulation
        /// </summary>
        public static OrderItem Create(Guid id, TenantId tenantId, Guid orderId, Guid productId, int quantity, decimal unitPrice, string productName = "")
        {
            OrderItem orderItem = new(tenantId, orderId, productId, quantity, unitPrice, productName);

            // Use internal access to set protected Id property
            Type orderItemType = typeof(OrderItem);
            System.Reflection.PropertyInfo? idProperty = orderItemType.GetProperty("Id");
            idProperty?.SetValue(orderItem, id);

            return orderItem;
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

        // EF Core constructor for materialization
        protected Ingredient() { }
    }

    public class Recipe : BaseEntity
    {
        public RecipeId RecipeId { get; set; } = new RecipeId(Guid.NewGuid());
        public Guid ProductId { get; set; } // 🛡️ PHASE 3 FIX: Use Guid instead of ProductId
        public Guid IngredientId { get; set; } // 🛡️ PHASE 3 FIX: Use Guid instead of IngredientId
        public decimal QuantityNeeded { get; set; }

        // Navigation properties
        // REMOVED: DataAnnotations violate Domain purity (FAIL-FAST MVP)
        public Product Product { get; set; } = null!;
        public Ingredient Ingredient { get; set; } = null!;

        // EF Core constructor for materialization
        protected Recipe() { }
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

        // ✅ FIXED: Add CustomerInfo property for UI compatibility
        public CustomerInfo? CustomerInfo { get; protected set; }

        // Order Details
        public string OrderType { get; protected set; } = "DINEIN"; // DINEIN, TAKEAWAY, DELIVERY
        public OrderStatusId Status { get; protected set; } = new OrderStatusId("Draft");

        // Voice & Text Commands for KDS
        public string? TextCommand { get; protected set; }
        public string? VoiceCommandUrl { get; protected set; }

        //  GOLDEN FLOW: Voice Note Properties (Operational Only)
        public string? VoiceNoteText { get; protected set; }
        [Obsolete("Audio storage removed per requirements v1.2 — STT only. TTS reads text at kitchen.")]
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
        public bool IsSyncedToCoreHub { get; protected set; }

        // Wave 5 (approved 2026-07-03): Industry sector override for this order.
        // Nullable — existing orders get NULL, falls back to Tenant.DefaultIndustrySector.
        // Per-order override: if set, takes precedence over Tenant default.
        public IndustrySector? IndustrySector { get; protected set; }

        // Navigation Properties
        public Customer? Customer { get; protected set; }
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
                    decimal itemDiscountProportion = item.SubTotal / SubTotal * DiscountAmount;
                    decimal discountedItemAmount = item.SubTotal - itemDiscountProportion;
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

        /// <summary>
        /// DDD Compliant Static Factory Method - Domain-Driven Design
        /// Phase 2.5.4: Unified API Integration - Single Backend Service
        /// Creates a new Order entity with proper domain encapsulation
        /// </summary>
        public static Order Create(Guid id, TenantId tenantId, Guid? customerId, List<OrderItem> items)
        {
            Order order = new(tenantId, customerId, 0);

            // Use internal access to set protected properties
            Type orderType = typeof(Order);

            // Set Id
            System.Reflection.PropertyInfo? idProperty = orderType.GetProperty("Id");
            idProperty?.SetValue(order, id);

            // Set Status to Pending
            System.Reflection.PropertyInfo? statusProperty = orderType.GetProperty("Status");
            statusProperty?.SetValue(order, OrderStatusId.Pending);

            // Set Items collection
            System.Reflection.PropertyInfo? itemsProperty = orderType.GetProperty("Items");
            itemsProperty?.SetValue(order, items);

            // Calculate totals using domain method
            order.CalculateTotals();

            return order;
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

        public void MarkAsSynced()
        {
            LastSyncedAt = DateTime.UtcNow;
            UpdateAudit();
        }

        public void SetCustomerDeviceId(string deviceFingerprint)
        {
            CustomerDeviceId = deviceFingerprint;
            UpdateAudit();
        }

        /// <summary>
        /// Bucket A: Attach guest customer info to order (approved 2026-07-07).
        /// CustomerInfo value object is persisted via EF Core OwnsOne.
        /// </summary>
        public void SetCustomerInfo(CustomerInfo info)
        {
            CustomerInfo = info;
            UpdateAudit();
        }

        /// <summary>
        /// Wave 5: Set per-order industry sector override (TT 152 S2a/S2b).
        /// If set, takes precedence over Tenant.DefaultIndustrySector when generating accounting entries.
        /// </summary>
        public void SetIndustrySector(IndustrySector? sector)
        {
            IndustrySector = sector;
            UpdateAudit();
        }

        public void UpdateVoiceNotes(string? voiceNoteText, string? voiceNoteAudioBlob)
        {
            VoiceNoteText = voiceNoteText;
            VoiceNoteAudioBlob = voiceNoteAudioBlob;
            UpdateAudit();
        }

        /// <summary>
        /// Sprint B: Confirm payment — marks order as Paid and records transaction.
        /// Accounting entries MUST only be generated AFTER this is called.
        /// TT 152/2025/TT-BTC: doanh thu ghi nhận theo thực thu (cash-basis).
        /// </summary>
        public void ConfirmPayment(string transactionId, string paymentMethod = "VIETQR")
        {
            if (PaymentStatus == "Paid")
                throw new InvalidOperationException($"Order {Id} payment already confirmed. Idempotency guard.");

            PaymentStatus = "Paid";
            PaymentMethod = paymentMethod;
            VietQR_TransactionId = transactionId;
            UpdateAudit();
        }
    }

    // Demo User cho ShopERP với Multi-tenancy
    [Obsolete("Use VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser instead.")]
    public class DemoUser : BaseEntity
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public UserRole Role { get; set; } = UserRole.Staff;
        public bool IsActive { get; set; } = true;

        // EF Core constructor for materialization
        protected DemoUser() { }
    }

    /// <summary>
    /// User-Tenant mapping entity — Cross-tenant entity (không kế thừa BaseEntity để tránh query filter)
    /// Domain Purity: NO EF Core, NO DataAnnotations
    /// </summary>
    [Obsolete("Use VanAn.Shared.Domain.Aggregates.UserAggregate.UserTenant instead.")]
    public class UserTenant
    {
        public Guid Id { get; protected set; } = Guid.NewGuid();

        // Reference to User (DemoUser)
        public Guid UserId { get; protected set; }

        // Reference to Tenant — cross-tenant entity nên dùng Guid thay vì TenantId strongly-typed
        public Guid TenantId { get; protected set; }

        // Role within this tenant (có thể khác với global role)
        public string Role { get; protected set; } = string.Empty;

        // Assignment timestamp
        public DateTime AssignedAt { get; protected set; } = DateTime.UtcNow;

        // Soft delete flag
        public bool IsActive { get; protected set; } = true;

        // EF Core constructor
        protected UserTenant() { }

        public UserTenant(Guid userId, Guid tenantId, string role)
        {
            if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty", nameof(userId));
            if (tenantId == Guid.Empty) throw new ArgumentException("TenantId cannot be empty", nameof(tenantId));
            if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role cannot be empty", nameof(role));

            UserId = userId;
            TenantId = tenantId;
            Role = role;
            AssignedAt = DateTime.UtcNow;
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void Reactivate()
        {
            IsActive = true;
        }

        public void ChangeRole(string newRole)
        {
            if (string.IsNullOrWhiteSpace(newRole)) throw new ArgumentException("Role cannot be empty", nameof(newRole));
            Role = newRole;
        }
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
        public OrderLegacy() { }
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
        public Collection<string> Features { get; init; } = [];

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
        public virtual Customer Customer { get; protected set; } = null!;

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

    // Financial Safety Infrastructure - Domain Entities
    public sealed class IdempotentOperation : BaseEntity
    {
        public string OperationId { get; } = string.Empty;
        public string OperationType { get; } = string.Empty;
        public string Result { get; } = string.Empty;
        public DateTime ProcessedAt { get; }

        protected IdempotentOperation() { }

        public IdempotentOperation(string operationId, string operationType, string result, DateTime processedAt)
            : base(TenantId.Empty) // System-level operations don't need tenant
        {
            OperationId = operationId;
            OperationType = operationType;
            Result = result;
            ProcessedAt = processedAt;
        }
    }

    public sealed class EntityVersion : BaseEntity
    {
        public string EntityType { get; } = string.Empty;
        public string EntityId { get; } = string.Empty;
        public int Version { get; }
        public string Changes { get; } = string.Empty;
        public string ChangedBy { get; } = string.Empty;
        public DateTime ChangedAt { get; }

        protected EntityVersion() { }

        public EntityVersion(string entityType, string entityId, int version, string changes, string changedBy, DateTime changedAt)
            : base(TenantId.Empty) // System-level operations don't need tenant
        {
            EntityType = entityType;
            EntityId = entityId;
            Version = version;
            Changes = changes;
            ChangedBy = changedBy;
            ChangedAt = changedAt;
        }
    }

    public sealed class QueuedEvent : BaseEntity
    {
        public string EventId { get; } = string.Empty;
        public string EventType { get; } = string.Empty;
        public string EventData { get; } = string.Empty;
        public string EntityId { get; } = string.Empty;
        public int Priority { get; }
        public EventStatus Status { get; private set; } = EventStatus.Pending;
        public DateTime QueuedAt { get; }
        public DateTime? ProcessedAt { get; private set; }
        public string? ErrorMessage { get; private set; }

        protected QueuedEvent() { }

        public QueuedEvent(string eventId, string eventType, string eventData, string entityId, int priority, DateTime queuedAt)
            : base(TenantId.Empty) // System-level operations don't need tenant
        {
            EventId = eventId;
            EventType = eventType;
            EventData = eventData;
            EntityId = entityId;
            Priority = priority;
            QueuedAt = queuedAt;
        }

        public void MarkAsProcessed(DateTime processedAt)
        {
            Status = EventStatus.Processed;
            ProcessedAt = processedAt;
            UpdateAudit();
        }

        public void MarkAsFailed(string error, DateTime processedAt)
        {
            Status = EventStatus.Failed;
            ErrorMessage = error;
            ProcessedAt = processedAt;
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

    // ====================== HKD BOOKS DOMAIN ENTITIES ======================

    /// <summary>
    /// General Ledger entry with running balance - Sổ Cái
    /// </summary>
    public record GeneralLedgerEntry(
        string AccountNumber,
        string AccountName,
        DateTime TransactionDate,
        string Description,
        decimal DebitAmount,
        decimal CreditAmount,
        decimal RunningBalance,
        string ReferenceType,
        Guid ReferenceId
    );

    /// <summary>
    /// Detailed Ledger entry for specific account - Sổ Chi tiết
    /// </summary>
    public record DetailedLedgerEntry(
        DateTime TransactionDate,
        string Description,
        decimal DebitAmount,
        decimal CreditAmount,
        decimal Balance,
        string ReferenceType,
        Guid ReferenceId
    );

    /// <summary>
    /// Trial Balance summary for period - Sổ Tổng hợp
    /// </summary>
    public record TrialBalance(
        AccountingPeriod Period,
        DateTime GeneratedAt,
        IEnumerable<TrialBalanceAccount> Accounts,
        decimal TotalDebit,
        decimal TotalCredit,
        bool IsBalanced
    );

    /// <summary>
    /// Trial Balance account summary
    /// </summary>
    public record TrialBalanceAccount(
        string AccountNumber,
        string AccountName,
        decimal DebitTotal,
        decimal CreditTotal,
        decimal Balance
    );

    /// <summary>
    /// Complete HKD Books package
    /// </summary>
    public record HKDBooksPackage(
        TenantId TenantId,
        AccountingPeriod Period,
        IEnumerable<JournalEntry> GeneralJournal,
        IEnumerable<GeneralLedgerEntry> GeneralLedger,
        Dictionary<string, IEnumerable<DetailedLedgerEntry>> DetailedLedgers,
        TrialBalance TrialBalance,
        DateTime GeneratedAt
    );

    // ====================== SPRINT 2: PERIOD CLOSING ======================

    /// <summary>
    /// Status of an accounting period closing workflow
    /// </summary>
    public enum PeriodClosingStatus
    {
        Open,
        Validating,
        Closing,
        Closed,
        Reopening
    }

    /// <summary>
    /// Result of pre-closing validation checks
    /// </summary>
    public record PeriodClosingCheckResult(bool IsValid, List<string> Errors, List<string> Warnings);

    /// <summary>
    /// Immutable record of a period closing action — Reversal Entry pattern for reopening
    /// </summary>
    public record ClosingEntry(Guid PeriodId, AccountingPeriod Period, DateTime ClosingDate, Guid CreatedBy);

    // ====================== VALUE OBJECTS & EF CORE CONFIGURATIONS ======================

    // Value Objects (LeadId thêm vào, CustomerId ã tón tai ó dòng 180)
    public record LeadId(Guid Value);

    // REMOVED: Value Objects must NOT use IEntityTypeConfiguration
    // public class LeadIdConfiguration : IEntityTypeConfiguration<LeadId> - VIOLATION
    // public class CustomerIdConfiguration : IEntityTypeConfiguration<CustomerId> - VIOLATION

    // ====================== SPRINT 3: E-INVOICE MULTI-PROVIDER INTEGRATION ======================

    /// <summary>
    /// Invoice Status - State Machine for E-Invoice lifecycle
    /// Enforced transitions: Draft → PendingSend → SentToProvider → TaxApproved/Failed
    /// </summary>
    public enum InvoiceStatus
    {
        Draft = 1,           // Initial state
        PendingSend = 2,     // Ready to submit to provider
        SentToProvider = 3,   // Submitted, waiting for callback
        TaxApproved = 4,      // Approved by tax authority
        Failed = 5,          // Submission failed
        Rejected = 6          // Rejected by tax authority
    }

    /// <summary>
    /// Invoice Type - Goods, Services, Mixed, or HKD (per Nghị định 123/2020/NĐ-CP)
    /// </summary>
    public enum InvoiceType
    {
        Goods = 1,
        Services = 2,
        Mixed = 3,
        HKD = 4  // Hộ kinh doanh (Nghị định 123/2020/NĐ-CP)
    }

    /// <summary>
    /// HKD Revenue Group - 4-level classification per Luật Thuế GTGT/TNCN sửa đổi 2025 +
    /// ND 117/2025/NĐ-CP + Nghị quyết 198/2025/QH15 (áp dụng từ 01/01/2026).
    /// Wave 5c (2026-07-03): thresholds updated 500M/1B/3B → 1B/3B/50B per 2026 regulatory compliance.
    /// </summary>
    public enum HKDRevenueGroup
    {
        Group1 = 1,  // ≤1B (không chịu thuế GTGT + TNCN)
        Group2 = 2,  // >1B - ≤3B (GTGT theo ngành nghề, TNCN theo doanh thu hoặc lợi nhuận)
        Group3 = 3,  // >3B - ≤50B (TNCN bắt buộc theo lợi nhuận 17%)
        Group4 = 4   // >50B (TNCN bắt buộc theo lợi nhuận 20%)
    }

    /// <summary>
    /// Provider Status - Health status of E-Invoice providers
    /// </summary>
    public enum ProviderStatus
    {
        Active = 1,        // Healthy and operational
        Inactive = 2,      // Disabled by configuration
        Error = 3,         // Temporary error
        Maintenance = 4    // Under maintenance
    }

    /// <summary>
    /// Electronic Invoice ID Value Object
    /// </summary>
    public record ElectronicInvoiceId(Guid Value)
    {
        public static implicit operator Guid(ElectronicInvoiceId id) => id.Value;
        public static implicit operator ElectronicInvoiceId(Guid value) => new(value);
        public static ElectronicInvoiceId FromGuid(Guid value) => new(value);
        public Guid ToGuid() => Value;
    }

    /// <summary>
    /// Provider ID Value Object
    /// </summary>
    public record ProviderId(string Value)
    {
        public static implicit operator string(ProviderId id) => id.Value;
        public static implicit operator ProviderId(string value) => new(value);
        public static ProviderId FromString(string value) => new(value);
    }

    /// <summary>
    /// Invoice Idempotency Key - Prevents duplicate submissions (legal compliance)
    /// </summary>
    public record InvoiceIdempotencyKey(string Value)
    {
        public static implicit operator string(InvoiceIdempotencyKey key) => key.Value;
        public static implicit operator InvoiceIdempotencyKey(string value) => new(value);
        public static InvoiceIdempotencyKey FromString(string value) => new(value);
    }

    /// <summary>
    /// Electronic Invoice - Base invoice entity for E-Invoice system
    /// Domain Purity: NO EF Core, NO DbContext, NO DataAnnotations
    /// </summary>
    public class ElectronicInvoice : BaseEntity
    {
        public ElectronicInvoiceId InvoiceId { get; protected set; } = new ElectronicInvoiceId(Guid.NewGuid());
        public OrderId OrderId { get; protected set; } = null!;
        public InvoiceIdempotencyKey IdempotencyKey { get; protected set; } = null!;
        public InvoiceType InvoiceType { get; protected set; }
        public decimal Amount { get; protected set; }
        public decimal VatAmount { get; protected set; }
        public decimal TotalAmount { get; protected set; }
        public string CustomerName { get; protected set; } = string.Empty;
        public string CustomerTaxCode { get; protected set; } = string.Empty;
        public string CustomerAddress { get; protected set; } = string.Empty;
        public InvoiceStatus Status { get; protected set; } = InvoiceStatus.Draft;
        public ProviderId? CurrentProvider { get; protected set; }
        public DateTime? SubmittedAt { get; protected set; }
        public DateTime? ApprovedAt { get; protected set; }
        public string? ProviderInvoiceNumber { get; protected set; }
        public string? FailureReason { get; protected set; }

        // Navigation (read-only)
        public virtual ICollection<SubmitAttempt> SubmitAttempts { get; protected set; } = new List<SubmitAttempt>();
        public virtual OutboxEvent? OutboxEvent { get; protected set; }
        public virtual ICollection<InvoiceItem> Items { get; protected set; } = new List<InvoiceItem>();

        protected ElectronicInvoice() { }

        public ElectronicInvoice(
            TenantId tenantId,
            OrderId orderId,
            InvoiceIdempotencyKey idempotencyKey,
            InvoiceType invoiceType,
            decimal amount,
            decimal vatAmount,
            decimal totalAmount,
            string customerName,
            string customerTaxCode,
            string customerAddress)
            : base(tenantId)
        {
            OrderId = orderId;
            IdempotencyKey = idempotencyKey;
            InvoiceType = invoiceType;
            Amount = amount;
            VatAmount = vatAmount;
            TotalAmount = totalAmount;
            CustomerName = customerName;
            CustomerTaxCode = customerTaxCode;
            CustomerAddress = customerAddress;
            Status = InvoiceStatus.Draft;
        }

        /// <summary>
        /// Submit invoice for processing - State transition: Draft → PendingSend
        /// </summary>
        public void Submit()
        {
            if (Status != InvoiceStatus.Draft)
                throw new InvalidOperationException($"Cannot submit invoice in status {Status}. Expected: Draft");

            Status = InvoiceStatus.PendingSend;
            UpdateAudit();
        }

        /// <summary>
        /// Mark as sent to provider - State transition: PendingSend → SentToProvider
        /// </summary>
        public void MarkAsSentToProvider(ProviderId providerId)
        {
            if (Status != InvoiceStatus.PendingSend)
                throw new InvalidOperationException($"Cannot mark as sent in status {Status}. Expected: PendingSend");

            Status = InvoiceStatus.SentToProvider;
            CurrentProvider = providerId;
            SubmittedAt = DateTime.UtcNow;
            UpdateAudit();
        }

        /// <summary>
        /// Mark as tax approved - State transition: SentToProvider → TaxApproved
        /// </summary>
        public void MarkAsTaxApproved(string providerInvoiceNumber)
        {
            if (Status != InvoiceStatus.SentToProvider)
                throw new InvalidOperationException($"Cannot mark as approved in status {Status}. Expected: SentToProvider");

            Status = InvoiceStatus.TaxApproved;
            ProviderInvoiceNumber = providerInvoiceNumber;
            ApprovedAt = DateTime.UtcNow;
            UpdateAudit();
        }

        /// <summary>
        /// Mark as failed - State transition: SentToProvider → Failed
        /// </summary>
        public void MarkAsFailed(string failureReason)
        {
            if (Status != InvoiceStatus.SentToProvider)
                throw new InvalidOperationException($"Cannot mark as failed in status {Status}. Expected: SentToProvider");

            Status = InvoiceStatus.Failed;
            FailureReason = failureReason;
            UpdateAudit();
        }

        /// <summary>
        /// Mark as rejected - State transition: SentToProvider → Rejected
        /// </summary>
        public void MarkAsRejected(string rejectionReason)
        {
            if (Status != InvoiceStatus.SentToProvider)
                throw new InvalidOperationException($"Cannot mark as rejected in status {Status}. Expected: SentToProvider");

            Status = InvoiceStatus.Rejected;
            FailureReason = rejectionReason;
            UpdateAudit();
        }
    }

    /// <summary>
    /// Invoice Item ID - Strongly-typed ID for InvoiceItem
    /// </summary>
    public record InvoiceItemId(Guid Value)
    {
        public static implicit operator Guid(InvoiceItemId id) => id.Value;
        public static implicit operator InvoiceItemId(Guid value) => new(value);
        public static InvoiceItemId FromGuid(Guid value) => new(value);
    }

    /// <summary>
    /// Invoice Item - Line item for electronic invoice (HKD mandatory per Nghị định 123/2020/NĐ-CP)
    /// Domain Purity: NO EF Core, NO DbContext, NO DataAnnotations
    /// </summary>
    public class InvoiceItem : BaseEntity
    {
        public new InvoiceItemId Id { get; protected set; } = new InvoiceItemId(Guid.NewGuid());
        public ElectronicInvoiceId InvoiceId { get; protected set; } = null!;

        /// <summary>
        /// Mã hàng hóa/dịch vụ
        /// </summary>
        public string ItemCode { get; protected set; } = string.Empty;

        /// <summary>
        /// Tên hàng hóa/dịch vụ
        /// </summary>
        public string ItemName { get; protected set; } = string.Empty;

        /// <summary>
        /// Đơn vị tính
        /// </summary>
        public string Unit { get; protected set; } = string.Empty;

        /// <summary>
        /// Số lượng
        /// </summary>
        public decimal Quantity { get; protected set; }

        /// <summary>
        /// Đơn giá
        /// </summary>
        public decimal UnitPrice { get; protected set; }

        /// <summary>
        /// Thuế suất (%)
        /// </summary>
        public decimal VatRate { get; protected set; }

        /// <summary>
        /// Thành tiền (Quantity * UnitPrice)
        /// </summary>
        public decimal Amount { get; protected set; }

        /// <summary>
        /// Tiền thuế (Amount * VatRate / 100)
        /// </summary>
        public decimal VatAmount { get; protected set; }

        // Navigation
        public virtual ElectronicInvoice Invoice { get; protected set; } = null!;

        protected InvoiceItem() { }

        public InvoiceItem(
            TenantId tenantId,
            ElectronicInvoiceId invoiceId,
            string itemCode,
            string itemName,
            string unit,
            decimal quantity,
            decimal unitPrice,
            decimal vatRate)
            : base(tenantId)
        {
            InvoiceId = invoiceId;
            ItemCode = itemCode;
            ItemName = itemName;
            Unit = unit;
            Quantity = quantity;
            UnitPrice = unitPrice;
            VatRate = vatRate;
            Amount = quantity * unitPrice;
            VatAmount = Amount * vatRate / 100;
        }
    }

    /// <summary>
    /// Invoice Aggregate - Root entity with enforced state machine
    /// Ensures business rules are enforced at domain level
    /// </summary>
    public class InvoiceAggregate : BaseEntity
    {
        public ElectronicInvoiceId InvoiceId { get; protected set; } = null!;
        public InvoiceStatus Status { get; protected set; }
        public ElectronicInvoice Invoice { get; protected set; } = null!;

        protected InvoiceAggregate() { }

        public InvoiceAggregate(ElectronicInvoice invoice)
        {
            Invoice = invoice;
            InvoiceId = invoice.InvoiceId;
            Status = invoice.Status;
        }

        /// <summary>
        /// Submit invoice - Enforced state transition
        /// </summary>
        public void Submit()
        {
            if (Status != InvoiceStatus.Draft)
                throw new InvalidOperationException($"Cannot submit invoice in status {Status}. Expected: Draft");

            Invoice.Submit();
            Status = Invoice.Status;
            UpdateAudit();
        }

        /// <summary>
        /// Mark as sent to provider - Enforced state transition
        /// </summary>
        public void MarkAsSentToProvider(ProviderId providerId)
        {
            if (Status != InvoiceStatus.PendingSend)
                throw new InvalidOperationException($"Cannot mark as sent in status {Status}. Expected: PendingSend");

            Invoice.MarkAsSentToProvider(providerId);
            Status = Invoice.Status;
            UpdateAudit();
        }

        /// <summary>
        /// Mark as tax approved - Enforced state transition
        /// </summary>
        public void MarkAsTaxApproved(string providerInvoiceNumber)
        {
            if (Status != InvoiceStatus.SentToProvider)
                throw new InvalidOperationException($"Cannot mark as approved in status {Status}. Expected: SentToProvider");

            Invoice.MarkAsTaxApproved(providerInvoiceNumber);
            Status = Invoice.Status;
            UpdateAudit();
        }

        /// <summary>
        /// Mark as failed - Enforced state transition
        /// </summary>
        public void MarkAsFailed(string failureReason)
        {
            if (Status != InvoiceStatus.SentToProvider)
                throw new InvalidOperationException($"Cannot mark as failed in status {Status}. Expected: SentToProvider");

            Invoice.MarkAsFailed(failureReason);
            Status = Invoice.Status;
            UpdateAudit();
        }
    }

    /// <summary>
    /// Outbox Event - Atomic link to Invoice for reliable async processing
    /// Domain Purity: NO EF Core, NO DbContext
    /// </summary>
    public class OutboxEvent : BaseEntity
    {
        public Guid OutboxEventId { get; protected set; } = Guid.NewGuid();
        public ElectronicInvoiceId InvoiceId { get; protected set; } = null!;
        public string EventType { get; protected set; } = string.Empty;
        public string EventData { get; protected set; } = string.Empty;
        public EventStatus Status { get; protected set; } = EventStatus.Pending;
        public DateTime? ProcessedAt { get; protected set; }
        public int RetryCount { get; protected set; }
        public string? ErrorDetails { get; protected set; }

        protected OutboxEvent() { }

        public OutboxEvent(
            TenantId tenantId,
            ElectronicInvoiceId invoiceId,
            string eventType,
            string eventData)
            : base(tenantId)
        {
            InvoiceId = invoiceId;
            EventType = eventType;
            EventData = eventData;
            Status = EventStatus.Pending;
            RetryCount = 0;
        }

        /// <summary>
        /// Mark as processed
        /// </summary>
        public void MarkAsProcessed()
        {
            Status = EventStatus.Processed;
            ProcessedAt = DateTime.UtcNow;
            UpdateAudit();
        }

        /// <summary>
        /// Mark as failed with retry increment
        /// </summary>
        public void MarkAsFailed(string errorDetails)
        {
            Status = EventStatus.Failed;
            ErrorDetails = errorDetails;
            RetryCount++;
            UpdateAudit();
        }
    }

    /// <summary>
    /// Submit Attempt - Track provider submission attempts for safe failover
    /// Domain Purity: NO EF Core, NO DbContext
    /// </summary>
    public class SubmitAttempt : BaseEntity
    {
        public Guid SubmitAttemptId { get; protected set; } = Guid.NewGuid();
        public ElectronicInvoiceId InvoiceId { get; protected set; } = null!;
        public ProviderId ProviderId { get; protected set; } = null!;
        public DateTime AttemptedAt { get; protected set; }
        public bool Success { get; protected set; }
        public string? ErrorMessage { get; protected set; }
        public int AttemptNumber { get; protected set; }

        protected SubmitAttempt() { }

        public SubmitAttempt(
            TenantId tenantId,
            ElectronicInvoiceId invoiceId,
            ProviderId providerId,
            int attemptNumber)
            : base(tenantId)
        {
            InvoiceId = invoiceId;
            ProviderId = providerId;
            AttemptedAt = DateTime.UtcNow;
            Success = false;
            AttemptNumber = attemptNumber;
        }

        /// <summary>
        /// Mark attempt as successful
        /// </summary>
        public void MarkAsSuccessful()
        {
            Success = true;
            UpdateAudit();
        }

        /// <summary>
        /// Mark attempt as failed
        /// </summary>
        public void MarkAsFailed(string errorMessage)
        {
            Success = false;
            ErrorMessage = errorMessage;
            UpdateAudit();
        }
    }

    /// <summary>
    /// HKD Revenue Classification - Revenue group classification per TT152-2025/TT-BTC
    /// Domain Purity: NO EF Core, NO DbContext
    /// </summary>
    public class HKDRevenueClassification : BaseEntity
    {
        public Guid ClassificationId { get; protected set; } = Guid.NewGuid();
        public TenantId TenantId { get; protected set; } = null!;
        public AccountingPeriod Period { get; protected set; } = null!;
        public decimal TotalRevenue { get; protected set; }
        public HKDRevenueGroup RevenueGroup { get; protected set; }
        public DateTime CalculatedAt { get; protected set; }

        protected HKDRevenueClassification() { }

        public HKDRevenueClassification(
            TenantId tenantId,
            AccountingPeriod period,
            decimal totalRevenue,
            HKDRevenueGroup revenueGroup)
            : base(tenantId)
        {
            TenantId = tenantId;
            Period = period;
            TotalRevenue = totalRevenue;
            RevenueGroup = revenueGroup;
            CalculatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Calculate revenue group based on 2026 regulatory thresholds
        /// (Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025 + NQ 198/2025/QH15).
        /// Wave 5c (2026-07-03): thresholds updated 500M/1B/3B → 1B/3B/50B.
        ///   Group1: ≤ 1B (không chịu thuế)
        ///   Group2: > 1B - ≤ 3B (GTGT + TNCN theo tỷ lệ ngành nghề)
        ///   Group3: > 3B - ≤ 50B (TNCN bắt buộc theo lợi nhuận 17%)
        ///   Group4: > 50B (TNCN bắt buộc theo lợi nhuận 20%)
        /// </summary>
        public static HKDRevenueGroup CalculateGroup(decimal totalRevenue)
        {
            if (totalRevenue <= 1_000_000_000) return HKDRevenueGroup.Group1;
            if (totalRevenue <= 3_000_000_000) return HKDRevenueGroup.Group2;
            if (totalRevenue <= 50_000_000_000) return HKDRevenueGroup.Group3;
            return HKDRevenueGroup.Group4;
        }

        /// <summary>
        /// Calculate TNCN (Thuế Thu nhập cá nhân) per 2026 regulatory formulas
        /// (Luật Thuế TNCN sửa đổi 2025 + ND 117/2025 + NQ 198/2025/QH15).
        /// Wave 5c (2026-07-03): replaces hardcoded 5%/10% + flat Revenue×rate formulas.
        ///   Group1: 0 (không chịu thuế TNCN — doanh thu ≤ 1B)
        ///   Group2: (totalRevenue - 1B) × industryRate  (trừ ngưỡng 1B trước khi áp suất thuế)
        ///   Group3: (totalRevenue - totalExpense) × 17%  (bắt buộc theo lợi nhuận)
        ///   Group4: (totalRevenue - totalExpense) × 20%  (bắt buộc theo lợi nhuận)
        /// </summary>
        /// <param name="group">Revenue group (determined by CalculateGroup)</param>
        /// <param name="totalRevenue">Total annual revenue (doanh thu)</param>
        /// <param name="totalExpense">Total annual deductible expense (chi phí) — used for Group3/4 profit-based formula</param>
        /// <param name="industryRate">Industry-specific TNCN rate as fraction (e.g., 0.005m = 0.5%) — used for Group2 revenue-based formula (per ND 117/2025)</param>
        /// <returns>TNCN amount in VND</returns>
        public static decimal CalculateTNCN(HKDRevenueGroup group, decimal totalRevenue, decimal totalExpense, decimal industryRate)
        {
            return group switch
            {
                HKDRevenueGroup.Group1 => 0m,                                              // ≤1B: không chịu thuế
                HKDRevenueGroup.Group2 => Math.Max(0m, totalRevenue - 1_000_000_000m) * industryRate,  // (Doanh thu - 1B) × rate
                HKDRevenueGroup.Group3 => (totalRevenue - totalExpense) * 0.17m,          // (Doanh thu - chi phí) × 17%
                HKDRevenueGroup.Group4 => (totalRevenue - totalExpense) * 0.20m,          // (Doanh thu - chi phí) × 20%
                _ => 0m
            };
        }

        /// <summary>
        /// Calculate GTGT (Thuế Giá trị gia tăng) per 2026 regulatory formulas.
        /// Wave 5c (2026-07-03): Group1 exemption (revenue ≤ 1B → GTGT = 0).
        ///   Group1: 0 (exemption — không chịu thuế GTGT)
        ///   Group2/3/4: totalRevenue × industryRate (theo suất thuế ngành nghề per ND 117/2025)
        /// </summary>
        /// <param name="group">Revenue group (determined by CalculateGroup)</param>
        /// <param name="totalRevenue">Total annual revenue (doanh thu)</param>
        /// <param name="industryRate">Industry-specific GTGT rate as fraction (e.g., 0.01m = 1%) — per ND 117/2025</param>
        /// <returns>GTGT amount in VND</returns>
        public static decimal CalculateGTGT(HKDRevenueGroup group, decimal totalRevenue, decimal industryRate)
        {
            return group == HKDRevenueGroup.Group1 ? 0m : totalRevenue * industryRate;
        }
    }

    /// <summary>
    /// Provider Configuration - Multi-tenant provider configuration
    /// Domain Purity: NO EF Core, NO DbContext
    /// </summary>
    public class ProviderConfiguration : BaseEntity
    {
        public Guid ConfigurationId { get; protected set; } = Guid.NewGuid();
        public new TenantId TenantId { get; protected set; } = null!;
        public ProviderId ProviderId { get; protected set; } = null!;
        public string ProviderName { get; protected set; } = string.Empty;
        public bool IsActive { get; protected set; }
        public int Priority { get; protected set; } // 1 = Primary, 2 = Fallback 1, etc.
        public string ConfigurationData { get; protected set; } = string.Empty; // JSON string
        public ProviderStatus Status { get; protected set; } = ProviderStatus.Active;

        protected ProviderConfiguration() { }

        public ProviderConfiguration(
            TenantId tenantId,
            ProviderId providerId,
            string providerName,
            bool isActive,
            int priority,
            string configurationData)
            : base(tenantId)
        {
            TenantId = tenantId;
            ProviderId = providerId;
            ProviderName = providerName;
            IsActive = isActive;
            Priority = priority;
            ConfigurationData = configurationData;
            Status = ProviderStatus.Active;
        }

        /// <summary>
        /// Update provider status
        /// </summary>
        public void UpdateStatus(ProviderStatus status)
        {
            Status = status;
            UpdateAudit();
        }

        /// <summary>
        /// Update configuration data
        /// </summary>
        public void UpdateConfiguration(string configurationData)
        {
            ConfigurationData = configurationData;
            UpdateAudit();
        }
    }

    /// <summary>
    /// Domain Event: Invoice Submitted
    /// </summary>
    public record InvoiceSubmitted(ElectronicInvoiceId InvoiceId, TenantId TenantId, DateTime OccurredAt);

    /// <summary>
    /// Domain Event: Invoice Confirmed (Tax Approved)
    /// </summary>
    public record InvoiceConfirmed(ElectronicInvoiceId InvoiceId, TenantId TenantId, string ProviderInvoiceNumber, DateTime OccurredAt);

    /// <summary>
    /// Domain Event: Invoice Rejected
    /// </summary>
    public record InvoiceRejected(ElectronicInvoiceId InvoiceId, TenantId TenantId, string RejectionReason, DateTime OccurredAt);

    /// <summary>
    /// Pending Invoice Queue - Batch processing for anonymous retail invoices
    /// UC1 Feature: Queue orders for batch invoice processing at 23:00 or threshold 500
    /// </summary>
    public class PendingInvoiceQueue : BaseEntity
    {
        public Guid QueueId { get; protected set; } = Guid.NewGuid();
        public OrderId OrderId { get; protected set; } = null!;
        public new TenantId TenantId { get; protected set; } = null!;
        public decimal TotalAmount { get; protected set; }
        public decimal VatAmount { get; protected set; }
        public PendingInvoiceStatus Status { get; protected set; } = PendingInvoiceStatus.PendingInvoice;
        public int RetryCount { get; protected set; } = 0;
        public string? ErrorMessage { get; protected set; }
        public DateTime? ProcessedAt { get; protected set; }
        public new DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

        protected PendingInvoiceQueue() { }

        public PendingInvoiceQueue(TenantId tenantId, OrderId orderId, decimal totalAmount, decimal vatAmount)
            : base(tenantId)
        {
            TenantId = tenantId;
            OrderId = orderId;
            TotalAmount = totalAmount;
            VatAmount = vatAmount;
        }

        /// <summary>
        /// Mark invoice as being processed
        /// </summary>
        public void MarkAsProcessing()
        {
            Status = PendingInvoiceStatus.Processing;
            UpdateAudit();
        }

        /// <summary>
        /// Mark invoice as successfully processed
        /// </summary>
        public void MarkAsProcessed()
        {
            Status = PendingInvoiceStatus.Invoiced;
            ProcessedAt = DateTime.UtcNow;
            UpdateAudit();
        }

        /// <summary>
        /// Mark invoice as failed with error message
        /// </summary>
        public void MarkAsFailed(string error)
        {
            RetryCount++;
            ErrorMessage = error;
            Status = PendingInvoiceStatus.Failed;
            UpdateAudit();
        }

        /// <summary>
        /// Reset invoice for retry attempt
        /// </summary>
        public void ResetForRetry()
        {
            Status = PendingInvoiceStatus.PendingInvoice;
            ErrorMessage = null;
            UpdateAudit();
        }
    }

    // ====================== WAVE 14: API KEY ENTITY ======================

    /// <summary>
    /// Wave 14: API Key entity for HMAC request signing.
    /// Supports per-tenant API keys with HMAC-SHA256 shared secret.
    /// Secret is stored as BCrypt hash; raw secret returned only at creation time.
    /// </summary>
    public class ApiKey
    {
        public Guid Id { get; private set; }
        public Guid TenantId { get; private set; }
        public string Name { get; private set; }
        /// <summary>BCrypt hash of the shared secret (HMAC key).</summary>
        public string SecretHash { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? LastUsedAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }

        // EF Core constructor
        private ApiKey() { Name = string.Empty; SecretHash = string.Empty; }

        public ApiKey(Guid tenantId, string name, string secretHash, int expirationDays = 90)
        {
            Id = Guid.NewGuid();
            TenantId = tenantId;
            Name = name;
            SecretHash = secretHash;
            IsActive = true;
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays);
        }

        public void Revoke()
        {
            IsActive = false;
            RevokedAt = DateTime.UtcNow;
        }

        public void RecordUsage()
        {
            LastUsedAt = DateTime.UtcNow;
        }

        public bool IsExpired() => DateTime.UtcNow > ExpiresAt;
        public bool IsValid() => IsActive && !IsExpired();
    }

    // ====================== VAS ENTERPRISE REPORTS — Domain Records (Wave 2) ======================
    // D5 approved: Domain modification for VAS Enterprise Financial Reports (TT 99/2025 + TT 133/2016 + TT 58/2026)
    // D9 approved: HKD↔DN conversion = Option B (New Tenant + Link)
    // Review 2026-07-04: BCTC records use ReportItemCode (Mã chỉ tiêu) + 2-column comparative (Ending/Opening)
    //   per Vietnamese accounting law (Mẫu B01-DN/B02-DN/B03-DN). NOT AccountCode-based (that's Trial Balance).
    // Domain Purity: NO EF Core, NO DbContext, NO DataAnnotations — records only

    /// <summary>
    /// Tenant business type — determines which accounting standard applies.
    /// Wave 2 (D9): drives feature flag routing in W8.
    /// </summary>
    public enum TenantType
    {
        HKD = 1,                    // Hộ kinh doanh (TT 152/2025/TT-BTC)
        Enterprise_SuperSmall = 2,  // DN siêu nhỏ (TT 58/2026)
        Enterprise_SME = 3,         // DN vừa và nhỏ (TT 133/2016)
        Enterprise_Large = 4        // DN lớn (TT 99/2025)
    }

    /// <summary>
    /// Account classification for chart-of-accounts mapping (W3).
    /// Contra accounts (e.g., TK 214) are typed as their parent group (Asset) with IsNormalCredit=true on AccountChartEntry.
    /// </summary>
    public enum AccountType { Asset, Liability, Equity, Revenue, Expense }

    /// <summary>Vietnamese accounting standards supported by VAS module (D1 approved scope).</summary>
    public enum AccountingStandard { TT99_2025, TT133_2016, TT58_2026 }

    /// <summary>
    /// Chuẩn cấu trúc một dòng Chỉ tiêu Báo cáo Tài chính theo pháp luật Việt Nam.
    /// Sử dụng Mã chỉ tiêu (ReportItemCode), KHÔNG dùng AccountCode.
    /// Bắt buộc có 2 cột số liệu để đối chiếu thời kỳ (Số cuối kỳ / Số đầu năm).
    /// </summary>
    public record FinancialStatementLine(
        string ReportItemCode,      // Mã chỉ tiêu (VD: "100", "110", "01", "20")
        string ReportItemName,      // Tên chỉ tiêu (VD: "Tài sản ngắn hạn", "Doanh thu bán hàng")
        decimal EndingAmount,       // Số cuối kỳ / Năm nay
        decimal OpeningAmount,      // Số đầu năm / Năm trước (So sánh)
        int Level,                  // Cấp bậc phân cấp cha-con trình bày UI
        bool IsNormalNegative       // Hiển thị số âm trong ngoặc đơn VD: (20,000,000)
    );

    // ── 1. BẢNG CÂN ĐỐI KẾ TOÁN (Mẫu B01-DN / B01-DNN) ──────────────────────────────────
    // Invariant: TotalAssetsEnding == TotalLiabilitiesAndEquityEnding (enforced at factory/service in W4).
    // No IsBalanced flag — unbalanced data throws, never stored.
    public record BalanceSheet(
        TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
        IEnumerable<FinancialStatementLine> Assets,
        IEnumerable<FinancialStatementLine> Liabilities,
        IEnumerable<FinancialStatementLine> Equity,
        decimal TotalAssetsEnding, decimal TotalAssetsOpening,
        decimal TotalLiabilitiesAndEquityEnding, decimal TotalLiabilitiesAndEquityOpening
    );

    // ── 2. BÁO CÁO KẾT QUẢ HOẠT ĐỘNG KINH DOANH (Mẫu B02-DN / B02-DNN) ────────────────
    public record IncomeStatement(
        TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
        decimal TotalRevenueEnding, decimal TotalRevenueOpening,
        decimal NetProfitEnding, decimal NetProfitOpening,
        IEnumerable<FinancialStatementLine> Lines
    );

    // ── 3. BÁO CÁO LƯU CHUYỂN TIỀN TỆ (Mẫu B03-DN / B03-DNN) ──────────────────────────
    public record CashFlowStatement(
        TenantId TenantId, AccountingPeriod Period, DateTime GeneratedAt,
        decimal OpeningCash, decimal ClosingCash, decimal NetChange,
        IEnumerable<FinancialStatementLine> OperatingActivities,
        IEnumerable<FinancialStatementLine> InvestingActivities,
        IEnumerable<FinancialStatementLine> FinancingActivities
    );

    // TrialBalance already exists above (Domain.cs ~line 1518) — keep as-is, W4 service wraps with TenantId.

    // ── 4. SỐ DƯ ĐẦU KỲ (Mở sổ / Khởi tạo dữ liệu) ─────────────────────────────────────
    public record OpeningBalance(
        TenantId TenantId, AccountingPeriod Period,
        IEnumerable<OpeningBalanceLine> Lines
    );
    public record OpeningBalanceLine(string AccountCode, decimal DebitOpening, decimal CreditOpening);

    /// <summary>
    /// In-memory chart-of-accounts entry. Storage decision (DB vs dictionary) deferred to W3.
    /// IsNormalCredit: true for contra accounts (e.g., TK 214 Hao mòn TSCĐ) — normal credit balance.
    /// </summary>
    public record AccountChartEntry(
        string AccountCode, string AccountName, AccountType Type,
        AccountingStandard Standard, bool IsNormalCredit
    );
}
