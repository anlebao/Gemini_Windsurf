using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
// Domain junction entity "StaffService" trùng tên service class → alias trong test.
using StaffServiceSvc = VanAn.CoreHub.Services.Booking.StaffService;
// Services layer exceptions (Exceptions.cs) — KHÁC VanAn.Shared.Domain.ValidationException.
using ValidationException = VanAn.CoreHub.Services.ValidationException;
using NotFoundException = VanAn.CoreHub.Services.NotFoundException;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// IStaffService — CRUD staff + skills + working schedule + override (SRS §11.1-11.3).
    /// Mọi query filter TenantId (lesson a21f97f2) — test isolation negative.
    /// </summary>
    public class StaffServiceTests
    {
        private static StaffServiceSvc BuildService(VanAnDbContext ctx) => new(ctx, NullLogger<StaffServiceSvc>.Instance);

        [Fact]
        public async Task Create_And_Get_Staff()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);

            var staff = await svc.CreateStaffAsync(BookingTestData.TenantId, "KTV Hoa", "Technician", staffUserId: null);

            Assert.NotEqual(Guid.Empty, staff.Id);
            Assert.Equal(staff.Id, staff.StaffId.Value); // Single-Identity
            Assert.Equal("KTV Hoa", staff.DisplayName);
            Assert.True(staff.IsActive);

            var loaded = await svc.GetStaffAsync(BookingTestData.TenantId, staff.Id);
            Assert.NotNull(loaded);
            Assert.Equal(staff.Id, loaded!.Id);
        }

        [Fact]
        public async Task UpdateStaff_ChangesFields_And_Deactivate()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);
            var staff = await svc.CreateStaffAsync(BookingTestData.TenantId, "Cũ");

            var updated = await svc.UpdateStaffAsync(BookingTestData.TenantId, staff.Id, "Mới", "Doctor", "http://avatar", null, isActive: false);

            Assert.Equal("Mới", updated.DisplayName);
            Assert.Equal("Doctor", updated.Role);
            Assert.False(updated.IsActive);
        }

        [Fact]
        public async Task ListStaff_ActiveOnly_FiltersInactive()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);
            var active = await svc.CreateStaffAsync(BookingTestData.TenantId, "Active 1");
            var inactive = await svc.CreateStaffAsync(BookingTestData.TenantId, "Inactive 1");
            await svc.UpdateStaffAsync(BookingTestData.TenantId, inactive.Id, "Inactive 1", null, null, null, isActive: false);

            var all = await svc.ListStaffAsync(BookingTestData.TenantId, activeOnly: false);
            Assert.Equal(2, all.Count);

            var activeOnly = await svc.ListStaffAsync(BookingTestData.TenantId, activeOnly: true);
            Assert.Single(activeOnly);
            Assert.Equal(active.Id, activeOnly[0].Id);
        }

        [Fact]
        public async Task SetStaffServices_ReplacesSkills_And_ListEligibleMatches()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);

            var staff = await svc.CreateStaffAsync(BookingTestData.TenantId, "KTV Skill");
            var offeringA = new AppointmentOffering(BookingTestData.TenantId, "Massage 60m", OfferingType.Service, 60, 300_000m);
            var offeringB = new AppointmentOffering(BookingTestData.TenantId, "Massage 90m", OfferingType.Service, 90, 450_000m);
            ctx.AppointmentOfferings.AddRange(offeringA, offeringB);
            await ctx.SaveChangesAsync();

            await svc.SetStaffServicesAsync(BookingTestData.TenantId, staff.Id, [offeringA.Id, offeringB.Id]);
            var skills = await svc.ListStaffServicesAsync(BookingTestData.TenantId, staff.Id);
            Assert.Equal(2, skills.Count);

            var eligible = await svc.ListEligibleStaffAsync(BookingTestData.TenantId, offeringA.Id);
            Assert.Contains(eligible, s => s.Id == staff.Id);

            // Replace: bỏ offeringA, giữ offeringB.
            await svc.SetStaffServicesAsync(BookingTestData.TenantId, staff.Id, [offeringB.Id]);
            var skills2 = await svc.ListStaffServicesAsync(BookingTestData.TenantId, staff.Id);
            Assert.Single(skills2);
            Assert.Equal(offeringB.Id, skills2[0].OfferingId);

            var eligibleAfter = await svc.ListEligibleStaffAsync(BookingTestData.TenantId, offeringA.Id);
            Assert.DoesNotContain(eligibleAfter, s => s.Id == staff.Id);
        }

        [Fact]
        public async Task UpsertWorkingSchedule_SameWeekday_UpdatesInPlace()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);
            var staff = await svc.CreateStaffAsync(BookingTestData.TenantId, "KTV Lịch");

            var created = await svc.UpsertWorkingScheduleAsync(
                BookingTestData.TenantId, staff.Id, (int)DayOfWeek.Monday,
                new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0), new TimeSpan(12, 0, 0), new TimeSpan(12, 30, 0));

            var updated = await svc.UpsertWorkingScheduleAsync(
                BookingTestData.TenantId, staff.Id, (int)DayOfWeek.Monday,
                new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0));

            Assert.Equal(created.Id, updated.Id); // upsert cùng row
            Assert.Equal(new TimeSpan(9, 0, 0), updated.StartTime);
            Assert.Null(updated.BreakStart);

            var schedules = await svc.ListWorkingSchedulesAsync(BookingTestData.TenantId, staff.Id);
            Assert.Single(schedules);
        }

        [Fact]
        public async Task Overrides_Add_List_Delete_ByDateRange()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);
            var staff = await svc.CreateStaffAsync(BookingTestData.TenantId, "KTV Override");

            var leave = await svc.AddOverrideAsync(BookingTestData.TenantId, staff.Id, BookingTestData.At(0, 0), StaffScheduleOverrideType.Leave);
            var working = await svc.AddOverrideAsync(
                BookingTestData.TenantId, staff.Id, BookingTestData.At(0, 0, day: 13),
                StaffScheduleOverrideType.Working, new TimeSpan(10, 0, 0), new TimeSpan(14, 0, 0));

            var listed = await svc.ListOverridesAsync(BookingTestData.TenantId, staff.Id, BookingTestData.At(0, 0, day: 11), BookingTestData.At(0, 0, day: 14));
            Assert.Equal(2, listed.Count);

            await svc.DeleteOverrideAsync(BookingTestData.TenantId, leave.Id);
            var afterDelete = await svc.ListOverridesAsync(BookingTestData.TenantId, staff.Id, BookingTestData.At(0, 0, day: 11), BookingTestData.At(0, 0, day: 14));
            Assert.Single(afterDelete);
            Assert.Equal(working.Id, afterDelete[0].Id);
        }

        [Fact]
        public async Task TenantIsolation_StaffOfTenantA_InvisibleFromTenantB()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);
            var staff = await svc.CreateStaffAsync(BookingTestData.TenantId, "Staff A");

            var fromOther = await svc.GetStaffAsync(BookingTestData.OtherTenantId, staff.Id);
            Assert.Null(fromOther);

            var listOther = await svc.ListStaffAsync(BookingTestData.OtherTenantId);
            Assert.Empty(listOther);

            // Update staff A qua tenant B → NotFound.
            await Assert.ThrowsAsync<NotFoundException>(() =>
                svc.UpdateStaffAsync(BookingTestData.OtherTenantId, staff.Id, "X", null, null, null, isActive: false));
        }

        [Fact]
        public async Task SetStaffServices_UnknownStaff_ThrowsNotFound()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var svc = BuildService(scope.Context);

            await Assert.ThrowsAsync<NotFoundException>(() =>
                svc.SetStaffServicesAsync(BookingTestData.TenantId, Guid.NewGuid(), [Guid.NewGuid()]));
        }
    }
}
