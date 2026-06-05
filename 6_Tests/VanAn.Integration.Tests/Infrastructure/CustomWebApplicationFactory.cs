using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Common;
using VanAn.KhachLink;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for integration testing
/// Configures in-memory database and test-specific services
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext configuration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<VanAnDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<VanAnDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());

            // Register test tenant provider
            services.AddScoped<ITenantProvider, TestTenantProvider>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure database is created
        using (var scope = host.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
            dbContext.Database.EnsureCreated();
        }

        return host;
    }
}
