using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Loyalty-B: EF Core configuration for Voucher entity.
    /// ShopERP SQLite (tenant-scoped). Issued upon redemption, unique code + QR.
    /// </summary>
    public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Voucher> builder)
        {
            builder.ToTable("Vouchers");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.RedemptionRecordId).IsRequired();
            builder.Property(e => e.CustomerId).IsRequired();
            builder.Property(e => e.VoucherCode).IsRequired().HasMaxLength(50);
            builder.Property(e => e.QRCodeData);
            builder.Property(e => e.Status).IsRequired().HasMaxLength(20).HasDefaultValue("Active");
            builder.Property(e => e.IssuedAt).IsRequired();
            builder.Property(e => e.UsedAt);
            builder.Property(e => e.ExpiresAt).IsRequired();

            builder.Property(e => e.TenantId).IsRequired();

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasIndex(e => e.VoucherCode).IsUnique();
            builder.HasIndex(e => e.CustomerId);
            builder.HasIndex(e => e.RedemptionRecordId);
            builder.HasIndex(e => new { e.TenantId, e.Status });

            builder.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);
        }
    }
}
