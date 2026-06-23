using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserTenant = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTenant;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for UserTenant entity — Cross-tenant mapping
    /// Wave 1 Phase 2: User-Tenant relationship
    /// </summary>
    public class UserTenantConfiguration : IEntityTypeConfiguration<UserTenant>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<UserTenant> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // UserId index for fast lookup
            _ = builder.HasIndex(e => e.UserId);

            // Composite index for tenant + user lookup
            _ = builder.HasIndex(e => new { e.TenantId, e.UserId }).IsUnique();

            // Active status index
            _ = builder.HasIndex(e => e.IsActive);

            _ = builder.Property(e => e.UserId)
                .IsRequired();

            _ = builder.Property(e => e.TenantId)
                .IsRequired();

            _ = builder.Property(e => e.Role)
                .IsRequired()
                .HasConversion<int>();

            _ = builder.Property(e => e.AssignedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            _ = builder.Property(e => e.IsActive)
                .HasDefaultValue(true);
        }
    }
}
