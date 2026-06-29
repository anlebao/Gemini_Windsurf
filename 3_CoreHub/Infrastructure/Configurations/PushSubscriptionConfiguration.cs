using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Wave 9: EF Core configuration for PushSubscription entity
    /// Separate table for push notification subscriptions (per user decision)
    /// </summary>
    public class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
    {
        public void Configure(EntityTypeBuilder<PushSubscription> builder)
        {
            // Primary key
            _ = builder.HasKey(e => e.Id);

            // Property configurations
            _ = builder.Property(e => e.PushSubscriptionId)
                .IsRequired()
                .HasDefaultValueSql("newid()");

            _ = builder.Property(e => e.CustomerId)
                .IsRequired();

            _ = builder.Property(e => e.SubscriptionJson)
                .IsRequired()
                .HasMaxLength(2000); // Sufficient for push subscription JSON

            _ = builder.Property(e => e.UserAgent)
                .HasMaxLength(500);

            _ = builder.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            _ = builder.Property(e => e.LastUsedAt)
                .IsRequired()
                .HasDefaultValueSql("datetime('now')");

            _ = builder.Property(e => e.ExpiresAt)
                .IsRequired();

            // TenantId converter (inherited from BaseEntity)
            _ = builder.Property(e => e.TenantId)
                .IsRequired();

            // Soft delete query filter (inherited from BaseEntity)
            _ = builder.HasQueryFilter(e => !e.IsDeleted);

            // Indexes for performance
            _ = builder.HasIndex(e => e.CustomerId);
            _ = builder.HasIndex(e => new { e.TenantId, e.CustomerId });
            _ = builder.HasIndex(e => e.IsActive);
            _ = builder.HasIndex(e => e.ExpiresAt);

            // Table name
            _ = builder.ToTable("PushSubscriptions");
        }
    }
}