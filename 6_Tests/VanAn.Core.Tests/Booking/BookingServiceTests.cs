using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
using BookingEntity = VanAn.Shared.Domain.Booking;
// Domain junction entity "StaffService" trùng tên service class → alias trong test.
using StaffServiceEntity = VanAn.Shared.Domain.StaffService;
// Services layer exceptions (Exceptions.cs) — KHÁC VanAn.Shared.Domain.ValidationException.
using ValidationException = VanAn.CoreHub.Services.ValidationException;
using NotFoundException = VanAn.CoreHub.Services.NotFoundException;

namespace VanAn.Core.Tests.BookingScheduling
{
    /// <summary>
    /// IBookingService — create (Idempotency-Key §21.1) + state machine (§9.3) + BookingEvent audit (§23-24)
    /// + assign/change staff idempotent (§21.3) + deposit policy (§16.1) + tenant isolation.
    /// </summary>
    public class BookingServiceTests
    {
        private static BookingService BuildService(VanAnDbContext ctx)
            => new(ctx, new AvailabilityService(ctx, NullLogger<AvailabilityService>.Instance), NullLogger<BookingService>.Instance);

        private static CreateBookingCommand Command(
            Guid offeringId, DateTime startAt, Guid? staffId = null, string? device = "device-c",
            IReadOnlyList<AddOnLine>? addOns = null, Guid? customerId = null)
            => new(offeringId, startAt, staffId, customerId, device, "Ghi chú", addOns ?? []);

        // ── Create ──────────────────────────────────────────────────────────

        [Fact]
        public async Task Create_NoStaff_PendingConfirmation_WithItemsAndEvents()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "key-1");

            Assert.Equal(BookingStatus.PendingConfirmation, booking.Status);
            Assert.Null(booking.StaffId);
            Assert.Equal(BookingTestData.At(14, 0), booking.StartAt);
            Assert.Equal(BookingTestData.At(15, 0), booking.EndAt);
            Assert.Equal(300_000m, booking.EstimatedTotal);
            Assert.False(string.IsNullOrWhiteSpace(booking.PublicBookingCode));
            Assert.Equal(booking.Id, booking.BookingId.Value); // Single-Identity
            Assert.Equal("Ghi chú", booking.CustomerNote);

            // Items: 1 offering line.
            var items = await ctx.BookingItems.IgnoreQueryFilters().Where(i => i.BookingId == booking.Id).ToListAsync();
            Assert.Single(items);
            Assert.Equal("OFFERING", items[0].ItemType);

            // Events: BookingCreated.
            var events = await ctx.BookingEvents.IgnoreQueryFilters().Where(e => e.BookingId == booking.Id).ToListAsync();
            Assert.Single(events);
            Assert.Equal("BookingCreated", events[0].EventType);

            // Idempotency record.
            Assert.NotNull(await ctx.BookingIdempotencyRecords.IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.IdempotencyKey == "key-1"));
        }

        [Fact]
        public async Task Create_WithStaff_StaffAssigned_And_SlotLocked()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0), staffId), "key-2");

            Assert.Equal(BookingStatus.StaffAssigned, booking.Status);
            Assert.Equal(staffId, booking.StaffId);

            // Assignment record + 2 events (Created + StaffAssigned).
            var assignments = await ctx.BookingStaffAssignments.IgnoreQueryFilters()
                .Where(a => a.BookingId == booking.Id).ToListAsync();
            Assert.Single(assignments);
            Assert.Equal("ASSIGN", assignments[0].AssignmentType);

            var events = await ctx.BookingEvents.IgnoreQueryFilters().Where(e => e.BookingId == booking.Id).ToListAsync();
            Assert.Equal(2, events.Count);
            Assert.Contains(events, e => e.EventType == "BookingStaffAssigned");
        }

        [Fact]
        public async Task Create_TenantNotEnabled_ThrowsValidation()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: false);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                svc.CreateBookingAsync(BookingTestData.TenantId, Command(offeringId, BookingTestData.At(14, 0)), "key-x"));
            Assert.Contains("đặt lịch", ex.Message);
        }

        [Fact]
        public async Task Create_PastTime_ThrowsValidation()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            await Assert.ThrowsAsync<ValidationException>(() =>
                svc.CreateBookingAsync(BookingTestData.TenantId,
                    Command(offeringId, DateTime.UtcNow.AddMinutes(-30)), "key-x"));
        }

        [Fact]
        public async Task Create_OverlappingBooking_ThrowsBusinessConflict()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var first = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0), staffId), "key-3");

            // 14:30 chồng 14:00-15:00 → 409 business conflict (§6.5 message).
            var ex = await Assert.ThrowsAsync<BookingConflictException>(() =>
                svc.CreateBookingAsync(BookingTestData.TenantId,
                    Command(offeringId, BookingTestData.At(14, 30), staffId), "key-4"));

            Assert.Contains("khung giờ", ex.Message);

            // Không tạo booking thứ 2.
            Assert.Equal(1, await ctx.Bookings.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task Create_IdempotentRetry_SameKey_ReturnsSameBooking()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var first = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "retry-key");

            // Retry cùng key (network retry §21.1) → trả booking gốc, không tạo mới.
            var second = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "retry-key");

            Assert.Equal(first.Id, second.Id);
            Assert.Equal(1, await ctx.Bookings.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task Create_DepositPolicy_Fixed_SetsDepositRequirement()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigWithDepositAsync(ctx, BookingTestData.TenantId, DepositPolicy.Fixed, fixedAmount: 100_000m);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "key-deposit");

            Assert.True(booking.DepositRequired);
            Assert.Equal(MoneyNatureType.SecurityDeposit, booking.DepositType);
            Assert.Equal(100_000m, booking.DepositAmount);
            Assert.Equal(PaymentStatus.Pending, booking.PaymentStatus);
            Assert.Equal(BookingSubState.DepositPending, booking.SubState);
        }

        // ── State machine ───────────────────────────────────────────────────

        [Fact]
        public async Task HappyPath_Confirm_Assign_CheckIn_Start_Complete_WithEvents()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "flow-key");

            var confirmed = await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id);
            Assert.Equal(BookingStatus.Confirmed, confirmed.Status);

            var assigned = await svc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId);
            Assert.Equal(BookingStatus.StaffAssigned, assigned.Status);
            Assert.Equal(staffId, assigned.StaffId);

            var checkedIn = await svc.CheckInAsync(BookingTestData.TenantId, booking.Id);
            Assert.Equal(BookingStatus.CheckedIn, checkedIn.Status);
            Assert.NotNull(checkedIn.CheckedInAt);

            var started = await svc.StartServiceAsync(BookingTestData.TenantId, booking.Id);
            Assert.Equal(BookingStatus.InService, started.Status);

            var completed = await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 350_000m);
            Assert.Equal(BookingStatus.Completed, completed.Status);
            Assert.Equal(350_000m, completed.ActualTotal);
            Assert.NotNull(completed.CompletedAt);

            // Audit events mọi transition (§24): Created, Confirmed, StaffAssigned, CheckedIn, Started, Completed.
            var events = await ctx.BookingEvents.IgnoreQueryFilters()
                .Where(e => e.BookingId == booking.Id).OrderBy(e => e.OccurredAt).ToListAsync();
            Assert.Equal(6, events.Count);
            string[] expected = ["BookingCreated", "BookingConfirmed", "BookingStaffAssigned", "BookingCheckedIn", "BookingStarted", "BookingCompleted"];
            Assert.Equal(expected, events.Select(e => e.EventType).ToArray());
        }

        [Fact]
        public async Task Confirm_AlreadyConfirmed_Idempotent_NoDuplicateEvent()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "ck-key");

            _ = await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id);
            var again = await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id); // §21.2

            Assert.Equal(BookingStatus.Confirmed, again.Status);
            Assert.Equal(1, await ctx.BookingEvents.IgnoreQueryFilters()
                .CountAsync(e => e.BookingId == booking.Id && e.EventType == "BookingConfirmed"));
        }

        [Fact]
        public async Task Assign_SameStaffTwice_NoDuplicateAssignmentRecord()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "as-key");
            _ = await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id);

            _ = await svc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId);
            var again = await svc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId); // §21.3

            Assert.Equal(BookingStatus.StaffAssigned, again.Status);
            Assert.Equal(1, await ctx.BookingStaffAssignments.IgnoreQueryFilters()
                .CountAsync(a => a.BookingId == booking.Id));
        }

        [Fact]
        public async Task ChangeStaff_FromConfirmed_CreatesReassignRecord()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);

            // 2 staff cùng skill + schedule.
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
                new StaffWorkingSchedule(BookingTestData.TenantId, staffA.Id, (int)DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)),
                new StaffWorkingSchedule(BookingTestData.TenantId, staffB.Id, (int)DayOfWeek.Monday, new TimeSpan(9, 0, 0), new TimeSpan(17, 0, 0)));
            await ctx.SaveChangesAsync();

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offering.Id, BookingTestData.At(14, 0)), "cs-key");
            _ = await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id);
            _ = await svc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffA.Id);

            var changed = await svc.ChangeStaffAsync(BookingTestData.TenantId, booking.Id, staffB.Id);
            Assert.Equal(staffB.Id, changed.StaffId);
            Assert.Equal(BookingStatus.StaffAssigned, changed.Status);

            var records = await ctx.BookingStaffAssignments.IgnoreQueryFilters()
                .Where(a => a.BookingId == booking.Id).OrderBy(a => a.AssignedAt).ToListAsync();
            Assert.Equal(2, records.Count);
            Assert.Equal("REASSIGN", records[1].AssignmentType);

            var events = await ctx.BookingEvents.IgnoreQueryFilters()
                .Where(e => e.BookingId == booking.Id && e.EventType == "BookingStaffReassigned").ToListAsync();
            Assert.Single(events);
        }

        [Fact]
        public async Task Cancel_FromPending_Then_Idempotent_And_FromStaffAssigned_Throws()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "cancel-key");

            var cancelled = await svc.CancelAsync(BookingTestData.TenantId, booking.Id, "Khách hủy");
            Assert.Equal(BookingStatus.Cancelled, cancelled.Status);

            // Idempotent cancel.
            var again = await svc.CancelAsync(BookingTestData.TenantId, booking.Id, "Khách hủy");
            Assert.Equal(BookingStatus.Cancelled, again.Status);

            // RV P6 decision (user approve 2026-10-06): StaffAssigned → cancel HỢP LỆ
            // (create-with-staff — khách chọn staff lúc đặt — phải hủy được).
            var assigned = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(15, 0), staffId), "cancel-key-2");
            Assert.Equal(BookingStatus.StaffAssigned, assigned.Status);
            var assignedCancelled = await svc.CancelAsync(BookingTestData.TenantId, assigned.Id, "Khách đổi ý");
            Assert.Equal(BookingStatus.Cancelled, assignedCancelled.Status);

            // CheckedIn → cancel vẫn KHÔNG hợp lệ (§9.3).
            var checkedIn = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(16, 0)), "cancel-key-3");
            await svc.ConfirmAsync(BookingTestData.TenantId, checkedIn.Id);
            await svc.AssignStaffAsync(BookingTestData.TenantId, checkedIn.Id, staffId);
            await svc.CheckInAsync(BookingTestData.TenantId, checkedIn.Id);
            Assert.Equal(BookingStatus.CheckedIn, checkedIn.Status);
            await Assert.ThrowsAsync<ValidationException>(() =>
                svc.CancelAsync(BookingTestData.TenantId, checkedIn.Id, "test"));
        }

        [Fact]
        public async Task Complete_FromPending_ThrowsValidation_InvalidTransitionRejected()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "inv-key");

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m));
            Assert.Contains("hoàn tất", ex.Message);
        }

        [Fact]
        public async Task Reject_OnlyFromPending()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "rj-key");

            var rejected = await svc.RejectAsync(BookingTestData.TenantId, booking.Id, "Hết chỗ");
            Assert.Equal(BookingStatus.Rejected, rejected.Status);
            Assert.Equal("Hết chỗ", rejected.CancellationReason);

            // Idempotent reject.
            var again = await svc.RejectAsync(BookingTestData.TenantId, booking.Id, "Hết chỗ");
            Assert.Equal(BookingStatus.Rejected, again.Status);
        }

        // ── Status polling + reads ──────────────────────────────────────────

        [Fact]
        public async Task GetPublicStatus_ReturnsDto_WithCurrentState()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "poll-key");
            _ = await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id);
            _ = await svc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId);

            var dto = await svc.GetPublicStatusAsync(booking.PublicBookingCode);

            Assert.NotNull(dto);
            Assert.Equal(BookingStatus.StaffAssigned, dto!.Status);
            Assert.Equal(staffId, dto.StaffId);
            Assert.Equal(booking.PublicBookingCode, dto.PublicBookingCode);
            Assert.Equal("Massage 60m", dto.OfferingNameSnapshot);
            Assert.True(dto.Version >= 2); // create(0) → confirm(1) → assign(2)

            Assert.Null(await svc.GetPublicStatusAsync("KHONG-TON-TAI"));
        }

        [Fact]
        public async Task GetQueue_FiltersByStatusAndRange()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            _ = await svc.CreateBookingAsync(BookingTestData.TenantId, Command(offeringId, BookingTestData.At(14, 0)), "q1");
            var second = await svc.CreateBookingAsync(BookingTestData.TenantId, Command(offeringId, BookingTestData.At(15, 0)), "q2");
            _ = await svc.ConfirmAsync(BookingTestData.TenantId, second.Id);

            var pending = await svc.GetQueueAsync(BookingTestData.TenantId, status: BookingStatus.PendingConfirmation);
            Assert.Single(pending);

            var confirmed = await svc.GetQueueAsync(BookingTestData.TenantId, status: BookingStatus.Confirmed);
            Assert.Single(confirmed);

            var ranged = await svc.GetQueueAsync(BookingTestData.TenantId, from: BookingTestData.At(14, 30));
            Assert.Single(ranged); // chỉ 15:00
        }

        [Fact]
        public async Task TenantIsolation_BookingOfTenantA_InvisibleFromTenantB()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);
            await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
            var (_, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

            var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
                Command(offeringId, BookingTestData.At(14, 0)), "iso-key");

            Assert.Null(await svc.GetBookingAsync(BookingTestData.OtherTenantId, booking.Id));
            Assert.Empty(await svc.GetQueueAsync(BookingTestData.OtherTenantId));

            // Transition qua tenant B → NotFound.
            await Assert.ThrowsAsync<NotFoundException>(() =>
                svc.ConfirmAsync(BookingTestData.OtherTenantId, booking.Id));

            // Status polling bằng public code (unique global) vẫn trả về (P4 quyết định expose).
            Assert.NotNull(await svc.GetPublicStatusAsync(booking.PublicBookingCode));
        }

        // ── BookingTenantConfig ─────────────────────────────────────────────

        [Fact]
        public async Task Config_GetOrCreate_Defaults_And_Update()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);

            var config = await svc.GetConfigAsync(BookingTestData.TenantId);
            Assert.False(config.IsEnabled); // Q5 default OFF
            Assert.Equal(DepositPolicy.None, config.DepositPolicy);

            var updated = await svc.UpdateConfigAsync(
                BookingTestData.TenantId, isEnabled: true, DepositPolicy.Fixed, 100_000m, null,
                "Hủy trước 2 tiếng", "direct_to_consumer");
            Assert.True(updated.IsEnabled);
            Assert.Equal(100_000m, updated.DepositFixedAmount);
            Assert.Equal("Hủy trước 2 tiếng", updated.CancelReschedulePolicy);

            var again = await svc.GetConfigAsync(BookingTestData.TenantId);
            Assert.Equal(config.Id, again.Id); // get-or-create không tạo row mới
            Assert.Equal(1, await ctx.BookingTenantConfigs.IgnoreQueryFilters().CountAsync());
        }

        [Fact]
        public async Task Config_InvalidDepositPolicy_Throws()
        {
            using var scope = VanAnDbContextTestFactory.Create();
            scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
            var ctx = scope.Context;
            var svc = BuildService(ctx);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.UpdateConfigAsync(BookingTestData.TenantId, true, DepositPolicy.Fixed, null, null, null, null));
        }
    }
}
