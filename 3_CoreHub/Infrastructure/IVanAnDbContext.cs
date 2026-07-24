using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
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

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
