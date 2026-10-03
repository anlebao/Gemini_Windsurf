using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for InventoryCount entity (VA-IIE Sprint B — kiểm kê đầu/cuối ca).
    /// </summary>
    public class InventoryCountConfiguration : IEntityTypeConfiguration<InventoryCount>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<InventoryCount> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: InventoryCountId is synced to Id in constructor (Id = InventoryCountId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.InventoryCountId.Value.
            _ = builder.Ignore(e => e.InventoryCountId);

            _ = builder.Property(e => e.CountType)
                .HasConversion<int>()
                .IsRequired();

            _ = builder.Property(e => e.Quantity)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.Unit)
                .IsRequired()
                .HasMaxLength(20);

            _ = builder.Property(e => e.MidShiftStockIn)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation properties configuration
            _ = builder.HasOne(e => e.Shift)
                .WithMany()
                .HasForeignKey(e => e.ShiftId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = builder.HasOne(e => e.Ingredient)
                .WithMany()
                .HasForeignKey(e => e.IngredientId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.ShiftId });
            _ = builder.HasIndex(e => new { e.TenantId, e.IngredientId });
        }
    }
}
