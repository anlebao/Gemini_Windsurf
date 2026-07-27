using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// WS-2: EF Core configuration for PromoCampaignRecipient entity.
    /// One row per customer targeted by a campaign — tracks Sent/Failed status.
    /// AF-P3-T2: extracted from PromoCampaignConfiguration.cs (same namespace, same logic).
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
