using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Notification Hub (2026-10-15): EF Core configuration for TenantNotification entity.
    /// PG-only (Gateway source of truth) — thông báo cho OWNER tenant (ShopERP đọc qua API).
    /// Tenant-scoped (TenantId via BaseEntity — TenantIdConverter global).
    /// Single-Identity Pattern: Id = PK only.
    /// </summary>
    public class TenantNotificationConfiguration : IEntityTypeConfiguration<TenantNotification>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<TenantNotification> builder)
        {
            builder.ToTable("TenantNotifications");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Category)
                .HasConversion<int>()
                .IsRequired();

            builder.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.Body)
                .IsRequired()
                .HasMaxLength(2000);

            builder.Property(e => e.ActionUrl).HasMaxLength(500);
            builder.Property(e => e.ReferenceType).HasMaxLength(50);
            builder.Property(e => e.ReferenceId);

            builder.Property(e => e.Severity)
                .HasConversion<int>()
                .HasDefaultValue(TenantNotificationSeverity.Info);

            builder.Property(e => e.IsRead)
                .HasDefaultValue(false);

            builder.Property(e => e.ReadAt);

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasIndex(e => new { e.TenantId, e.Category, e.IsRead });
            builder.HasIndex(e => new { e.TenantId, e.CreatedAt });
            builder.HasIndex(e => new { e.ReferenceType, e.ReferenceId });

            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted)
                .HasDefaultValue(false);
        }
    }
}
