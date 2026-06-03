using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    /// <summary>
    /// FACTORY for VanAnDbContext - Direct instantiation, NO DI.
    /// Uses SQLite in-memory with TestTenantProvider.
    /// </summary>
    public static class VanAnDbContextTestFactory
    {
        /// <summary>
        /// Creates a TestContextScope with VanAnDbContext via direct instantiation.
        /// NO DI, NO ServiceCollection, NO IServiceScope.
        /// </summary>
        public static TestContextScope Create()
        {
            SqliteConnection connection = new("DataSource=:memory:");
            connection.Open();

            DbContextOptions<VanAnDbContext> options = new DbContextOptionsBuilder<VanAnDbContext>()
                .UseSqlite(connection)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .LogTo(Console.WriteLine, LogLevel.Information)
                .Options;

            TestTenantProvider tenantProvider = new();
            VanAnDbContext context = new(options, tenantProvider);
            context.Database.EnsureCreated();

            return new TestContextScope(context, connection);
        }

        /// <summary>
        /// Creates a TestContextScope with custom database name (API compatibility).
        /// </summary>
        public static TestContextScope CreateInMemory(string? databaseName = null)
        {
            return Create();
        }
    }
}
