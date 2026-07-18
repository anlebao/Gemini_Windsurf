using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.Shared.Domain;
using VanAn.Shared.Models;

namespace VanAn.CoreHub.Services
{
    public interface IOnboardingService
    {
        Task<IEnumerable<OnboardingTemplate>> GetTemplatesAsync();
        Task<OnboardingTemplate?> GetTemplateAsync(Guid id);
        Task<OnboardingTemplate> CreateTemplateAsync(OnboardingTemplate template);
        Task<OnboardingTemplate> ApplyTemplateAsync(Guid templateId, Guid shopId);
        Task<OnboardingTemplate> UpdateTemplateAsync(OnboardingTemplate template);
        Task<bool> DeleteTemplateAsync(Guid id);
    }

    /// <summary>
    /// OnboardingService — delegates template application to IIndustrySeedStrategy.
    ///
    /// TemplateId → IndustryCode mapping (deterministic Guids from QuickSetup.razor + OnboardingController):
    ///   a1111111-1111-1111-1111-111111111111 → "F&B"      (Quán Cafe — FnbSeedStrategy)
    ///   b2222222-2222-2222-2222-222222222222 → "SPA"      (Spa & Beauty — SpaSeedStrategy)
    ///   c3333333-3333-3333-3333-333333333333 → "RETAIL"   (Cửa hàng — RetailSeedStrategy)
    ///   d4444444-4444-4444-4444-444444444444 → "CLOTHES"  (Thời trang — ClothesSeedStrategy)
    ///
    /// Idempotency: If tenant already has products, ApplyTemplateAsync skips seeding and
    /// returns existing product count. Safe to call multiple times.
    /// </summary>
    public class OnboardingService(
        IVanAnDbContext dbContext,
        IEnumerable<IIndustrySeedStrategy> seedStrategies,
        ILogger<OnboardingService> logger) : IOnboardingService
    {
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IEnumerable<IIndustrySeedStrategy> _seedStrategies = seedStrategies;
        private readonly ILogger<OnboardingService> _logger = logger;

        // TemplateId → IndustryCode mapping
        private static readonly Dictionary<Guid, string> TemplateToIndustry = new()
        {
            { Guid.Parse("a1111111-1111-1111-1111-111111111111"), "F&B" },
            { Guid.Parse("b2222222-2222-2222-2222-222222222222"), "SPA" },
            { Guid.Parse("c3333333-3333-3333-3333-333333333333"), "RETAIL" },
            { Guid.Parse("d4444444-4444-4444-4444-444444444444"), "CLOTHES" },
        };

        // TemplateId → display name
        private static readonly Dictionary<Guid, string> TemplateNames = new()
        {
            { Guid.Parse("a1111111-1111-1111-1111-111111111111"), "Quán Cafe" },
            { Guid.Parse("b2222222-2222-2222-2222-222222222222"), "Spa & Beauty" },
            { Guid.Parse("c3333333-3333-3333-3333-333333333333"), "Cửa hàng" },
            { Guid.Parse("d4444444-4444-4444-4444-444444444444"), "Thời trang" },
        };

        public async Task<IEnumerable<OnboardingTemplate>> GetTemplatesAsync()
        {
            await Task.CompletedTask;
            return TemplateToIndustry.Select(kvp => new OnboardingTemplate
            {
                Id = kvp.Key,
                Name = TemplateNames.GetValueOrDefault(kvp.Key, "Unknown"),
                Description = $"Industry: {kvp.Value}"
            }).ToList();
        }

        public async Task<OnboardingTemplate?> GetTemplateAsync(Guid id)
        {
            await Task.CompletedTask;
            if (TemplateToIndustry.TryGetValue(id, out string? industryCode))
            {
                return new OnboardingTemplate
                {
                    Id = id,
                    Name = TemplateNames.GetValueOrDefault(id, "Unknown"),
                    Description = $"Industry: {industryCode}"
                };
            }
            return null;
        }

        public async Task<OnboardingTemplate> ApplyTemplateAsync(Guid templateId, Guid shopId)
        {
            // 1. Resolve templateId → IndustryCode
            if (!TemplateToIndustry.TryGetValue(templateId, out string? industryCode))
            {
                throw new ArgumentException(
                    $"Unknown template ID: {templateId}. Available: {string.Join(", ", TemplateToIndustry.Keys)}",
                    nameof(templateId));
            }

            var tenantId = new TenantId(shopId);
            _logger.LogInformation(
                "ApplyTemplateAsync: templateId={TemplateId} → industryCode={IndustryCode}, tenantId={TenantId}",
                templateId, industryCode, shopId);

            // 2. Idempotency check — skip if tenant already has products
            int existingProducts = await _dbContext.Products
                .IgnoreQueryFilters()
                .CountAsync(p => p.TenantId == tenantId && !p.IsDeleted);

            if (existingProducts > 0)
            {
                _logger.LogInformation(
                    "ApplyTemplateAsync: tenant {TenantId} already has {Count} products — skipping seed (idempotent)",
                    shopId, existingProducts);

                return new OnboardingTemplate
                {
                    Id = templateId,
                    Name = $"{TemplateNames.GetValueOrDefault(templateId, "Template")} (already seeded — {existingProducts} products exist)",
                    Description = $"Industry: {industryCode}. Skipped seeding — tenant already has products."
                };
            }

            // 3. Resolve strategy by IndustryCode
            var strategy = _seedStrategies
                .FirstOrDefault(s => string.Equals(s.IndustryCode, industryCode, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException(
                    $"No IIndustrySeedStrategy registered for IndustryCode '{industryCode}'. " +
                    $"Available: {string.Join(", ", _seedStrategies.Select(s => s.IndustryCode))}",
                    nameof(templateId));

            _logger.LogInformation(
                "ApplyTemplateAsync: resolved strategy {Strategy} for industry {IndustryCode}",
                strategy.GetType().Name, industryCode);

            // 4. Seed
            var seedResult = await strategy.SeedAsync(tenantId, _dbContext, CancellationToken.None);
            await _dbContext.SaveChangesAsync(CancellationToken.None);

            _logger.LogInformation(
                "ApplyTemplateAsync: seed complete for tenant {TenantId} — {Products} products, {Ingredients} ingredients, {Recipes} recipes, {Shops} shops",
                shopId, seedResult.ProductsCreated, seedResult.IngredientsCreated,
                seedResult.RecipesCreated, seedResult.ShopsCreated);

            if (seedResult.Warnings.Count > 0)
            {
                _logger.LogWarning(
                    "ApplyTemplateAsync: seed completed with {Count} warnings: {Warnings}",
                    seedResult.Warnings.Count, string.Join("; ", seedResult.Warnings));
            }

            return new OnboardingTemplate
            {
                Id = templateId,
                Name = TemplateNames.GetValueOrDefault(templateId, "Applied Template"),
                Description = $"Industry: {industryCode}. Seeded {seedResult.ProductsCreated} products, " +
                              $"{seedResult.IngredientsCreated} ingredients, {seedResult.RecipesCreated} recipes."
            };
        }

        public async Task<OnboardingTemplate> CreateTemplateAsync(OnboardingTemplate template)
        {
            await Task.Delay(10);
            return template;
        }

        public async Task<OnboardingTemplate> UpdateTemplateAsync(OnboardingTemplate template)
        {
            await Task.Delay(10);
            return template;
        }

        public async Task<bool> DeleteTemplateAsync(Guid id)
        {
            await Task.Delay(10);
            return true;
        }
    }
}
