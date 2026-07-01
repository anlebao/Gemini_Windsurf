using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding.Strategies
{
    /// <summary>
    /// Stub seed strategy for Hotel industry.
    /// Wave 1: Not yet implemented — returns empty result with warning.
    /// Implement in a future wave when Hotel tenant onboarding is required.
    /// </summary>
    public sealed class HotelSeedStrategy : IIndustrySeedStrategy
    {
        public string IndustryCode => "HOTEL";
        public string IndustryName => "Hotel";

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
