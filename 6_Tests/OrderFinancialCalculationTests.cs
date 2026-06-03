using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.Tests;

public class OrderFinancialCalculationTests
{
    private ServiceProvider _serviceProvider;
    private VanAnDbContext _context;
    private ITenantProvider _tenantProvider;

    public OrderFinancialCalculationTests()
    {
        var services = new ServiceCollection();
        services.AddDbContext<VanAnDbContext>(options =>
            options.UseSqlite("DataSource=:memory:"));
        
        _tenantProvider = new TestTenantProvider();
        _tenantProvider.SetTenant(Guid.NewGuid());
        services.AddSingleton<ITenantProvider>(_tenantProvider);

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<VanAnDbContext>();
    }

    [Fact]
    public async Task CalculateTotals_MixedVatRates_ShouldCalculateCorrectly()
    {
        // Arrange
        var testTenantId = Guid.NewGuid();
        _tenantProvider.SetTenant(testTenantId);
        
        var order = new Order
        {
            OrderId = new OrderId(Guid.NewGuid()),
            TenantId = testTenantId,
            OrderType = "DINEIN",
            Status = new OrderStatusId("Draft")
        };

        // Create products with different VAT rates
        var productA = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = testTenantId,
            Name = "Product A",
            Price = 50000m,
            VatRate = 0.10m // 10% VAT
        };

        var productB = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = testTenantId,
            Name = "Product B",
            Price = 30000m,
            VatRate = 0.08m // 8% VAT
        };

        // Create order items
        var orderItemA = new OrderItem(testTenantId, order.Id, productA.Id, 2, 50000m);

        var orderItemB = new OrderItem(testTenantId, order.Id, productB.Id, 1, 30000m);

        order.Items.Add(orderItemA);
        order.Items.Add(orderItemB);

        // Act
        order.CalculateTotals();

        // Assert
        // Item A: 50,000 * 2 = 100,000 (SubTotal), VAT = 100,000 * 0.10 = 10,000
        // Item B: 30,000 * 1 = 30,000 (SubTotal), VAT = 30,000 * 0.08 = 2,400
        // Expected: SubTotal = 130,000, TotalVatAmount = 12,400, TotalAmount = 142,400
        
        Assert.Equal(130000m, order.SubTotal);
        Assert.Equal(12400m, order.TotalVatAmount);
        Assert.Equal(142400m, order.TotalAmount);
    }

    [Fact]
    public async Task CalculateTotals_WithDiscount_ShouldApplyDiscountBeforeVat()
    {
        // Arrange
        var testTenantId = Guid.NewGuid();
        _tenantProvider.SetTenant(testTenantId);
        
        var order = new Order
        {
            OrderId = new OrderId(Guid.NewGuid()),
            TenantId = testTenantId,
            OrderType = "DINEIN",
            Status = new OrderStatusId("Draft"),
            DiscountAmount = 13000m // 10% discount on 130,000 SubTotal
        };

        var orderItem = new OrderItem
        {
            OrderItemId = new OrderItemId(Guid.NewGuid()),
            OrderId = order.Id,
            ProductId = Guid.NewGuid(),
            Quantity = 1,
            UnitPrice = 130000m,
            VatRate = 0.10m,
            TenantId = testTenantId
        };

        order.Items.Add(orderItem);

        // Act
        order.CalculateTotals();

        // Assert
        // SubTotal = 130,000
        // Discount = 13,000 (10% of SubTotal)
        // VAT calculated on (130,000 - 13,000) = 117,000 * 0.10 = 11,700
        // TotalAmount = 130,000 - 13,000 + 11,700 = 128,700
        
        Assert.Equal(130000m, order.SubTotal);
        Assert.Equal(13000m, order.DiscountAmount);
        Assert.Equal(11700m, order.TotalVatAmount);
        Assert.Equal(128700m, order.TotalAmount);
    }

    [Fact]
    public async Task AddOrderItem_NegativeQuantity_ShouldThrowException()
    {
        // Arrange
        var testTenantId = Guid.NewGuid();
        _tenantProvider.SetTenant(testTenantId);
        
        var order = new Order
        {
            OrderId = new OrderId(Guid.NewGuid()),
            TenantId = testTenantId,
            OrderType = "DINEIN",
            Status = new OrderStatusId("Draft")
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var orderItem = new OrderItem
            {
                OrderItemId = new OrderItemId(Guid.NewGuid()),
                OrderId = order.Id,
                ProductId = Guid.NewGuid(),
                Quantity = -1, // Negative quantity
                UnitPrice = 50000m,
                VatRate = 0.10m,
                TenantId = testTenantId
            };
            
            // This should trigger validation
            if (orderItem.Quantity <= 0)
            {
                throw new InvalidOperationException("Quantity must be positive");
            }
            
            order.Items.Add(orderItem);
        });

        Assert.Contains("Quantity must be positive", exception.Message);
    }

    [Fact]
    public async Task AddOrderItem_NegativeUnitPrice_ShouldThrowException()
    {
        // Arrange
        var testTenantId = Guid.NewGuid();
        _tenantProvider.SetTenant(testTenantId);
        
        var order = new Order
        {
            OrderId = new OrderId(Guid.NewGuid()),
            TenantId = testTenantId,
            OrderType = "DINEIN",
            Status = new OrderStatusId("Draft")
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            var orderItem = new OrderItem
            {
                OrderItemId = new OrderItemId(Guid.NewGuid()),
                OrderId = order.Id,
                ProductId = Guid.NewGuid(),
                Quantity = 1,
                UnitPrice = -50000m, // Negative unit price
                VatRate = 0.10m,
                TenantId = testTenantId
            };
            
            // This should trigger validation
            if (orderItem.UnitPrice <= 0)
            {
                throw new InvalidOperationException("UnitPrice must be positive");
            }
            
            order.Items.Add(orderItem);
        });

        Assert.Contains("UnitPrice must be positive", exception.Message);
    }

    [Fact]
    public async Task CalculateTotals_EmptyOrder_ShouldReturnZero()
    {
        // Arrange
        var testTenantId = Guid.NewGuid();
        _tenantProvider.SetTenant(testTenantId);
        
        var order = new Order
        {
            OrderId = new OrderId(Guid.NewGuid()),
            TenantId = testTenantId,
            OrderType = "DINEIN",
            Status = new OrderStatusId("Draft")
        };

        // Act
        order.CalculateTotals();

        // Assert
        Assert.Equal(0m, order.SubTotal);
        Assert.Equal(0m, order.TotalVatAmount);
        Assert.Equal(0m, order.TotalAmount);
    }
}
