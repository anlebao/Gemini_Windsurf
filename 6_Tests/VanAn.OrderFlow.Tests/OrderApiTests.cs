using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using Xunit;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Tests.TestInfrastructure;

namespace VanAn.OrderFlow.Tests;

/// <summary>
/// Order API Tests - TDD Phase 1 (RED)
/// Tests must FAIL before implementation
/// </summary>
public class OrderApiTests : IDisposable
{
    private readonly VanAnDbContext _dbContext;
    private readonly string _testDatabaseName = "VanAn_OrderFlow_Test";

    public OrderApiTests()
    {
        // Create test database context with DI
        var services = new ServiceCollection();

        services.AddDbContext<VanAnDbContext>(options =>
            options.UseNpgsql($"Host=localhost;Port=5432;Database={_testDatabaseName};Username=vanan_admin;Password=VanAn@2024!"));

        services.AddScoped<ITenantProvider, TestTenantProvider>();

        var provider = services.BuildServiceProvider();

        var scope = provider.CreateScope();

        _dbContext = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();
        
        // Ensure clean database
        _dbContext.Database.EnsureDeleted();
        _dbContext.Database.EnsureCreated();
    }

    [Fact(DisplayName = "Order Entity - Should Persist To PostgreSQL")]
    public async Task OrderEntity_ShouldPersistToPostgreSQL()
    {
        // Arrange
        var orderId = new OrderId(Guid.NewGuid());
        var order = new Order
        {
            OrderId = orderId,
            CustomerDeviceId = "test-device-123",
            OrderType = "DINEIN",
            Status = new OrderStatusId("PENDING"),
            CustomerNotes = "Test order creation",
            TenantId = Guid.NewGuid()
        };

        // Add order items
        var orderItem = new OrderItem
        {
            OrderItemId = new OrderItemId(Guid.NewGuid()),
            OrderId = order.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            UnitPrice = 25000m,
            VatRate = 0.10m,
            Notes = "Extra ice",
            TenantId = order.TenantId
        };
        order.Items.Add(orderItem);

        // Act
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Assert - Should FAIL initially (RED) if Order entity doesn't exist
        var savedOrder = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId.Value == orderId.Value);
        
        Assert.NotNull(savedOrder);
        Assert.Equal("PENDING", savedOrder.Status.Value);
        Assert.Equal("test-device-123", savedOrder.CustomerDeviceId);
        Assert.Equal("DINEIN", savedOrder.OrderType);
        Assert.True(savedOrder.Items.Count > 0, "Order should have items");
        Assert.Equal(2, savedOrder.Items.First().Quantity);
        Assert.Equal(25000m, savedOrder.Items.First().UnitPrice);
    }

    [Fact(DisplayName = "Order Entity - Should Calculate Totals Correctly")]
    public async Task OrderEntity_ShouldCalculateTotalsCorrectly()
    {
        // Arrange
        var orderId = new OrderId(Guid.NewGuid());
        var order = new Order
        {
            OrderId = orderId,
            CustomerDeviceId = "test-device-456",
            OrderType = "TAKEAWAY",
            Status = new OrderStatusId("PENDING"),
            CustomerNotes = "Test order calculation",
            TenantId = Guid.NewGuid()
        };

        // Add multiple order items
        var orderItem1 = new OrderItem
        {
            OrderItemId = new OrderItemId(Guid.NewGuid()),
            OrderId = order.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 2,
            UnitPrice = 25000m,
            VatRate = 0.10m,
            Notes = "Item 1",
            TenantId = order.TenantId
        };

        var orderItem2 = new OrderItem
        {
            OrderItemId = new OrderItemId(Guid.NewGuid()),
            OrderId = order.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 1,
            UnitPrice = 35000m,
            VatRate = 0.10m,
            Notes = "Item 2",
            TenantId = order.TenantId
        };

        order.Items.Add(orderItem1);
        order.Items.Add(orderItem2);

        // Act
        order.CalculateTotals();
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Assert - Should FAIL initially (RED) if CalculateTotals doesn't exist
        var savedOrder = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderId.Value == orderId.Value);
        
        Assert.NotNull(savedOrder);
        Assert.True(savedOrder.SubTotal > 0, "SubTotal should be calculated");
        Assert.True(savedOrder.TotalVatAmount > 0, "TotalVatAmount should be calculated");
        Assert.True(savedOrder.TotalAmount > 0, "TotalAmount should be calculated");
        
        // Verify calculations: (2 * 25000 + 1 * 35000) = 85000 SubTotal
        Assert.Equal(85000m, savedOrder.SubTotal);
        // VAT: 85000 * 0.10 = 8500
        Assert.Equal(8500m, savedOrder.TotalVatAmount);
        // Total: 85000 + 8500 = 93500
        Assert.Equal(93500m, savedOrder.TotalAmount);
    }

    [Fact(DisplayName = "Order Entity - Should Have Default Status")]
    public async Task OrderEntity_ShouldHaveDefaultStatus()
    {
        // Arrange
        var orderId = new OrderId(Guid.NewGuid());
        var order = new Order
        {
            OrderId = orderId,
            CustomerDeviceId = "test-device-default",
            OrderType = "DINEIN",
            TenantId = Guid.NewGuid()
        };

        // Act
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Assert - Should FAIL initially (RED) if default status logic doesn't exist
        var savedOrder = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.OrderId.Value == orderId.Value);
        
        Assert.NotNull(savedOrder);
        Assert.Equal("PENDING", savedOrder.Status.Value);
    }

    public void Dispose()
    {
        _dbContext?.Database?.EnsureDeleted();
        _dbContext?.Dispose();
    }
}
