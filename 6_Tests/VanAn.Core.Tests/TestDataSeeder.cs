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

            // Create 2 tenant IDs (Shop entity removed 2026-07-21 — Tenant is single identity)
            TenantId shop1TenantId = new(Guid.NewGuid());
            TenantId shop2TenantId = new(Guid.NewGuid());

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
