using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// Stub seed strategy for Pet Shop industry.
    /// Wave 1: Not yet implemented — returns empty result with warning.
    /// Implement in a future wave when Pet Shop tenant onboarding is required.
    /// </summary>
    public sealed class PetShopSeedStrategy : IIndustrySeedStrategy
    {
        public string IndustryCode => "PETSHOP";
        public string IndustryName => "Pet Shop";

        public Task<IndustrySeedResult> SeedAsync(
            TenantId tenantId,
            IVanAnDbContext dbContext,
            CancellationToken ct = default)
        {
            var result = new IndustrySeedResult(0, 0, 0, 0,
                [$"{IndustryName} seeding not yet implemented"]);
            return Task.FromResult(result);
        }
    }
}
