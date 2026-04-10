using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    public class TestDataBuilder
    {
        private readonly List<Shop> _shops = new();
        private readonly List<Order> _orders = new();
        private readonly List<Customer> _customers = new();

        public TestDataBuilder WithShops(int count)
        {
            for (int i = 1; i <= count; i++)
            {
                _shops.Add(new Shop 
                { 
                    Id = Guid.NewGuid(),
                    Name = $"Shop {i}",
                    Address = $"Address {i}",
                    Phone = $"12345678{i}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            return this;
        }

        public TestDataBuilder WithOrders(int count, bool synced = true)
        {
            if (_shops.Count == 0)
            {
                WithShops(1); // Auto-create a shop if none exists
            }

            for (int i = 1; i <= count; i++)
            {
                var shop = _shops[i % _shops.Count]; // Round-robin shop assignment
                _orders.Add(new Order 
                { 
                    Id = Guid.NewGuid(),
                    OrderId = new OrderId(Guid.NewGuid()),
                    TenantId = shop.Id,
                    Status = new OrderStatusId("COMPLETED"),
                    OrderType = i % 2 == 0 ? "DINE_IN" : "TAKEAWAY",
                    SubTotal = (i * 100) + 0.01m,
                    TotalVatAmount = (i * 10) + 0.01m,
                    ShippingFee = i % 2 == 0 ? 0m : 5m,
                    DiscountAmount = i % 3 == 0 ? 10m : 0m,
                    TotalAmount = (i * 110) + (i % 2 == 0 ? 0m : 5m) - (i % 3 == 0 ? 10m : 0m),
                    OrderDate = DateTime.UtcNow.AddHours(-i),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-i * 10),
                    LastSyncedAt = synced ? DateTime.UtcNow.AddMinutes(-i * 5) : default(DateTime),
                    CreatedAt = DateTime.UtcNow.AddHours(-i),
                    RowVersion = i
                });
            }
            return this;
        }

        public TestDataBuilder WithCustomers(int count)
        {
            for (int i = 1; i <= count; i++)
            {
                _customers.Add(new Customer 
                { 
                    Id = Guid.NewGuid(),
                    FullName = $"Customer {i}",
                    Email = $"customer{i}@example.com",
                    PhoneNumber = $"555000{i:D4}",
                    TenantId = _shops.Count > 0 ? _shops[i % _shops.Count].Id : Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow.AddDays(-i),
                    UpdatedAt = DateTime.UtcNow
                });
            }
            return this;
        }

        public TestDataBuilder WithMixedSyncStatus()
        {
            if (_orders.Count == 0)
            {
                WithOrders(4);
            }

            // Set mixed sync status: 75% synced (3/4)
            for (int i = 0; i < _orders.Count; i++)
            {
                if (i < _orders.Count * 0.75) // First 75% are synced
                {
                    _orders[i].LastSyncedAt = DateTime.UtcNow.AddMinutes(-i * 5);
                }
                else // Last 25% are not synced
                {
                    _orders[i].LastSyncedAt = default(DateTime);
                }
            }
            return this;
        }

        public async Task BuildAsync(VanAnDbContext context)
        {
            Console.WriteLine($"[TestDataBuilder] Starting to build test data...");
            
            // Clear existing data
            context.Orders.RemoveRange(context.Orders);
            context.Shops.RemoveRange(context.Shops);
            context.Customers.RemoveRange(context.Customers);
            await context.SaveChangesAsync();

            Console.WriteLine($"[TestDataBuilder] Cleared existing data. Shops: {_shops.Count}, Orders: {_orders.Count}, Customers: {_customers.Count}");

            // Add new data in proper order (parent first)
            if (_shops.Any())
            {
                await context.Shops.AddRangeAsync(_shops);
                await context.SaveChangesAsync();
            }

            if (_customers.Any())
            {
                await context.Customers.AddRangeAsync(_customers);
                await context.SaveChangesAsync();
            }

            if (_orders.Any())
            {
                await context.Orders.AddRangeAsync(_orders);
                await context.SaveChangesAsync();
            }

            Console.WriteLine($"[TestDataBuilder] Test data built successfully. Final counts - Shops: {await context.Shops.CountAsync()}, Orders: {await context.Orders.CountAsync()}, Customers: {await context.Customers.CountAsync()}");
        }

        // Static factory methods for common scenarios
        public static TestDataBuilder CreateBasicScenario()
        {
            return new TestDataBuilder()
                .WithShops(2)
                .WithOrders(4)
                .WithMixedSyncStatus();
        }

        public static TestDataBuilder CreateLargeScenario()
        {
            return new TestDataBuilder()
                .WithShops(5)
                .WithOrders(100)
                .WithCustomers(50)
                .WithMixedSyncStatus();
        }

        public static TestDataBuilder CreateEmptyScenario()
        {
            return new TestDataBuilder();
        }
    }
}
