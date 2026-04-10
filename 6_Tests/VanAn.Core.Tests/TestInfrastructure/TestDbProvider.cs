using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    public interface ITestDbProvider
    {
        VanAnDbContext Create();
        void Dispose(VanAnDbContext context);
    }

    public class SqliteTestDbProvider : ITestDbProvider
    {
        public VanAnDbContext Create()
        {
            return TestDbFactory.CreateSqliteInMemory();
        }

        public void Dispose(VanAnDbContext context)
        {
            context?.Database?.EnsureDeleted();
            context?.Dispose();
        }
    }

    public class PostgreSqlTestDbProvider : ITestDbProvider
    {
        private readonly string _connectionString;

        public PostgreSqlTestDbProvider(string connectionString)
        {
            _connectionString = connectionString;
        }

        public VanAnDbContext Create()
        {
            var services = new ServiceCollection();

            services.AddDbContext<VanAnDbContext>(options =>
                options.UseNpgsql(_connectionString));

            services.AddScoped<ITenantProvider, TestTenantProvider>();

            var provider = services.BuildServiceProvider();

            var scope = provider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
            context.Database.EnsureCreated();
            return context;
        }

        public void Dispose(VanAnDbContext context)
        {
            context?.Dispose();
        }
    }

    public static class TestDbProviderFactory
    {
        public static ITestDbProvider CreateSqlite() => new SqliteTestDbProvider();
        
        public static ITestDbProvider CreatePostgres(string connectionString) 
            => new PostgreSqlTestDbProvider(connectionString);
    }
}
