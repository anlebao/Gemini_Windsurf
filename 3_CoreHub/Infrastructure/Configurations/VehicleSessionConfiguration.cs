using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core Configuration for VehicleSession entity (Issue #126 — Guard QR Verify).
    /// Single-Identity Pattern: Id (PK) = VehicleSessionId.Value, Ignore business key VO.
    /// PG-only entity (Gateway DbContext).
    /// </summary>
    public class VehicleSessionConfiguration : IEntityTypeConfiguration<VehicleSession>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<VehicleSession> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // Single-Identity Pattern: Ignore business key VO (no separate DB column)
            _ = builder.Ignore(e => e.VehicleSessionId);

            // String constraints
            _ = builder.Property(e => e.PlateNumber).HasMaxLength(20).IsRequired();
            _ = builder.Property(e => e.PlatePhotoKey).HasMaxLength(200).IsRequired();
            _ = builder.Property(e => e.CustomerPhotoKey).HasMaxLength(200).IsRequired();
            _ = builder.Property(e => e.QrTokenHash).HasMaxLength(64).IsRequired(); // SHA256 hex = 64 chars
            _ = builder.Property(e => e.ShortCode).HasMaxLength(6).IsRequired();
            _ = builder.Property(e => e.CustomerPhone).HasMaxLength(20);
            _ = builder.Property(e => e.FlagReason).HasMaxLength(500);

            // Status enum mapping
            _ = builder.Property(e => e.Status)
                .HasConversion<int>()
                .IsRequired();

            // Timestamps
            _ = builder.Property(e => e.IssuedAt).IsRequired();
            _ = builder.Property(e => e.IssuedBy).IsRequired();

            // Unique indexes (INV-G01, INV-G02)
            _ = builder.HasIndex(e => new { e.TenantId, e.QrTokenHash }).IsUnique();
            _ = builder.HasIndex(e => new { e.TenantId, e.ShortCode, e.IssuedAt }).IsUnique();

            // Query indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.Status });
            _ = builder.HasIndex(e => e.CustomerId);
            _ = builder.HasIndex(e => e.IssuedAt);

            // TenantId value object converter (same pattern as OrderConfiguration)
            _ = builder.Property(e => e.TenantId)
                .IsRequired()
                .HasConversion(id => id.Value, value => new TenantId(value));
        }
    }
}
