using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>
/// VALCN v2.0 Phase 1 — EF Core configuration for LoyaltyIssuanceRecord entity.
/// Tracks loyalty points issued per order (for Phase 4 refund reversal).
/// Single-Identity Pattern: Ignore business key VO (LoyaltyIssuanceRecordId).
/// </summary>
public class LoyaltyIssuanceRecordConfiguration : IEntityTypeConfiguration<LoyaltyIssuanceRecord>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<LoyaltyIssuanceRecord> builder)
    {
        _ = builder.HasKey(e => e.Id);

        // Single-Identity Pattern: ignore business key VO (not mapped to DB column)
        _ = builder.Ignore(e => e.LoyaltyIssuanceRecordId);

        _ = builder.Property(e => e.OrderId).IsRequired();
        _ = builder.Property(e => e.CustomerId).IsRequired();
        _ = builder.Property(e => e.PointsIssued).HasDefaultValue(0);
        _ = builder.Property(e => e.IsReversed).HasDefaultValue(false);
        _ = builder.Property(e => e.IssuedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Index for Phase 4 query: GetByOrderIdAsync(orderId, tenantId)
        _ = builder.HasIndex(e => e.OrderId);

        _ = builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
