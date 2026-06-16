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


            // Note: ShopId is Guid (not value object)

            _ = builder.Property(e => e.UtmSource)
                .IsRequired()
                .HasMaxLength(100);

            _ = builder.Property(e => e.CampaignName)
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.TrackingCode)
                .IsRequired()
                .HasMaxLength(50);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Navigation properties
            _ = builder.HasOne(e => e.Shop)
                .WithMany(s => s.SocialCampaigns)
                .HasForeignKey(e => e.ShopId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.ShopId });
            _ = builder.HasIndex(e => e.TrackingCode);
            _ = builder.HasIndex(e => e.IsActive);
        }
    }
}
