using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.ProductCostPriceAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for ProductCostPrice entity — Sprint 7 (Q1).
    /// Vạn An's negotiated cost price per product per tenant.
    /// Unique index on (TenantId, ProductId).
    /// </summary>
    public class ProductCostPriceConfiguration : IEntityTypeConfiguration<ProductCostPrice>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<ProductCostPrice> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.ProductId).IsRequired();
            builder.Property(e => e.CostPrice).HasPrecision(18, 2).IsRequired();
            builder.Property(e => e.UpdatedAt);
            builder.Property(e => e.UpdatedBy);
            builder.Property(e => e.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value))
                .IsRequired();
            builder.HasIndex(e => new { e.TenantId, e.ProductId }).IsUnique();
        }
    }
}
