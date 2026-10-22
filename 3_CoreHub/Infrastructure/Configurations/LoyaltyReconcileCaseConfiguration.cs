using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for LoyaltyReconcileCase entity.
    /// Loyalty Alliance Phase 5 (P0.2): tenant-scoped audit row for unreconciled
    /// Alliance deductions (redeem deducted points, voucher failed, compensation failed).
    /// Tenant-scoped by the redeeming tenant — surfaced via /admin/loyalty-reconcile.
    /// </summary>
    public class LoyaltyReconcileCaseConfiguration : IEntityTypeConfiguration<LoyaltyReconcileCase>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<LoyaltyReconcileCase> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.VoucherCode).IsRequired().HasMaxLength(50);
            _ = builder.Property(e => e.CustomerDeviceId).IsRequired();
            _ = builder.Property(e => e.CustomerId).IsRequired(false);
            _ = builder.Property(e => e.Points).IsRequired();
            _ = builder.Property(e => e.Reason).IsRequired().HasMaxLength(500);

            _ = builder.Property(e => e.Status)
                .HasConversion<int>()
                .IsRequired();

            _ = builder.Property(e => e.FailedAt).IsRequired();
            _ = builder.Property(e => e.ResolvedAt).IsRequired(false);
            _ = builder.Property(e => e.ResolvedBy).HasMaxLength(200).IsRequired(false);
            _ = builder.Property(e => e.ResolutionNote).HasMaxLength(1000).IsRequired(false);

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Common query: list open cases for admin reconciliation view
            _ = builder.HasIndex(e => new { e.Status, e.FailedAt });
        }
    }
}
