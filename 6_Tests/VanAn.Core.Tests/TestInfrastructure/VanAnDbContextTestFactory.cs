using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            SqliteConnection connection = new($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
            connection.Open();

            var efServiceProvider = new ServiceCollection()
                .AddEntityFrameworkSqlite()
                .BuildServiceProvider();

            DbContextOptions<VanAnDbContext> options = new DbContextOptionsBuilder<VanAnDbContext>()
                .UseInternalServiceProvider(efServiceProvider)
                .UseSqlite(connection)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .LogTo(Console.WriteLine, LogLevel.Information)
                .Options;

            TestTenantProvider tenantProvider = new();
            VanAnDbContext context = new(options, tenantProvider);
            _ = context.Database.EnsureCreated();

            return new TestContextScope(context, connection, tenantProvider);
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
