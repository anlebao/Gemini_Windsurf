using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for DeviceRegistration entity (Community Commerce Sprint 0 v1.2 NEW).
    /// Self-hosted device fingerprint + token. Max 3 active per Customer (application-layer enforce).
    /// </summary>
    public class DeviceRegistrationConfiguration : IEntityTypeConfiguration<DeviceRegistration>
    {
        public void Configure(EntityTypeBuilder<DeviceRegistration> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.CustomerId).IsRequired();
            _ = builder.Property(e => e.DeviceToken).IsRequired().HasMaxLength(64);
            _ = builder.Property(e => e.FingerprintHash).IsRequired().HasMaxLength(64);
            _ = builder.Property(e => e.FingerprintSignals).IsRequired(); // JSON
            _ = builder.Property(e => e.UserAgent).HasMaxLength(500);
            _ = builder.Property(e => e.Platform).HasMaxLength(50);
            _ = builder.Property(e => e.IpAddress).HasMaxLength(50);
            _ = builder.Property(e => e.TenantId).IsRequired();
            _ = builder.HasIndex(e => e.DeviceToken).IsUnique(); // 1 token = 1 device
            _ = builder.HasIndex(e => new { e.CustomerId, e.IsActive }); // query active devices per customer
            _ = builder.HasIndex(e => e.FingerprintHash); // anti-fraud check: ai khác dùng fingerprint này?
        }
    }
}
