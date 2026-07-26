using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for WalletTransaction entity (Community Commerce Sprint 0).
    /// Immutable append-only ledger (like AccountingEntry). Reversal pattern via RelatedTransactionId.
    /// v1.4: Base atomic CreateTransactionAsync in WalletService (HR-SCALE-3 SELECT FOR UPDATE).
    /// </summary>
    public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
    {
        public void Configure(EntityTypeBuilder<WalletTransaction> builder)
        {
            _ = builder.HasKey(e => e.Id);
            _ = builder.Property(e => e.OwnerId).IsRequired();
            _ = builder.Property(e => e.Type).HasConversion<int>().IsRequired();
            _ = builder.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.BalanceAfter).HasPrecision(18, 2).IsRequired();
            _ = builder.Property(e => e.Description).IsRequired().HasMaxLength(500);
            _ = builder.Property(e => e.RelatedOrderId);
            _ = builder.Property(e => e.RelatedTransactionId); // v1.1 NEW — Reversal entry reference
            _ = builder.HasIndex(e => e.OwnerId);
            _ = builder.HasIndex(e => e.RelatedOrderId);
            _ = builder.HasIndex(e => e.RelatedTransactionId); // v1.1 NEW — query reversal for original
            _ = builder.Property(e => e.TenantId).IsRequired();
        }
    }
}
