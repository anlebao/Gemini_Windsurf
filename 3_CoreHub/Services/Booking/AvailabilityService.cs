using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Booking
{
    /// <summary>
    /// AvailabilityService — MVP availability rule (SRS §11.4): active ∧ skill match ∧ working interval
    /// ∧ không break/leave/unavailable ∧ không booking conflict. KHÔNG load-balancing/solver (MVP).
    /// Slot step 30 phút; slot duration = offering.DurationMinutes (snapshot).
    /// Mọi query IgnoreQueryFilters + TenantId (bài học a21f97f2).
    /// </summary>
    public sealed class AvailabilityService(VanAnDbContext context, ILogger<AvailabilityService> logger) : IAvailabilityService
    {
        /// <summary>Slot step mặc định MVP (SRS §5.2 time slot buttons) — P5 có thể cấu hình.</summary>
        public const int SlotStepMinutes = 30;

        private static readonly BookingStatus[] ActiveStatuses =
        [
            BookingStatus.PendingConfirmation,
            BookingStatus.Confirmed,
            BookingStatus.StaffAssigned,
            BookingStatus.CheckedIn,
            BookingStatus.InService
        ];

        private readonly VanAnDbContext _context = context;
        private readonly ILogger<AvailabilityService> _logger = logger;

        public async Task<IReadOnlyList<AvailableSlot>> GetAvailableSlotsAsync(
            TenantId tenantId, Guid offeringId, DateOnly date, Guid? staffId = null, CancellationToken ct = default)
        {
            AppointmentOffering offering = await GetOfferingOrThrowAsync(tenantId, offeringId, ct);

            List<Staff> eligible = await LoadEligibleStaffAsync(tenantId, offeringId, staffId, ct);

            List<AvailableSlot> slots = [];
            foreach (Staff staff in eligible)
            {
                IEnumerable<(DateTime Start, DateTime End)> interval = await GetWorkingIntervalAsync(tenantId, staff.Id, date, ct);
                List<AvailableSlot> staffSlots = BuildSlots(staff, offering.DurationMinutes, interval, date);
                slots.AddRange(await FilterConflictsAsync(tenantId, staff.Id, staffSlots, ct));
            }

            return slots.OrderBy(s => s.StartAt).ThenBy(s => s.StaffName).ToList();
        }

        public async Task<IReadOnlyList<StaffAvailabilityStatus>> GetStaffAvailabilityMatrixAsync(
            TenantId tenantId, DateOnly date, Guid? offeringId = null, CancellationToken ct = default)
        {
            List<Staff> staffs = offeringId is null
                ? (await _context.Staffs.IgnoreQueryFilters()
                    .Where(s => s.TenantId == tenantId && s.IsActive)
                    .OrderBy(s => s.DisplayName)
                    .ToListAsync(ct))
                : await LoadEligibleStaffAsync(tenantId, offeringId.Value, null, ct);

            // Skill match: offeringId null → matrix không chấm skill (admin xem toàn bộ staff).
            List<StaffAvailabilityStatus> result = [];
            foreach (Staff staff in staffs)
            {
                IEnumerable<(DateTime Start, DateTime End)> interval = await GetWorkingIntervalAsync(tenantId, staff.Id, date, ct);
                int duration = offeringId is not null
                    ? (await GetOfferingOrThrowAsync(tenantId, offeringId.Value, ct)).DurationMinutes
                    : SlotStepMinutes;

                foreach ((DateTime start, DateTime end) in interval)
                {
                    for (DateTime slot = start; slot.AddMinutes(duration) <= end; slot = slot.AddMinutes(SlotStepMinutes))
                    {
                        var slotEnd = slot.AddMinutes(duration);
                        string? reason = await GetUnavailableReasonAsync(tenantId, staff.Id, offeringId, slot, slotEnd, ct);
                        result.Add(new StaffAvailabilityStatus(staff.Id, staff.DisplayName, slot, slotEnd, reason is null, reason));
                    }
                }
            }

            return result;
        }

        public async Task<AvailabilityCheckResult> ValidateSlotAsync(
            TenantId tenantId, Guid offeringId, Guid staffId, DateTime startAt, CancellationToken ct = default)
        {
            AppointmentOffering offering = await GetOfferingOrThrowAsync(tenantId, offeringId, ct);
            var slotEnd = startAt.AddMinutes(offering.DurationMinutes);

            // 1. Staff active + skill match (SRS §11.2).
            Staff? staff = await _context.Staffs.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == staffId && s.IsActive, ct);
            if (staff is null)
                return new AvailabilityCheckResult(false, "Staff không hoạt động hoặc không tồn tại.");

            bool hasSkill = await _context.StaffServices.IgnoreQueryFilters()
                .AnyAsync(x => x.TenantId == tenantId && x.StaffId == staffId && x.OfferingId == offeringId, ct);
            if (!hasSkill)
                return new AvailabilityCheckResult(false, "Staff không có kỹ năng phù hợp với dịch vụ này.");

            // 2. Working interval + break/leave/unavailable.
            IEnumerable<(DateTime Start, DateTime End)> intervals = await GetWorkingIntervalAsync(tenantId, staffId, DateOnly.FromDateTime(startAt), ct);
            bool insideInterval = intervals.Any(i => startAt >= i.Start && slotEnd <= i.End);
            if (!insideInterval)
                return new AvailabilityCheckResult(false, "Ngoài khung giờ làm việc của staff.");

            string? reason = await GetUnavailableReasonAsync(tenantId, staffId, offeringId, startAt, slotEnd, ct);
            if (reason is not null)
                return new AvailabilityCheckResult(false, reason);

            return new AvailabilityCheckResult(true, null);
        }

        // ── Internals ───────────────────────────────────────────────────────

        private async Task<AppointmentOffering> GetOfferingOrThrowAsync(TenantId tenantId, Guid offeringId, CancellationToken ct)
            => await _context.AppointmentOfferings.IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Id == offeringId && o.IsActive, ct)
               ?? throw new NotFoundException("Dịch vụ không tồn tại hoặc không hoạt động.");

        private async Task<List<Staff>> LoadEligibleStaffAsync(TenantId tenantId, Guid offeringId, Guid? staffId, CancellationToken ct)
        {
            IQueryable<Staff> query = from staff in _context.Staffs.IgnoreQueryFilters()
                                      join skill in _context.StaffServices.IgnoreQueryFilters()
                                          on staff.Id equals skill.StaffId
                                      where staff.TenantId == tenantId
                                            && skill.TenantId == tenantId
                                            && skill.OfferingId == offeringId
                                            && staff.IsActive
                                            && (staffId == null || staff.Id == staffId)
                                      select staff;
            return await query.Distinct().OrderBy(s => s.DisplayName).ToListAsync(ct);
        }

        /// <summary>
        /// Working interval của staff trong ngày: weekday schedule + exact-date overrides
        /// (WORKING → thay interval; LEAVE/UNAVAILABLE/BREAK → loại trừ khoảng thời gian).
        /// Trả về danh sách khoảng (có thể rỗng = không làm việc hôm đó).
        /// </summary>
        private async Task<IEnumerable<(DateTime Start, DateTime End)>> GetWorkingIntervalAsync(
            TenantId tenantId, Guid staffId, DateOnly date, CancellationToken ct)
        {
            DateTime dayStart = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            StaffWorkingSchedule? schedule = await _context.StaffWorkingSchedules.IgnoreQueryFilters()
                .FirstOrDefaultAsync(w => w.TenantId == tenantId
                                          && w.StaffId == staffId
                                          && w.Weekday == (int)date.DayOfWeek
                                          && w.IsActive, ct);

            List<StaffScheduleOverride> overrides = await _context.StaffScheduleOverrides.IgnoreQueryFilters()
                .Where(o => o.TenantId == tenantId && o.StaffId == staffId && o.Date == dayStart)
                .ToListAsync(ct);

            // LEAVE → cả ngày unavailable.
            if (overrides.Any(o => o.OverrideType == StaffScheduleOverrideType.Leave))
                return [];

            List<(DateTime Start, DateTime End)> intervals = [];

            // WORKING override thay thế interval; ngược lại dùng weekday schedule.
            StaffScheduleOverride? working = overrides.FirstOrDefault(o => o.OverrideType == StaffScheduleOverrideType.Working);
            if (working is not null && working.StartTime is not null && working.EndTime is not null)
            {
                intervals.Add((dayStart.Add(working.StartTime.Value), dayStart.Add(working.EndTime.Value)));
            }
            else if (schedule is not null)
            {
                intervals.Add((dayStart.Add(schedule.StartTime), dayStart.Add(schedule.EndTime)));
                // Break trong weekday schedule.
                if (schedule.BreakStart is not null && schedule.BreakEnd is not null)
                    intervals = CutOut(intervals, dayStart.Add(schedule.BreakStart.Value), dayStart.Add(schedule.BreakEnd.Value));
            }

            // UNAVAILABLE / BREAK override — cắt khoảng.
            foreach (StaffScheduleOverride o in overrides.Where(o => o.OverrideType is StaffScheduleOverrideType.Unavailable or StaffScheduleOverrideType.Break))
            {
                if (o.StartTime is not null && o.EndTime is not null)
                    intervals = CutOut(intervals, dayStart.Add(o.StartTime.Value), dayStart.Add(o.EndTime.Value));
            }

            return intervals;
        }

        private static List<(DateTime Start, DateTime End)> CutOut(
            IEnumerable<(DateTime Start, DateTime End)> intervals, DateTime cutStart, DateTime cutEnd)
        {
            var result = new List<(DateTime, DateTime)>();
            foreach ((DateTime start, DateTime end) in intervals)
            {
                if (cutEnd <= start || cutStart >= end)
                {
                    result.Add((start, end));
                    continue;
                }
                if (cutStart > start)
                    result.Add((start, cutStart));
                if (cutEnd < end)
                    result.Add((cutEnd, end));
            }
            return result;
        }

        private static List<AvailableSlot> BuildSlots(Staff staff, int durationMinutes, IEnumerable<(DateTime Start, DateTime End)> intervals, DateOnly date)
        {
            var slots = new List<AvailableSlot>();
            foreach ((DateTime start, DateTime end) in intervals)
            {
                for (DateTime slot = start; slot.AddMinutes(durationMinutes) <= end; slot = slot.AddMinutes(SlotStepMinutes))
                {
                    slots.Add(new AvailableSlot(slot, slot.AddMinutes(durationMinutes), staff.Id, staff.DisplayName));
                }
            }
            return slots;
        }

        /// <summary>Loại slot trùng booking conflict (active statuses) của staff.</summary>
        private async Task<List<AvailableSlot>> FilterConflictsAsync(TenantId tenantId, Guid staffId, IEnumerable<AvailableSlot> slots, CancellationToken ct)
        {
            if (!slots.Any())
                return [];

            DateTime minStart = slots.Min(s => s.StartAt);
            DateTime maxEnd = slots.Max(s => s.EndAt);

            List<(DateTime StartAt, DateTime EndAt)> ranges = await _context.Bookings.IgnoreQueryFilters()
                .Where(b => b.TenantId == tenantId
                            && b.StaffId == staffId
                            && ActiveStatuses.Contains(b.Status)
                            && b.StartAt < maxEnd
                            && b.EndAt > minStart)
                .Select(b => new { b.StartAt, b.EndAt })
                .ToListAsync(ct)
                .ContinueWith(t => t.Result.Select(r => (r.StartAt, r.EndAt)).ToList(), ct);

            return slots.Where(s => !ranges.Any(r => s.StartAt < r.EndAt && s.EndAt > r.StartAt)).ToList();
        }

        /// <summary>Lý do unavailable tối thiểu cho 1 slot (tenant admin). Null = available.</summary>
        private async Task<string?> GetUnavailableReasonAsync(
            TenantId tenantId, Guid staffId, Guid? offeringId, DateTime slotStart, DateTime slotEnd, CancellationToken ct)
        {
            // Break/leave/unavailable đã được xử lý trong GetWorkingIntervalAsync — kiểm tra interval phủ slot.
            IEnumerable<(DateTime Start, DateTime End)> intervals = await GetWorkingIntervalAsync(
                tenantId, staffId, DateOnly.FromDateTime(slotStart), ct);
            if (!intervals.Any(i => slotStart >= i.Start && slotEnd <= i.End))
                return "Ngoài khung giờ làm việc / nghỉ.";

            if (offeringId is not null)
            {
                bool hasSkill = await _context.StaffServices.IgnoreQueryFilters()
                    .AnyAsync(x => x.TenantId == tenantId && x.StaffId == staffId && x.OfferingId == offeringId, ct);
                if (!hasSkill)
                    return "Không đủ kỹ năng cho dịch vụ.";
            }

            bool conflict = await _context.Bookings.IgnoreQueryFilters()
                .AnyAsync(b => b.TenantId == tenantId
                               && b.StaffId == staffId
                               && ActiveStatuses.Contains(b.Status)
                               && b.StartAt < slotEnd
                               && b.EndAt > slotStart, ct);
            return conflict ? "Bận — đã có booking trong khung giờ." : null;
        }
    }
}
