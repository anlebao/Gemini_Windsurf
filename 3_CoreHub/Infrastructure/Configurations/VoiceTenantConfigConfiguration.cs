using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for VoiceTenantConfig (VOICE SERVICE C3 — kill-switch + open calling).
    /// PG-only (Gateway VanAnDbContext). 1 row / tenant — unique index on TenantId.
    /// Single-Identity: Id (PK) = tenantId, no business key VO.
    /// </summary>
    public class VoiceTenantConfigConfiguration : IEntityTypeConfiguration<VoiceTenantConfig>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<VoiceTenantConfig> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.VoiceFeatureEnabled).IsRequired();
            _ = builder.Property(e => e.VoiceOpenCalling).IsRequired();
            _ = builder.Property(e => e.LastChangedBy).HasMaxLength(256);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // 1 config per tenant — unique index on TenantId
            _ = builder.HasIndex(e => new { e.TenantId }).IsUnique();
        }
    }
}