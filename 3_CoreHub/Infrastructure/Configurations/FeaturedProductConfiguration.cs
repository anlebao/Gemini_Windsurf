using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Phase 6 (Admin UI): EF Core configuration for FeaturedProduct entity.
    /// PG-only marketing table — NOT in ShopERP SQLite.
    /// Tenant-scoped (inherits TenantId from BaseEntity via TenantIdConverter).
    /// Follows Single-Identity Pattern: FeaturedProductId VO ignored, Id = PK.
    /// </summary>
    public class FeaturedProductConfiguration : IEntityTypeConfiguration<FeaturedProduct>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<FeaturedProduct> builder)
        {
            builder.ToTable("FeaturedProducts");

            builder.HasKey(e => e.Id);

            // Single-Identity Pattern: FeaturedProductId VO not mapped to column
            builder.Ignore(e => e.FeaturedProductId);

            builder.Property(e => e.ProductId).IsRequired();

            builder.Property(e => e.DisplayName)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.DisplayDescription)
                .HasMaxLength(500);

            builder.Property(e => e.ImageUrl)
                .HasMaxLength(500);

            builder.Property(e => e.DisplayPrice)
                .HasPrecision(18, 2);

            builder.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(e => e.SortOrder)
                .HasDefaultValue(0);

            builder.Property(e => e.FeaturedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Unique: one featured entry per ProductId per Tenant
            builder.HasIndex(e => new { e.ProductId, e.TenantId }).IsUnique();

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
