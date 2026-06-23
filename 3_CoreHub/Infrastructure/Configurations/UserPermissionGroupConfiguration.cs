using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserPermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.UserPermissionGroup;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for UserPermissionGroup mapping (Wave 6).
    /// </summary>
    public class UserPermissionGroupConfiguration : IEntityTypeConfiguration<UserPermissionGroup>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<UserPermissionGroup> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasIndex(e => new { e.UserId, e.GroupId, e.TenantId }).IsUnique();
            builder.HasIndex(e => e.UserId);
            builder.HasIndex(e => e.GroupId);

            builder.Property(e => e.AssignedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(e => e.IsActive)
                .HasDefaultValue(true);
        }
    }
}
