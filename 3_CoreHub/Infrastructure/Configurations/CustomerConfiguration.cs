using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for Customer entity
/// </summary>
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        // Primary key
        builder.HasKey(e => e.Id);
        
        // Property configurations
        builder.Property(e => e.FullName)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(e => e.PhoneNumber)
            .IsRequired()
            .HasMaxLength(20);
        
        builder.Property(e => e.Email)
            .HasMaxLength(100);
        
        builder.Property(e => e.CustomerTier)
            .IsRequired()
            .HasMaxLength(20);
        
        builder.Property(e => e.TotalSpent)
            .HasPrecision(18, 2);
        
        builder.Property(e => e.DeviceId);
        
        // TenantId converter
        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasConversion(
                id => id.Value,
                value => new TenantId(value));
        
        // Soft delete query filter
        builder.HasQueryFilter(e => !e.IsDeleted);
        
        // Indexes
        builder.HasIndex(e => e.DeviceId);
        builder.HasIndex(e => new { e.TenantId, e.DeviceId });
    }
}
