using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Recipe entity (VA-IIE Sprint B refactor: header + RecipeLine).
    /// </summary>
    public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Recipe> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: RecipeId is synced to Id in constructor (Id = RecipeId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.RecipeId.Value.
            _ = builder.Ignore(e => e.RecipeId);

            _ = builder.Property(e => e.ProductId)
                .IsRequired();

            _ = builder.Property(e => e.Version)
                .IsRequired()
                .HasDefaultValue(1);

            _ = builder.Property(e => e.Yield)
                .HasPrecision(18, 4)
                .HasDefaultValue(1m);

            _ = builder.Property(e => e.WasteFactor)
                .HasPrecision(18, 4)
                .HasDefaultValue(0m);

            _ = builder.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            _ = builder.Property(e => e.EffectiveFrom)
                .IsRequired();

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation properties configuration
            _ = builder.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            _ = builder.HasMany(e => e.Lines)
                .WithOne(l => l.Recipe)
                .HasForeignKey(l => l.RecipeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.ProductId });
            _ = builder.HasIndex(e => new { e.TenantId, e.IsActive });
        }
    }
}
