using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Audit;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Configurations;
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

        // Outbox pattern tables
        public DbSet<CoreOutboxMessage> OutboxMessages { get; set; }

        // Local business tables
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Product> Products { get; set; }

        // Additional tables required by IVanAnDbContext (for Offline Mode)
        public DbSet<AccountingEntry> AccountingEntries { get; set; }
        public DbSet<LoyaltyRewards> LoyaltyRewards { get; set; }
        public DbSet<SocialCampaign> SocialCampaigns { get; set; }
        public DbSet<HKDBook> HKDBooks { get; set; }
        public DbSet<JournalEntry> JournalEntries { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

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

            _ = configurationBuilder.Properties<ShopId>()
                .HaveConversion<ShopIdConverter>();

            _ = configurationBuilder.Properties<OrderItemId>()
                .HaveConversion<OrderItemIdConverter>();

            _ = configurationBuilder.Properties<JournalEntryId>()
                .HaveConversion<JournalEntryIdConverter>();
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

            // Apply configurations from CoreHub assembly via assembly scanning
            // This avoids direct reference to CoreHub.Infrastructure.Configurations
            System.Reflection.Assembly coreHubAssembly = typeof(CoreOutboxMessage).Assembly;
            _ = modelBuilder.ApplyConfigurationsFromAssembly(coreHubAssembly,
                t => t.Name.EndsWith("Configuration") && t.GetInterface(nameof(IEntityConfiguration)) != null);

            // === VALUE OBJECT CONFIGURATIONS ===
            // Order: Use BaseEntity.Id as PK, OwnsOne for CustomerInfo
            _ = modelBuilder.Entity<Order>(entity =>
            {
                _ = entity.HasKey(o => o.Id);
                _ = entity.OwnsOne(o => o.CustomerInfo, ci =>
                {
                    _ = ci.Property(c => c.FullName).HasMaxLength(200);
                    _ = ci.Property(c => c.PhoneNumber).HasMaxLength(50);
                    _ = ci.Property(c => c.Email).HasMaxLength(200);
                    _ = ci.Property(c => c.Address).HasMaxLength(500);
                    _ = ci.Property(c => c.Notes).HasMaxLength(1000);
                });
            });

            // JournalEntry: OwnsMany for JournalEntryLine (Value Object)
            _ = modelBuilder.Entity<JournalEntry>(entity =>
            {
                _ = entity.HasKey(e => e.Id);
                _ = entity.OwnsMany(e => e.Lines, lineBuilder =>
                {
                    _ = lineBuilder.WithOwner().HasForeignKey(l => l.JournalEntryId);
                    _ = lineBuilder.Property(l => l.AccountNumber).IsRequired().HasMaxLength(50);
                    _ = lineBuilder.Property(l => l.DebitAmount).HasPrecision(18, 2);
                    _ = lineBuilder.Property(l => l.CreditAmount).HasPrecision(18, 2);
                    _ = lineBuilder.Property(l => l.Description).HasMaxLength(500);
                });
            });

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
    }
}
