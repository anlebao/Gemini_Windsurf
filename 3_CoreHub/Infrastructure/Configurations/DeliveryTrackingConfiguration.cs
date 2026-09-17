using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Append-only GPS pings — no update methods on entity.
    /// Realtime Platform P2 (2026-09-17): SubjectType/SubjectId/TrackerId added. Generic pings
    /// (e.g. Shop) leave DeliveryTaskId = Guid.Empty, so the legacy (DeliveryTaskId, RecordedAt)
    /// index no longer covers them — a subject-scoped index is added alongside it.
    /// </summary>
    public class DeliveryTrackingConfiguration : IEntityTypeConfiguration<DeliveryTracking>
    {
        public void Configure(EntityTypeBuilder<DeliveryTracking> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.DeliveryTaskId).IsRequired();
            _ = builder.Property(e => e.SubjectType).IsRequired().HasMaxLength(32)
                .HasDefaultValue(RealtimeSubjectType.Delivery.ToString());
            _ = builder.Property(e => e.SubjectId).IsRequired();
            _ = builder.Property(e => e.TrackerId);
            _ = builder.Property(e => e.Latitude).IsRequired();
            _ = builder.Property(e => e.Longitude).IsRequired();
            _ = builder.Property(e => e.RecordedAt).IsRequired();
            _ = builder.HasIndex(e => new { e.DeliveryTaskId, e.RecordedAt }); // legacy delivery pings
            _ = builder.HasIndex(e => new { e.TenantId, e.SubjectType, e.SubjectId, e.RecordedAt }); // generic subject pings
            _ = builder.Property(e => e.TenantId).IsRequired();
        }
    }
}
