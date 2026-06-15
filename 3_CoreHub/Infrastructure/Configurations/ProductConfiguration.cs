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

            // ProductId value object converter
            _ = builder.Property(e => e.ProductId)
                .HasConversion(id => id.Value, value => new ProductId(value))
                .IsRequired();

            // TenantId value object converter (from BaseEntity)
            _ = builder.Property(e => e.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value))
                .IsRequired();

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

            _ = builder.Property(e => e.VatRate)
                .HasPrecision(5, 4);

            _ = builder.Property(e => e.ImageUrl)
                .HasMaxLength(500);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            _ = builder.HasIndex(e => e.ProductId);
            _ = builder.HasIndex(e => new { e.TenantId, e.Category });
            _ = builder.HasIndex(e => e.IsActive);
        }
    }
}
