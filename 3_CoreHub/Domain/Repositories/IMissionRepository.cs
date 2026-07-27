using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Domain.Repositories
{
    /// <summary>
    /// Loyalty-C WS-B: Repository for gamification system (missions + completions).
    /// ShopERP SQLite (tenant-scoped).
    /// </summary>
    public interface IMissionRepository
    {
        // === Missions (admin CRUD) ===
        Task<Mission?> GetMissionByIdAsync(Guid id);
        Task<IReadOnlyList<Mission>> GetActiveMissionsAsync();
        Task<IReadOnlyList<Mission>> GetAllMissionsAsync();
        Task<Mission?> GetMissionByTypeAsync(MissionType missionType);
        Task<Mission> AddMissionAsync(Mission mission);
        Task<Mission> UpdateMissionAsync(Mission mission);
        Task<bool> SoftDeleteMissionAsync(Guid id);

        // === Mission Completions ===
        Task<MissionCompletion?> GetCompletionByIdAsync(Guid id);
        Task<IReadOnlyList<MissionCompletion>> GetCompletionsByCustomerAsync(Guid customerId);

        /// <summary>
        /// AF-P1-T3: Get customer completions paged (newest first). Returns (items, total count).
        /// page is 1-based. pageSize clamped to 1-100.
        /// </summary>
        Task<(IReadOnlyList<MissionCompletion> Items, int Total)> GetCompletionsByCustomerPagedAsync(Guid customerId, int page, int pageSize);

        Task<IReadOnlyList<MissionCompletion>> GetCompletionsByCustomerAndMissionAsync(Guid customerId, Guid missionId);
        Task<int> CountCompletionsTodayAsync(Guid customerId, Guid missionId);
        Task<int> CountCompletionsByMissionAsync(Guid customerId, Guid missionId);
        Task<int> CountCompletionsByMissionAndYearAsync(Guid customerId, Guid missionId, int year);
        Task<MissionCompletion> AddCompletionAsync(MissionCompletion completion);

        // === Save ===
        Task<int> SaveChangesAsync();
    }
}
