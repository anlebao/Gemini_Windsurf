using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for LoyaltyGlobalConfig entity.
    /// Single-row global config (PG-only, NOT tenant-scoped — TenantId = Empty).
    /// Loyalty Alliance System Phase 1B.
    /// </summary>
    public class LoyaltyGlobalConfigConfiguration : IEntityTypeConfiguration<LoyaltyGlobalConfig>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<LoyaltyGlobalConfig> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.Mode)
                .HasConversion<int>()
                .IsRequired();

            _ = builder.Property(e => e.PointsRate).IsRequired();
            _ = builder.Property(e => e.MinPointsPerOrder).IsRequired();
            _ = builder.Property(e => e.MaxPointsPerOrder).IsRequired();
            _ = builder.Property(e => e.MaxWalletPoints).IsRequired();

            _ = builder.Property(e => e.LastChangedBy).HasMaxLength(256);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }
}
