using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Infrastructure
{
    public class VanAnDbContext(DbContextOptions<VanAnDbContext> options, ITenantProvider tenantProvider = null!) : DbContext(options), IVanAnDbContext
    {
        private readonly ITenantProvider _tenantProvider = tenantProvider;

        // 🛡️ PUBLIC PROPERTY FOR EF Core Query Filter
        public Guid CurrentTenantId => _tenantProvider?.TenantId ?? Guid.Empty;

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

        // Multi-tenant Shops
        public DbSet<Shop> Shops { get; set; }

        // HKD Business Tenants
        public DbSet<Tenant> Tenants { get; set; }

        // PHASE 2: SOCIAL FLYWHEEL ENTITIES
        public DbSet<SocialCampaign> SocialCampaigns { get; set; }
        public DbSet<LoyaltyRewards> LoyaltyRewards { get; set; }

        // WEEK 1: ACCOUNTING ENGINE ENTITIES
        public DbSet<CoreAccountingEntry> AccountingEntries { get; set; }

        // Outbox Pattern for Event Sourcing
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<JournalTemplate> JournalTemplates { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }

        // E-Invoice (Sprint 3 — persisted state for atomic transaction with Outbox)
        public DbSet<ElectronicInvoice> ElectronicInvoices { get; set; }

        // E-Invoice Webhook Idempotency — durable deduplication store (Finding #5 fix)
        public DbSet<ProcessedWebhookKey> ProcessedWebhookKeys { get; set; }

        // PHASE 2.9.4: Audit Trail - Immutable append-only logs
        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Global convention for all ValueObject<T> types - EF Core 8 proper 2-way converters
            // All converters now use separate classes for consistency

            _ = configurationBuilder.Properties<TenantId>()
                .HaveConversion<TenantIdConverter>();

            _ = configurationBuilder.Properties<AccountingBookType>()
                .HaveConversion<AccountingBookTypeConverter>();

            // Keep existing converters for other entities (not AccountingEntry)
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

            _ = configurationBuilder.Properties<ShopId>()
                .HaveConversion<ShopIdConverter>();

            _ = configurationBuilder.Properties<OrderItemId>()
                .HaveConversion<OrderItemIdConverter>();

            _ = configurationBuilder.Properties<JournalEntryId>()
                .HaveConversion<JournalEntryIdConverter>();

            _ = configurationBuilder.Properties<ElectronicInvoiceId>()
                .HaveConversion<ElectronicInvoiceIdConverter>();
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

            // Configure Product entity
            _ = modelBuilder.Entity<Product>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                _ = entity.Property(e => e.Description).HasMaxLength(500);
                _ = entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
                _ = entity.Property(e => e.Price).HasPrecision(18, 2);
                _ = entity.Property(e => e.VatRate).HasPrecision(5, 4); // 0.0000 to 1.0000
                _ = entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Configure Ingredient entity
            _ = modelBuilder.Entity<Ingredient>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                _ = entity.Property(e => e.Unit).IsRequired().HasMaxLength(20);
                _ = entity.Property(e => e.PricePerUnit).HasPrecision(18, 2);
                _ = entity.Property(e => e.CurrentStock).HasPrecision(18, 4);
                _ = entity.Property(e => e.MinStockThreshold).HasPrecision(18, 4);
                _ = entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Configure Recipe entity
            _ = modelBuilder.Entity<Recipe>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.QuantityNeeded).HasPrecision(18, 4);
                _ = entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Configure Inventory entity
            _ = modelBuilder.Entity<Inventory>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.Quantity).HasPrecision(18, 4);
                _ = entity.Property(e => e.LastUpdated).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Order entity is configured in OrderConfiguration.cs (OwnsOne for CustomerInfo, HasKey for OrderId)

            // Configure Customer entity
            _ = modelBuilder.Entity<Customer>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
                _ = entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
                _ = entity.Property(e => e.Email).HasMaxLength(100);
                _ = entity.Property(e => e.CustomerTier).IsRequired().HasMaxLength(20);
                _ = entity.Property(e => e.TotalSpent).HasPrecision(18, 2);
                _ = entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Configure OrderItem entity
            _ = modelBuilder.Entity<OrderItem>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.Quantity);
                _ = entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
                _ = entity.Property(e => e.VatRate).HasPrecision(5, 4);
                _ = entity.Property(e => e.Notes).HasMaxLength(500);
                _ = entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Navigation properties
                _ = entity.HasOne(e => e.Order)
                      .WithMany(o => o.Items)
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                _ = entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Shop entity
            _ = modelBuilder.Entity<Shop>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                _ = entity.Property(e => e.Address).HasMaxLength(500);
                _ = entity.Property(e => e.Phone).HasMaxLength(20);
                _ = entity.Property(e => e.Email).HasMaxLength(100);
                _ = entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            });

            // Configure DemoUser entity
            _ = modelBuilder.Entity<DemoUser>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
                _ = entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
                _ = entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
                _ = entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // 🛡️ PHASE 2: Configure SocialCampaign entity
            _ = modelBuilder.Entity<SocialCampaign>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.UtmSource).IsRequired().HasMaxLength(100);
                _ = entity.Property(e => e.CampaignName).IsRequired().HasMaxLength(200);
                _ = entity.Property(e => e.TrackingCode).IsRequired().HasMaxLength(50);
                _ = entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

                // Navigation properties
                _ = entity.HasOne(e => e.Shop)
                      .WithMany(s => s.SocialCampaigns)
                      .HasForeignKey(e => e.ShopId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 🛡️ PHASE 2: Configure LoyaltyRewards entity
            _ = modelBuilder.Entity<LoyaltyRewards>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.Property(e => e.History).HasMaxLength(2000);
                _ = entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            });


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

            // Get current tenant dynamically from ITenantProvider
            Guid currentTenantId = _tenantProvider.TenantId;

            // Apply to all entities implement IMustHaveTenant (except AccountingEntry)
            // AccountingEntry is excluded: special case for cross-tenant queries, audit/history, reconciliation
            IEnumerable<Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType> entityTypes = modelBuilder.Model.GetEntityTypes()
                .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType) && e.ClrType != typeof(CoreAccountingEntry));

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType? entityType in entityTypes)
            {
                try
                {
                    System.Linq.Expressions.ParameterExpression parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");

                    // Use EF.Property<TenantId> to access the TenantId property with value converter
                    // This allows EF Core to translate the filter correctly with the converter
                    System.Reflection.MethodInfo propertyMethod = typeof(EF).GetMethod(nameof(EF.Property))!
                        .MakeGenericMethod(typeof(TenantId));
                    System.Linq.Expressions.MethodCallExpression tenantIdProperty = System.Linq.Expressions.Expression.Call(
                        propertyMethod,
                        parameter,
                        System.Linq.Expressions.Expression.Constant("TenantId"));

                    System.Linq.Expressions.ConstantExpression currentTenantIdValue = System.Linq.Expressions.Expression.Constant(
                        new TenantId(currentTenantId));
                    System.Linq.Expressions.BinaryExpression filter = System.Linq.Expressions.Expression.Equal(tenantIdProperty, currentTenantIdValue);

                    // Use non-generic overload that infers delegate type automatically
                    System.Linq.Expressions.LambdaExpression lambda = System.Linq.Expressions.Expression.Lambda(filter, parameter);

                    _ = modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
                catch (Exception ex)
                {
                    // Log error but continue - some entities may not have TenantId
                    Console.WriteLine($"Failed to apply tenant filter to {entityType.ClrType.Name}: {ex.Message}");
                }
            }
        }

        // 🛡️ AUTO-INJECTION TENANT ID
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Auto-set TenantId cho new entities
            IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added && e.Entity is IMustHaveTenant);

            foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry? entry in entries)
            {
                IMustHaveTenant entity = (IMustHaveTenant)entry.Entity;
                if (entity.TenantId.Value == Guid.Empty)
                {
                    if (entity is BaseEntity baseEntity)
                    {
                        baseEntity.SetTenantId(new TenantId(_tenantProvider?.TenantId ?? Guid.Empty));
                    }
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        // IVanAnDbContext implementation
        public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return await Database.BeginTransactionAsync(cancellationToken);
        }
    }

    // MOVED: All ValueConverter classes moved to separate files for consistency
    // See: Infrastructure/ValueConverters/ directory
}
