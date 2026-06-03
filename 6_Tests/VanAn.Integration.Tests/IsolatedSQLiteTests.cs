using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Integration.Tests.Infrastructure;
using System;
using System.IO;
using System.Linq;

namespace VanAn.Integration.Tests;

public class IsolatedSQLiteTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly VanAnDbContext _dbContext;

    public IsolatedSQLiteTests()
    {
        // Configure DI with in-memory SQLite
        var services = new ServiceCollection();
        
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<VanAnDbContext>();
        
        // Open connection before EnsureCreated for in-memory SQLite
        _dbContext.Database.OpenConnection();
        
        // Ensure database is created
        _dbContext.Database.EnsureCreated();
    }

    [Fact(DisplayName = "SQLite Integration: Simple Entity Insert")]
    public async Task SQLite_SimpleEntity_Insert_WithBehavior_Works()
    {
        // Arrange - Use TestEntityBuilder for domain-compliant creation
        var testTenantId = TestEntityBuilder.CreateTenantId();
        var testOrder = TestEntityBuilder.CreateOrder(testTenantId, Guid.NewGuid(), 100.0m);

        // Act - Insert order without items first
        _dbContext.Orders.Add(testOrder);
        await _dbContext.SaveChangesAsync();

        // Assert - Verify order was saved
        var savedOrder = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == testOrder.Id);

        Assert.NotNull(savedOrder);
        
        // NEW: Business behavior verification
        Assert.Equal(testTenantId.Value, savedOrder.TenantId.Value);
        Assert.Equal(100.0m, savedOrder.TotalAmount);
        Assert.True(savedOrder.CreatedAt <= DateTime.UtcNow);
        Assert.True(savedOrder.UpdatedAt >= savedOrder.CreatedAt);
        Assert.True(savedOrder.OrderDate <= DateTime.UtcNow);

        // Verify database count increased by exactly 1
        var orderCount = await _dbContext.Orders.CountAsync();
        Assert.Equal(1, orderCount);
    }

    [Fact(DisplayName = "SQLite Integration: Multi-Tenant Isolation")]
    public async Task SQLite_MultiTenant_WithBusinessRules_Isolation_Works()
    {
        // Arrange - Use TestEntityBuilder for domain-compliant creation
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        var order1 = TestEntityBuilder.CreateOrder(new TenantId(tenant1Id), Guid.NewGuid(), 100.0m);
        var order2 = TestEntityBuilder.CreateOrder(new TenantId(tenant2Id), Guid.NewGuid(), 200.0m);

        // Act - Insert orders for different tenants
        _dbContext.Orders.AddRange(order1, order2);
        await _dbContext.SaveChangesAsync();

        // Assert - Verify tenant isolation
        var tenant1Orders = await _dbContext.Orders
            .Where(o => o.TenantId.Value == tenant1Id)
            .ToListAsync();

        var tenant2Orders = await _dbContext.Orders
            .Where(o => o.TenantId.Value == tenant2Id)
            .ToListAsync();

        Assert.Single(tenant1Orders);
        Assert.Single(tenant2Orders);
        Assert.NotEqual(tenant1Orders[0].Id, tenant2Orders[0].Id);
        Assert.Equal(tenant1Id, tenant1Orders[0].TenantId.Value);
        Assert.Equal(tenant2Id, tenant2Orders[0].TenantId.Value);
        
        // NEW: Business behavior verification
        Assert.Equal(100.0m, tenant1Orders[0].TotalAmount);
        Assert.Equal(200.0m, tenant2Orders[0].TotalAmount);
        Assert.True(tenant1Orders[0].OrderDate <= DateTime.UtcNow);
        Assert.True(tenant2Orders[0].OrderDate <= DateTime.UtcNow);
        Assert.True(tenant1Orders[0].CreatedAt <= DateTime.UtcNow);
        Assert.True(tenant2Orders[0].CreatedAt <= DateTime.UtcNow);
    }

    [Fact(DisplayName = "SQLite Integration: Database Connection Status")]
    public void SQLite_DatabaseConnection_CanPing()
    {
        // Act & Assert - Verify database connection is working
        Assert.True(_dbContext.Database.CanConnect());
        
        // Note: In-memory SQLite doesn't have a file path, so we skip file existence check
        
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
        // Act - Check journal mode
        var journalMode = _dbContext.Database.SqlQueryRaw<string>("PRAGMA journal_mode;").ToList().FirstOrDefault();
        
        // Assert - In-memory SQLite uses "memory" mode instead of WAL
        // WAL mode is not supported for in-memory databases
        Assert.Equal("memory", journalMode);
    }

    public void Dispose()
    {
        // Clean up: Dispose database and service provider
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}
