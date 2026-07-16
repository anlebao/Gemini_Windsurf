using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Product entity
    /// </summary>
    public class ProductConfiguration : IEntityTypeConfiguration<Product>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: ProductId is synced to Id in constructor (Id = ProductId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.ProductId.Value.
            _ = builder.Ignore(e => e.ProductId);


            _ = builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.Description)
                .HasMaxLength(500);

            _ = builder.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(100);

            _ = builder.Property(e => e.Price)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.CostPrice)
                .HasPrecision(18, 2)
                .HasDefaultValue(0m); // DMD-2 fix — default 0 for backward compat

            _ = builder.Property(e => e.VatRate)
                .HasPrecision(5, 4);

            _ = builder.Property(e => e.ImageUrl)
                .HasMaxLength(500);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.Category });
            _ = builder.HasIndex(e => e.IsActive);
        }
    }
}
