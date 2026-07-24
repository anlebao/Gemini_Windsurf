using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Loyalty-B: EF Core configuration for RedemptionCatalogItem entity.
    /// ShopERP SQLite (tenant-scoped). Admin-managed redeemable catalog.
    /// Follows Single-Identity Pattern: Id = PK only.
    /// </summary>
    public class RedemptionCatalogItemConfiguration : IEntityTypeConfiguration<RedemptionCatalogItem>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<RedemptionCatalogItem> builder)
        {
            builder.ToTable("RedemptionCatalogItems");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Description).HasMaxLength(1000);
            builder.Property(e => e.ImageUrl).HasMaxLength(500);
            builder.Property(e => e.PointsRequired).IsRequired();
            builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            builder.Property(e => e.StockCount);
            builder.Property(e => e.ValidFrom).IsRequired();
            builder.Property(e => e.ValidTo);
            builder.Property(e => e.VoucherExpiryDays).IsRequired().HasDefaultValue(30);

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasIndex(e => new { e.TenantId, e.IsActive });
            builder.HasIndex(e => e.PointsRequired);

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }
}
