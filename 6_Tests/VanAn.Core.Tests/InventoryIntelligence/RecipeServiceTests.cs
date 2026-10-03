using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE (Sprint B, P2.2): RecipeService — CRUD + versioning + cache active theo ProductId (SRS §3.2, §7.3).
    /// DB-backed (VanAnDbContextTestFactory — SQLite in-memory, FK enforcement ON).
    /// </summary>
    public class RecipeServiceTests
    {
        private static readonly TenantId TenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        private static (TestContextScope scope, RecipeService service, Guid productId, Guid ingredientA, Guid ingredientB) Setup()
        {
            var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(TenantId.Value);
            var service = new RecipeService(scope.Context, Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeService>.Instance);
            var product = new Product(TenantId, "Cà phê sữa đá", "Test", 30_000m, "Cà phê");
            var ingA = new Ingredient(TenantId, "Cà phê bột", "g", 100_000m, 5_000m, 200m);
            var ingB = new Ingredient(TenantId, "Sữa đặc", "lon", 100m, 10m, 25_000m);
            scope.Context.Products.Add(product);
            scope.Context.Ingredients.AddRange(ingA, ingB);
            scope.Context.SaveChanges();
            return (scope, service, product.Id, ingA.Id, ingB.Id);
        }

        [Fact]
        public async Task Create_ThenGetActive_ReturnsV1WithLines()
        {
            var (scope, service, productId, ingA, _) = Setup();
            using (scope)
            {
                var recipe = await service.CreateAsync(TenantId, productId, [new RecipeLineInput(ingA, 20m, "g")]);

                Assert.Equal(1, recipe.Version);
                Assert.True(recipe.IsActive);
                Assert.Single(recipe.Lines);
                Assert.Equal(recipe.Id, recipe.RecipeId.Value); // Single-Identity

                var active = await service.GetActiveByProductIdAsync(productId);
                Assert.NotNull(active);
                Assert.Equal(recipe.Id, active!.Id);
            }
        }

        [Fact]
        public async Task Create_WhenActiveExists_Throws()
        {
            var (scope, service, productId, ingA, _) = Setup();
            using (scope)
            {
                await service.CreateAsync(TenantId, productId, [new RecipeLineInput(ingA, 20m, "g")]);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.CreateAsync(TenantId, productId, [new RecipeLineInput(ingA, 30m, "g")]));
            }
        }

        [Fact]
        public async Task PublishNewVersion_DeactivatesCurrent_AndCopiesNewLines()
        {
            var (scope, service, productId, ingA, ingB) = Setup();
            using (scope)
            {
                var v1 = await service.CreateAsync(TenantId, productId, [new RecipeLineInput(ingA, 20m, "g")]);

                var v2 = await service.PublishNewVersionAsync(TenantId, productId,
                    [new RecipeLineInput(ingA, 25m, "g"), new RecipeLineInput(ingB, 0.03m, "lon")]);

                Assert.Equal(2, v2.Version);
                Assert.True(v2.IsActive);
                Assert.Equal(2, v2.Lines.Count);
                Assert.Equal(25m, v2.Lines[0].Quantity);
                Assert.Equal("lon", v2.Lines[1].Unit);

                // v1 bị deactivate
                var v1Reloaded = await service.GetByIdAsync(v1.Id);
                Assert.False(v1Reloaded!.IsActive);

                // cache trả v2
                var active = await service.GetActiveByProductIdAsync(productId);
                Assert.Equal(v2.Id, active!.Id);
            }
        }

        [Fact]
        public async Task PublishNewVersion_NoExisting_ActsAsCreate()
        {
            var (scope, service, productId, ingA, _) = Setup();
            using (scope)
            {
                var v1 = await service.PublishNewVersionAsync(TenantId, productId, [new RecipeLineInput(ingA, 20m, "g")]);

                Assert.Equal(1, v1.Version);
            }
        }

        [Fact]
        public async Task SetActive_Toggling_WorksWithGuard()
        {
            var (scope, service, productId, ingA, _) = Setup();
            using (scope)
            {
                var v1 = await service.CreateAsync(TenantId, productId, [new RecipeLineInput(ingA, 20m, "g")]);
                var v2 = await service.PublishNewVersionAsync(TenantId, productId, [new RecipeLineInput(ingA, 30m, "g")]);

                // Bật v1 lại → guard chặn vì v2 active.
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetActiveAsync(v1.Id, true));

                // Deactivate v2 → bật v1 OK
                await service.SetActiveAsync(v2.Id, false);
                var reactivated = await service.SetActiveAsync(v1.Id, true);
                Assert.True(reactivated.IsActive);

                var active = await service.GetActiveByProductIdAsync(productId);
                Assert.Equal(v1.Id, active!.Id);
            }
        }

        [Fact]
        public async Task InvalidLines_Throw()
        {
            var (scope, service, productId, ingA, _) = Setup();
            using (scope)
            {
                await Assert.ThrowsAsync<ArgumentException>(
                    () => service.CreateAsync(TenantId, productId, []));
                await Assert.ThrowsAsync<ArgumentException>(
                    () => service.CreateAsync(TenantId, productId, [new RecipeLineInput(ingA, 0m, "g")]));
                await Assert.ThrowsAsync<ArgumentException>(
                    () => service.CreateAsync(TenantId, productId, [new RecipeLineInput(ingA, 20m, " ")]));
                await Assert.ThrowsAsync<ArgumentException>(
                    () => service.CreateAsync(TenantId, productId, [new RecipeLineInput(ingA, 20m, "g"), new RecipeLineInput(ingA, 10m, "g")]));
            }
        }

        [Fact]
        public async Task GetVersions_ReturnsNewestFirst()
        {
            var (scope, service, productId, ingA, _) = Setup();
            using (scope)
            {
                await service.CreateAsync(TenantId, productId, [new RecipeLineInput(ingA, 20m, "g")]);
                await service.PublishNewVersionAsync(TenantId, productId, [new RecipeLineInput(ingA, 30m, "g")]);

                var versions = await service.GetVersionsAsync(productId);
                Assert.Equal(2, versions.Count);
                Assert.Equal(2, versions[0].Version);
                Assert.Equal(1, versions[1].Version);
            }
        }
    }
}
