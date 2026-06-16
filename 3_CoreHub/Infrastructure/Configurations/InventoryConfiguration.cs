using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Inventory entity
    /// </summary>
    public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Inventory> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // InventoryId value object converter
            _ = builder.Property(e => e.InventoryId)
                .HasConversion(id => id.Value, value => new InventoryId(value))
                .IsRequired();


            // NOTE: IngredientId is Guid (not value object) per PHASE 3 FIX
            // See Domain.cs line 676: "Use Guid instead of IngredientId"

            _ = builder.Property(e => e.Quantity)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.LastUpdated)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation properties configuration
            _ = builder.HasOne(e => e.Ingredient)
                .WithMany()
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            _ = builder.HasIndex(e => e.InventoryId);
            _ = builder.HasIndex(e => new { e.TenantId, e.IngredientId });
            _ = builder.HasIndex(e => e.LastUpdated);
        }
    }
}
