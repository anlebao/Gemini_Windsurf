using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;
using VanAn.Shared.Extensions;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Domain;
using VanAn.CoreHub.Infrastructure.ValueConverters;
using VanAn.CoreHub.Infrastructure.Configurations;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;

namespace VanAn.CoreHub.Infrastructure;

public class VanAnDbContext : DbContext, IVanAnDbContext
{
    private readonly ITenantProvider _tenantProvider;

    public VanAnDbContext(DbContextOptions<VanAnDbContext> options, ITenantProvider tenantProvider = null!) 
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

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
    public DbSet<Shared.Domain.DemoUser> Users { get; set; }
    
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

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Global convention for all ValueObject<T> types - EF Core 8 proper 2-way converters
        // All converters now use separate classes for consistency
        
        configurationBuilder.Properties<TenantId>()
            .HaveConversion<TenantIdConverter>();
        
        configurationBuilder.Properties<AccountingBookType>()
            .HaveConversion<AccountingBookTypeConverter>();
        
        // Keep existing converters for other entities (not AccountingEntry)
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

        // === AUTO-DISCOVER ALL CONFIGURATIONS ===
        // Architect++: Use auto-discovery instead of manual registration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VanAnDbContext).Assembly);

        // 🛡️ GLOBAL QUERY FILTERS - Multi-tenancy isolation
        // ApplyMultiTenancyFilters(modelBuilder);

        // Configure Product entity
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.VatRate).HasPrecision(5, 4); // 0.0000 to 1.0000
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Configure Ingredient entity
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Unit).IsRequired().HasMaxLength(20);
            entity.Property(e => e.PricePerUnit).HasPrecision(18, 2);
            entity.Property(e => e.CurrentStock).HasPrecision(18, 4);
            entity.Property(e => e.MinStockThreshold).HasPrecision(18, 4);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Configure Recipe entity
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuantityNeeded).HasPrecision(18, 4);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Configure Inventory entity
        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.LastUpdated).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // Order entity is configured in OrderConfiguration.cs (OwnsOne for CustomerInfo, HasKey for OrderId)
        
        // Configure Customer entity
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.CustomerTier).IsRequired().HasMaxLength(20);
            entity.Property(e => e.TotalSpent).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        // Configure OrderItem entity
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.VatRate).HasPrecision(5, 4);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Navigation properties
            entity.HasOne(e => e.Order)
                  .WithMany(o => o.Items)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Shop entity
        modelBuilder.Entity<Shop>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
        });

        // Configure DemoUser entity
        modelBuilder.Entity<Shared.Domain.DemoUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        
        // 🛡️ PHASE 2: Configure SocialCampaign entity
        modelBuilder.Entity<SocialCampaign>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UtmSource).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CampaignName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.TrackingCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Navigation properties
            entity.HasOne(e => e.Shop)
                  .WithMany(s => s.SocialCampaigns)
                  .HasForeignKey(e => e.ShopId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        // 🛡️ PHASE 2: Configure LoyaltyRewards entity
        modelBuilder.Entity<LoyaltyRewards>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.History).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
        });
        
                
        // 🛡️ GLOBAL QUERY FILTERS - Multi-tenancy isolation for other entities
        ApplyMultiTenancyFilters(modelBuilder);
    }

    // 🛡️ MULTI-TENANCY HELPER METHODS
    private void ApplyMultiTenancyFilters(ModelBuilder modelBuilder)
    {
        // Skip if tenant provider is null (for design-time or migrations)
        if (_tenantProvider == null) return;
        
        // Get current tenant dynamically from ITenantProvider
        var currentTenantId = _tenantProvider.TenantId;
        
        // Apply to all entities implement IMustHaveTenant (except AccountingEntry)
        // AccountingEntry is excluded: special case for cross-tenant queries, audit/history, reconciliation
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => typeof(IMustHaveTenant).IsAssignableFrom(e.ClrType) && e.ClrType != typeof(CoreAccountingEntry));

        foreach (var entityType in entityTypes)
        {
            try
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "e");
                
                // Use EF.Property<TenantId> to access the TenantId property with value converter
                // This allows EF Core to translate the filter correctly with the converter
                var propertyMethod = typeof(EF).GetMethod(nameof(EF.Property))!
                    .MakeGenericMethod(typeof(TenantId));
                var tenantIdProperty = System.Linq.Expressions.Expression.Call(
                    propertyMethod,
                    parameter,
                    System.Linq.Expressions.Expression.Constant("TenantId"));
                
                var currentTenantIdValue = System.Linq.Expressions.Expression.Constant(
                    new TenantId(currentTenantId));
                var filter = System.Linq.Expressions.Expression.Equal(tenantIdProperty, currentTenantIdValue);
                
                // Use non-generic overload that infers delegate type automatically
                var lambda = System.Linq.Expressions.Expression.Lambda(filter, parameter);
                
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter((System.Linq.Expressions.LambdaExpression)lambda);
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
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added && e.Entity is IMustHaveTenant);

        foreach (var entry in entries)
        {
            var entity = (IMustHaveTenant)entry.Entity;
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
