using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    public class SchemaSyncEngine(ILogger<SchemaSyncEngine> logger = null!)
    {
        private readonly ILogger<SchemaSyncEngine> _logger = logger ?? NullLogger<SchemaSyncEngine>.Instance;

        public async Task<bool> EnsureSchemaAsync(VanAnDbContext context)
        {
            try
            {
                // For tests, use EnsureCreatedAsync instead of MigrateAsync
                if (context.Database.IsSqlite())
                {
                    _logger?.LogInformation("Creating schema using EnsureCreatedAsync for SQLite");
                    await context.Database.EnsureCreatedAsync();
                    _logger?.LogInformation("Schema creation completed successfully");
                }
                else
                {
                    // For PostgreSQL, use migrations
                    IEnumerable<string> pendingMigrations = await context.Database.GetPendingMigrationsAsync();

                    if (pendingMigrations.Any())
                    {
                        _logger?.LogInformation("Applying {Count} pending migrations", pendingMigrations.Count());
                        await context.Database.MigrateAsync();
                        _logger?.LogInformation("Schema sync completed successfully");
                    }
                    else
                    {
                        _logger?.LogInformation("Schema is already up to date");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Schema sync failed");
                return false;
            }
        }

        public async Task<bool> ResetAndRecreateAsync(VanAnDbContext context)
        {
            try
            {
                _logger?.LogInformation("Resetting database schema");

                // Delete and recreate database
                await context.Database.EnsureDeletedAsync();
                await context.Database.EnsureCreatedAsync();

                // Apply migrations
                await EnsureSchemaAsync(context);

                // CRITICAL: Disable foreign keys for test isolation
                if (context.Database.IsSqlite())
                {
                    context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
                }

                _logger?.LogInformation("Database reset and schema sync completed");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Database reset failed");
                return false;
            }
        }

        public async Task<bool> ValidateSchemaAsync(VanAnDbContext context)
        {
            try
            {
                // Test basic database connectivity
                bool canConnect = await context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    _logger?.LogWarning("Cannot connect to database");
                    return false;
                }

                // Test basic query on key tables
                int shopCount = await context.Shops.CountAsync();
                int orderCount = await context.Orders.CountAsync();

                _logger?.LogInformation("Schema validation successful - Shops: {ShopCount}, Orders: {OrderCount}",
                    shopCount, orderCount);

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Schema validation failed");
                return false;
            }
        }
    }

    public static class TestHarnessExtensions
    {
        public static async Task<bool> SetupTestDatabaseAsync(this VanAnDbContext context, ILogger logger = null!)
        {
            SchemaSyncEngine syncEngine = new(logger as ILogger<SchemaSyncEngine> ?? new NullLogger<SchemaSyncEngine>());

            // Reset and recreate schema
            bool resetSuccess = await syncEngine.ResetAndRecreateAsync(context);
            if (!resetSuccess)
            {
                return false;
            }

            // Validate schema
            bool validationSuccess = await syncEngine.ValidateSchemaAsync(context);
            return validationSuccess;
        }

        public static async Task<bool> SeedTestDataAsync(this VanAnDbContext context, TestDataBuilder builder = null!)
        {
            try
            {
                builder ??= TestDataBuilder.CreateBasicScenario();
                await builder.BuildAsync(context);
                return true;
            }
            catch (Exception ex)
            {
                // Log error if logger available
                Console.WriteLine($"Test data seeding failed: {ex.Message}");
                return false;
            }
        }
    }
}
