using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.UserAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Shift entity (VA-IIE Sprint B — ca làm việc + báo cáo cuối ca).
    /// </summary>
    public class ShiftConfiguration : IEntityTypeConfiguration<Shift>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Shift> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: ShiftId is synced to Id in constructor (Id = ShiftId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.ShiftId.Value.
            _ = builder.Ignore(e => e.ShiftId);

            _ = builder.Property(e => e.ShiftType)
                .HasConversion<int>()
                .IsRequired();

            _ = builder.Property(e => e.Status)
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(ShiftStatus.Draft);

            _ = builder.Property(e => e.StartTime)
                .IsRequired();

            _ = builder.Property(e => e.HandoverNotes)
                .HasMaxLength(2000);

            _ = builder.Property(e => e.CashCount)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.PosCashTotal)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation properties: StaffUser (FK → Users/DemoUser.Id) — chuẩn bị HR-Payroll B2.
            _ = builder.HasOne(e => e.StaffUser)
                .WithMany()
                .HasForeignKey(e => e.StaffUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.Status });
            _ = builder.HasIndex(e => new { e.TenantId, e.StaffUserId });
        }
    }
}
