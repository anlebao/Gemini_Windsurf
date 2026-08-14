using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core Configuration for GuardScanLog entity (Issue #126 — Guard QR Verify).
    /// Single-Identity Pattern: Id (PK) = GuardScanLogId.Value, Ignore business key VO.
    /// PG-only entity (Gateway DbContext).
    /// </summary>
    public class GuardScanLogConfiguration : IEntityTypeConfiguration<GuardScanLog>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<GuardScanLog> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // Single-Identity Pattern: Ignore business key VO
            _ = builder.Ignore(e => e.GuardScanLogId);

            // String constraints
            _ = builder.Property(e => e.ScannedQrTokenHash).HasMaxLength(64).IsRequired();
            _ = builder.Property(e => e.Notes).HasMaxLength(1000);

            // Enum mapping
            _ = builder.Property(e => e.MatchResult)
                .HasConversion<int>()
                .IsRequired();

            // Timestamps
            _ = builder.Property(e => e.ScannedAt).IsRequired();
            _ = builder.Property(e => e.ScannedBy).IsRequired();

            // FK to VehicleSession
            _ = builder.HasOne<VehicleSession>()
                .WithMany()
                .HasForeignKey(e => e.VehicleSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Query indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.ScannedAt });
            _ = builder.HasIndex(e => e.VehicleSessionId);

            // TenantId value object converter
            _ = builder.Property(e => e.TenantId)
                .IsRequired()
                .HasConversion(id => id.Value, value => new TenantId(value));
        }
    }
}
