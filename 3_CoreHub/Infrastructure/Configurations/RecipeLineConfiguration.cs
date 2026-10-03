using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for RecipeLine entity (VA-IIE Sprint B — recipe định mức).
    /// </summary>
    public class RecipeLineConfiguration : IEntityTypeConfiguration<RecipeLine>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<RecipeLine> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: RecipeLineId is synced to Id in constructor (Id = RecipeLineId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.RecipeLineId.Value.
            _ = builder.Ignore(e => e.RecipeLineId);

            _ = builder.Property(e => e.Quantity)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.Unit)
                .IsRequired()
                .HasMaxLength(20);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation properties configuration
            _ = builder.HasOne(e => e.Recipe)
                .WithMany(r => r.Lines)
                .HasForeignKey(e => e.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasOne(e => e.Ingredient)
                .WithMany()
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.RecipeId });
            _ = builder.HasIndex(e => new { e.TenantId, e.IngredientId });
        }
    }
}
