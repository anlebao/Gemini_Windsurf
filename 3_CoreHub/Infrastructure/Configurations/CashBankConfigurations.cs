using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>SRS v1.2 W3/D: BankAccount — unique (TenantId, AccountCode) mọi TK tiền của tenant.</summary>
    public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccountEntity>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<BankAccountEntity> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.AccountCode).HasMaxLength(20).IsRequired();
            builder.Property(e => e.BankName).HasMaxLength(200);
            builder.Property(e => e.AccountNumber).HasMaxLength(50);
            builder.Property(e => e.DisplayName).HasMaxLength(200).IsRequired();
            builder.Property(e => e.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value));
            builder.HasIndex(e => new { e.TenantId, e.AccountCode }).IsUnique();
        }
    }

    /// <summary>SRS v1.2 W4/C2: OrderPayment — payment event per order; SourceEventId unique (ACC12-004).</summary>
    public class OrderPaymentConfiguration : IEntityTypeConfiguration<OrderPaymentEntity>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<OrderPaymentEntity> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Amount).HasPrecision(18, 2);
            builder.Property(e => e.Method).HasMaxLength(30);
            builder.Property(e => e.SourceEventId).HasMaxLength(120).IsRequired();
            builder.Property(e => e.Reference).HasMaxLength(120);
            builder.Property(e => e.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value));
            builder.HasIndex(e => new { e.TenantId, e.SourceEventId }).IsUnique();
            builder.HasIndex(e => new { e.TenantId, e.OrderId });
        }
    }

    /// <summary>SRS v1.2 W3/D: InternalTransfer — phiếu chuyển tiền nội bộ + status machine.</summary>
    public class InternalTransferConfiguration : IEntityTypeConfiguration<InternalTransferEntity>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<InternalTransferEntity> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.VoucherNumber).HasMaxLength(30).IsRequired();
            builder.Property(e => e.FromAccountCode).HasMaxLength(20).IsRequired();
            builder.Property(e => e.ToAccountCode).HasMaxLength(20).IsRequired();
            builder.Property(e => e.Amount).HasPrecision(18, 2);
            builder.Property(e => e.FeeAmount).HasPrecision(18, 2);
            builder.Property(e => e.FeeAccountCode).HasMaxLength(20);
            builder.Property(e => e.BankReference).HasMaxLength(100);
            builder.Property(e => e.Description).HasMaxLength(500);
            builder.Property(e => e.Status).HasConversion<int>();
            builder.Property(e => e.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value));
            builder.HasIndex(e => new { e.TenantId, e.VoucherNumber }).IsUnique();
            builder.HasIndex(e => new { e.TenantId, e.Status });
        }
    }
}
