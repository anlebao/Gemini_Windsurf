using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;
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
        public DbSet<InvoiceItem> InvoiceItems { get; set; }

        // E-Invoice Webhook Idempotency — durable deduplication store (Finding #5 fix)
        public DbSet<ProcessedWebhookKey> ProcessedWebhookKeys { get; set; }

        // UC1: Pending Invoice Queue - Batch processing for anonymous retail invoices
        public DbSet<PendingInvoiceQueue> PendingInvoiceQueues { get; set; }

        // PHASE 2.9.4: Audit Trail - Immutable append-only logs
        public DbSet<AuditLog> AuditLogs { get; set; }


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
            modelBuilder.Ignore<ShopId>();
            modelBuilder.Ignore<JournalEntryId>();
            modelBuilder.Ignore<OrderItemId>();
            modelBuilder.Ignore<OrderStatusId>();

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
                _ = entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
                _ = entity.Property(e => e.Email).HasMaxLength(100);
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

            // NOTE: Shop, DemoUser, SocialCampaign, LoyaltyRewards configurations moved to
            // dedicated IEntityTypeConfiguration files to support TenantId value object converter
            // Configurations: ShopConfiguration.cs, DemoUserConfiguration.cs,
            //                 SocialCampaignConfiguration.cs, LoyaltyRewardsConfiguration.cs


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

            // Resolve EF.Property<Guid> MethodInfo safely with explicit parameter types to avoid
            // ambiguous match between different EF.Property<T> overloads.
            System.Reflection.MethodInfo efPropertyMethod = typeof(EF)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(m => m.Name == "Property" && m.IsGenericMethod && m.GetParameters().Length == 2)
                .Select(m => new { Method = m, Parameters = m.GetParameters() })
                .Where(x => x.Parameters[0].ParameterType == typeof(object) && x.Parameters[1].ParameterType == typeof(string))
                .Select(x => x.Method)
                .FirstOrDefault()
                ?.MakeGenericMethod(typeof(Guid)) ?? throw new InvalidOperationException("Unable to resolve EF.Property<Guid> method");

            foreach (Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType in entityTypes)
            {
                System.Type clrType = entityType.ClrType;

                // Build query filter expression: e => EF.Property<Guid>(e, "TenantId") == currentTenantId
                ParameterExpression parameter = System.Linq.Expressions.Expression.Parameter(clrType, "e");
                System.Linq.Expressions.MethodCallExpression propertyCall = System.Linq.Expressions.Expression.Call(
                    null, // static method
                    efPropertyMethod,
                    System.Linq.Expressions.Expression.Convert(parameter, typeof(object)),
                    System.Linq.Expressions.Expression.Constant("TenantId", typeof(string))
                );
                System.Linq.Expressions.BinaryExpression comparison = System.Linq.Expressions.Expression.Equal(
                    propertyCall,
                    System.Linq.Expressions.Expression.Constant(currentTenantId, typeof(Guid))
                );
                LambdaExpression filterExpression = System.Linq.Expressions.Expression.Lambda(comparison, parameter);

                // Apply query filter
                modelBuilder.Entity(clrType).HasQueryFilter(filterExpression);
            }
        }

        // MOVED: All ValueConverter classes moved to separate files for consistency
        // See: Infrastructure/ValueConverters/ directory

        // Interface implementation for IVanAnDbContext
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            return Database.BeginTransactionAsync(cancellationToken);
        }
    }
}
