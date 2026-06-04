using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// EF Core configuration for Customer entity
    /// </summary>
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            // Primary key
            _ = builder.HasKey(e => e.Id);

            // Property configurations
            _ = builder.Property(e => e.FullName)
                .IsRequired()
                .HasMaxLength(200);

            _ = builder.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasMaxLength(20);

            _ = builder.Property(e => e.Email)
                .HasMaxLength(100);

            _ = builder.Property(e => e.CustomerTier)
                .IsRequired()
                .HasMaxLength(20);

            _ = builder.Property(e => e.TotalSpent)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.DeviceId);

            // TenantId converter
            _ = builder.Property(e => e.TenantId)
                .IsRequired()
                .HasConversion(
                    id => id.Value,
                    value => new TenantId(value));

            // Soft delete query filter
            _ = builder.HasQueryFilter(e => !e.IsDeleted);

            // Navigation property: Orders - REMOVED
            // Relationship configuration moved to OrderConfiguration.cs to avoid duplicate EF Core configuration
            // OrderConfiguration defines the complete relationship with both navigation properties
            // (HasOne(o => o.Customer).WithMany(c => c.Orders))
            // This prevents SQLite schema generation errors ("no such table: Orders")

            // Indexes
            _ = builder.HasIndex(e => e.DeviceId);
            _ = builder.HasIndex(e => new { e.TenantId, e.DeviceId });
        }
    }
}
