using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.CoreHub.Infrastructure.DataProtection;
using VanAn.CoreHub.Infrastructure.ValueConverters;
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

            // Wave 2: PII encryption for PhoneNumber and Email
            _ = builder.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasMaxLength(500)
                .HasConversion(new EncryptedStringConverter(
                    DataProtectionProviderAccessor.CreateProtector("Customer.PhoneNumber")));

            _ = builder.Property(e => e.Email)
                .HasMaxLength(500)
                .HasConversion(new EncryptedStringConverter(
                    DataProtectionProviderAccessor.CreateProtector("Customer.Email")));

            _ = builder.Property(e => e.CustomerTier)
                .IsRequired()
                .HasMaxLength(20);

            _ = builder.Property(e => e.IdentityLevel)
                .IsRequired()
                .HasConversion<int>()
                .HasDefaultValue(IdentityLevel.Social);

            _ = builder.Property(e => e.TotalSpent)
                .HasPrecision(18, 2);

            _ = builder.Property(e => e.DeviceId);

            // CustomerId converter
            _ = builder.Property(e => e.CustomerId)
                .IsRequired()
                .HasConversion(
                    id => id.Value,
                    value => new CustomerId(value));

            // TenantId converter
            _ = builder.Property(e => e.TenantId)
                .IsRequired();

            // Soft delete query filter
            _ = builder.HasQueryFilter(e => !e.IsDeleted);

            // Navigation property: Orders - REMOVED
            // Relationship configuration moved to OrderConfiguration.cs to avoid duplicate EF Core configuration
            // OrderConfiguration defines the complete relationship with both navigation properties
            // (HasOne(o => o.Customer).WithMany(c => c.Orders))

            // Navigation property: LoyaltyRewards - REMOVED
            // Relationship configured in LoyaltyRewardsConfiguration.cs (one-to-one)
            // This prevents SQLite schema generation errors

            // Indexes
            _ = builder.HasIndex(e => e.DeviceId);
            _ = builder.HasIndex(e => new { e.TenantId, e.DeviceId });
        }
    }
}
