using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>
/// W3: EF Core configuration for AccountCharts reference-data table.
/// Auto-discovered via <c>ApplyConfigurationsFromAssembly</c> in VanAnDbContext.OnModelCreating.
/// </summary>
public class AccountChartConfiguration : IEntityTypeConfiguration<AccountChartEntity>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<AccountChartEntity> builder)
    {
        builder.ToTable("AccountCharts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.AccountCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.AccountName)
            .IsRequired()
            .HasMaxLength(300);

        // Enums → int (matches TenantConfiguration pattern)
        builder.Property(e => e.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.Standard)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.IsNormalCredit)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Unique constraint: one account code per standard (prevents duplicate seeds)
        builder.HasIndex(e => new { e.Standard, e.AccountCode })
            .IsUnique();

        // Lookup index by standard (W4 services query all accounts of a standard)
        builder.HasIndex(e => e.Standard);
    }
}
