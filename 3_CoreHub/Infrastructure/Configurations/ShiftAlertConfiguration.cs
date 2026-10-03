using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for ShiftAlert entity (VA-IIE Sprint B — cảnh báo khi đóng ca, in-app).
    /// </summary>
    public class ShiftAlertConfiguration : IEntityTypeConfiguration<ShiftAlert>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<ShiftAlert> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: ShiftAlertId is synced to Id in constructor (Id = ShiftAlertId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.ShiftAlertId.Value.
            _ = builder.Ignore(e => e.ShiftAlertId);

            _ = builder.Property(e => e.AlertCode)
                .IsRequired()
                .HasMaxLength(50);

            _ = builder.Property(e => e.Severity)
                .HasConversion<int>()
                .IsRequired();

            _ = builder.Property(e => e.Message)
                .IsRequired()
                .HasMaxLength(500);

            _ = builder.Property(e => e.VarianceValue)
                .HasPrecision(18, 4);

            _ = builder.Property(e => e.VariancePercent)
                .HasPrecision(8, 2);

            _ = builder.Property(e => e.ResolutionNote)
                .HasMaxLength(1000);

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
            _ = builder.HasIndex(e => new { e.TenantId, e.IsResolved });
        }
    }
}
