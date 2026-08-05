using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for KhachLinkHomeSettings entity.
    /// Single-row global config (PG-only, NOT tenant-scoped — TenantId = Empty).
    /// #100: KhachLink home page section toggles.
    /// </summary>
    public class KhachLinkHomeSettingsConfiguration : IEntityTypeConfiguration<KhachLinkHomeSettings>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<KhachLinkHomeSettings> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.Home_CampaignSection_Enabled).IsRequired().HasDefaultValue(true);
            _ = builder.Property(e => e.Home_StoreSection_Enabled).IsRequired().HasDefaultValue(true);
            _ = builder.Property(e => e.Home_FeaturedSection_Enabled).IsRequired().HasDefaultValue(true);
            _ = builder.Property(e => e.Home_SocialHub_Enabled).IsRequired().HasDefaultValue(true);

            _ = builder.Property(e => e.LastChangedBy).HasMaxLength(256);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            _ = builder.ToTable("KhachLinkHomeSettings");
        }
    }
}
