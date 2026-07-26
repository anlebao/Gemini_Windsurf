using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for DeliveryTask entity (Community Commerce Sprint 0).
    /// </summary>
    public class DeliveryTaskConfiguration : IEntityTypeConfiguration<DeliveryTask>
    {
        public void Configure(EntityTypeBuilder<DeliveryTask> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.OrderId).IsRequired();
            _ = builder.Property(e => e.ShipperId).IsRequired();
            _ = builder.Property(e => e.Status).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.AssignedAt).IsRequired();
            _ = builder.Property(e => e.ShopLat).IsRequired();
            _ = builder.Property(e => e.ShopLng).IsRequired();
            _ = builder.Property(e => e.FailureReason).HasMaxLength(500);
            _ = builder.HasIndex(e => e.OrderId);
            _ = builder.HasIndex(e => e.ShipperId);
            _ = builder.HasIndex(e => new { e.OrderId, e.Status }); // For "active task per order" check
            _ = builder.Property(e => e.TenantId).IsRequired();
        }
    }
}
