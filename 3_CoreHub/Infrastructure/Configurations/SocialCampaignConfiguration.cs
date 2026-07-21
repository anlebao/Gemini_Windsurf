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


            // ShopId is nullable Guid — campaign can apply to all shops in tenant (null) or specific shop
            _ = builder.Property(e => e.ShopId)
                .IsRequired(false);

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

            // Navigation properties — optional (ShopId can be null for tenant-wide campaigns)
            _ = builder.HasOne(e => e.Shop)
                .WithMany(s => s.SocialCampaigns)
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.ShopId });
            _ = builder.HasIndex(e => e.TrackingCode);
            _ = builder.HasIndex(e => e.IsActive);
        }
    }
}
