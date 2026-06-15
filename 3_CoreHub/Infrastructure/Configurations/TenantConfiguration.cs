using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Tenant entity
    /// NOTE: Tenant is a record (not inheriting from BaseEntity) with TenantId as primary key
    /// </summary>
    public class TenantConfiguration : IEntityTypeConfiguration<Tenant>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            // Primary key: TenantId value object
            _ = builder.HasKey(e => e.Id);

            // TenantId value object converter (special case: Tenant itself uses TenantId as PK)
            _ = builder.Property(e => e.Id)
                .HasConversion(id => id.Value, value => new TenantId(value))
                .IsRequired();

            // Name property
            _ = builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            // BusinessType enum conversion to int
            _ = builder.Property(e => e.BusinessType)
                .HasConversion<int>();

            // HKDGroup nullable enum conversion to int?
            _ = builder.Property(e => e.HKDGroup)
                .HasConversion<int?>();

            // CreatedAt with default value
            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // IsActive with default value
            _ = builder.Property(e => e.IsActive)
                .HasDefaultValue(true);

            // Index on Id (already has PK, but explicit for clarity)
            _ = builder.HasIndex(e => e.Id);
        }
    }
}
