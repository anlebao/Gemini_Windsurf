namespace VanAn.CoreHub.Services.Onboarding
{
    /// <summary>
    /// Orchestrates the full tenant onboarding flow:
    /// create tenant → create owner user → seed industry data → assign permission groups.
    ///
    /// Wave 1: Interface contract. Implementation added in Wave 3.
    /// </summary>
    public interface ITenantOnboardingService
    {
        /// <summary>
        /// Onboards a new tenant end-to-end.
        /// Selects the <see cref="IIndustrySeedStrategy"/> matching
        /// <see cref="OnboardTenantRequest.IndustryCode"/> from the registered strategies.
        /// </summary>
        /// <param name="request">Tenant details + industry code + owner credentials.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>
        /// <see cref="TenantOnboardingResult"/> with the new tenant/owner IDs and seeding counts.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="OnboardTenantRequest.IndustryCode"/> is not registered.
        /// </exception>
        Task<TenantOnboardingResult> OnboardAsync(
            OnboardTenantRequest request,
            CancellationToken ct = default);
    }
}
