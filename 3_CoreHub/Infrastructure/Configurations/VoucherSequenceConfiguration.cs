using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>SRS v1.2 W2/C1: VoucherSequences — unique (TenantId, VoucherType, Year).</summary>
public class VoucherSequenceConfiguration : IEntityTypeConfiguration<VoucherSequenceEntity>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<VoucherSequenceEntity> builder)
    {
        _ = builder.ToTable("VoucherSequences");
        _ = builder.HasKey(e => e.Id);
        _ = builder.Property(e => e.Id).ValueGeneratedOnAdd();
        _ = builder.Property(e => e.TenantId).IsRequired();
        _ = builder.Property(e => e.VoucherType).IsRequired().HasMaxLength(10);
        _ = builder.Property(e => e.Year).IsRequired();
        _ = builder.Property(e => e.LastNumber).IsRequired();
        _ = builder.Property(e => e.UpdatedAt).IsRequired();

        _ = builder.HasIndex(e => new { e.TenantId, e.VoucherType, e.Year }).IsUnique();
    }
}
