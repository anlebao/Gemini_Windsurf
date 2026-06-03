using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Configurations;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using CoreOutboxMessage = VanAn.CoreHub.Infrastructure.OutboxMessage;

namespace VanAn.ShopERP.Infrastructure;

/// <summary>
/// ShopERP-specific DbContext for SQLite database
/// Handles orders, outbox messages, and local business data
/// Implements IVanAnDbContext for decoupling from VanAnDbContext
/// </summary>
public class ShopERPDbContext : DbContext, IVanAnDbContext
{
    public ShopERPDbContext(DbContextOptions<ShopERPDbContext> options) : base(options) { }

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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Global convention for all ValueObject<T> types - EF Core 8 proper 2-way converters
        // All converters now use separate classes for consistency
        // MUST match VanAnDbContext conventions for Strongly Typed IDs

        configurationBuilder.Properties<AccountingEntryId>()
            .HaveConversion<AccountingEntryIdConverter>();

        configurationBuilder.Properties<TenantId>()
            .HaveConversion<TenantIdConverter>();

        configurationBuilder.Properties<Money>()
            .HaveConversion<MoneyConverter>();

        configurationBuilder.Properties<AccountingPeriod>()
            .HaveConversion<AccountingPeriodConverter>();

        configurationBuilder.Properties<AccountingBookType>()
            .HaveConversion<AccountingBookTypeConverter>();

        configurationBuilder.Properties<LeadId>()
            .HaveConversion<LeadIdConverter>();

        configurationBuilder.Properties<CustomerId>()
            .HaveConversion<CustomerIdConverter>();

        configurationBuilder.Properties<ProductId>()
            .HaveConversion<ProductIdConverter>();

        configurationBuilder.Properties<IngredientId>()
            .HaveConversion<IngredientIdConverter>();

        configurationBuilder.Properties<RecipeId>()
            .HaveConversion<RecipeIdConverter>();

        configurationBuilder.Properties<InventoryId>()
            .HaveConversion<InventoryIdConverter>();

        configurationBuilder.Properties<OrderId>()
            .HaveConversion<OrderIdConverter>();

        configurationBuilder.Properties<OrderStatusId>()
            .HaveConversion<OrderStatusIdConverter>();

        configurationBuilder.Properties<ShopId>()
            .HaveConversion<ShopIdConverter>();

        configurationBuilder.Properties<OrderItemId>()
            .HaveConversion<OrderItemIdConverter>();

        configurationBuilder.Properties<JournalEntryId>()
            .HaveConversion<JournalEntryIdConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // === GLOBAL IGNORES ===
        // AccountingPeriod is a value object (record) used as computed property
        // It should never be mapped as a separate entity
        modelBuilder.Ignore<VanAn.Shared.Domain.AccountingPeriod>();

        // HKDBook is an abstract base class for dynamic report generation
        // It's not meant to be persisted as an entity
        modelBuilder.Ignore<VanAn.Shared.Domain.HKDBook>();
        modelBuilder.Ignore<VanAn.Shared.Domain.GenericHKDBook>();

        // Apply configurations from CoreHub assembly via assembly scanning
        // This avoids direct reference to CoreHub.Infrastructure.Configurations
        var coreHubAssembly = typeof(VanAn.CoreHub.Infrastructure.OutboxMessage).Assembly;
        modelBuilder.ApplyConfigurationsFromAssembly(coreHubAssembly, 
            t => t.Name.EndsWith("Configuration") && t.GetInterface(typeof(IEntityConfiguration).Name) != null);

        // === VALUE OBJECT CONFIGURATIONS ===
        // Order: Use BaseEntity.Id as PK, OwnsOne for CustomerInfo
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.OwnsOne(o => o.CustomerInfo, ci =>
            {
                ci.Property(c => c.FullName).HasMaxLength(200);
                ci.Property(c => c.PhoneNumber).HasMaxLength(50);
                ci.Property(c => c.Email).HasMaxLength(200);
                ci.Property(c => c.Address).HasMaxLength(500);
                ci.Property(c => c.Notes).HasMaxLength(1000);
            });
        });

        // JournalEntry: OwnsMany for JournalEntryLine (Value Object)
        modelBuilder.Entity<JournalEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.OwnsMany(e => e.Lines, lineBuilder =>
            {
                lineBuilder.WithOwner().HasForeignKey(l => l.JournalEntryId);
                lineBuilder.Property(l => l.AccountNumber).IsRequired().HasMaxLength(50);
                lineBuilder.Property(l => l.DebitAmount).HasPrecision(18, 2);
                lineBuilder.Property(l => l.CreditAmount).HasPrecision(18, 2);
                lineBuilder.Property(l => l.Description).HasMaxLength(500);
            });
        });

        // JournalTemplate: OwnsMany for JournalTemplateLine + TemplateValidationRule
        modelBuilder.Entity<JournalTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.OwnsMany(e => e.Lines, lineBuilder =>
            {
                lineBuilder.Property(l => l.AccountNumber).IsRequired().HasMaxLength(50);
                lineBuilder.Property(l => l.AmountFormula).HasMaxLength(200);
                lineBuilder.Property(l => l.DescriptionTemplate).HasMaxLength(500);
                lineBuilder.Ignore(l => l.IsCredit);
            });
            entity.OwnsMany(e => e.ValidationRules, ruleBuilder =>
            {
                ruleBuilder.Property(r => r.Rule).IsRequired().HasMaxLength(500);
                ruleBuilder.Property(r => r.Message).HasMaxLength(500);
            });
            entity.Ignore(e => e.BusinessRules);
        });

        // Apply global query filters for multi-tenancy
        ApplyGlobalQueryFilters(modelBuilder);
    }

    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<OrderItem>().HasQueryFilter(e => !e.Order.IsDeleted);
        modelBuilder.Entity<CoreOutboxMessage>().HasQueryFilter(e => e.ProcessedAt == null);
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
