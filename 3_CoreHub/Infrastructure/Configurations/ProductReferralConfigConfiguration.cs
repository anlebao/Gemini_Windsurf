using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for ProductReferralConfig entity (Community Commerce Sprint 0 v1.1 NEW).
    /// Per-product commission rate (2-5%) + app-install bonus. 1 config per product (unique ProductId).
    /// </summary>
    public class ProductReferralConfigConfiguration : IEntityTypeConfiguration<ProductReferralConfig>
    {
        public void Configure(EntityTypeBuilder<ProductReferralConfig> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.ProductId).IsRequired();
            _ = builder.Property(e => e.ProductShortCode).HasMaxLength(20);
            _ = builder.Property(e => e.CommissionRate).HasPrecision(18, 4).IsRequired();
            _ = builder.Property(e => e.AppInstallBonus).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.IsActive).IsRequired();
            _ = builder.Property(e => e.CommissionBase).HasConversion<int>().HasDefaultValue(CommissionBase.OnOrderTotal);
            _ = builder.Property(e => e.TenantId).IsRequired();
            _ = builder.HasIndex(e => e.ProductId).IsUnique(); // 1 config per product
            // Filtered unique index — only non-null ProductShortCode values are unique within tenant
            _ = builder.HasIndex(e => new { e.TenantId, e.ProductShortCode })
                .IsUnique()
                .HasFilter("\"ProductShortCode\" IS NOT NULL");
        }
    }
}
