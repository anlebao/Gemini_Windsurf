using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using VanAn.KhachLink;

namespace VanAn.Integration.Tests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for integration testing
/// Configures test-specific services for HTTP integration tests
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // KhachLink uses Gateway API, not direct DB access
        // No DbContext configuration needed for HTTP-only tests
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        return base.CreateHost(builder);
    }
}
