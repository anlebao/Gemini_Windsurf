using Microsoft.EntityFrameworkCore;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    /// <summary>
    /// Test-specific EF Core configuration that bypasses complex production setups
    /// Follows windsurf-guard.js rules: minimal test configurations
    /// </summary>
    public static class TestDbContextConfiguration
    {
        public static void ConfigureTestVanAnDbContext(ModelBuilder modelBuilder)
        {
            // Only configure Customer entity for CustomerService tests
            ConfigureCustomer(modelBuilder);

            // Skip complex production configurations
            // No ApplyConfigurationsFromAssembly() - avoids conflicts
            // No global query filters - simplifies test isolation
        }

        private static void ConfigureCustomer(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);

                // Value object conversion
                entity.Property(e => e.CustomerId)
                    .HasConversion<Guid>()
                    .IsRequired();

                entity.Property(e => e.DeviceId)
                    .IsRequired();

                entity.Property(e => e.FullName)
                    .HasMaxLength(200);

                entity.Property(e => e.PhoneNumber)
                    .HasMaxLength(20);

                entity.Property(e => e.CustomerTier)
                    .HasMaxLength(50)
                    .HasDefaultValue("Bronze");

                entity.Property(e => e.TenantId)
                    .IsRequired();

                // No complex multi-tenancy filters for tests
                // No audit configurations for tests
            });
        }
    }
}
