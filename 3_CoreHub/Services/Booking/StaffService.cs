using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
// Domain junction entity "StaffService" (Staff ↔ Offering) trùng tên service class → alias.
using StaffServiceEntity = VanAn.Shared.Domain.StaffService;

namespace VanAn.CoreHub.Services.Booking
{
    /// <summary>
    /// StaffService — CRUD Staff + skills + schedule + override (SRS §11.1-11.3).
    /// Multi-tenancy: mọi SELECT dùng IgnoreQueryFilters + WHERE TenantId == tenantId
    /// (service nhận tenantId tường minh từ caller — QR resolve / authenticated context, KHÔNG tin client).
    /// Booking data sống ở Gateway PG (D1) — DbContext = VanAnDbContext concrete.
    /// </summary>
    public sealed class StaffService(VanAnDbContext context, ILogger<StaffService> logger) : IStaffService
    {
        private readonly VanAnDbContext _context = context;
        private readonly ILogger<StaffService> _logger = logger;

        public async Task<Staff> CreateStaffAsync(TenantId tenantId, string displayName, string? role = null, Guid? staffUserId = null, CancellationToken ct = default)
        {
            var staff = new Staff(tenantId, displayName, role, staffUserId);
            _context.Staffs.Add(staff);
            _ = await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Staff created: {StaffId} tenant={TenantId}", staff.Id, tenantId.Value);
            return staff;
        }

        public async Task<Staff> UpdateStaffAsync(
            TenantId tenantId, Guid staffId, string displayName, string? role,
            string? avatarUrl, Guid? staffUserId, bool isActive, CancellationToken ct = default)
        {
            Staff staff = await GetStaffOrThrowAsync(tenantId, staffId, ct);
            staff.Update(displayName, role, avatarUrl, staffUserId, isActive);
            _ = await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Staff updated: {StaffId} tenant={TenantId}", staff.Id, tenantId.Value);
            return staff;
        }

        public async Task<Staff?> GetStaffAsync(TenantId tenantId, Guid staffId, CancellationToken ct = default)
            => await _context.Staffs
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == staffId, ct);

        public async Task<IReadOnlyList<Staff>> ListStaffAsync(TenantId tenantId, bool activeOnly = false, CancellationToken ct = default)
        {
            IQueryable<Staff> query = _context.Staffs
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId);
            if (activeOnly)
                query = query.Where(s => s.IsActive);
            return await query.OrderBy(s => s.DisplayName).ToListAsync(ct);
        }

        public async Task<IReadOnlyList<Staff>> ListEligibleStaffAsync(TenantId tenantId, Guid offeringId, bool activeOnly = true, CancellationToken ct = default)
        {
            // Skill match qua StaffService junction (SRS §11.2) — staff active + có junction row.
            IQueryable<Staff> query = from staff in _context.Staffs.IgnoreQueryFilters()
                                      join skill in _context.StaffServices.IgnoreQueryFilters()
                                          on staff.Id equals skill.StaffId
                                      where staff.TenantId == tenantId
                                            && skill.TenantId == tenantId
                                            && skill.OfferingId == offeringId
                                            && (!activeOnly || staff.IsActive)
                                      select staff;
            return await query.Distinct().OrderBy(s => s.DisplayName).ToListAsync(ct);
        }

        public async Task<IReadOnlyList<StaffServiceEntity>> ListStaffServicesAsync(TenantId tenantId, Guid staffId, CancellationToken ct = default)
            => await _context.StaffServices
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.StaffId == staffId)
                .ToListAsync(ct);

        public async Task SetStaffServicesAsync(TenantId tenantId, Guid staffId, IReadOnlyCollection<Guid> offeringIds, CancellationToken ct = default)
        {
            // Staff phải tồn tại trong tenant trước khi gán skill.
            _ = await GetStaffOrThrowAsync(tenantId, staffId, ct);

            List<StaffServiceEntity> existing = await _context.StaffServices
                .IgnoreQueryFilters()
                .Where(x => x.TenantId == tenantId && x.StaffId == staffId)
                .ToListAsync(ct);

            HashSet<Guid> target = offeringIds.ToHashSet();
            _context.StaffServices.RemoveRange(existing.Where(e => !target.Contains(e.OfferingId)));

            HashSet<Guid> current = existing.Select(e => e.OfferingId).ToHashSet();
            foreach (Guid offeringId in target.Where(id => !current.Contains(id)))
            {
                _context.StaffServices.Add(new StaffServiceEntity(tenantId, staffId, offeringId));
            }

            _ = await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Staff services replaced: {StaffId} count={Count} tenant={TenantId}", staffId, target.Count, tenantId.Value);
        }

        public async Task<StaffWorkingSchedule> UpsertWorkingScheduleAsync(
            TenantId tenantId, Guid staffId, int weekday, TimeSpan startTime, TimeSpan endTime,
            TimeSpan? breakStart = null, TimeSpan? breakEnd = null, CancellationToken ct = default)
        {
            _ = await GetStaffOrThrowAsync(tenantId, staffId, ct);

            StaffWorkingSchedule? existing = await _context.StaffWorkingSchedules
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.StaffId == staffId && w.Weekday == weekday, ct);

            if (existing is null)
            {
                existing = new StaffWorkingSchedule(tenantId, staffId, weekday, startTime, endTime, breakStart, breakEnd);
                _context.StaffWorkingSchedules.Add(existing);
            }
            else
            {
                existing.Update(startTime, endTime, breakStart, breakEnd, isActive: true);
            }

            _ = await _context.SaveChangesAsync(ct);
            return existing;
        }

        public async Task<IReadOnlyList<StaffWorkingSchedule>> ListWorkingSchedulesAsync(TenantId tenantId, Guid staffId, CancellationToken ct = default)
        {
            // SQLite không hỗ trợ ORDER BY TimeSpan → sort client-side.
            List<StaffWorkingSchedule> schedules = await _context.StaffWorkingSchedules
                .IgnoreQueryFilters()
                .Where(w => w.TenantId == tenantId && w.StaffId == staffId && w.IsActive)
                .ToListAsync(ct);
            return schedules
                .OrderBy(w => w.Weekday)
                .ThenBy(w => w.StartTime)
                .ToList();
        }

        public async Task DeleteWorkingScheduleAsync(TenantId tenantId, Guid scheduleId, CancellationToken ct = default)
        {
            StaffWorkingSchedule? schedule = await _context.StaffWorkingSchedules
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == scheduleId, ct)
                ?? throw new NotFoundException("Lịch làm việc không tồn tại.");
            _context.StaffWorkingSchedules.Remove(schedule);
            _ = await _context.SaveChangesAsync(ct);
        }

        public async Task<StaffScheduleOverride> AddOverrideAsync(
            TenantId tenantId, Guid staffId, DateTime date, StaffScheduleOverrideType overrideType,
            TimeSpan? startTime = null, TimeSpan? endTime = null, string? note = null, CancellationToken ct = default)
        {
            _ = await GetStaffOrThrowAsync(tenantId, staffId, ct);

            var overrideRow = new StaffScheduleOverride(tenantId, staffId, date, overrideType, startTime, endTime, note);
            _context.StaffScheduleOverrides.Add(overrideRow);
            _ = await _context.SaveChangesAsync(ct);
            return overrideRow;
        }

        public async Task<IReadOnlyList<StaffScheduleOverride>> ListOverridesAsync(TenantId tenantId, Guid staffId, DateTime from, DateTime to, CancellationToken ct = default)
            => await _context.StaffScheduleOverrides
                .IgnoreQueryFilters()
                .Where(o => o.TenantId == tenantId
                            && o.StaffId == staffId
                            && o.Date >= from.Date
                            && o.Date <= to.Date)
                .OrderBy(o => o.Date)
                .ToListAsync(ct);

        public async Task DeleteOverrideAsync(TenantId tenantId, Guid overrideId, CancellationToken ct = default)
        {
            StaffScheduleOverride? overrideRow = await _context.StaffScheduleOverrides
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == overrideId, ct)
                ?? throw new NotFoundException("Override lịch không tồn tại.");
            _context.StaffScheduleOverrides.Remove(overrideRow);
            _ = await _context.SaveChangesAsync(ct);
        }

        private async Task<Staff> GetStaffOrThrowAsync(TenantId tenantId, Guid staffId, CancellationToken ct)
            => await GetStaffAsync(tenantId, staffId, ct)
               ?? throw new NotFoundException("Staff không tồn tại trong tenant này.");
    }
}
