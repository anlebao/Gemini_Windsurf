using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.Repositories
{
    /// <summary>
    /// Loyalty-C WS-B: EF Core implementation of IMissionRepository.
    /// ShopERP SQLite (tenant-scoped). Always filters by tenant + soft-delete.
    /// </summary>
    public class MissionRepository(IVanAnDbContext context) : IMissionRepository
    {
        private readonly IVanAnDbContext _context = context;
        private readonly Guid _currentTenantId = context is VanAnDbContext vanAnContext ? vanAnContext.CurrentTenantId : Guid.Empty;

        // === Missions (admin CRUD) ===

        public async Task<Mission?> GetMissionByIdAsync(Guid id)
        {
            return await _context.Missions
                .Where(m => m.Id == id && !m.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<Mission>> GetActiveMissionsAsync()
        {
            return await _context.Missions
                .Where(m => !m.IsDeleted && m.IsActive)
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.MissionType)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Mission>> GetAllMissionsAsync()
        {
            return await _context.Missions
                .Where(m => !m.IsDeleted)
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.MissionType)
                .ToListAsync();
        }

        public async Task<Mission?> GetMissionByTypeAsync(MissionType missionType)
        {
            return await _context.Missions
                .Where(m => m.MissionType == missionType && !m.IsDeleted && m.IsActive)
                .FirstOrDefaultAsync();
        }

        public async Task<Mission> AddMissionAsync(Mission mission)
        {
            _ = await _context.Missions.AddAsync(mission);
            _ = await _context.SaveChangesAsync();
            return mission;
        }

        public async Task<Mission> UpdateMissionAsync(Mission mission)
        {
            _context.Missions.Update(mission);
            _ = await _context.SaveChangesAsync();
            return mission;
        }

        public async Task<bool> SoftDeleteMissionAsync(Guid id)
        {
            Mission? mission = await _context.Missions
                .Where(m => m.Id == id && !m.IsDeleted)
                .FirstOrDefaultAsync();
            if (mission == null) return false;
            mission.SoftDelete();
            _ = await _context.SaveChangesAsync();
            return true;
        }

        // === Mission Completions ===

        public async Task<MissionCompletion?> GetCompletionByIdAsync(Guid id)
        {
            return await _context.MissionCompletions
                .Where(c => c.Id == id && !c.IsDeleted)
                .FirstOrDefaultAsync();
        }

        public async Task<IReadOnlyList<MissionCompletion>> GetCompletionsByCustomerAsync(Guid customerId)
        {
            return await _context.MissionCompletions
                .Where(c => c.CustomerId == customerId && !c.IsDeleted)
                .OrderByDescending(c => c.CompletedAt)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<MissionCompletion>> GetCompletionsByCustomerAndMissionAsync(Guid customerId, Guid missionId)
        {
            return await _context.MissionCompletions
                .Where(c => c.CustomerId == customerId && c.MissionId == missionId && !c.IsDeleted)
                .OrderByDescending(c => c.CompletedAt)
                .ToListAsync();
        }

        public async Task<int> CountCompletionsTodayAsync(Guid customerId, Guid missionId)
        {
            DateTime todayUtc = DateTime.UtcNow.Date;
            DateTime tomorrowUtc = todayUtc.AddDays(1);
            return await _context.MissionCompletions
                .CountAsync(c => c.CustomerId == customerId
                    && c.MissionId == missionId
                    && !c.IsDeleted
                    && c.CompletedAt >= todayUtc
                    && c.CompletedAt < tomorrowUtc);
        }

        public async Task<int> CountCompletionsByMissionAsync(Guid customerId, Guid missionId)
        {
            return await _context.MissionCompletions
                .CountAsync(c => c.CustomerId == customerId
                    && c.MissionId == missionId
                    && !c.IsDeleted);
        }

        public async Task<int> CountCompletionsByMissionAndYearAsync(Guid customerId, Guid missionId, int year)
        {
            DateTime yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime yearEnd = new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return await _context.MissionCompletions
                .CountAsync(c => c.CustomerId == customerId
                    && c.MissionId == missionId
                    && !c.IsDeleted
                    && c.CompletedAt >= yearStart
                    && c.CompletedAt < yearEnd);
        }

        public async Task<MissionCompletion> AddCompletionAsync(MissionCompletion completion)
        {
            _ = await _context.MissionCompletions.AddAsync(completion);
            _ = await _context.SaveChangesAsync();
            return completion;
        }

        // === Save ===

        public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
    }
}
