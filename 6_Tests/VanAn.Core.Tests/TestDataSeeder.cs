using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    public static class TestDataSeeder
    {
        public static async Task SeedBasicAsync(VanAnDbContext context)
        {
            if (context.Orders.Any()) return;

            // Create 2 shops first (parent entities) - using Shop instead of Tenant
            var shop1 = new Shop { Id = Guid.NewGuid(), Name = "Shop A", Address = "Address A", Phone = "123456789", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var shop2 = new Shop { Id = Guid.NewGuid(), Name = "Shop B", Address = "Address B", Phone = "987654321", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

            context.Shops.AddRange(shop1, shop2);
            await context.SaveChangesAsync(); // Save shops first

            // Create 4 orders with correct sync flags (child entities)
            var orders = new List<Order>
            {
                new Order 
                { 
                    Id = Guid.NewGuid(),
                    OrderId = new OrderId(Guid.NewGuid()),
                    TenantId = shop1.Id, // Use Shop Id as TenantId
                    Status = new OrderStatusId("COMPLETED"),
                    OrderType = "DINE_IN",
                    SubTotal = 100.00m,
                    TotalVatAmount = 10.00m,
                    ShippingFee = 0.00m,
                    DiscountAmount = 0.00m,
                    TotalAmount = 110.00m,
                    OrderDate = DateTime.UtcNow,
                    LastSyncedAt = DateTime.UtcNow.AddMinutes(-5), // Synced
                    CreatedAt = DateTime.UtcNow,
                    RowVersion = 1
                },
                new Order 
                { 
                    Id = Guid.NewGuid(),
                    OrderId = new OrderId(Guid.NewGuid()),
                    TenantId = shop1.Id, // Use Shop Id as TenantId
                    Status = new OrderStatusId("COMPLETED"),
                    OrderType = "TAKEAWAY",
                    SubTotal = 200.00m,
                    TotalVatAmount = 20.00m,
                    ShippingFee = 5.00m,
                    DiscountAmount = 10.00m,
                    TotalAmount = 215.00m,
                    OrderDate = DateTime.UtcNow.AddHours(-1),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-30),
                    LastSyncedAt = DateTime.UtcNow.AddMinutes(-10), // Synced
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    RowVersion = 2
                },
                new Order 
                { 
                    Id = Guid.NewGuid(),
                    OrderId = new OrderId(Guid.NewGuid()),
                    TenantId = shop2.Id, // Use Shop Id as TenantId
                    Status = new OrderStatusId("PENDING"),
                    OrderType = "DINE_IN",
                    SubTotal = 300.00m,
                    TotalVatAmount = 30.00m,
                    ShippingFee = 0.00m,
                    DiscountAmount = 0.00m,
                    TotalAmount = 330.00m,
                    OrderDate = DateTime.UtcNow,
                    // No LastSyncedAt = Not synced
                    CreatedAt = DateTime.UtcNow,
                    RowVersion = 3
                },
                new Order 
                { 
                    Id = Guid.NewGuid(),
                    OrderId = new OrderId(Guid.NewGuid()),
                    TenantId = shop2.Id, // Use Shop Id as TenantId
                    Status = new OrderStatusId("COMPLETED"),
                    OrderType = "TAKEAWAY",
                    SubTotal = 400.00m,
                    TotalVatAmount = 40.00m,
                    ShippingFee = 5.00m,
                    DiscountAmount = 15.00m,
                    TotalAmount = 430.00m,
                    OrderDate = DateTime.UtcNow.AddHours(-2),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-45),
                    LastSyncedAt = DateTime.UtcNow.AddMinutes(-15), // Synced
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    RowVersion = 4
                }
            };

            context.Orders.AddRange(orders);
            await context.SaveChangesAsync();
        }
    }
}
