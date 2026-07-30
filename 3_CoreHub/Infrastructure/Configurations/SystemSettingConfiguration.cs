using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.SystemSettingAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for SystemSetting entity — Sprint 7.
    /// Key-value config for global settings (GlobalCommerceMode, DefaultPlatformFeeRate, etc.).
    /// TenantId nullable — global settings have TenantId = Guid.Empty.
    /// </summary>
    public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<SystemSetting> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Key).HasMaxLength(100).IsRequired();
            builder.Property(e => e.Value).HasMaxLength(500).IsRequired();
            builder.Property(e => e.UpdatedAt);
            builder.Property(e => e.UpdatedBy);
            builder.Property(e => e.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value));
            builder.HasIndex(e => e.Key).IsUnique();
        }
    }
}
