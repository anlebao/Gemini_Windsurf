using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Onboarding
{
    /// <summary>
    /// Request to onboard a new tenant with industry-specific seed data.
    /// Wave 1: Generic abstraction for multi-industry tenant onboarding.
    /// </summary>
    public record OnboardTenantRequest(
        string Name,
        BusinessType BusinessType,
        HKDGroup? HKDGroup,
        string? ContactEmail,
        string? ContactPhone,
        string? Address,
        string? TaxCode,
        string IndustryCode,
        string OwnerUsername,
        string OwnerPassword,
        string OwnerDisplayName);

    /// <summary>
    /// Result of a complete tenant onboarding operation.
    /// Aggregates counts from all sub-operations (tenant creation, user creation, seed).
    /// </summary>
    public record TenantOnboardingResult(
        Guid TenantId,
        Guid OwnerUserId,
        int ProductsCreated,
        int IngredientsCreated,
        int RecipesCreated,
        int ShopsCreated,
        int PermissionGroupsCreated,
        IReadOnlyList<string> Warnings);

    /// <summary>
    /// Result of an industry-specific seed operation.
    /// Returned by <see cref="IIndustrySeedStrategy.SeedAsync"/>.
    /// </summary>
    public record IndustrySeedResult(
        int ProductsCreated,
        int IngredientsCreated,
        int RecipesCreated,
        int ShopsCreated,
        IReadOnlyList<string> Warnings)
    {
        /// <summary>Empty result with no data seeded and no warnings.</summary>
        public static IndustrySeedResult Empty { get; } = new(0, 0, 0, 0, []);
    }
}
