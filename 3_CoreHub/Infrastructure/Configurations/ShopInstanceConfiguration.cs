using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Phase 1 (Multi-VPS Checkout): EF Core configuration for ShopInstance entity.
    /// Platform-level routing entity — NOT tenant-scoped (TenantId = Guid.Empty sentinel).
    /// No multi-tenancy query filter applied (use IgnoreQueryFilters when querying).
    /// </summary>
    public class ShopInstanceConfiguration : IEntityTypeConfiguration<ShopInstance>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<ShopInstance> builder)
        {
            builder.ToTable("ShopInstances");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedOnAdd();

            // TenantId — inherited from BaseEntity, stored as Guid (platform sentinel = Guid.Empty).
            // NOT unique-indexed (all rows share the same sentinel).
            builder.Property("TenantId").IsRequired();

            builder.Property(e => e.BaseUrl)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(e => e.Label)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.MaxTenants)
                .IsRequired()
                .HasDefaultValue(50);

            builder.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(e => e.HealthCheckUrl)
                .HasMaxLength(500);

            builder.Property(e => e.LastHealthCheck);

            builder.Property(e => e.HealthStatus)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Unknown");

            // Unique index on BaseUrl — one ShopInstance per URL
            builder.HasIndex(e => e.BaseUrl).IsUnique();

            // Audit fields from BaseEntity
            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted)
                .HasDefaultValue(false);
        }
    }
}
