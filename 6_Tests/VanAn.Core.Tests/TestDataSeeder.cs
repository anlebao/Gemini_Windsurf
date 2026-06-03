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
            var shop1TenantId = new TenantId(Guid.NewGuid());
            var shop2TenantId = new TenantId(Guid.NewGuid());
            var shop1 = new Shop(shop1TenantId, "Shop A", "Address A", "1234567890", "shop1@vanan.com");
            var shop2 = new Shop(shop2TenantId, "Shop B", "Address B", "0987654321", "shop2@vanan.com");

            context.Shops.AddRange(shop1, shop2);
            await context.SaveChangesAsync(); // Save shops first

            // Create 4 orders with correct sync flags (child entities)
            var orders = new List<Order>
            {
                new Order(shop1TenantId, Guid.NewGuid(), 110.00m),
                new Order(shop1TenantId, Guid.NewGuid(), 215.00m),
                new Order(shop2TenantId, Guid.NewGuid(), 330.00m),
                new Order(shop2TenantId, Guid.NewGuid(), 430.00m)
            };

            context.Orders.AddRange(orders);
            await context.SaveChangesAsync();
        }
    }
}
