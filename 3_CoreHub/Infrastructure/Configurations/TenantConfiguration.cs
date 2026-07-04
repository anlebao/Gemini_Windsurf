using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for the Rich Domain Tenant aggregate (Wave 5).
    /// Maps VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant to the "Tenants" table.
    /// The old anemic record VanAn.Shared.Domain.Tenant is [Obsolete] and no longer mapped.
    /// </summary>
    public class TenantConfiguration : IEntityTypeConfiguration<Tenant>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.ToTable("Tenants");

            // Primary key: TenantId value object (stored as TEXT/UUID)
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                .HasColumnName("Id")
                .HasConversion(id => id.Value, value => new TenantId(value))
                .IsRequired();

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            // BusinessType enum → int
            builder.Property(e => e.BusinessType)
                .HasConversion<int>();

            // HKDGroup nullable enum → int?
            builder.Property(e => e.HKDGroup)
                .HasConversion<int?>();

            // Wave 5 (approved 2026-07-03): Default industry sector for HKD Group 2 tenants.
            // Nullable — existing tenants get NULL, must be set before generating S2a/S2b.
            builder.Property(e => e.DefaultIndustrySector)
                .HasConversion<int?>();

            // Wave 5: TenantStatus enum → int (default Active=1)
            builder.Property(e => e.Status)
                .HasConversion<int>()
                .HasDefaultValue(TenantStatus.Active)
                .IsRequired();

            // Wave 5: TenantSettings owned value object (flattened into Tenants table)
            builder.OwnsOne(e => e.Settings, settings =>
            {
                settings.Property(s => s.ContactEmail).HasColumnName("Settings_ContactEmail").HasMaxLength(200);
                settings.Property(s => s.ContactPhone).HasColumnName("Settings_ContactPhone").HasMaxLength(50);
                settings.Property(s => s.Address).HasColumnName("Settings_Address").HasMaxLength(500);
                settings.Property(s => s.LogoUrl).HasColumnName("Settings_LogoUrl").HasMaxLength(500);
                settings.Property(s => s.TaxCode).HasColumnName("Settings_TaxCode").HasMaxLength(20);
            });

            // Audit fields from BaseEntity
            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted)
                .HasDefaultValue(false);

            // Ignore domain events (not persisted)
            builder.Ignore(e => e.DomainEvents);

            // C2 fix 2026-07-04: Explicitly ignore W2 conversion fields until W8 adds migration + full mapping.
            // Without this, EF Core convention would attempt to map these nullable value objects/enums
            // and fail at runtime (TenantId? is a record, not a primitive). W8 will:
            //   1. Remove these Ignore() calls
            //   2. Add HasConversion + HasColumnName for each field
            //   3. Create migration adding columns to Tenants table
            // Until then, conversion is Domain-only (no persistence) — safe because no conversion
            // service exists yet (W8 scope).
            builder.Ignore(e => e.PredecessorTenantId);
            builder.Ignore(e => e.SuccessorTenantId);
            builder.Ignore(e => e.ConvertedAt);
            builder.Ignore(e => e.AccountingStandard);
            builder.Ignore(e => e.Type);

            builder.HasIndex(e => e.Id);
        }
    }
}
