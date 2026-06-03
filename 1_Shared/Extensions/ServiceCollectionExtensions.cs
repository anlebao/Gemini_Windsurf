using Microsoft.Extensions.DependencyInjection;

namespace VanAn.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add SQLite DbContext with WAL Mode and optimized settings for Edge Nodes
        /// NOTE: This method should be moved to Infrastructure layer - EF Core references violate Domain purity
        /// </summary>
        public static IServiceCollection AddVanAnSqlite(this IServiceCollection services, string connectionString)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(connectionString);

            // TODO: Move EF Core DbContext configuration to Infrastructure layer
            // This is a placeholder for the actual implementation

            return services;
        }

        /// <summary>
        /// Initialize SQLite database with WAL mode and optimizations
        /// NOTE: This method should be moved to Infrastructure layer - EF Core references violate Domain purity
        /// </summary>
        public static async Task InitializeSqliteDatabaseAsync(this IServiceProvider serviceProvider)
        {
            // TODO: Move EF Core DbContext initialization to Infrastructure layer
            // This is a placeholder for the actual implementation

            await Task.CompletedTask;
        }
    }
}
