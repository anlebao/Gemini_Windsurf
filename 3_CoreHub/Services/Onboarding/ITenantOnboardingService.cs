namespace VanAn.CoreHub.Services.Onboarding
{
    /// <summary>
    /// Orchestrates the full tenant onboarding flow:
    /// create tenant → create owner user → assign permission groups.
    ///
    /// Phase 3.6: Product seeding removed — Gateway PG no longer stores Products (Option C).
    /// Tenant owner runs QuickSetup manually after first login to seed industry data in ShopERP SQLite.
    /// The IndustryCode field in OnboardTenantRequest is kept for backward API compat but not used for seeding.
    ///
    /// Crawl-to-Onboard Pipeline (2026-08-25): Extended with OnboardUnverifiedAsync (Pending only)
    /// + VerifyAsync (Pending → Active + owner user + groups + Option A outbox publish).
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

        /// <summary>
        /// Crawl-to-Onboard (2026-08-25): Creates a Pending tenant from crawled business listing.
        /// NO owner user, NO permission groups, NO welcome email (tenant not yet verified).
        /// SĐT section HIDDEN on profile (M3 — CrawledPhone stored internal, ContactPhone=null).
        /// Auto-generates pending slug: pending-{taxCode}-{random4} with retry on collision.
        /// Checks for duplicate MST via IDuplicateDetectionService (correction H5: first canonical).
        /// Saves CrawlSource audit row for provenance.
        /// </summary>
        /// <param name="listing">Crawled business listing data.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The new Pending tenant ID.</returns>
        Task<Guid> OnboardUnverifiedAsync(
            CrawlListingDto listing,
            CancellationToken ct = default);

        /// <summary>
        /// Crawl-to-Onboard (2026-08-25): Verifies a Pending tenant → Active.
        /// Creates owner user + Owner role + 4 default permission groups + assigns owner to Quản lý.
        /// Sets ContactPhone from owner-provided form (M3 — consented, NOT from CrawledPhone).
        /// Updates slug to clean slug (now UpdateSlug works — tenant is Active).
        /// Option A: Publishes OutboxMessage TenantVerifiedEvent → NATS → TenantSyncSubscriber
        /// upserts Tenant row in SQLite with same Guid (data integrity PG↔SQLite).
        /// </summary>
        /// <param name="tenantId">The Pending tenant to verify.</param>
        /// <param name="request">Owner credentials + clean slug + SysAdmin approver ID.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><see cref="VerifyResult"/> with tenant ID, owner user ID, group count, published slug.</returns>
        Task<VerifyResult> VerifyAsync(
            Guid tenantId,
            VerifyTenantRequest request,
            CancellationToken ct = default);
    }
}
