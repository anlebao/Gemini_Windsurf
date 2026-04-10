using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Infrastructure;

namespace VanAn.Integration.Tests;

public class IsolatedSQLiteTests : IDisposable
{
    private readonly string _uniqueDbPath;
    private readonly ServiceProvider _serviceProvider;
    private readonly VanAnDbContext _dbContext;

    public IsolatedSQLiteTests()
    {
        // Generate unique database name for each test run to prevent locking
        _uniqueDbPath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid():N}.db");
        
        // Configure DI with isolated SQLite
        var services = new ServiceCollection();
        
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<VanAnDbContext>();
        
        // Ensure database is created
        _dbContext.Database.EnsureCreated();
    }

    [Fact(DisplayName = "SQLite Integration: Simple Entity Insert")]
    public async Task SQLite_SimpleEntity_Insert_Works()
    {
        // Arrange - Create a simple order without foreign key constraints
        var testTenantId = Guid.NewGuid();
        var testOrder = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = testTenantId,
            CustomerDeviceId = "test-device-123",
            OrderType = "DINEIN",
            CustomerNotes = "Test order for SQLite integration",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Insert order without items first
        _dbContext.Orders.Add(testOrder);
        await _dbContext.SaveChangesAsync();

        // Assert - Verify order was saved
        var savedOrder = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == testOrder.Id);

        Assert.NotNull(savedOrder);
        Assert.Equal(testOrder.CustomerDeviceId, savedOrder.CustomerDeviceId);
        Assert.Equal(testOrder.OrderType, savedOrder.OrderType);
        Assert.Equal(testOrder.CustomerNotes, savedOrder.CustomerNotes);

        // Verify database count increased by exactly 1
        var orderCount = await _dbContext.Orders.CountAsync();
        Assert.Equal(1, orderCount);
    }

    [Fact(DisplayName = "SQLite Integration: Multi-Tenant Isolation")]
    public async Task SQLite_MultiTenant_Isolation_Works()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        var order1 = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenant1Id,
            CustomerDeviceId = "tenant1-device",
            OrderType = "DINEIN",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var order2 = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenant2Id,
            CustomerDeviceId = "tenant2-device",
            OrderType = "DELIVERY",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act - Insert orders for different tenants
        _dbContext.Orders.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();

        // Assert - Verify tenant isolation
        var tenant1Orders = await _dbContext.Orders
            .Where(o => o.TenantId == tenant1Id)
            .ToListAsync();

        var tenant2Orders = await _dbContext.Orders
            .Where(o => o.TenantId == tenant2Id)
            .ToListAsync();

        Assert.Single(tenant1Orders);
        Assert.Single(tenant2Orders);
        Assert.NotEqual(tenant1Orders[0].Id, tenant2Orders[0].Id);
        Assert.Equal(tenant1Id, tenant1Orders[0].TenantId);
        Assert.Equal(tenant2Id, tenant2Orders[0].TenantId);
    }

    [Fact(DisplayName = "SQLite Integration: Database Connection Status")]
    public void SQLite_DatabaseConnection_CanPing()
    {
        // Act & Assert - Verify database connection is working
        Assert.True(_dbContext.Database.CanConnect());
        
        // Verify database file exists
        Assert.True(File.Exists(_uniqueDbPath));
        
        // Verify we can create a simple table
        var connection = _dbContext.Database.GetDbConnection();
        connection.Open();
        
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";
        var result = command.ExecuteScalar();
        
        connection.Close();
        
        Assert.Equal(1L, result);
    }

    [Fact(DisplayName = "SQLite Integration: WAL Mode Verification")]
    public void SQLite_WALMode_IsEnabled()
    {
        // Act - Check if WAL mode is enabled
        var connection = _dbContext.Database.GetDbConnection();
        connection.Open();
        
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        
        var journalMode = command.ExecuteScalar()?.ToString();
        
        // Assert - WAL mode should be enabled
        Assert.Equal("wal", journalMode?.ToLower());
        
        connection.Close();
    }

    public void Dispose()
    {
        // Clean up: Delete the test database file
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
        
        if (File.Exists(_uniqueDbPath))
        {
            try
            {
                File.Delete(_uniqueDbPath);
            }
            catch
            {
                // Ignore cleanup errors in case of file locks
            }
        }
    }
}
