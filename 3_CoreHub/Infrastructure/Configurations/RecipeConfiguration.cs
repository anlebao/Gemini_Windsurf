using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Recipe entity
    /// </summary>
    public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Recipe> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: RecipeId is synced to Id in constructor (Id = RecipeId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.RecipeId.Value.
            _ = builder.Ignore(e => e.RecipeId);


            // NOTE: ProductId and IngredientId are Guid (not value objects) per PHASE 3 FIX
            // See Domain.cs line 660-661: "Use Guid instead of ProductId/IngredientId"

            _ = builder.Property(e => e.QuantityNeeded)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation properties configuration
            _ = builder.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            _ = builder.HasOne(e => e.Ingredient)
                .WithMany()
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.ProductId });
            _ = builder.HasIndex(e => e.IngredientId);
        }
    }
}
