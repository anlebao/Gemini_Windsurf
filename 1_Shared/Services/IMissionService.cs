using VanAn.Shared.Domain;

namespace VanAn.Shared.Services
{
    /// <summary>
    /// Loyalty-C WS-B: Mission system service contract.
    /// Admin CRUD for missions + customer completion flow (with daily cap + one-time enforcement).
    /// </summary>
    public interface IMissionService
    {
        // === Admin CRUD ===
        Task<IReadOnlyList<Mission>> GetActiveMissionsAsync();
        Task<IReadOnlyList<Mission>> GetAllMissionsAsync();
        Task<Mission?> GetMissionAsync(Guid id);
        Task<Mission> CreateMissionAsync(MissionType missionType, string title, string? description, int pointsReward,
            bool isOneTime, int? dailyCap, int sortOrder, string? config);
        Task<Mission> UpdateMissionAsync(Guid id, string title, string? description, int pointsReward,
            bool isOneTime, int? dailyCap, bool isActive, int sortOrder, string? config);
        Task<bool> DeleteMissionAsync(Guid id);

        // === Customer completion ===
        Task<MissionCompletionResult> CompleteMissionAsync(Guid customerId, MissionType missionType, string? metadata = null);
        Task<MissionCompletionResult> CompleteAnnualMissionAsync(Guid customerId, MissionType missionType, string? metadata = null);
        Task<IReadOnlyList<MissionCompletion>> GetCustomerCompletionsAsync(Guid customerId);

        /// <summary>
        /// AF-P1-T3: Get customer completions paged (newest first). Returns (items, total count).
        /// page is 1-based. pageSize clamped to 1-100.
        /// </summary>
        Task<(IReadOnlyList<MissionCompletion> Items, int Total)> GetCustomerCompletionsPagedAsync(Guid customerId, int page, int pageSize);

        Task<IReadOnlyList<Mission>> GetCustomerProgressAsync(Guid customerId);
    }

    /// <summary>
    /// Result of a mission completion attempt — carries completion info on success, error reason on failure.
    /// </summary>
    public record MissionCompletionResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
        public MissionCompletion? Completion { get; init; }
        public int PointsAwarded { get; init; }
        public int NewPointBalance { get; init; }

        public static MissionCompletionResult Ok(MissionCompletion completion, int pointsAwarded, int newBalance) => new()
        {
            Success = true,
            Completion = completion,
            PointsAwarded = pointsAwarded,
            NewPointBalance = newBalance
        };

        public static MissionCompletionResult Fail(string error) => new() { Success = false, Error = error };
    }
}
