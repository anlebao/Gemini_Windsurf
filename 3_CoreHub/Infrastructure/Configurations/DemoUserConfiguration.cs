using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for DemoUser entity
    /// </summary>
    public class DemoUserConfiguration : IEntityTypeConfiguration<DemoUser>, IEntityConfiguration
    {
        public void Configure(EntityTypeBuilder<DemoUser> builder)
        {
            _ = builder.HasKey(e => e.Id);

            // TenantId value object converter (from BaseEntity)
            _ = builder.Property(e => e.TenantId)
                .HasConversion(id => id.Value, value => new TenantId(value))
                .IsRequired();

            _ = builder.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(100);

            _ = builder.Property(e => e.PasswordHash)
                .IsRequired()
                .HasMaxLength(500);

            _ = builder.Property(e => e.DisplayName)
                .IsRequired()
                .HasMaxLength(200);

            // Enum conversion for UserRole
            _ = builder.Property(e => e.Role)
                .HasConversion<int>();

            _ = builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indexes
            _ = builder.HasIndex(e => new { e.TenantId, e.Username }).IsUnique();
        }
    }
}
