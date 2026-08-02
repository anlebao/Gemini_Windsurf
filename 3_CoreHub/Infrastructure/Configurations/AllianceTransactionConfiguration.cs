using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for AllianceTransaction entity.
    /// Append-only transaction log (PG-only, NOT tenant-scoped — TenantId = Empty).
    /// TransactionTenantId records which tenant the EARN/REDEEM/ADJUST occurred at.
    /// Composite index on (WalletId, TransactionAt) for efficient wallet history queries.
    /// Loyalty Alliance System Phase 1B.
    /// </summary>
    public class AllianceTransactionConfiguration : IEntityTypeConfiguration<AllianceTransaction>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<AllianceTransaction> builder)
        {
            _ = builder.HasKey(e => e.Id);

            _ = builder.Property(e => e.WalletId).IsRequired();

            // TransactionTenantId: Guid (not TenantId value object) — records where transaction occurred.
            // Named differently from BaseEntity.TenantId to avoid shadowing (Phase 1A fix).
            _ = builder.Property(e => e.TransactionTenantId).IsRequired();

            _ = builder.Property(e => e.Type)
                .HasConversion<int>()
                .IsRequired();

            _ = builder.Property(e => e.Points).IsRequired();
            _ = builder.Property(e => e.BalanceAfter).IsRequired();
            _ = builder.Property(e => e.Reason).IsRequired().HasMaxLength(500);
            _ = builder.Property(e => e.VoucherCode).HasMaxLength(50);
            _ = builder.Property(e => e.TransactionAt).IsRequired();

            // Loyalty Consistency Fix Phase 0: IdempotencyKey for retry-safe HTTP proxy.
            // Non-unique index — most rows have NULL (only ShopERP-proxied calls set it);
            // uniqueness enforced at application layer (AllianceWalletService checks before insert).
            _ = builder.Property(e => e.IdempotencyKey).HasMaxLength(200).IsRequired(false);
            _ = builder.HasIndex(e => e.IdempotencyKey).HasDatabaseName("IX_AllianceTransactions_IdempotencyKey");

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Composite index for wallet history queries (most common: get transactions for a wallet, newest first)
            _ = builder.HasIndex(e => new { e.WalletId, e.TransactionAt });
        }
    }
}
