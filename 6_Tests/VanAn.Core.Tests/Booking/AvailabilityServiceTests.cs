using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
// Domain junction entity "StaffService" trùng tên service class → alias trong test.
using StaffServiceEntity = VanAn.Shared.Domain.StaffService;
// Services layer exceptions (Exceptions.cs) — KHÁC VanAn.Shared.Domain.NotFoundException.
using NotFoundException = VanAn.CoreHub.Services.NotFoundException;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// IAvailabilityService — MVP availability rule (SRS §11.4): active ∧ skill match ∧ working interval
    /// ∧ không break/leave/unavailable ∧ không booking conflict. Slot step 30 phút.
    /// Schedule mẫu: Monday 09:00-17:00, break 12:00-13:00, offering 60 phút.
    /// </summary>
    public class AvailabilityServiceTests
    {
        private static AvailabilityService BuildService(VanAnDbContext ctx) => new(ctx, NullLogger<AvailabilityService>.Instance);

        private static async Task<(Guid StaffId, Guid OfferingId)> SeedAsync(
            VanAnDbContext ctx, TenantId tenantId, int durationMinutes = 60, bool withSchedule = true, string staffName = "KTV A")
            => await BookingTestData.SeedStaffOfferingAsync(ctx, tenantId, durationMinutes, 300_000m, staffName, withSchedule);

        [Fact]
        public async Task GetAvailableSlots_GeneratesSlotsWithinWorkingInterval_ExcludingBreak()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            var (staffId, offeringId) = await SeedAsync(ctx, BookingTestData.TenantId);

            var slots = await svc.GetAvailableSlotsAsync(BookingTestData.TenantId, offeringId, BookingTestData.TestMonday);

            // 09:00..11:00 (5: slot phải KẾT THÚC trước break 12:00) + 13:00..16:00 (7) = 12 slots.
            Assert.Equal(12, slots.Count);
            Assert.All(slots, s => Assert.Equal(staffId, s.StaffId));
            Assert.All(slots, s => Assert.True(s.StartAt.TimeOfDay < new TimeSpan(12, 0, 0) || s.StartAt.TimeOfDay >= new TimeSpan(13, 0, 0)));
            Assert.DoesNotContain(slots, s => s.StartAt.Hour is 12);
            Assert.Contains(slots, s => s.StartAt.Hour == 13);
            Assert.Equal(new DateTime(2026, 10, 12, 16, 0, 0, DateTimeKind.Utc), slots[^1].StartAt);
        }

        [Fact]
        public async Task GetAvailableSlots_StaffWithoutSkill_Excluded()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            var (_, offeringId) = await SeedAsync(ctx, BookingTestData.TenantId);
            await BookingTestData.SeedStaffNoSkillAsync(ctx, BookingTestData.TenantId);

            var slots = await svc.GetAvailableSlotsAsync(BookingTestData.TenantId, offeringId, BookingTestData.TestMonday);

            // Staff có skill → 12 slots; staff không skill không xuất hiện.
            Assert.Equal(12, slots.Count);
            Assert.All(slots, s => Assert.Equal("KTV A", s.StaffName));
        }

        [Fact]
        public async Task GetAvailableSlots_InactiveStaff_Excluded()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            var (staffId, offeringId) = await SeedAsync(ctx, BookingTestData.TenantId);

            var staff = await ctx.Staffs.IgnoreQueryFilters().FirstAsync(s => s.Id == staffId);
            staff.Deactivate();
            await ctx.SaveChangesAsync();

            var slots = await svc.GetAvailableSlotsAsync(BookingTestData.TenantId, offeringId, BookingTestData.TestMonday);
            Assert.Empty(slots);
        }

        [Fact]
        public async Task GetAvailableSlots_LeaveOverride_NoSlots()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            var (staffId, offeringId) = await SeedAsync(ctx, BookingTestData.TenantId);

            ctx.StaffScheduleOverrides.Add(new StaffScheduleOverride(
                BookingTestData.TenantId, staffId, BookingTestData.At(0, 0), StaffScheduleOverrideType.Leave));
            await ctx.SaveChangesAsync();

            var slots = await svc.GetAvailableSlotsAsync(BookingTestData.TenantId, offeringId, BookingTestData.TestMonday);
            Assert.Empty(slots);
        }

        [Fact]
        public async Task GetAvailableSlots_WorkingOverride_ReplacesInterval()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            var (staffId, offeringId) = await SeedAsync(ctx, BookingTestData.TenantId);

            // Working override 10:00-12:00 thay interval mặc định 09:00-17:00 (không break).
            ctx.StaffScheduleOverrides.Add(new StaffScheduleOverride(
                BookingTestData.TenantId, staffId, BookingTestData.At(0, 0), StaffScheduleOverrideType.Working,
                new TimeSpan(10, 0, 0), new TimeSpan(12, 0, 0)));
            await ctx.SaveChangesAsync();

            var slots = await svc.GetAvailableSlotsAsync(BookingTestData.TenantId, offeringId, BookingTestData.TestMonday);
            Assert.Equal(3, slots.Count); // 10:00, 10:30, 11:00
            Assert.All(slots, s => Assert.True(s.StartAt.TimeOfDay >= new TimeSpan(10, 0, 0)));
        }

        [Fact]
        public async Task GetAvailableSlots_UnavailableOverride_CutsInterval()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            var (staffId, offeringId) = await SeedAsync(ctx, BookingTestData.TenantId);

            // Unavailable 14:00-15:00 → cắt 2 slot (14:00, 14:30).
            ctx.StaffScheduleOverrides.Add(new StaffScheduleOverride(
                BookingTestData.TenantId, staffId, BookingTestData.At(0, 0), StaffScheduleOverrideType.Unavailable,
                new TimeSpan(14, 0, 0), new TimeSpan(15, 0, 0)));
            await ctx.SaveChangesAsync();

            var slots = await svc.GetAvailableSlotsAsync(BookingTestData.TenantId, offeringId, BookingTestData.TestMonday);
            Assert.Equal(9, slots.Count); // 12 - 3 slot (13:30/14:00/14:30)
            Assert.DoesNotContain(slots, s => s.StartAt.Hour == 14);
        }

        [Fact]
        public async Task GetAvailableSlots_BookingConflict_ExcludesOverlappingSlots()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            var (staffId, offeringId) = await SeedAsync(ctx, BookingTestData.TenantId);

            // Booking 14:00-15:00 (active) → loại slot 13:30 (13:30-14:30 overlap), 14:00, 14:30; giữ 13:00.
            var booking = new VanAn.Shared.Domain.Booking(
                BookingTestData.TenantId, "CODE00000000000001", offeringId, "Massage 60m", 60, 300_000m,
                BookingTestData.At(14, 0), BookingTestData.At(15, 0), 300_000m, customerDeviceId: "device-x");
            ctx.Bookings.Add(booking);
            await ctx.SaveChangesAsync();
            booking.AssignStaff(staffId);
            await ctx.SaveChangesAsync();

            var slots = await svc.GetAvailableSlotsAsync(BookingTestData.TenantId, offeringId, BookingTestData.TestMonday);

            Assert.DoesNotContain(slots, s => s.StartAt == BookingTestData.At(13, 30));
            Assert.DoesNotContain(slots, s => s.StartAt == BookingTestData.At(14, 0));
            Assert.DoesNotContain(slots, s => s.StartAt == BookingTestData.At(14, 30));
            Assert.Contains(slots, s => s.StartAt == BookingTestData.At(13, 0));
            Assert.Equal(9, slots.Count); // 12 - 3 bị loại
        }

        [Fact]
        public async Task GetAvailableSlots_StaffFilter_OnlyThatStaff()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);

            // 1 offering + 2 staff cùng skill (junction) + schedule cho cả 2.
            var offering = new AppointmentOffering(BookingTestData.TenantId, "Massage 60m", OfferingType.Service, 60, 300_000m);
            ctx.AppointmentOfferings.Add(offering);
            var staffA = new Staff(BookingTestData.TenantId, "KTV A", "Technician");
            var staffB = new Staff(BookingTestData.TenantId, "KTV B", "Technician");
            ctx.Staffs.AddRange(staffA, staffB);
            await ctx.SaveChangesAsync();
            ctx.StaffServices.AddRange(
                new StaffServiceEntity(BookingTestData.TenantId, staffA.Id, offering.Id),
                new StaffServiceEntity(BookingTestData.TenantId, staffB.Id, offering.Id));
            ctx.StaffWorkingSchedules.AddRange(
                new StaffWorkingSchedule(BookingTestData.TenantId, staffA.Id, (int)DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0)),
                new StaffWorkingSchedule(BookingTestData.TenantId, staffB.Id, (int)DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0), new TimeSpan(12, 0, 0), new TimeSpan(13, 0, 0)));
            await ctx.SaveChangesAsync();

            var slots = await svc.GetAvailableSlotsAsync(BookingTestData.TenantId, offering.Id, BookingTestData.TestMonday, staffId: staffB.Id);

            Assert.NotEqual(staffA.Id, staffB.Id);
            Assert.All(slots, s => Assert.Equal(staffB.Id, s.StaffId));
            Assert.Equal(12, slots.Count);
        }

        [Fact]
        public async Task ValidateSlot_Available_Unavailable_Conflict()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            var (staffId, offeringId) = await SeedAsync(ctx, BookingTestData.TenantId);

            // Available.
            var ok = await svc.ValidateSlotAsync(BookingTestData.TenantId, offeringId, staffId, BookingTestData.At(14, 0));
            Assert.True(ok.IsAvailable);

            // Ngoài interval (07:00) → unavailable.
            var early = await svc.ValidateSlotAsync(BookingTestData.TenantId, offeringId, staffId, BookingTestData.At(7, 0));
            Assert.False(early.IsAvailable);

            // Trong break (12:15) → unavailable.
            var breakTime = await svc.ValidateSlotAsync(BookingTestData.TenantId, offeringId, staffId, BookingTestData.At(12, 15));
            Assert.False(breakTime.IsAvailable);

            // Staff không skill → unavailable.
            var noSkill = await BookingTestData.SeedStaffNoSkillAsync(ctx, BookingTestData.TenantId);
            var noSkillCheck = await svc.ValidateSlotAsync(BookingTestData.TenantId, offeringId, noSkill, BookingTestData.At(14, 0));
            Assert.False(noSkillCheck.IsAvailable);
            Assert.Contains("kỹ năng", noSkillCheck.UnavailableReason);

            // Conflict với booking đã có.
            var booking = new VanAn.Shared.Domain.Booking(
                BookingTestData.TenantId, "CODE00000000000002", offeringId, "Massage 60m", 60, 300_000m,
                BookingTestData.At(14, 0), BookingTestData.At(15, 0), 300_000m, customerDeviceId: "device-y");
            ctx.Bookings.Add(booking);
            await ctx.SaveChangesAsync();
            booking.AssignStaff(staffId);
            await ctx.SaveChangesAsync();

            var conflict = await svc.ValidateSlotAsync(BookingTestData.TenantId, offeringId, staffId, BookingTestData.At(14, 0));
            Assert.False(conflict.IsAvailable);
        }

        [Fact]
        public async Task GetStaffAvailabilityMatrix_ReturnsReasons()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            var (staffId, offeringId) = await SeedAsync(ctx, BookingTestData.TenantId);

            // Booking 14:00-15:00 → matrix đánh dấu 13:30/14:00/14:30 Busy.
            var booking = new VanAn.Shared.Domain.Booking(
                BookingTestData.TenantId, "CODE00000000000003", offeringId, "Massage 60m", 60, 300_000m,
                BookingTestData.At(14, 0), BookingTestData.At(15, 0), 300_000m, customerDeviceId: "device-z");
            ctx.Bookings.Add(booking);
            await ctx.SaveChangesAsync();
            booking.AssignStaff(staffId);
            await ctx.SaveChangesAsync();

            var matrix = await svc.GetStaffAvailabilityMatrixAsync(BookingTestData.TenantId, BookingTestData.TestMonday, offeringId);

            Assert.Equal(12, matrix.Count);
            Assert.Equal(9, matrix.Count(m => m.IsAvailable));
            Assert.Equal(3, matrix.Count(m => !m.IsAvailable));
            Assert.Contains(matrix, m => !m.IsAvailable && m.UnavailableReason!.Contains("Bận"));
        }

        [Fact]
        public async Task GetAvailableSlots_OtherTenant_NoCrossTenantLeak()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            var (staffId, offeringId) = await SeedAsync(ctx, BookingTestData.TenantId);

            // Offering của tenant A KHÔNG resolve được từ tenant B → NotFound (không leak, không trả dữ liệu A).
            await Assert.ThrowsAsync<NotFoundException>(() =>
                svc.GetAvailableSlotsAsync(BookingTestData.OtherTenantId, offeringId, BookingTestData.TestMonday));
        }
    }
}
