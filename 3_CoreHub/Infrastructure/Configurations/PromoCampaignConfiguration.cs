using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// WS-2: EF Core configuration for PromoCampaign entity.
    /// ShopERP SQLite (tenant-scoped). Bulk marketing push campaign with per-recipient tracking.
    /// </summary>
    public class PromoCampaignConfiguration : IEntityTypeConfiguration<PromoCampaign>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<PromoCampaign> builder)
        {
            builder.ToTable("PromoCampaigns");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            builder.Property(e => e.Url).HasMaxLength(500);
            builder.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
            builder.Property(e => e.TotalRecipients).HasDefaultValue(0);
            builder.Property(e => e.SentCount).HasDefaultValue(0);
            builder.Property(e => e.FailedCount).HasDefaultValue(0);
            builder.Property(e => e.StartedAt);
            builder.Property(e => e.CompletedAt);
            builder.Property(e => e.SegmentSnapshotJson);
            builder.Property(e => e.ErrorMessage).HasMaxLength(1000);

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasIndex(e => new { e.TenantId, e.Status });
            builder.HasIndex(e => e.CreatedAt);

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }

    /// <summary>
    /// WS-2: EF Core configuration for PromoCampaignRecipient entity.
    /// One row per customer targeted by a campaign — tracks Sent/Failed status.
    /// </summary>
    public class PromoCampaignRecipientConfiguration : IEntityTypeConfiguration<PromoCampaignRecipient>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<PromoCampaignRecipient> builder)
        {
            builder.ToTable("PromoCampaignRecipients");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.PromoCampaignId).IsRequired();
            builder.Property(e => e.CustomerId).IsRequired();
            builder.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Pending");
            builder.Property(e => e.SentAt);
            builder.Property(e => e.ErrorMessage).HasMaxLength(1000);

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasIndex(e => new { e.PromoCampaignId, e.Status });
            builder.HasIndex(e => e.CustomerId);

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }
}
