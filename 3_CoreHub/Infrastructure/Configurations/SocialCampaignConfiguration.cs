using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for SocialCampaign entity
    /// </summary>
    public class SocialCampaignConfiguration : IEntityTypeConfiguration<SocialCampaign>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<SocialCampaign> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // ShopId + Shop navigation removed 2026-07-21 — campaigns are tenant-wide only.

            _ = builder.Property(e => e.UtmSource)
                .IsRequired()
                .HasMaxLength(100);

            _ = builder.Property(e => e.CampaignName)
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.TrackingCode)
                .IsRequired()
                .HasMaxLength(50);

            // Media fields (optional)
            _ = builder.Property(e => e.ImageUrl)
                .HasMaxLength(500);

            _ = builder.Property(e => e.VideoUrl)
                .HasMaxLength(500);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            _ = builder.HasIndex(e => e.TenantId);
            _ = builder.HasIndex(e => e.TrackingCode);
            _ = builder.HasIndex(e => e.IsActive);
        }
    }
}
