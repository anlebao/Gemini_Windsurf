using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Tests.TestInfrastructure;

public class TestDbFactory
{
    public static VanAnDbContext CreateSqliteInMemory()
    {
        var services = new ServiceCollection();

        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        services.AddScoped<ITenantProvider, TestTenantProvider>();

        var provider = services.BuildServiceProvider();

        var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();

        context.Database.OpenConnection();
        context.Database.EnsureCreated();

        // Disable foreign keys for test isolation
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");

        return context;
    }
}
