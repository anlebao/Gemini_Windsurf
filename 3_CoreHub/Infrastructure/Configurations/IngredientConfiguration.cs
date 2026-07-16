using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Ingredient entity
    /// </summary>
    public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Ingredient> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: IngredientId is synced to Id in constructor (Id = IngredientId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.IngredientId.Value.
            _ = builder.Ignore(e => e.IngredientId);


            _ = builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.Unit)
                .IsRequired()
                .HasMaxLength(20);

            _ = builder.Property(e => e.PricePerUnit)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.CurrentStock)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.MinStockThreshold)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.Name });
        }
    }
}
