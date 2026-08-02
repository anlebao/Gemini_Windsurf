using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for LoyaltyTenantConfig entity.
    /// Per-tenant override (PG-only, tenant-scoped). Null fields inherit from LoyaltyGlobalConfig.
    /// Unique index on TenantId ensures 1 config per tenant.
    /// Loyalty Alliance System Phase 1B.
    /// </summary>
    public class LoyaltyTenantConfigConfiguration : IEntityTypeConfiguration<LoyaltyTenantConfig>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<LoyaltyTenantConfig> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // Mode: nullable enum → int? (null = inherit global)
            _ = builder.Property(e => e.Mode)
                .HasConversion<int?>();

            _ = builder.Property(e => e.IsAllianceMember).IsRequired();

            // MaxWalletPoints: nullable int (null = inherit global)
            _ = builder.Property(e => e.MaxWalletPoints);

            _ = builder.Property(e => e.LastChangedBy).HasMaxLength(256);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // 1 config per tenant — unique index on TenantId
            _ = builder.HasIndex(e => new { e.TenantId }).IsUnique();
        }
    }
}
