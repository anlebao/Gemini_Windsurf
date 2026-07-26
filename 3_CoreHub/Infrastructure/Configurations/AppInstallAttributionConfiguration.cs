using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for AppInstallAttribution entity (Community Commerce Sprint 0 v1.1 NEW).
    /// 1 customer = 1 attribution (unique CustomerId). v1.2: +RiskScore/RiskFactors/HoldUntil/DeviceRegistrationId.
    /// </summary>
    public class AppInstallAttributionConfiguration : IEntityTypeConfiguration<AppInstallAttribution>
    {
        public void Configure(EntityTypeBuilder<AppInstallAttribution> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.CustomerId).IsRequired();
            _ = builder.Property(e => e.SalesmanId).IsRequired();
            _ = builder.Property(e => e.ProductId).IsRequired();
            _ = builder.Property(e => e.BonusAmount).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.AttributionStatus).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.InstalledAt).IsRequired();
            _ = builder.Property(e => e.TenantId).IsRequired();
            // v1.2 NEW — risk scoring fields
            _ = builder.Property(e => e.RiskScore).HasDefaultValue(0);
            _ = builder.Property(e => e.RiskFactors); // JSON, nullable
            _ = builder.Property(e => e.HoldUntil);
            _ = builder.Property(e => e.DeviceRegistrationId);
            _ = builder.HasIndex(e => e.CustomerId).IsUnique(); // 1 customer 1 attribution (UC-12 AC-12.2)
            _ = builder.HasIndex(e => e.SalesmanId); // query bonus per salesman
            _ = builder.HasIndex(e => e.ProductId);
            _ = builder.HasIndex(e => e.AttributionStatus); // v1.2 NEW — query Held/Pending/Rejected
        }
    }
}
