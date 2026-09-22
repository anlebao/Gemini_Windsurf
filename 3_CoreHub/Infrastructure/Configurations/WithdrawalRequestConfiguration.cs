using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.WalletAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for WithdrawalRequest entity — Settlement Batch-3 (TC-09).
    /// Payout lifecycle: Pending → Approved/Rejected (SystemAdmin) → Paid (manual bank ref).
    /// Status transitions are guarded by domain methods — not append-only like WalletTransaction.
    /// </summary>
    public class WithdrawalRequestConfiguration : IEntityTypeConfiguration<WithdrawalRequest>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<WithdrawalRequest> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.OwnerId).IsRequired();
            builder.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(e => e.Status).HasConversion<int>().IsRequired();
            builder.Property(e => e.BankReference).HasMaxLength(200);
            builder.Property(e => e.RejectReason).HasMaxLength(500);
            builder.Property(e => e.RequestedAt).IsRequired();
            builder.Property(e => e.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value));
            builder.HasIndex(e => e.OwnerId);
            builder.HasIndex(e => e.Status);
        }
    }
}
