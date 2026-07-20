namespace VanAn.CoreHub.Services.Onboarding
{
    /// <summary>
    /// Orchestrates the full tenant onboarding flow:
    /// create tenant → create owner user → assign permission groups.
    ///
    /// Phase 3.6: Product seeding removed — Gateway PG no longer stores Products (Option C).
    /// Tenant owner runs QuickSetup manually after first login to seed industry data in ShopERP SQLite.
    /// The IndustryCode field in OnboardTenantRequest is kept for backward API compat but not used for seeding.
    /// </summary>
    public interface ITenantOnboardingService
    {
        /// <summary>
        /// Onboards a new tenant end-to-end (tenant + owner + permission groups only).
        /// Product seeding is deferred to QuickSetup (run by tenant owner after first login).
        /// </summary>
        /// <param name="request">Tenant details + industry code + owner credentials.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// <see cref="TenantOnboardingResult"/> with the new tenant/owner IDs.
        /// Seed counts (ProductsCreated, etc.) are always 0 — seeding deferred to QuickSetup.
        /// </returns>
        Task<TenantOnboardingResult> OnboardAsync(
            OnboardTenantRequest request,
            CancellationToken ct = default);
    }
}
