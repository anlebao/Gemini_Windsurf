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
        Task<IReadOnlyList<MissionCompletion>> GetCompletionsByCustomerAndMissionAsync(Guid customerId, Guid missionId);
        Task<int> CountCompletionsTodayAsync(Guid customerId, Guid missionId);
        Task<int> CountCompletionsByMissionAsync(Guid customerId, Guid missionId);
        Task<int> CountCompletionsByMissionAndYearAsync(Guid customerId, Guid missionId, int year);
        Task<MissionCompletion> AddCompletionAsync(MissionCompletion completion);

        // === Save ===
        Task<int> SaveChangesAsync();
    }
}
