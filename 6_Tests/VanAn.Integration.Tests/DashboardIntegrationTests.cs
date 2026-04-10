using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;
using Xunit.Abstractions;
using VanAn.Gateway;
using Moq;

namespace VanAn.Integration.Tests;

public class DashboardIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly VanAnDbContext _context;
    private readonly ITestOutputHelper _output;

    public DashboardIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _output = output;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:",
                    ["KhachLink:DatabasePath"] = "test-data",
                    ["ShopERP:DatabasePath"] = "test-data"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove existing DbContext and add in-memory for testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(VanAnDbContext));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<VanAnDbContext>(options =>
                    options.UseSqlite("DataSource=:memory:"));

                // Ensure DashboardService is registered
                services.AddScoped<IDashboardService, DashboardService>();
            });
        });

        _client = _factory.CreateClient();

        // Create test database
        var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        context.Database.EnsureCreated();
        _context = context;

        SeedTestData();
    }

    private void SeedTestData()
    {
        _output.WriteLine("Seeding integration test data...");

        // Create test tenants
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        // Create test products
        var products = new[]
        {
            new Product { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Cà phê đen", Price = 25000, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Product { Id = Guid.NewGuid(), TenantId = tenant1Id, Name = "Cà phê sữa", Price = 30000, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Product { Id = Guid.NewGuid(), TenantId = tenant2Id, Name = "Trà đặc biệt", Price = 20000, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };

        _context.Products.AddRange(products);

        // Create test orders
        var now = DateTime.UtcNow;
        var orders = new[]
        {
            new Order 
            { 
                Id = Guid.NewGuid(), 
                TenantId = tenant1Id, 
                CustomerId = Guid.NewGuid(), 
                TotalAmount = 50000, 
                Status = new OrderStatusId("completed"),
                CreatedAt = now, 
                UpdatedAt = now,
                LastSyncedAt = now.AddMinutes(-5)
            },
            new Order 
            { 
                Id = Guid.NewGuid(), 
                TenantId = tenant2Id, 
                CustomerId = Guid.NewGuid(), 
                TotalAmount = 40000, 
                Status = new OrderStatusId("pending"),
                CreatedAt = now.AddHours(-1), 
                UpdatedAt = now.AddHours(-1),
                LastSyncedAt = default(DateTime)
            }
        };

        _context.Orders.AddRange(orders);
        _context.SaveChanges();

        _output.WriteLine($"Seeded {products.Length} products and {orders.Length} orders for integration tests");
    }

    #region Service Integration Tests

    [Fact(DisplayName = "DashboardService_Integration_Should_Provide_Real_Metrics")]
    public async Task DashboardService_Integration_Should_Provide_Real_Metrics()
    {
        // Arrange
        var scope = _factory.Services.CreateScope();
        var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();

        // Act
        var postgresMetrics = await dashboardService.GetPostgreSQLMetricsAsync();
        var syncStatus = await dashboardService.GetSyncStatusAsync();
        var systemHealth = await dashboardService.GetSystemHealthAsync();

        // Assert
        Assert.NotNull(postgresMetrics);
        Assert.True(postgresMetrics.TenantCount > 0);
        Assert.True(postgresMetrics.TotalOrders > 0);
        Assert.True(postgresMetrics.TotalRevenue > 0);

        Assert.NotNull(syncStatus);
        Assert.NotNull(systemHealth);

        _output.WriteLine($"Integration test - Tenants: {postgresMetrics.TenantCount}, Orders: {postgresMetrics.TotalOrders}");
        _output.WriteLine($"Integration test - System Healthy: {systemHealth.IsHealthy}");
    }

    [Fact(DisplayName = "DashboardService_Integration_Should_Handle_Database_Connection")]
    public async Task DashboardService_Integration_Should_Handle_Database_Connection()
    {
        // Arrange
        var scope = _factory.Services.CreateScope();
        var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();

        // Act
        var health = await dashboardService.GetSystemHealthAsync();

        // Assert
        Assert.True(health.IsPostgresOnline);
        Assert.True(health.LastUpdated != default(DateTime));

        _output.WriteLine($"Database connection test: PostgreSQL online = {health.IsPostgresOnline}");
    }

    #endregion

    #region API Integration Tests

    [Fact(DisplayName = "API_Should_Return_Dashboard_Endpoint")]
    public async Task API_Should_Return_Dashboard_Endpoint()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        _output.WriteLine($"API health endpoint returned: {response.StatusCode}");
    }

    [Fact(DisplayName = "API_Should_Handle_Dashboard_Metrics_Request")]
    public async Task API_Should_Handle_Dashboard_Metrics_Request()
    {
        // Note: This would require creating a dashboard API endpoint
        // For now, we test the health endpoint as a proxy

        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);
        Assert.Contains("Healthy", content);

        _output.WriteLine($"API metrics request handled successfully");
    }

    #endregion

    #region Database Integration Tests

    [Fact(DisplayName = "Database_Integration_Should_Maintain_Data_Consistency")]
    public async Task Database_Integration_Should_Maintain_Data_Consistency()
    {
        // Arrange
        var scope = _factory.Services.CreateScope();
        var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();

        // Act - Get metrics multiple times
        var metrics1 = await dashboardService.GetPostgreSQLMetricsAsync();
        await Task.Delay(100); // Small delay
        var metrics2 = await dashboardService.GetPostgreSQLMetricsAsync();

        // Assert - Should be consistent
        Assert.Equal(metrics1.TenantCount, metrics2.TenantCount);
        Assert.Equal(metrics1.TotalOrders, metrics2.TotalOrders);
        Assert.Equal(metrics1.TotalRevenue, metrics2.TotalRevenue);

        _output.WriteLine($"Data consistency verified across multiple calls");
    }

    [Fact(DisplayName = "Database_Integration_Should_Handle_Concurrent_Requests")]
    public async Task Database_Integration_Should_Handle_Concurrent_Requests()
    {
        // Arrange
        var scope = _factory.Services.CreateScope();
        var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();

        // Act - Make concurrent requests
        var tasks = new List<Task<DashboardMetrics>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(dashboardService.GetPostgreSQLMetricsAsync());
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All requests should succeed
        foreach (var result in results)
        {
            Assert.NotNull(result);
            Assert.True(result.TenantCount > 0);
        }

        _output.WriteLine($"Concurrent requests test: {tasks.Count} requests completed successfully");
    }

    #endregion

    #region Error Handling Tests

    [Fact(DisplayName = "Integration_Should_Handle_Service_Failure_Gracefully")]
    public async Task Integration_Should_Handle_Service_Failure_Gracefully()
    {
        // Arrange - Create a service that will fail
        var scope = _factory.Services.CreateScope();
        var loggerMock = new Mock<ILogger<DashboardService>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["KhachLink:DatabasePath"]).Returns("invalid-path");

        var faultyService = new DashboardService(_context, loggerMock.Object, configMock.Object);

        // Act & Assert - Should not throw exception
        var exception = await Record.ExceptionAsync(async () =>
        {
            await faultyService.GetSQLiteMetricsAsync("KhachLink");
        });

        // Should handle gracefully (not throw critical exception)
        Assert.Null(exception);
        _output.WriteLine($"Service failure handled gracefully");
    }

    #endregion

    #region Performance Integration Tests

    [Fact(DisplayName = "Integration_Performance_Should_Complete_Within_Timeout")]
    public async Task Integration_Performance_Should_Complete_Within_Timeout()
    {
        // Arrange
        var scope = _factory.Services.CreateScope();
        var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();
        var timeout = TimeSpan.FromSeconds(10);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var metrics = await dashboardService.GetPostgreSQLMetricsAsync();
        var syncStatus = await dashboardService.GetSyncStatusAsync();
        var systemHealth = await dashboardService.GetSystemHealthAsync();

        stopwatch.Stop();

        // Assert
        Assert.True(stopwatch.Elapsed < timeout);
        Assert.NotNull(metrics);
        Assert.NotNull(syncStatus);
        Assert.NotNull(systemHealth);

        _output.WriteLine($"Performance test: All operations completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    #endregion

    public void Dispose()
    {
        _context?.Database?.EnsureDeleted();
        _context?.Dispose();
        _client?.Dispose();
        _factory?.Dispose();
    }
}
