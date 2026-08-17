using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain.Aggregates.DomainResellerAggregate;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for TenantDomain entity — Domain Reseller R1.
    /// Platform-level entity — NOT tenant-scoped (TenantId = Guid.Empty sentinel).
    /// Excluded from multi-tenancy query filter (see VanAnDbContext.ApplyMultiTenancyFilters).
    /// Follows KhachLinkInstanceConfiguration pattern.
    /// </summary>
    public class TenantDomainConfiguration : IEntityTypeConfiguration<TenantDomain>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<TenantDomain> builder)
        {
            builder.ToTable("TenantDomains");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedOnAdd();

            // TenantId — inherited from BaseEntity, stored as Guid (platform sentinel = Guid.Empty).
            builder.Property("TenantId").IsRequired();

            builder.Property(e => e.Domain)
                .IsRequired()
                .HasMaxLength(255);

            // Unique index on Domain — one TenantDomain record per domain name
            builder.HasIndex(e => e.Domain).IsUnique();

            builder.Property(e => e.Registrar)
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(RegistrarProvider.GoDaddy);

            builder.Property(e => e.OwnerTenantId)
                .IsRequired();

            builder.Property(e => e.KhachLinkInstanceId)
                .IsRequired(false);

            builder.Property(e => e.RegisteredAt)
                .IsRequired();

            builder.Property(e => e.ExpiresAt)
                .IsRequired();

            builder.Property(e => e.AutoRenew)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(e => e.Status)
                .HasConversion<int>()
                .IsRequired()
                .HasDefaultValue(DomainStatus.Pending);

            builder.Property(e => e.RegistrantEmail)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(e => e.LastOperationId)
                .HasMaxLength(200);

            builder.Property(e => e.LastError)
                .HasMaxLength(1000);

            // Audit fields from BaseEntity
            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(e => e.IsDeleted)
                .HasDefaultValue(false);
        }
    }
}
