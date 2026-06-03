using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Tests.TestInfrastructure;

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
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<VanAnDbContext>()
            .UseSqlite(connection)
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .LogTo(Console.WriteLine, LogLevel.Information)
            .Options;

        var tenantProvider = new TestTenantProvider();
        var context = new VanAnDbContext(options, tenantProvider);
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
