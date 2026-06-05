using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Configurations
{
    /// <summary>
    /// Configuration for TenantId value object in EF Core
    /// </summary>
    public static class TenantIdConfiguration
    {
        /// <summary>
        /// Configures TenantId as owned type with converter
        /// </summary>
        public static void Configure<T>(OwnedNavigationBuilder<T, TenantId> builder)
            where T : class
        {
            _ = builder.Property(t => t.Value)
                .HasColumnName("TenantId")
                .HasConversion<ValueConverters.TenantIdConverter>();
        }
    }
}
