using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;
using VanAn.Shared.Domain.Aggregates.ProductCostPriceAggregate;
using VanAn.Shared.Domain.Aggregates.CommunityFundAggregate;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserTenant = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTenant;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserPermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.UserPermissionGroup;

namespace VanAn.CoreHub.Infrastructure
{
    public class VanAnDbContext(DbContextOptions<VanAnDbContext> options, ITenantProvider tenantProvider = null!) : DbContext(options), IVanAnDbContext, IAccountingDbContext
    {
        private readonly ITenantProvider _tenantProvider = tenantProvider;

        // 🛡️ PUBLIC PROPERTY FOR EF Core Query Filter
        public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Empty;

        // Used by global query filter: TenantId column is stored as TEXT (UUID string)
        // Both SQLite and Npgsql can compare TEXT columns with string parameters.
        public string CurrentTenantIdString => CurrentTenantId.ToString();

        // TenantId value object — used by ApplyMultiTenancyFilters expression tree
        // so EF Core can translate e.TenantId == CurrentTenantIdValue with the
        // TenantId→string converter, emitting a properly parameterized SQL query.
        public TenantId CurrentTenantIdValue => new TenantId(CurrentTenantId);

        // Domain Tables dengan Multi-tenancy
        public DbSet<Product> Products { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<Inventory> Inventories { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Customer> Customers { get; set; }

        // Facebook Lead Integration Entities
        public DbSet<Lead> Leads { get; set; }
        public DbSet<FacebookLead> FacebookLeads { get; set; }
        public DbSet<LeadActivity> LeadActivities { get; set; }
        public DbSet<CustomerOnboarding> CustomerOnboardings { get; set; }
        public DbSet<OnboardingActivity> OnboardingActivities { get; set; }

        // Demo Users for Multi-Role ShopERP
        public DbSet<DemoUser> Users { get; set; }

        // Shop entity removed 2026-07-21 — Tenant is the single identity.

        // HKD Business Tenants — Wave 5: now uses Rich Domain TenantAggregate.Tenant
        public DbSet<Tenant> Tenants { get; set; }

        // Wave 1 Phase 2: User-Tenant mapping (cross-tenant entity)
        public DbSet<UserTenant> UserTenants { get; set; }

        // Wave 6: Permission groups for bundle-based RBAC
        public DbSet<PermissionGroup> PermissionGroups { get; set; }
        public DbSet<UserPermissionGroup> UserPermissionGroups { get; set; }

        // Wave 14: API Keys for HMAC request signing
        public DbSet<ApiKey> ApiKeys { get; set; }

        // Wave 9: Push Subscriptions for Web Push notifications
        public DbSet<PushSubscription> PushSubscriptions { get; set; }

        // PHASE 2: SOCIAL FLYWHEEL ENTITIES
        public DbSet<SocialCampaign> SocialCampaigns { get; set; }
        public DbSet<LoyaltyRewards> LoyaltyRewards { get; set; }
        public DbSet<LoyaltyIssuanceRecord> LoyaltyIssuanceRecords { get; set; }  // VALCN v2.0 Phase 1

        // WEEK 1: ACCOUNTING ENGINE ENTITIES
        public DbSet<CoreAccountingEntry> AccountingEntries { get; set; }

        // Outbox Pattern for Event Sourcing
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<JournalTemplate> JournalTemplates { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }

        // E-Invoice (Sprint 3 — persisted state for atomic transaction with Outbox)
        public DbSet<ElectronicInvoice> ElectronicInvoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }

        // E-Invoice Webhook Idempotency — durable deduplication store (Finding #5 fix)
        public DbSet<ProcessedWebhookKey> ProcessedWebhookKeys { get; set; }

        // UC1: Pending Invoice Queue - Batch processing for anonymous retail invoices
        public DbSet<PendingInvoiceQueue> PendingInvoiceQueues { get; set; }

        // PHASE 2.9.4: Audit Trail - Immutable append-only logs
        public DbSet<AuditLog> AuditLogs { get; set; }

        // W3: VAS Account Chart reference data (global — NOT tenant-scoped, NOT IMustHaveTenant)
        public DbSet<Entities.AccountChartEntity> AccountCharts { get; set; }

        // W5: Period closing status persistence (tenant-scoped, IMustHaveTenant via BaseEntity).
        // Replaces the previous in-memory static Dictionary in PeriodClosingService.
        public DbSet<Entities.PeriodClosingStatusEntity> PeriodClosingStatuses { get; set; }

        // Platform SystemAdmin: Platform-level users (cross-tenant, NOT tenant-scoped)
        public DbSet<Entities.PlatformUser> PlatformUsers { get; set; }

        // KhachLink Full Flow W0: Shop feature toggle settings (tenant-scoped)
        public DbSet<Entities.ShopFeatureSettingsEntity> ShopFeatureSettings { get; set; }

        // Phase 1 (Multi-VPS Checkout): ShopERP hosting instances (platform-level, NOT tenant-scoped)
        public DbSet<ShopInstance> ShopInstances { get; set; }

        // Phase 6 (Admin UI): Sysadmin-curated featured products for Home.razor (PG-only, tenant-scoped)
        public DbSet<FeaturedProduct> FeaturedProducts { get; set; }

        // Phase 5: Campaign push jobs + delivery tracking (PG-only, tenant-scoped)
        public DbSet<CampaignPushJob> CampaignPushJobs { get; set; }
        public DbSet<PushNotificationDelivery> PushNotificationDeliveries { get; set; }

        // Loyalty-B: Redemption system — DbSets for interface compliance.
        // Storage is ShopERP SQLite; Gateway PG ignores these (configurations auto-applied but tables not used).
        public DbSet<RedemptionCatalogItem> RedemptionCatalogItems { get; set; }
        public DbSet<RedemptionRecord> RedemptionRecords { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        // Loyalty-C WS-B: Gamification framework
        public DbSet<Mission> Missions { get; set; }
        public DbSet<MissionCompletion> MissionCompletions { get; set; }

        // WS-2: Promo campaign system — bulk marketing push with per-recipient tracking
        public DbSet<PromoCampaign> PromoCampaigns { get; set; }
        public DbSet<PromoCampaignRecipient> PromoCampaignRecipients { get; set; }

        // Community Commerce Sprint 0 (v1.2: 11 DbSet) — Gateway PG only, tenant-scoped via IMustHaveTenant
        public DbSet<CommunityRole> CommunityRoles { get; set; }
        public DbSet<DeliveryTask> DeliveryTasks { get; set; }
        public DbSet<DeliveryTracking> DeliveryTrackings { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<SalesReferral> SalesReferrals { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<ProductReferralConfig> ProductReferralConfigs { get; set; } // v1.1 NEW
        public DbSet<AppInstallAttribution> AppInstallAttributions { get; set; } // v1.1 NEW
        public DbSet<DeviceRegistration> DeviceRegistrations { get; set; } // v1.2 NEW
        public DbSet<FraudFlag> FraudFlags { get; set; } // v1.2 NEW

        // Sprint 7 — Commerce Mode Toggle (3 new DbSets)
        public DbSet<SystemSetting> SystemSettings { get; set; } // global config (TenantId nullable)
        public DbSet<ProductCostPrice> ProductCostPrices { get; set; } // Q1: Vạn An's negotiated cost per product
        public DbSet<CommunityFundSpendRecord> CommunityFundSpendRecords { get; set; } // Q3: audit trail for fund disbursement

        // Loyalty Alliance System — cross-tenant wallet (PG-only)
        public DbSet<LoyaltyGlobalConfig> LoyaltyGlobalConfigs { get; set; } // single-row global config (TenantId = Empty)
        public DbSet<LoyaltyTenantConfig> LoyaltyTenantConfigs { get; set; } // per-tenant override (tenant-scoped)
        public DbSet<AllianceWallet> AllianceWallets { get; set; } // cross-tenant wallet (TenantId = Empty)
        public DbSet<AllianceTransaction> AllianceTransactions { get; set; } // append-only transaction log (TenantId = Empty)

        // #100: KhachLink home page section toggles — GLOBAL (not tenant-scoped, single row)
        public DbSet<KhachLinkHomeSettings> KhachLinkHomeSettings { get; set; } // single-row global config (TenantId = Empty)

        // #126: Guard QR Verification — vehicle session tracking + scan logs (PG-only)
        public DbSet<VehicleSession> VehicleSessions { get; set; }
        public DbSet<GuardScanLog> GuardScanLogs { get; set; }

        // KhachLink Multi-Profile R1: KhachLink instances (platform-level, NOT tenant-scoped — TenantId = Guid.Empty sentinel)
        public DbSet<KhachLinkInstance> KhachLinkInstances { get; set; }

        // Domain Reseller R1: Tenant-owned domains registered via Vạn An reseller platform
        // Platform-level (TenantId = Guid.Empty sentinel) — tracks OwnerTenantId separately
        public DbSet<VanAn.Shared.Domain.Aggregates.DomainResellerAggregate.TenantDomain> TenantDomains { get; set; }

        // VA-FI-MVP2 (2026-08-21): Tenant business profile for Financial Intelligence.
        // PG-only (Gateway source of truth for accounting + tenant config). Tenant-scoped (1 row per tenant).
        // ShopERP accesses via HTTP proxy to FinancialIntelligenceController (no direct DbContext injection).
        public DbSet<BusinessProfile> BusinessProfiles { get; set; }

        // Crawl-to-Onboard Pipeline (2026-08-25): Claim requests from business owners for Pending tenants.
        // PG-only (Gateway source of truth per Option C). NOT mirrored to ShopERP SQLite.
        // SysAdmin queue — reviewed + approved/rejected via TenantClaimController.
        public DbSet<TenantClaimRequest> TenantClaimRequests { get; set; }

        // Crawl-to-Onboard Pipeline (2026-08-25): Audit trail of crawled business listings (provenance).
        // PG-only. Cascade delete with Tenant (audit meaningless without tenant).
        public DbSet<CrawlSource> CrawlSources { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // KHÓA CHẶT: Chặn EF Core tự động quét và biến Strong-typed ID thành bảng độc lập
            // Phải đặt TRƯỚC base.OnModelCreating để ngăn auto-discovery
            modelBuilder.Ignore<OrderId>();
            modelBuilder.Ignore<ElectronicInvoiceId>();
            modelBuilder.Ignore<InvoiceItemId>();
            modelBuilder.Ignore<TenantId>();
            modelBuilder.Ignore<LeadId>();
            modelBuilder.Ignore<CustomerId>();
            modelBuilder.Ignore<ProductId>();
            modelBuilder.Ignore<IngredientId>();
            modelBuilder.Ignore<RecipeId>();
            modelBuilder.Ignore<InventoryId>();
            modelBuilder.Ignore<JournalEntryId>();
            modelBuilder.Ignore<OrderItemId>();
            modelBuilder.Ignore<OrderStatusId>();
            modelBuilder.Ignore<FeaturedProductId>();
            // #126: Guard QR Verify — business key VOs (Single-Identity Pattern)
            modelBuilder.Ignore<VehicleSessionId>();
            modelBuilder.Ignore<GuardScanLogId>();
            // VA-FI-MVP2 (2026-08-21): BusinessProfile business key VO (Single-Identity Pattern).
            modelBuilder.Ignore<BusinessProfileId>();

            base.OnModelCreating(modelBuilder);

            // === GLOBAL IGNORES ===
            // AccountingPeriod is a value object (record) used as computed property
            // It should never be mapped as a separate entity
            _ = modelBuilder.Ignore<AccountingPeriod>();

            // HKDBook is an abstract base class for dynamic report generation
            // It's not meant to be persisted as an entity
            _ = modelBuilder.Ignore<HKDBook>();
            _ = modelBuilder.Ignore<GenericHKDBook>();

            // E-Invoice value objects — not entities, used as converted properties
            _ = modelBuilder.Ignore<ProviderId>();
            _ = modelBuilder.Ignore<InvoiceIdempotencyKey>();
            _ = modelBuilder.Ignore<InvoiceAggregate>();
            _ = modelBuilder.Ignore<SubmitAttempt>();

            // OutboxEvent is a domain entity — persistence via OutboxMessage (OutboxRepository maps between them)
            // Must be ignored to prevent EF from creating a duplicate OutboxEvent table
            _ = modelBuilder.Ignore<OutboxEvent>();

            // === AUTO-DISCOVER ALL CONFIGURATIONS ===
            // Architect++: Use auto-discovery instead of manual registration
            _ = modelBuilder.ApplyConfigurationsFromAssembly(typeof(VanAnDbContext).Assembly);

            // 🛡️ GLOBAL QUERY FILTERS - Multi-tenancy isolation
            // ApplyMultiTenancyFilters(modelBuilder);

            // NOTE: Product, Ingredient, Recipe, Inventory configurations moved to
            // dedicated IEntityTypeConfiguration files to support value object converters
            // (ProductId, IngredientId, RecipeId, InventoryId, TenantId)
            // Configurations: ProductConfiguration.cs, IngredientConfiguration.cs,
            //                 RecipeConfiguration.cs, InventoryConfiguration.cs

            // Order entity is configured in OrderConfiguration.cs (OwnsOne for CustomerInfo, HasKey for OrderId)

            // Configure Customer entity
            _ = modelBuilder.Entity<Customer>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);

                // Wave 2: PII encryption for PhoneNumber and Email
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
                _ = entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // NOTE: OrderItem configuration moved to OrderItemConfiguration.cs
            // to support OrderItemId and TenantId value object converters

            // 🛡️ E-Invoice: Configure InvoiceItem entity
            _ = modelBuilder.Entity<InvoiceItem>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.Id).HasConversion(v => v.Value, v => new InvoiceItemId(v));
                _ = entity.Property(e => e.ItemCode).IsRequired().HasMaxLength(50);
                _ = entity.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
                _ = entity.Property(e => e.Unit).IsRequired().HasMaxLength(20);
                _ = entity.Property(e => e.Quantity).HasPrecision(18, 4);
                _ = entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
                _ = entity.Property(e => e.VatRate).HasPrecision(5, 4);
                _ = entity.Property(e => e.Amount).HasPrecision(18, 2);
                _ = entity.Property(e => e.VatAmount).HasPrecision(18, 2);

                _ = entity.HasOne(e => e.Invoice)
                      .WithMany(i => i.Items)
                      .HasForeignKey(e => e.InvoiceId)
                      .HasPrincipalKey(i => i.InvoiceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // NOTE: DemoUser, SocialCampaign, LoyaltyRewards configurations moved to
            // dedicated IEntityTypeConfiguration files to support TenantId value object converter
            // Configurations: DemoUserConfiguration.cs, SocialCampaignConfiguration.cs,
            //                 LoyaltyRewardsConfiguration.cs
            // (ShopConfiguration removed 2026-07-21 — Shop entity deleted)


            // 🛡️ GLOBAL QUERY FILTERS - Multi-tenancy isolation for other entities
            ApplyMultiTenancyFilters(modelBuilder);
        }

        // 🛡️ MULTI-TENANCY HELPER METHODS
        private void ApplyMultiTenancyFilters(ModelBuilder modelBuilder)
        {
            // Skip if tenant provider is null (for design-time or migrations)
            if (_tenantProvider == null)
            {
                return;
            }

            // Phase 1: During startup (no HTTP context), TenantId is Guid.Empty.
            // Filters use CurrentTenantIdValue which is evaluated at QUERY TIME, so they work
            // correctly regardless of TenantId during model creation.
            // The fail-fast guard for empty TenantId is enforced at repository/service layer.
            // if (_tenantProvider.TenantId == Guid.Empty)
            // {
            //     throw new InvalidOperationException("TenantId is empty — cannot query tenant-scoped data. Ensure JWT claim 'TenantId' is set.");
            // }

            // Apply to all entities implementing IMustHaveTenant
            // (AccountingEntry excluded: special cross-tenant audit/reconciliation queries).
            // (SystemSetting excluded: global settings, TenantId = Guid.Empty — Sprint 7)
            // (CommunityFundSpendRecord excluded: system-wide fund audit, TenantId = Guid.Empty — Sprint 7 Q3)
            // (Loyalty Alliance: 3 entities are cross-tenant, TenantId = Empty — Phase 1B)
            //   LoyaltyTenantConfig is NOT excluded — it's per-tenant config (tenant-scoped, correct).
            IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType> entityTypes = modelBuilder.Model.GetEntityTypes()
                .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType)
                    && e.ClrType != typeof(CoreAccountingEntry)
                    && e.ClrType != typeof(SystemSetting)
                    && e.ClrType != typeof(CommunityFundSpendRecord)
                    && e.ClrType != typeof(LoyaltyGlobalConfig)
                    && e.ClrType != typeof(AllianceWallet)
                    && e.ClrType != typeof(AllianceTransaction)
                    && e.ClrType != typeof(KhachLinkHomeSettings)
                    && e.ClrType != typeof(KhachLinkInstance)
                    && e.ClrType != typeof(VanAn.Shared.Domain.Aggregates.DomainResellerAggregate.TenantDomain));

            // Capture context so EF Core evaluates CurrentTenantIdValue at QUERY TIME.
            // Using TenantId (model type) as RHS ensures:
            //   Sanitize<TenantId>(TenantId_value) -> "value is TenantId" -> TRUE (no Convert.ChangeType)
            // When reading from DB:
            //   ConvertFromProvider(string_from_db) -> Sanitize<string>(string) -> "string is string" -> TRUE
            VanAnDbContext capturedContext = this;

            // Property: IMustHaveTenant.TenantId (CLR type: TenantId)
            System.Reflection.PropertyInfo tenantIdProp =
                typeof(IMustHaveTenant).GetProperty(nameof(IMustHaveTenant.TenantId))
                ?? throw new InvalidOperationException("IMustHaveTenant.TenantId property not found");

            // Property: VanAnDbContext.CurrentTenantIdValue (CLR type: TenantId)
            System.Reflection.PropertyInfo currentTenantIdProp =
                typeof(VanAnDbContext).GetProperty(nameof(CurrentTenantIdValue))
                ?? throw new InvalidOperationException("VanAnDbContext.CurrentTenantIdValue not found");

            System.Linq.Expressions.MemberExpression currentTenantIdExpr =
                System.Linq.Expressions.Expression.Property(
                    System.Linq.Expressions.Expression.Constant(capturedContext),
                    currentTenantIdProp);

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)
            {
                System.Type clrType = entityType.ClrType;

                // e => ((IMustHaveTenant)e).TenantId == capturedContext.CurrentTenantIdValue
                // TenantId has ValueConverter<TenantId,string>: EF Core translates to
                //   WHERE "TenantId" = @p0   (with @p0 built via Sanitize<TenantId>(TenantId_val) -> OK)
                ParameterExpression parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");

                System.Linq.Expressions.MemberExpression entityTenantId =
                    System.Linq.Expressions.Expression.Property(
                        System.Linq.Expressions.Expression.Convert(parameter, typeof(IMustHaveTenant)),
                        tenantIdProp);

                System.Linq.Expressions.BinaryExpression comparison =
                    System.Linq.Expressions.Expression.Equal(entityTenantId, currentTenantIdExpr);

                LambdaExpression filterExpression =
                    System.Linq.Expressions.Expression.Lambda(comparison, parameter);

                modelBuilder.Entity(clrType).HasQueryFilter(filterExpression);
            }
        }


        // MOVED: All ValueConverter classes moved to separate files for consistency
        // See: Infrastructure/ValueConverters/ directory

        /// <summary>
        /// Register TenantId global type converter via Conventions API (EF Core 6+).
        /// This ensures ALL TenantId properties across all entities use ValueConverter<TenantId, string>,
        /// fixing the SQLite IConvertible error that occurs with Guid as provider type.
        /// </summary>
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            _ = configurationBuilder.Properties<TenantId>()
                .HaveConversion<ValueConverters.TenantIdConverter>();
        }

        // Interface implementation for IVanAnDbContext
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Database.BeginTransactionAsync(cancellationToken);
        }

        /// <summary>EF Core provider name — used for provider-specific query logic (e.g. FOR UPDATE on PG).</summary>
        public string ProviderName => Database.ProviderName ?? string.Empty;
    }
}
