using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for DeliveryTracking entity (Community Commerce Sprint 0).
    /// Append-only GPS pings — no update methods on entity.
    /// </summary>
    public class DeliveryTrackingConfiguration : IEntityTypeConfiguration<DeliveryTracking>
    {
        public void Configure(EntityTypeBuilder<DeliveryTracking> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.DeliveryTaskId).IsRequired();
            _ = builder.Property(e => e.Latitude).IsRequired();
            _ = builder.Property(e => e.Longitude).IsRequired();
            _ = builder.Property(e => e.RecordedAt).IsRequired();
            _ = builder.HasIndex(e => new { e.DeliveryTaskId, e.RecordedAt });
            _ = builder.Property(e => e.TenantId).IsRequired();
        }
    }
}
