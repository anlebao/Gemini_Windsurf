using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for TheoreticalConsumption entity (VA-IIE Sprint B — tiêu hao lý thuyết vs thực tế).
    /// </summary>
    public class TheoreticalConsumptionConfiguration : IEntityTypeConfiguration<TheoreticalConsumption>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<TheoreticalConsumption> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: TheoreticalConsumptionId is synced to Id in constructor (Id = TheoreticalConsumptionId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.TheoreticalConsumptionId.Value.
            _ = builder.Ignore(e => e.TheoreticalConsumptionId);

            _ = builder.Property(e => e.TheoreticalQuantity)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.ActualQuantity)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.Variance)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.VariancePercent)
                .HasPrecision(8, 2);

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
