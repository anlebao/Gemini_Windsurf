using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.CommunityFundAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for CommunityFundSpendRecord entity — Sprint 7 (Q3).
    /// Audit trail for community fund disbursement. TenantId = Guid.Empty (system-wide).
    /// Immutable — no update methods (audit trail integrity).
    /// </summary>
    public class CommunityFundSpendRecordConfiguration : IEntityTypeConfiguration<CommunityFundSpendRecord>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<CommunityFundSpendRecord> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            builder.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            builder.Property(e => e.Recipient).HasMaxLength(200).IsRequired();
            builder.Property(e => e.ApprovedBy).IsRequired();
            builder.Property(e => e.SpentAt).IsRequired();
            builder.Property(e => e.WalletTransactionId).IsRequired();
            builder.Property(e => e.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value));
            builder.HasIndex(e => e.SpentAt);
        }
    }
}
