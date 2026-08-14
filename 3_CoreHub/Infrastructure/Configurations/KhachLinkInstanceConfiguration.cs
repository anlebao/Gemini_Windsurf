using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for KhachLinkInstance entity — KhachLink Multi-Profile R1.
    /// Platform-level routing entity — NOT tenant-scoped (TenantId = Guid.Empty sentinel).
    /// No multi-tenancy query filter applied (entity excluded in ApplyMultiTenancyFilters).
    /// Follows ShopInstanceConfiguration pattern.
    /// </summary>
    public class KhachLinkInstanceConfiguration : IEntityTypeConfiguration<KhachLinkInstance>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<KhachLinkInstance> builder)
        {
            builder.ToTable("KhachLinkInstances");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedOnAdd();

            // TenantId — inherited from BaseEntity, stored as Guid (platform sentinel = Guid.Empty).
            // NOT unique-indexed (all rows share the same sentinel).
            builder.Property("TenantId").IsRequired();

            builder.Property(e => e.Label)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.Profile)
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(KhachLinkProfile.FullCommerce);

            builder.Property(e => e.CustomDomain)
                .IsRequired()
                .HasMaxLength(255);

            // Unique index on CustomDomain — one KhachLinkInstance per domain
            builder.HasIndex(e => e.CustomDomain).IsUnique();

            builder.Property(e => e.OwnerTenantId)
                .IsRequired(false);

            builder.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            // Owned nav flags — 15 flattened bool columns with defaults matching FullCommerce preset
            builder.OwnsOne(e => e.NavFlags, nf =>
            {
                nf.Property(p => p.ShowHome).HasDefaultValue(true).HasColumnName("NavFlags_ShowHome");
                nf.Property(p => p.ShowCart).HasDefaultValue(true).HasColumnName("NavFlags_ShowCart");
                nf.Property(p => p.ShowOrders).HasDefaultValue(true).HasColumnName("NavFlags_ShowOrders");
                nf.Property(p => p.ShowLoyaltyHistory).HasDefaultValue(true).HasColumnName("NavFlags_ShowLoyaltyHistory");
                nf.Property(p => p.ShowMissions).HasDefaultValue(true).HasColumnName("NavFlags_ShowMissions");
                nf.Property(p => p.ShowRewards).HasDefaultValue(true).HasColumnName("NavFlags_ShowRewards");
                nf.Property(p => p.ShowAllianceWallet).HasDefaultValue(true).HasColumnName("NavFlags_ShowAllianceWallet");
                nf.Property(p => p.ShowStores).HasDefaultValue(true).HasColumnName("NavFlags_ShowStores");
                nf.Property(p => p.ShowCampaigns).HasDefaultValue(true).HasColumnName("NavFlags_ShowCampaigns");
                nf.Property(p => p.ShowScan).HasDefaultValue(true).HasColumnName("NavFlags_ShowScan");
                nf.Property(p => p.ShowQrClaim).HasDefaultValue(true).HasColumnName("NavFlags_ShowQrClaim");
                nf.Property(p => p.ShowCommunity).HasDefaultValue(true).HasColumnName("NavFlags_ShowCommunity");
                nf.Property(p => p.ShowJobs).HasDefaultValue(false).HasColumnName("NavFlags_ShowJobs");
                nf.Property(p => p.ShowProfile).HasDefaultValue(true).HasColumnName("NavFlags_ShowProfile");
                nf.Property(p => p.ShowStaffDashboard).HasDefaultValue(true).HasColumnName("NavFlags_ShowStaffDashboard");
            });

            // Audit fields from BaseEntity
            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted)
                .HasDefaultValue(false);
        }
    }
}
