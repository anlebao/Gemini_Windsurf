using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    public class TestDataBuilder(ILogger? logger = null)
    {
        private readonly ILogger? _logger = logger;
        private readonly List<Shop> _shops = [];
        private readonly List<Order> _orders = [];
        private readonly List<Customer> _customers = [];

        public TestDataBuilder WithShops(int count)
        {
            for (int i = 1; i <= count; i++)
            {
                TenantId tenantId = new(Guid.NewGuid());
                _shops.Add(new Shop(tenantId, $"Shop {i}", $"Address {i}", $"09{i:D8}", $"shop{i}@vanan.com"));
            }
            return this;
        }

        public TestDataBuilder WithOrders(int count, bool synced = true)
        {
            if (_shops.Count == 0)
            {
                _ = WithShops(1); // Auto-create a shop if none exists
            }

            for (int i = 1; i <= count; i++)
            {
                Shop shop = _shops[i % _shops.Count]; // Round-robin shop assignment
                decimal totalAmount = (i * 110) + (i % 2 == 0 ? 0m : 5m) - (i % 3 == 0 ? 10m : 0m);
                _orders.Add(new Order(shop.TenantId, null, totalAmount)); // null customerId avoids FK constraint
            }
            return this;
        }

        public TestDataBuilder WithCustomers(int count)
        {
            for (int i = 1; i <= count; i++)
            {
                TenantId customerTenantId = _shops.Count > 0 ? _shops[i % _shops.Count].TenantId : new TenantId(Guid.NewGuid());
                _customers.Add(new Customer(customerTenantId, $"Customer {i}", $"555000{i:D4}", $"customer{i}@example.com"));
            }
            return this;
        }

        public TestDataBuilder WithMixedSyncStatus()
        {
            if (_orders.Count == 0)
            {
                _ = WithOrders(4);
            }

            // Mark 3 out of every 4 orders as synced (75% sync rate)
            for (int i = 0; i < _orders.Count; i++)
            {
                if (i % 4 != 3) // Skip every 4th order (0-indexed), leaving it unsynced
                {
                    _orders[i].MarkAsSynced();
                }
            }
            return this;
        }

        public async Task BuildAsync(VanAnDbContext context)
        {
            _logger?.LogInformation("[TestDataBuilder] Starting to build test data...");

            using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            try
            {
                // Clear existing data
                context.Orders.RemoveRange(context.Orders);
                context.Shops.RemoveRange(context.Shops);
                context.Customers.RemoveRange(context.Customers);
                _ = await context.SaveChangesAsync();

                _logger?.LogInformation("[TestDataBuilder] Cleared existing data. Shops: {ShopsCount}, Orders: {OrdersCount}, Customers: {CustomersCount}", _shops.Count, _orders.Count, _customers.Count);

                // Add new data in proper order (parent first)
                if (_shops.Count > 0)
                {
                    await context.Shops.AddRangeAsync(_shops);
                    _ = await context.SaveChangesAsync();
                }

                if (_customers.Count > 0)
                {
                    await context.Customers.AddRangeAsync(_customers);
                    _ = await context.SaveChangesAsync();
                }

                if (_orders.Count > 0)
                {
                    await context.Orders.AddRangeAsync(_orders);
                    _ = await context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                _logger?.LogInformation("[TestDataBuilder] Test data built successfully. Final counts - Shops: {ShopsCount}, Orders: {OrdersCount}, Customers: {CustomersCount}", await context.Shops.CountAsync(), await context.Orders.CountAsync(), await context.Customers.CountAsync());
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
