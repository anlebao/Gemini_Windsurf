using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): EF Core configuration for BusinessProfile entity.
    /// Tenant-scoped (1 row per tenant via unique index on TenantId).
    /// Single-Identity Pattern: BusinessProfileId VO ignored, Id = PK only.
    /// TenantId mapped via global TenantIdConverter (no explicit HasConversion needed).
    /// Auto-discovered via ApplyConfigurationsFromAssembly in VanAnDbContext.OnModelCreating.
    /// </summary>
    public class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<BusinessProfile> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // SINGLE-IDENTITY: BusinessProfileId synced to Id in constructor (Id = BusinessProfileId.Value).
            // Ignore — no separate DB column. Code reads entity.Id, not entity.BusinessProfileId.Value.
            _ = builder.Ignore(e => e.BusinessProfileId);

            // Computed property — not stored in DB.
            _ = builder.Ignore(e => e.TotalMonthlyFixedCost);

            // Fixed costs (monthly, VND) — decimal(18,2) per NFR-13 currency rule.
            _ = builder.Property(e => e.MonthlyRent).HasPrecision(18, 2);
            _ = builder.Property(e => e.MonthlyPayroll).HasPrecision(18, 2);
            _ = builder.Property(e => e.MonthlyUtilities).HasPrecision(18, 2);
            _ = builder.Property(e => e.MonthlyMarketing).HasPrecision(18, 2);
            _ = builder.Property(e => e.MonthlyLogistics).HasPrecision(18, 2);
            _ = builder.Property(e => e.MonthlyOtherOpEx).HasPrecision(18, 2);
            _ = builder.Property(e => e.MonthlyDepreciation).HasPrecision(18, 2);

            // Capacity
            _ = builder.Property(e => e.DailyCapacityUnits);
            _ = builder.Property(e => e.OperatingDaysPerMonth);

            // PricingModel enum → string (readable in DB)
            _ = builder.Property(e => e.PricingModel)
                .HasConversion<string>()
                .HasMaxLength(20);

            // FinancialModelVersion struct → string "Major.Minor" (BR-006 traceability)
            _ = builder.Property(e => e.Version)
                .HasConversion(
                    v => v.ToString(),
                    v => FinancialModelVersion.Parse(v))
                .HasMaxLength(10);

            // Notes
            _ = builder.Property(e => e.Notes).HasMaxLength(2000);

            // Audit fields from BaseEntity (CreatedAt/UpdatedAt/UpdatedBy)
            _ = builder.Property(e => e.CreatedAt);
            _ = builder.Property(e => e.UpdatedAt);
            _ = builder.Property(e => e.UpdatedBy);

            // Unique index — 1 BusinessProfile per tenant
            _ = builder.HasIndex(e => e.TenantId).IsUnique();
        }
    }
}
