using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;
using VanAn.Shared.Domain.Aggregates.ProductCostPriceAggregate;
using VanAn.Shared.Domain.Aggregates.CommunityFundAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserTenant = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTenant;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserPermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.UserPermissionGroup;

namespace VanAn.CoreHub.Infrastructure
{
    /// <summary>
    /// Abstraction for DbContext to allow different implementations (PostgreSQL vs SQLite)
    /// This enables Offline-First architecture without tight coupling to specific database provider
    /// </summary>
    public interface IVanAnDbContext : IDisposable
    {
        DbSet<Order> Orders { get; }
        DbSet<OrderItem> OrderItems { get; }
        DbSet<Customer> Customers { get; }
        DbSet<Product> Products { get; }
        DbSet<Inventory> Inventories { get; }
        DbSet<Ingredient> Ingredients { get; }
        DbSet<Recipe> Recipes { get; }
        DbSet<LoyaltyRewards> LoyaltyRewards { get; }
        DbSet<LoyaltyIssuanceRecord> LoyaltyIssuanceRecords { get; }  // VALCN v2.0 Phase 1 — per-order loyalty tracking
        DbSet<SocialCampaign> SocialCampaigns { get; }
        DbSet<OutboxMessage> OutboxMessages { get; }

        // Wave 0: Demo users for BCrypt authentication
        DbSet<DemoUser> Users { get; }

        // Wave 5: Rich Domain Tenant aggregate
        DbSet<Tenant> Tenants { get; }

        // Wave 1 Phase 2: User-Tenant mapping for multi-tenancy
        DbSet<UserTenant> UserTenants { get; }

        // Wave 6: Permission groups for bundle-based RBAC
        DbSet<PermissionGroup> PermissionGroups { get; }
        DbSet<UserPermissionGroup> UserPermissionGroups { get; }

        // Wave 14: API Keys for HMAC request signing
        DbSet<ApiKey> ApiKeys { get; }

        // Wave 9: Push Subscriptions for Web Push notifications
        DbSet<PushSubscription> PushSubscriptions { get; }

        // W3: VAS Account Chart reference data (NOT tenant-scoped) — moved to IAccountingDbContext

        // W5: Period closing status persistence (tenant-scoped) — moved to IAccountingDbContext

        // Platform SystemAdmin: Platform-level users (cross-tenant, NOT tenant-scoped)
        DbSet<VanAn.CoreHub.Infrastructure.Entities.PlatformUser> PlatformUsers { get; }

        // KhachLink Full Flow W0: Shop feature toggle settings (tenant-scoped)
        DbSet<VanAn.CoreHub.Infrastructure.Entities.ShopFeatureSettingsEntity> ShopFeatureSettings { get; }

        // Phase 1 (Multi-VPS Checkout): ShopERP hosting instances (platform-level, NOT tenant-scoped)
        DbSet<ShopInstance> ShopInstances { get; }

        // Phase 6 (Admin UI): Sysadmin-curated featured products (PG-only, tenant-scoped)
        DbSet<FeaturedProduct> FeaturedProducts { get; }

        // Phase 5: Campaign push jobs + delivery tracking (PG-only, tenant-scoped)
        DbSet<CampaignPushJob> CampaignPushJobs { get; }
        DbSet<PushNotificationDelivery> PushNotificationDeliveries { get; }

        // Loyalty-B: Redemption system (ShopERP SQLite, tenant-scoped)
        DbSet<RedemptionCatalogItem> RedemptionCatalogItems { get; }
        DbSet<RedemptionRecord> RedemptionRecords { get; }
        DbSet<Voucher> Vouchers { get; }

        // Loyalty-C WS-B: Gamification framework (ShopERP SQLite, tenant-scoped)
        DbSet<Mission> Missions { get; }
        DbSet<MissionCompletion> MissionCompletions { get; }

        // WS-2: Promo campaign system (ShopERP SQLite, tenant-scoped)
        DbSet<PromoCampaign> PromoCampaigns { get; }
        DbSet<PromoCampaignRecipient> PromoCampaignRecipients { get; }

        // Community Commerce Sprint 0 (v1.2: 11 DbSet) — Gateway PG only, tenant-scoped via IMustHaveTenant
        DbSet<CommunityRole> CommunityRoles { get; }
        DbSet<DeliveryTask> DeliveryTasks { get; }
        DbSet<DeliveryTracking> DeliveryTrackings { get; }
        DbSet<Conversation> Conversations { get; }
        DbSet<Message> Messages { get; }
        DbSet<SalesReferral> SalesReferrals { get; }
        DbSet<WalletTransaction> WalletTransactions { get; }
        DbSet<ProductReferralConfig> ProductReferralConfigs { get; } // v1.1 NEW
        DbSet<AppInstallAttribution> AppInstallAttributions { get; } // v1.1 NEW
        DbSet<DeviceRegistration> DeviceRegistrations { get; } // v1.2 NEW
        DbSet<FraudFlag> FraudFlags { get; } // v1.2 NEW

        // Sprint 7 — Commerce Mode Toggle (3 new DbSets)
        DbSet<SystemSetting> SystemSettings { get; } // global config (TenantId nullable)
        DbSet<ProductCostPrice> ProductCostPrices { get; } // Q1: Vạn An's negotiated cost per product
        DbSet<CommunityFundSpendRecord> CommunityFundSpendRecords { get; } // Q3: audit trail for fund disbursement

        // Loyalty Alliance System — cross-tenant wallet (PG-only, NOT tenant-scoped except LoyaltyTenantConfig)
        DbSet<LoyaltyGlobalConfig> LoyaltyGlobalConfigs { get; } // single-row global config (TenantId = Empty)
        DbSet<LoyaltyTenantConfig> LoyaltyTenantConfigs { get; } // per-tenant override (tenant-scoped)
        DbSet<AllianceWallet> AllianceWallets { get; } // cross-tenant wallet (TenantId = Empty)
        DbSet<AllianceTransaction> AllianceTransactions { get; } // append-only transaction log (TenantId = Empty)

        // #100: KhachLink home page section toggles — GLOBAL (not tenant-scoped, single row)
        DbSet<KhachLinkHomeSettings> KhachLinkHomeSettings { get; } // single-row global config (TenantId = Empty)

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>EF Core provider name (e.g. "Microsoft.EntityFrameworkCore.Sqlite", "Npgsql"). Used for provider-specific query logic.</summary>
        string ProviderName { get; }
    }
}
