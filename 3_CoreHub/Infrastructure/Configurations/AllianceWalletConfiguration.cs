using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for AllianceWallet entity.
    /// Cross-tenant wallet — 1 per CustomerDeviceId (PG-only, NOT tenant-scoped — TenantId = Empty).
    /// Unique index on CustomerDeviceId ensures 1 wallet per device.
    /// Loyalty Alliance System Phase 1B.
    /// </summary>
    public class AllianceWalletConfiguration : IEntityTypeConfiguration<AllianceWallet>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<AllianceWallet> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // 1 wallet per device — unique index
            _ = builder.HasIndex(e => e.CustomerDeviceId).IsUnique();

            _ = builder.Property(e => e.CustomerDeviceId).IsRequired();
            _ = builder.Property(e => e.PhoneNumber).HasMaxLength(20);
            _ = builder.Property(e => e.TotalPointBalance).IsRequired();
            _ = builder.Property(e => e.IsActive).IsRequired();

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
