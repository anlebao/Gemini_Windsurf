using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Tests.TestInfrastructure
{
    public static class TestDataSeeder
    {
        public static async Task SeedBasicAsync(VanAnDbContext context)
        {
            if (context.Orders.Any())
            {
                return;
            }

            // Create 2 shops first (parent entities) - using Shop instead of Tenant
            TenantId shop1TenantId = new(Guid.NewGuid());
            TenantId shop2TenantId = new(Guid.NewGuid());
            Shop shop1 = new(shop1TenantId, "Shop A", "Address A", "1234567890", "shop1@vanan.com");
            Shop shop2 = new(shop2TenantId, "Shop B", "Address B", "0987654321", "shop2@vanan.com");

            context.Shops.AddRange(shop1, shop2);
            _ = await context.SaveChangesAsync(); // Save shops first

            // Create 4 orders with correct sync flags (child entities)
            List<Order> orders =
            [
                new Order(shop1TenantId, Guid.NewGuid(), 110.00m),
                new Order(shop1TenantId, Guid.NewGuid(), 215.00m),
                new Order(shop2TenantId, Guid.NewGuid(), 330.00m),
                new Order(shop2TenantId, Guid.NewGuid(), 430.00m)
            ];

            context.Orders.AddRange(orders);
            _ = await context.SaveChangesAsync();
        }
    }
}
