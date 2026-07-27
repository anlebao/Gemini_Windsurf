using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Loyalty-C WS-B: Mission system service implementation.
    /// Admin CRUD for missions + customer completion flow (one-time + daily cap enforcement).
    /// Storage: ShopERP SQLite (tenant-scoped).
    /// ACID: CompleteMissionAsync wraps all steps in a single transaction via IVanAnDbContext.
    ///   AddPointsAsync uses the same DbContext (scoped DI) → nested transaction = savepoint.
    ///   If any step fails → rollback (no partial state: no completion without points, no points without completion).
    /// </summary>
    public class MissionService(
        IMissionRepository repository,
        ICustomerRepository customerRepository,
        ILoyaltyRewardsService loyaltyRewardsService,
        ITenantProvider tenantProvider,
        IVanAnDbContext dbContext,
        IShopFeatureSettingsService? shopFeatureSettingsService,
        PushNotificationService? pushNotificationService,
        ILogger<MissionService> logger) : IMissionService
    {
        private readonly IMissionRepository _repository = repository;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
        private readonly ITenantProvider _tenantProvider = tenantProvider;
        private readonly IVanAnDbContext _dbContext = dbContext;
        private readonly IShopFeatureSettingsService? _shopFeatureSettingsService = shopFeatureSettingsService;
        private readonly PushNotificationService? _pushNotificationService = pushNotificationService;
        private readonly ILogger<MissionService> _logger = logger;

        // === Admin CRUD ===

        public Task<IReadOnlyList<Mission>> GetActiveMissionsAsync()
            => _repository.GetActiveMissionsAsync();

        public Task<IReadOnlyList<Mission>> GetAllMissionsAsync()
            => _repository.GetAllMissionsAsync();

        public Task<Mission?> GetMissionAsync(Guid id)
            => _repository.GetMissionByIdAsync(id);

        public async Task<Mission> CreateMissionAsync(MissionType missionType, string title, string? description, int pointsReward,
            bool isOneTime, int? dailyCap, int sortOrder, string? config)
        {
            var mission = new Mission(new TenantId(_tenantProvider.TenantId), missionType, title, pointsReward);
            mission.UpdateDetails(title, description, pointsReward, isOneTime, dailyCap, isActive: true, sortOrder, config);
            return await _repository.AddMissionAsync(mission);
        }

        public async Task<Mission> UpdateMissionAsync(Guid id, string title, string? description, int pointsReward,
            bool isOneTime, int? dailyCap, bool isActive, int sortOrder, string? config)
        {
            var mission = await _repository.GetMissionByIdAsync(id)
                ?? throw new KeyNotFoundException($"Mission {id} not found.");
            mission.UpdateDetails(title, description, pointsReward, isOneTime, dailyCap, isActive, sortOrder, config);
            return await _repository.UpdateMissionAsync(mission);
        }

        public async Task<bool> DeleteMissionAsync(Guid id)
            => await _repository.SoftDeleteMissionAsync(id);

        // === Customer completion ===

        public async Task<MissionCompletionResult> CompleteMissionAsync(Guid customerId, MissionType missionType, string? metadata = null)
        {
            // 1. Validate mission exists + active
            var mission = await _repository.GetMissionByTypeAsync(missionType);
            if (mission == null)
            {
                _logger.LogWarning("CompleteMission failed: no active mission of type {MissionType} for tenant {TenantId}",
                    missionType, _tenantProvider.TenantId);
                return MissionCompletionResult.Fail("Nhiệm vụ không tồn tại hoặc đã bị vô hiệu hóa.");
            }

            // 2. Validate customer exists
            var customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer == null)
            {
                _logger.LogWarning("CompleteMission failed: customer {CustomerId} not found", customerId);
                return MissionCompletionResult.Fail("Khách hàng không tồn tại.");
            }

            // 3. Enforce one-time rule
            if (mission.IsOneTime)
            {
                int priorCount = await _repository.CountCompletionsByMissionAsync(customerId, mission.Id);
                if (priorCount > 0)
                {
                    _logger.LogInformation("CompleteMission skipped: one-time mission {MissionId} already completed by customer {CustomerId}",
                        mission.Id, customerId);
                    return MissionCompletionResult.Fail("Nhiệm vụ này chỉ hoàn thành được 1 lần.");
                }
            }

            // 4. Enforce daily cap (only if mission is not one-time)
            if (!mission.IsOneTime && mission.DailyCap.HasValue)
            {
                int todayCount = await _repository.CountCompletionsTodayAsync(customerId, mission.Id);
                if (todayCount >= mission.DailyCap.Value)
                {
                    _logger.LogInformation("CompleteMission skipped: daily cap {Cap} reached for mission {MissionId}, customer {CustomerId}",
                        mission.DailyCap.Value, mission.Id, customerId);
                    return MissionCompletionResult.Fail($"Đã đạt giới hạn {mission.DailyCap.Value} lần/ngày cho nhiệm vụ này.");
                }
            }

            // ACID: Wrap completion + points awarding in a single transaction.
            // AddPointsAsync uses the same IVanAnDbContext (scoped DI) → its internal
            // BeginTransactionAsync creates a savepoint within this outer transaction.
            // If any step fails → rollback (no completion record without points, no points without completion).
            await using IDbContextTransaction transaction = await _dbContext.BeginTransactionAsync();
            try
            {
                // 5. Create MissionCompletion record
                var completion = new MissionCompletion(
                    new TenantId(_tenantProvider.TenantId),
                    customerId,
                    mission.Id,
                    mission.PointsReward,
                    metadata);
                completion = await _repository.AddCompletionAsync(completion);

                // 6. Award loyalty points
                bool awarded = await _loyaltyRewardsService.AddPointsAsync(customerId, mission.PointsReward, $"Mission: {mission.Title}");
                if (!awarded)
                {
                    _logger.LogError("CompleteMission failed: AddPointsAsync returned false for customer {CustomerId}, mission {MissionId}. Rolling back.",
                        customerId, mission.Id);
                    await transaction.RollbackAsync();
                    return MissionCompletionResult.Fail("Không thể cộng điểm thưởng. Vui lòng thử lại.");
                }

                // 7. Update customer tracking fields (for share missions)
                if (missionType == MissionType.FacebookShare)
                {
                    customer.IncrementFacebookShareCount();
                    _ = await _customerRepository.UpdateAsync(customer);
                }
                else if (missionType == MissionType.TikTokShare)
                {
                    customer.IncrementTikTokShareCount();
                    _ = await _customerRepository.UpdateAsync(customer);
                }

                await transaction.CommitAsync();

                // Read new balance for response (after commit — reflects final state)
                var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customerId);
                int newBalance = rewards?.PointBalance ?? 0;

                // Loyalty-C WS-C: Send mission completed push notification (if toggle enabled)
                try
                {
                    if (_shopFeatureSettingsService != null && _pushNotificationService != null)
                    {
                        var settings = await _shopFeatureSettingsService.GetSettingsAsync(customer.TenantId);
                        if (settings.Notify_MissionCompleted)
                        {
                            _ = await _pushNotificationService.SendLoyaltyPointsChangedNotificationAsync(
                                customerId, mission.PointsReward, newBalance, $"Hoàn thành nhiệm vụ: {mission.Title}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send mission completed notification for customer {CustomerId}, mission {MissionId}",
                        customerId, mission.Id);
                }

                _logger.LogInformation("CompleteMission success: customer {CustomerId} completed mission {MissionId} ({MissionType}), awarded {Points} points. New balance: {Balance}",
                    customerId, mission.Id, missionType, mission.PointsReward, newBalance);

                return MissionCompletionResult.Ok(completion, mission.PointsReward, newBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CompleteMission failed with exception for customer {CustomerId}, mission {MissionId}. Rolling back transaction.",
                    customerId, mission.Id);
                await transaction.RollbackAsync();
                return MissionCompletionResult.Fail("Lỗi hệ thống khi hoàn thành nhiệm vụ. Vui lòng thử lại.");
            }
        }

        public Task<IReadOnlyList<MissionCompletion>> GetCustomerCompletionsAsync(Guid customerId)
            => _repository.GetCompletionsByCustomerAsync(customerId);

        public async Task<IReadOnlyList<Mission>> GetCustomerProgressAsync(Guid customerId)
        {
            // Return all active missions (UI shows progress via separate completion query)
            return await _repository.GetActiveMissionsAsync();
        }

        /// <summary>
        /// Loyalty-C WS-B: Complete an annual mission (e.g., birthday annual bonus).
        /// Enforces one-completion-per-calendar-year instead of one-time-all-time or daily cap.
        /// Used by BirthdayBonusJob to award birthday bonus points once per year.
        /// </summary>
        public async Task<MissionCompletionResult> CompleteAnnualMissionAsync(Guid customerId, MissionType missionType, string? metadata = null)
        {
            // 1. Validate mission exists + active
            var mission = await _repository.GetMissionByTypeAsync(missionType);
            if (mission == null)
            {
                _logger.LogWarning("CompleteAnnualMission failed: no active mission of type {MissionType} for tenant {TenantId}",
                    missionType, _tenantProvider.TenantId);
                return MissionCompletionResult.Fail("Nhiệm vụ không tồn tại hoặc đã bị vô hiệu hóa.");
            }

            // 2. Validate customer exists
            var customer = await _customerRepository.GetByIdAsync(customerId);
            if (customer == null)
            {
                _logger.LogWarning("CompleteAnnualMission failed: customer {CustomerId} not found", customerId);
                return MissionCompletionResult.Fail("Khách hàng không tồn tại.");
            }

            // 3. Enforce annual rule: max 1 completion per calendar year
            int currentYear = DateTime.UtcNow.Year;
            int yearCount = await _repository.CountCompletionsByMissionAndYearAsync(customerId, mission.Id, currentYear);
            if (yearCount > 0)
            {
                _logger.LogInformation("CompleteAnnualMission skipped: mission {MissionId} already completed by customer {CustomerId} in year {Year}",
                    mission.Id, customerId, currentYear);
                return MissionCompletionResult.Fail($"Nhiệm vụ này đã được hoàn thành trong năm {currentYear}.");
            }

            // ACID: Wrap completion + points awarding in a single transaction.
            await using IDbContextTransaction transaction = await _dbContext.BeginTransactionAsync();
            try
            {
                // 4. Create MissionCompletion record
                var completion = new MissionCompletion(
                    new TenantId(_tenantProvider.TenantId),
                    customerId,
                    mission.Id,
                    mission.PointsReward,
                    metadata);
                completion = await _repository.AddCompletionAsync(completion);

                // 5. Award loyalty points
                bool awarded = await _loyaltyRewardsService.AddPointsAsync(customerId, mission.PointsReward, $"Annual mission: {mission.Title} ({currentYear})");
                if (!awarded)
                {
                    _logger.LogError("CompleteAnnualMission failed: AddPointsAsync returned false for customer {CustomerId}, mission {MissionId}. Rolling back.",
                        customerId, mission.Id);
                    await transaction.RollbackAsync();
                    return MissionCompletionResult.Fail("Không thể cộng điểm thưởng. Vui lòng thử lại.");
                }

                await transaction.CommitAsync();

                // Read new balance for response
                var rewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(customerId);
                int newBalance = rewards?.PointBalance ?? 0;

                _logger.LogInformation("CompleteAnnualMission success: customer {CustomerId} completed mission {MissionId} ({MissionType}) for year {Year}, awarded {Points} points. New balance: {Balance}",
                    customerId, mission.Id, missionType, currentYear, mission.PointsReward, newBalance);

                return MissionCompletionResult.Ok(completion, mission.PointsReward, newBalance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CompleteAnnualMission failed with exception for customer {CustomerId}, mission {MissionId}. Rolling back transaction.",
                    customerId, mission.Id);
                await transaction.RollbackAsync();
                return MissionCompletionResult.Fail("Lỗi hệ thống khi hoàn thành nhiệm vụ. Vui lòng thử lại.");
            }
        }
    }
}
