using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Phase 5: EF Core configuration for PushNotificationDelivery entity.
    /// PG-only (Gateway) — tracks per-notification delivery + click status.
    /// Tenant-scoped (inherits TenantId from BaseEntity via TenantIdConverter).
    /// Follows Single-Identity Pattern: Id = PK only (no separate business key VO).
    /// </summary>
    public class PushNotificationDeliveryConfiguration : IEntityTypeConfiguration<PushNotificationDelivery>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<PushNotificationDelivery> builder)
        {
            builder.ToTable("PushNotificationDeliveries");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.CustomerId).IsRequired();
            builder.Property(e => e.CampaignPushJobId);
            builder.Property(e => e.NotificationId).IsRequired();

            builder.Property(e => e.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Delivered");

            builder.Property(e => e.ClickedAt);
            builder.Property(e => e.ActionUrl).HasMaxLength(500);

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasIndex(e => e.NotificationId);
            builder.HasIndex(e => e.CustomerId);
            builder.HasIndex(e => e.CampaignPushJobId);
            builder.HasIndex(e => new { e.TenantId, e.CustomerId });

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted)
                .HasDefaultValue(false);
        }
    }
}
