using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding
{
    /// <summary>
    /// Strategy for seeding industry-specific default data (products, ingredients, recipes, shops)
    /// when a new tenant is onboarded.
    ///
    /// Wave 1: Generic abstraction. Implement per industry (F&amp;B, SPA, Hotel, Barber, Clothes, Healthy, Pet Shop).
    /// Register all implementations with DI; the orchestrator selects by <see cref="IndustryCode"/>.
    /// </summary>
    public interface IIndustrySeedStrategy
    {
        /// <summary>
        /// Unique, stable identifier for this industry (e.g. "F&amp;B", "SPA", "HOTEL").
        /// Must be non-empty, upper-case, ASCII-safe. Used as a lookup key.
        /// </summary>
        string IndustryCode { get; }

        /// <summary>
        /// Human-readable industry name shown in logs and warnings.
        /// </summary>
        string IndustryName { get; }

        /// <summary>
        /// Seeds default data for the given tenant into the provided <paramref name="dbContext"/>.
        /// Implementations must ensure all entities are tagged with <paramref name="tenantId"/>.
        /// Does NOT call <see cref="IVanAnDbContext.SaveChangesAsync"/> — the caller is responsible
        /// for the save + transaction boundary.
        /// </summary>
        /// <param name="tenantId">Target tenant. Must not be <see cref="TenantId.Empty"/>.</param>
        /// <param name="dbContext">DbContext scoped to the current request/unit-of-work.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><see cref="IndustrySeedResult"/> with counts and any non-fatal warnings.</returns>
        Task<IndustrySeedResult> SeedAsync(
            TenantId tenantId,
            IVanAnDbContext dbContext,
            CancellationToken ct = default);
    }
}
