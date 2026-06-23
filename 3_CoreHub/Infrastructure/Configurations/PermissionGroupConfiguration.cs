using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for PermissionGroup aggregate (Wave 6).
    /// </summary>
    public class PermissionGroupConfiguration : IEntityTypeConfiguration<PermissionGroup>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<PermissionGroup> builder)
        {
            builder.ToTable("PermissionGroups");

            builder.HasKey(e => e.Id);

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.Description)
                .HasMaxLength(500);

            // Store bundled roles as a comma-separated string for simplicity
            builder.Property(e => e.SerializedRoles)
                .HasMaxLength(200);

            builder.Ignore(e => e.DomainEvents);

            builder.HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
        }
    }
}
