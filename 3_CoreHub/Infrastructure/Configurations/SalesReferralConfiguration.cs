using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for SalesReferral entity (Community Commerce Sprint 0).
    /// v1.1: composite code (SalesmanCode + ProductShortCode) + per-product commission snapshot.
    /// v1.2: +RiskScore/RiskFactors/HoldUntil + index on CommissionStatus.
    /// </summary>
    public class SalesReferralConfiguration : IEntityTypeConfiguration<SalesReferral>
    {
        public void Configure(EntityTypeBuilder<SalesReferral> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.SalesmanId).IsRequired();
            _ = builder.Property(e => e.SalesmanCode).IsRequired().HasMaxLength(10);
            _ = builder.Property(e => e.ProductId).IsRequired(); // v1.1 NEW
            _ = builder.Property(e => e.ProductShortCode).HasMaxLength(20); // v1.1 NEW
            _ = builder.Property(e => e.CommissionAmount).HasPrecision(18, 2);
            _ = builder.Property(e => e.CommissionRate).HasPrecision(18, 4); // v1.1 NEW — snapshot rate
            _ = builder.Property(e => e.CommissionStatus).HasConversion<int>();
            _ = builder.Property(e => e.AppInstallBonusAmount).HasPrecision(18, 2); // v1.1 NEW
            _ = builder.Property(e => e.AppInstallBonusStatus).HasConversion<int>(); // v1.1 NEW
            // v1.2 NEW — risk scoring fields
            _ = builder.Property(e => e.RiskScore).HasDefaultValue(0);
            _ = builder.Property(e => e.RiskFactors); // JSON, nullable
            _ = builder.Property(e => e.HoldUntil);
            _ = builder.HasIndex(e => e.SalesmanCode);
            _ = builder.HasIndex(e => e.OrderId);
            _ = builder.HasIndex(e => e.ProductId); // v1.1 NEW — query referrals per product
            _ = builder.HasIndex(e => e.CommissionStatus); // v1.2 NEW — query Held/Pending/Rejected
            _ = builder.Property(e => e.TenantId).IsRequired();
        }
    }
}
