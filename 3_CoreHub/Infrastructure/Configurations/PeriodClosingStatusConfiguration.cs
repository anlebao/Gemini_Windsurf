using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.Entities;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>
/// W5: EF Core configuration for the PeriodClosingStatuses table.
/// Auto-discovered via <c>ApplyConfigurationsFromAssembly</c> in VanAnDbContext.OnModelCreating.
///
/// <see cref="PeriodClosingStatusEntity"/> inherits <see cref="VanAn.Shared.Domain.Common.BaseEntity"/>
/// so <c>TenantId</c> is mapped automatically via the global <c>TenantIdConverter</c>
/// (configured in <see cref="VanAnDbContext.ConfigureConventions"/>).
/// </summary>
public class PeriodClosingStatusConfiguration : IEntityTypeConfiguration<PeriodClosingStatusEntity>, IEntityConfiguration
{
    public void Configure(EntityTypeBuilder<PeriodClosingStatusEntity> builder)
    {
        builder.ToTable("PeriodClosingStatuses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        // TenantId: value object → string via global TenantIdConverter (no explicit HasConversion needed here).
        // Multi-tenancy query filter applied automatically by VanAnDbContext.ApplyMultiTenancyFilters
        // (entity implements IMustHaveTenant via BaseEntity).

        builder.Property(e => e.PeriodYear)
            .IsRequired();

        builder.Property(e => e.PeriodMonth)
            .IsRequired();

        // PeriodClosingStatus enum → int (matches AccountChartConfiguration / TenantConfiguration pattern)
        builder.Property(e => e.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.ClosedAt)
            .IsRequired(false);

        builder.Property(e => e.ClosedBy)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(e => e.ReopenReason)
            .HasMaxLength(1000)
            .IsRequired(false);

        // Audit fields from BaseEntity
        builder.Property(e => e.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(e => e.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false);

        // Unique constraint: one status record per tenant per period (year+month)
        builder.HasIndex(e => new { e.TenantId, e.PeriodYear, e.PeriodMonth })
            .IsUnique();

        // Lookup index by tenant (service queries by tenant + period)
        builder.HasIndex(e => e.TenantId);
    }
}
