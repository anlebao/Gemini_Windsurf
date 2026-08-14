using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;
using VanAn.Shared.Domain.Aggregates.ProductCostPriceAggregate;
using VanAn.Shared.Domain.Aggregates.CommunityFundAggregate;
using VanAn.CoreHub.Infrastructure;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserTenant = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTenant;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserPermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.UserPermissionGroup;
using VanAn.CoreHub.Infrastructure.Configurations;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using CoreOutboxMessage = VanAn.CoreHub.Infrastructure.OutboxMessage;

namespace VanAn.ShopERP.Infrastructure
{
    /// <summary>
    /// ShopERP-specific DbContext for SQLite database
    /// Handles orders, outbox messages, and local business data
    /// Implements IVanAnDbContext for decoupling from VanAnDbContext
    /// </summary>
    public class ShopERPDbContext(DbContextOptions<ShopERPDbContext> options) : DbContext(options), IVanAnDbContext
    {

        // Order-related tables
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        // Shop entity removed 2026-07-21 — Tenant is the single identity.

        // Outbox pattern tables
        public DbSet<CoreOutboxMessage> OutboxMessages { get; set; }

        // Local business tables
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<Recipe> Recipes { get; set; }

        // Additional tables required by IVanAnDbContext (for Offline Mode)
        // NOTE: Accounting DbSets (AccountingEntries, JournalEntries, AuditLogs,
        // PendingInvoiceQueues, AccountCharts, PeriodClosingStatuses) removed —
        // accounting is always-online on PostgreSQL via IAccountingDbContext (ADR-001).
        // HKDBook removed (abstract base, ignored in OnModelCreating — never persisted).
        public DbSet<LoyaltyRewards> LoyaltyRewards { get; set; }
        public DbSet<LoyaltyIssuanceRecord> LoyaltyIssuanceRecords { get; set; }  // VALCN v2.0 Phase 1
        public DbSet<SocialCampaign> SocialCampaigns { get; set; }

        // Wave 5: Tenant management (required by IVanAnDbContext)
        public DbSet<Tenant> Tenants { get; set; }

        // Wave 0: DemoUser for BCrypt authentication
        public DbSet<DemoUser> Users { get; set; }

        // Wave 1 Phase 2: User-Tenant mapping for multi-tenancy
        public DbSet<UserTenant> UserTenants { get; set; }

        // Wave 6: Permission groups for bundle-based RBAC
        public DbSet<PermissionGroup> PermissionGroups { get; set; }
        public DbSet<UserPermissionGroup> UserPermissionGroups { get; set; }

        // Wave 14: API Keys for HMAC request signing
        public DbSet<ApiKey> ApiKeys { get; set; }

        // Wave 9: Push Subscriptions for Web Push notifications
        public DbSet<PushSubscription> PushSubscriptions { get; set; }

        // W3: VAS Account Chart reference data — moved to IAccountingDbContext (ADR-001)

        // W5: Period closing status persistence — moved to IAccountingDbContext (ADR-001)

        // Platform SystemAdmin: Platform-level users (cross-tenant, NOT tenant-scoped)
        public DbSet<VanAn.CoreHub.Infrastructure.Entities.PlatformUser> PlatformUsers { get; set; }

        // KhachLink Full Flow W0: Shop feature toggle settings (tenant-scoped)
        public DbSet<VanAn.CoreHub.Infrastructure.Entities.ShopFeatureSettingsEntity> ShopFeatureSettings { get; set; }

        // Phase 1 (Multi-VPS Checkout): ShopERP hosting instances (platform-level, NOT tenant-scoped)
        public DbSet<ShopInstance> ShopInstances { get; set; }

        // Phase 6: FeaturedProduct is PG-only — ShopERP SQLite ignores this entity (see OnModelCreating).
        // DbSet exists to satisfy IVanAnDbContext interface contract; never queried from ShopERP.
        public DbSet<FeaturedProduct> FeaturedProducts { get; set; }

        // Phase 5: CampaignPushJob + PushNotificationDelivery are PG-only (Gateway).
        // DbSet exists to satisfy IVanAnDbContext interface contract; never queried from ShopERP.
        public DbSet<CampaignPushJob> CampaignPushJobs { get; set; }
        public DbSet<PushNotificationDelivery> PushNotificationDeliveries { get; set; }

        // Loyalty-B: Redemption system — ShopERP SQLite (tenant-scoped business data).
        public DbSet<RedemptionCatalogItem> RedemptionCatalogItems { get; set; }
        public DbSet<RedemptionRecord> RedemptionRecords { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        // Loyalty-C WS-B: Gamification framework
        public DbSet<Mission> Missions { get; set; }
        public DbSet<MissionCompletion> MissionCompletions { get; set; }
        // WS-2: Promo campaign system — bulk marketing push with per-recipient tracking
        public DbSet<PromoCampaign> PromoCampaigns { get; set; }
        public DbSet<PromoCampaignRecipient> PromoCampaignRecipients { get; set; }

        // Community Commerce Sprint 0 (v1.2: 11 DbSet) — PG-only on Gateway.
        // DbSet exists to satisfy IVanAnDbContext interface contract; never queried from ShopERP SQLite.
        // v1.3: Community entities are PG-only (cross-tenant nature, avoid 300K SQLite files migration).
        public DbSet<CommunityRole> CommunityRoles { get; set; }
        public DbSet<DeliveryTask> DeliveryTasks { get; set; }
        public DbSet<DeliveryTracking> DeliveryTrackings { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<SalesReferral> SalesReferrals { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<ProductReferralConfig> ProductReferralConfigs { get; set; }
        public DbSet<AppInstallAttribution> AppInstallAttributions { get; set; }
        public DbSet<DeviceRegistration> DeviceRegistrations { get; set; }
        public DbSet<FraudFlag> FraudFlags { get; set; }

        // Sprint 7 — Commerce Mode Toggle (3 new DbSets — IVanAnDbContext interface)
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<ProductCostPrice> ProductCostPrices { get; set; }
        public DbSet<CommunityFundSpendRecord> CommunityFundSpendRecords { get; set; }

        // Loyalty Alliance System — PG-only (Gateway VanAnDbContext). ShopERP SQLite ignores these entities.
        // DbSet declarations remain for IVanAnDbContext interface contract; entities are Ignored in OnModelCreating.
        public DbSet<LoyaltyGlobalConfig> LoyaltyGlobalConfigs { get; set; }
        public DbSet<LoyaltyTenantConfig> LoyaltyTenantConfigs { get; set; }
        public DbSet<AllianceWallet> AllianceWallets { get; set; }
        public DbSet<AllianceTransaction> AllianceTransactions { get; set; }

        // #100: KhachLink home page section toggles — GLOBAL (PG-only, ignored in ShopERP SQLite)
        public DbSet<KhachLinkHomeSettings> KhachLinkHomeSettings { get; set; }

        // #126: Guard QR Verification — PG-only (Gateway), ignored in ShopERP SQLite
        public DbSet<VehicleSession> VehicleSessions { get; set; }
        public DbSet<GuardScanLog> GuardScanLogs { get; set; }

        // KhachLink Multi-Profile R1: PG-only (Gateway), ignored in ShopERP SQLite
        public DbSet<VanAn.Shared.Domain.Aggregates.KhachLinkAggregate.KhachLinkInstance> KhachLinkInstances { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Global convention for all ValueObject<T> types - EF Core 8 proper 2-way converters
            // All converters now use separate classes for consistency
            // MUST match VanAnDbContext conventions for Strongly Typed IDs

            _ = configurationBuilder.Properties<AccountingEntryId>()
                .HaveConversion<AccountingEntryIdConverter>();

            _ = configurationBuilder.Properties<TenantId>()
                .HaveConversion<TenantIdConverter>();

            _ = configurationBuilder.Properties<Money>()
                .HaveConversion<MoneyConverter>();

            _ = configurationBuilder.Properties<AccountingPeriod>()
                .HaveConversion<AccountingPeriodConverter>();

            _ = configurationBuilder.Properties<AccountingBookType>()
                .HaveConversion<AccountingBookTypeConverter>();

            _ = configurationBuilder.Properties<LeadId>()
                .HaveConversion<LeadIdConverter>();

            _ = configurationBuilder.Properties<CustomerId>()
                .HaveConversion<CustomerIdConverter>();

            _ = configurationBuilder.Properties<ProductId>()
                .HaveConversion<ProductIdConverter>();

            _ = configurationBuilder.Properties<IngredientId>()
                .HaveConversion<IngredientIdConverter>();

            _ = configurationBuilder.Properties<RecipeId>()
                .HaveConversion<RecipeIdConverter>();

            _ = configurationBuilder.Properties<InventoryId>()
                .HaveConversion<InventoryIdConverter>();

            _ = configurationBuilder.Properties<OrderId>()
                .HaveConversion<OrderIdConverter>();

            _ = configurationBuilder.Properties<OrderStatusId>()
                .HaveConversion<OrderStatusIdConverter>();

            _ = configurationBuilder.Properties<OrderItemId>()
                .HaveConversion<OrderItemIdConverter>();

            _ = configurationBuilder.Properties<JournalEntryId>()
                .HaveConversion<JournalEntryIdConverter>();

            _ = configurationBuilder.Properties<ElectronicInvoiceId>()
                .HaveConversion<ElectronicInvoiceIdConverter>();

            _ = configurationBuilder.Properties<InvoiceItemId>()
                .HaveConversion<InvoiceItemIdConverter>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // === GLOBAL IGNORES ===
            // AccountingPeriod is a value object (record) used as computed property
            // It should never be mapped as a separate entity
            _ = modelBuilder.Ignore<AccountingPeriod>();

            // HKDBook is an abstract base class for dynamic report generation
            // It's not meant to be persisted as an entity
            _ = modelBuilder.Ignore<HKDBook>();
            _ = modelBuilder.Ignore<GenericHKDBook>();

            // Phase 6: FeaturedProductId is a value object — never mapped as a separate entity
            _ = modelBuilder.Ignore<FeaturedProductId>();

            // Apply configurations from CoreHub assembly via assembly scanning
            // This avoids direct reference to CoreHub.Infrastructure.Configurations
            System.Reflection.Assembly coreHubAssembly = typeof(CoreOutboxMessage).Assembly;
            _ = modelBuilder.ApplyConfigurationsFromAssembly(coreHubAssembly,
                t => t.Name.EndsWith("Configuration") && t.GetInterface(nameof(IEntityConfiguration)) != null);

            // Community Commerce Sprint 0 (F8 fix 2026-07-26): 11 community entities are PG-only (v1.3).
            // DbSet declarations remain for IVanAnDbContext interface contract, but entities are Ignored
            // in the SQLite model so EF Core does not map them to non-existent SQLite tables.
            // Any query against these DbSets from ShopERP will fail-fast with "entity not in model"
            // rather than runtime SQL error against a missing table.
            // NOTE: These Ignore() calls MUST be after ApplyConfigurationsFromAssembly — otherwise
            // the CoreHub configurations (CommunityRoleConfiguration, etc.) re-add the entities to the model.
            _ = modelBuilder.Ignore<CommunityRole>();
            _ = modelBuilder.Ignore<DeliveryTask>();
            _ = modelBuilder.Ignore<DeliveryTracking>();
            _ = modelBuilder.Ignore<Conversation>();
            _ = modelBuilder.Ignore<Message>();
            _ = modelBuilder.Ignore<SalesReferral>();
            _ = modelBuilder.Ignore<WalletTransaction>();
            _ = modelBuilder.Ignore<ProductReferralConfig>();
            _ = modelBuilder.Ignore<AppInstallAttribution>();
            _ = modelBuilder.Ignore<DeviceRegistration>();
            _ = modelBuilder.Ignore<FraudFlag>();

            // Loyalty Alliance System: 4 entities are PG-only (Gateway VanAnDbContext).
            // ShopERP SQLite ignores these — cross-tenant wallet system lives in PG.
            // DbSet declarations remain for IVanAnDbContext interface contract.
            // NOTE: These Ignore() calls MUST be after ApplyConfigurationsFromAssembly — otherwise
            // the CoreHub configurations (AllianceWalletConfiguration, etc.) re-add the entities to the model.
            _ = modelBuilder.Ignore<LoyaltyGlobalConfig>();
            _ = modelBuilder.Ignore<LoyaltyTenantConfig>();
            _ = modelBuilder.Ignore<AllianceWallet>();
            _ = modelBuilder.Ignore<AllianceTransaction>();
            _ = modelBuilder.Ignore<KhachLinkHomeSettings>(); // #100: PG-only global config
            _ = modelBuilder.Ignore<VehicleSession>(); // #126: PG-only Guard QR Verify
            _ = modelBuilder.Ignore<GuardScanLog>(); // #126: PG-only Guard QR Verify
            _ = modelBuilder.Ignore<VanAn.Shared.Domain.Aggregates.KhachLinkAggregate.KhachLinkInstance>(); // R1: PG-only KhachLink instances

            // === VALUE OBJECT CONFIGURATIONS ===
            // Order: Configured via OrderConfiguration from CoreHub assembly (applied above via ApplyConfigurationsFromAssembly)
            // Inline config removed to avoid duplicate OwnsOne conflict with OrderConfiguration

            // Wave 2: PII encryption for Customer inline config (ShopERP uses inline config instead of CustomerConfiguration)
            _ = modelBuilder.Entity<Customer>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                // SINGLE-IDENTITY: CustomerId synced to Id in constructor. Ignore — no DB column.
                _ = entity.Ignore(e => e.CustomerId);
                _ = entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
                _ = entity.Property(e => e.PhoneNumber)
                    .IsRequired()
                    .HasMaxLength(500)
                    .HasConversion(new EncryptedStringConverter(
                        DataProtectionProviderAccessor.CreateProtector("Customer.PhoneNumber")));
                _ = entity.Property(e => e.Email)
                    .HasMaxLength(500)
                    .HasConversion(new EncryptedStringConverter(
                        DataProtectionProviderAccessor.CreateProtector("Customer.Email")));
                _ = entity.Property(e => e.CustomerTier).IsRequired().HasMaxLength(20);
                _ = entity.Property(e => e.TotalSpent).HasPrecision(18, 2);
            });

            // JournalEntry: Configured via JournalEntryConfiguration from CoreHub assembly
            // (applied above via ApplyConfigurationsFromAssembly — implements IEntityConfiguration since W5).
            // Inline config removed to avoid duplicate OwnsMany conflict + ensure Description/EntryDate/ReferenceId/IsReversal mapped.

            // JournalTemplate: OwnsMany for JournalTemplateLine + TemplateValidationRule
            _ = modelBuilder.Entity<JournalTemplate>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.OwnsMany(e => e.Lines, lineBuilder =>
                {
                    _ = lineBuilder.Property(l => l.AccountNumber).IsRequired().HasMaxLength(50);
                    _ = lineBuilder.Property(l => l.AmountFormula).HasMaxLength(200);
                    _ = lineBuilder.Property(l => l.DescriptionTemplate).HasMaxLength(500);
                    _ = lineBuilder.Ignore(l => l.IsCredit);
                });
                _ = entity.OwnsMany(e => e.ValidationRules, ruleBuilder =>
                {
                    _ = ruleBuilder.Property(r => r.Rule).IsRequired().HasMaxLength(500);
                    _ = ruleBuilder.Property(r => r.Message).HasMaxLength(500);
                });
                _ = entity.Ignore(e => e.BusinessRules);
            });

            // Apply global query filters for multi-tenancy
            ApplyGlobalQueryFilters(modelBuilder);
        }

        private static void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
        {
            _ = modelBuilder.Entity<Order>().HasQueryFilter(e => !e.IsDeleted);
            _ = modelBuilder.Entity<OrderItem>().HasQueryFilter(e => !e.Order.IsDeleted);
            _ = modelBuilder.Entity<CoreOutboxMessage>().HasQueryFilter(e => e.ProcessedAt == null);
        }

        // IVanAnDbContext implementation
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return await Database.BeginTransactionAsync(cancellationToken);
        }

        /// <summary>EF Core provider name — used for provider-specific query logic.</summary>
        public string ProviderName => Database.ProviderName ?? string.Empty;
    }
}
