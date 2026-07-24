using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Phase 5: EF Core configuration for CampaignPushJob entity.
    /// PG-only (Gateway) — tracks bulk push notification jobs for SocialCampaigns.
    /// Tenant-scoped (inherits TenantId from BaseEntity via TenantIdConverter).
    /// Follows Single-Identity Pattern: Id = PK only (no separate business key VO).
    /// </summary>
    public class CampaignPushJobConfiguration : IEntityTypeConfiguration<CampaignPushJob>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<CampaignPushJob> builder)
        {
            builder.ToTable("CampaignPushJobs");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.CampaignId).IsRequired();

            builder.Property(e => e.CriteriaJson)
                .IsRequired()
                .HasMaxLength(2000);

            builder.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            builder.Property(e => e.SentCount).HasDefaultValue(0);
            builder.Property(e => e.FailedCount).HasDefaultValue(0);
            builder.Property(e => e.ClickedCount).HasDefaultValue(0);

            builder.Property(e => e.SentAt);
            builder.Property(e => e.ErrorMessage).HasMaxLength(1000);

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasIndex(e => e.CampaignId);
            builder.HasIndex(e => new { e.TenantId, e.CampaignId });

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted)
                .HasDefaultValue(false);
        }
    }
}
