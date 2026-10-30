using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>SRS v1.2 W2/B: TenantAccountCharts — COA override per-tenant. TenantId filter tường minh ở query.</summary>
public class TenantAccountChartConfiguration : IEntityTypeConfiguration<TenantAccountChartEntity>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<TenantAccountChartEntity> builder)
    {
        _ = builder.ToTable("TenantAccountCharts");
        _ = builder.HasKey(e => e.Id);
        _ = builder.Property(e => e.Id).ValueGeneratedOnAdd();
        _ = builder.Property(e => e.TenantId).IsRequired();
        _ = builder.Property(e => e.AccountCode).IsRequired().HasMaxLength(20);
        _ = builder.Property(e => e.AccountName).IsRequired().HasMaxLength(300);
        _ = builder.Property(e => e.Type).HasConversion<int?>();
        _ = builder.Property(e => e.IsNormalCredit);
        _ = builder.Property(e => e.ParentCode).HasMaxLength(20);
        _ = builder.Property(e => e.IsActive).IsRequired();
        _ = builder.Property(e => e.EffectiveFrom);
        _ = builder.Property(e => e.Source).IsRequired().HasMaxLength(10);
        _ = builder.Property(e => e.CreatedAt).IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP");
        _ = builder.Property(e => e.UpdatedAt).IsRequired();

        // 1 tenant chỉ có 1 row per AccountCode (override hoặc custom)
        _ = builder.HasIndex(e => new { e.TenantId, e.AccountCode }).IsUnique();
    }
}
