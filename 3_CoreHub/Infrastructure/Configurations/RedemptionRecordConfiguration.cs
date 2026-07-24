using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Loyalty-B: EF Core configuration for RedemptionRecord entity.
    /// ShopERP SQLite (tenant-scoped). Tracks customer redemptions.
    /// </summary>
    public class RedemptionRecordConfiguration : IEntityTypeConfiguration<RedemptionRecord>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<RedemptionRecord> builder)
        {
            builder.ToTable("RedemptionRecords");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.CustomerId).IsRequired();
            builder.Property(e => e.CatalogItemId).IsRequired();
            builder.Property(e => e.VoucherId);
            builder.Property(e => e.PointsSpent).IsRequired();
            builder.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
            builder.Property(e => e.RedeemedAt).IsRequired();
            builder.Property(e => e.FulfilledAt);
            builder.Property(e => e.CancelledAt);
            builder.Property(e => e.Notes).HasMaxLength(1000);

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasIndex(e => e.CustomerId);
            builder.HasIndex(e => e.CatalogItemId);
            builder.HasIndex(e => e.VoucherId);
            builder.HasIndex(e => new { e.TenantId, e.Status });

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }
}
