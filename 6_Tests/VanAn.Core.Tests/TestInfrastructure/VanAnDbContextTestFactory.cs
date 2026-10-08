using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    /// <summary>
    /// FACTORY for VanAnDbContext - Direct instantiation, NO DI.
    /// Uses SQLite in-memory with TestTenantProvider.
    ///
    /// Pattern `1d211a3c` (EF Core model cache isolation): mỗi test có connection string
    /// UNIQUE (DB isolation) — giữ nguyên. NHƯNG internal ServiceProvider giờ là SHARED
    /// static + TenantAwareModelCacheKeyFactory → EF model build CHỈ 1 lần per (tenant),
    /// EnsureCreated ~2s/test (trước đây rebuild model ~15-22s/test → suite kẹt).
    /// </summary>
    public static class VanAnDbContextTestFactory
    {
        // Shared internal service provider — model cache dùng chung theo (type + tenant).
        // Filter tenant capture context instance nhưng cùng tenant → cùng giá trị → an toàn.
        private static readonly Lazy<ServiceProvider> SharedEfServiceProvider = new(() =>
            new ServiceCollection()
                .AddEntityFrameworkSqlite()
                .AddSingleton<IModelCacheKeyFactory, TenantAwareModelCacheKeyFactory>()
                .BuildServiceProvider());

        /// <summary>
        /// Creates a TestContextScope with VanAnDbContext via direct instantiation.
        /// NO DI, NO ServiceCollection, NO IServiceScope.
        /// </summary>
        public static TestContextScope Create()
        {
            SqliteConnection connection = new($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
            connection.Open();

            DbContextOptions<VanAnDbContext> options = new DbContextOptionsBuilder<VanAnDbContext>()
                .UseInternalServiceProvider(SharedEfServiceProvider.Value)
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
