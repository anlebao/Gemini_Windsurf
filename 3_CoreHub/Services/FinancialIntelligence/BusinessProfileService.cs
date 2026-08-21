using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.FinancialIntelligence
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Implementation of <see cref="IBusinessProfileService"/>.
    /// Pattern follows ShopFeatureSettingsService — read entity, create if missing, update fields, save.
    /// Uses IBusinessProfileRepository (not IVanAnDbContext directly — repository abstraction).
    /// </summary>
    public class BusinessProfileService : IBusinessProfileService
    {
        private readonly IBusinessProfileRepository _repo;
        private readonly ILogger<BusinessProfileService> _logger;

        public BusinessProfileService(IBusinessProfileRepository repo, ILogger<BusinessProfileService> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<BusinessProfile?> GetAsync(TenantId tenantId, CancellationToken ct = default)
        {
            try
            {
                return await _repo.GetByTenantAsync(tenantId, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting BusinessProfile for tenant {TenantId}", tenantId.Value);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<BusinessProfile> GetOrCreateDefaultAsync(TenantId tenantId, CancellationToken ct = default)
        {
            BusinessProfile? existing = await _repo.GetByTenantAsync(tenantId, ct).ConfigureAwait(false);
            if (existing is not null)
                return existing;

            _logger.LogInformation("Creating default BusinessProfile for tenant {TenantId}", tenantId.Value);
            var profile = new BusinessProfile(
                tenantId,
                monthlyRent: 0m, monthlyPayroll: 0m, monthlyUtilities: 0m,
                monthlyMarketing: 0m, monthlyLogistics: 0m, monthlyOtherOpEx: 0m,
                monthlyDepreciation: 0m,
                dailyCapacityUnits: 0, operatingDaysPerMonth: 30,
                pricingModel: PricingModel.FixedPrice,
                notes: "Khởi tạo tự động — chủ doanh nghiệp cần cập nhật fixed costs");
            return await _repo.AddAsync(profile, ct).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<BusinessProfile> UpdateAsync(TenantId tenantId, UpdateBusinessProfileCommand cmd, CancellationToken ct = default)
        {
            BusinessProfile? existing = await _repo.GetByTenantAsync(tenantId, ct).ConfigureAwait(false);

            if (existing is null)
            {
                // Create new with provided values
                _logger.LogInformation("Creating BusinessProfile for tenant {TenantId}", tenantId.Value);
                var profile = new BusinessProfile(
                    tenantId,
                    cmd.MonthlyRent, cmd.MonthlyPayroll, cmd.MonthlyUtilities,
                    cmd.MonthlyMarketing, cmd.MonthlyLogistics, cmd.MonthlyOtherOpEx,
                    cmd.MonthlyDepreciation,
                    cmd.DailyCapacityUnits, cmd.OperatingDaysPerMonth,
                    cmd.PricingModel, cmd.Notes);
                return await _repo.AddAsync(profile, ct).ConfigureAwait(false);
            }

            // Update existing — Version auto-increments in BusinessProfile.Update
            existing.Update(
                cmd.MonthlyRent, cmd.MonthlyPayroll, cmd.MonthlyUtilities,
                cmd.MonthlyMarketing, cmd.MonthlyLogistics, cmd.MonthlyOtherOpEx,
                cmd.MonthlyDepreciation,
                cmd.DailyCapacityUnits, cmd.OperatingDaysPerMonth,
                cmd.PricingModel, cmd.Notes);
            return await _repo.UpdateAsync(existing, ct).ConfigureAwait(false);
        }
    }
}
