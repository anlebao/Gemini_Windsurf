using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Booking;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using Xunit;
using BookingEntity = VanAn.Shared.Domain.Booking;
using ValidationException = VanAn.CoreHub.Services.ValidationException;
using StaffServiceEntity = VanAn.Shared.Domain.StaffService;

namespace VanAn.Core.Tests.BookingScheduling;

/// <summary>
/// BS-1 (2026-10-14, user approve): UpdateBookingAsync — điều chỉnh nội dung booking
/// (dịch vụ/giờ/staff/add-on/ghi chú) kể cả sau check-in. Re-validate slot/conflict (SRS §12,
/// exclude self) + refresh snapshots (§8.4) + BookingEvent audit. Field null = giữ nguyên.
/// </summary>
public class BookingUpdateTests
{
    private static BookingService BuildService(VanAnDbContext ctx)
        => new(ctx, new AvailabilityService(ctx, NullLogger<AvailabilityService>.Instance),
            NullLogger<BookingService>.Instance, orderService: null, financialService: null);

    private static async Task<(BookingEntity Booking, Guid StaffId, Guid OfferingId, Guid AddOnId)> CreateBookingAsync(
        VanAnDbContext ctx, BookingService svc, int startHour = 14, string key = "key-update")
    {
        await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
        var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);
        var addOn = new AddOn(BookingTestData.TenantId, "Tinh dầu", 50_000m);
        ctx.AddOns.Add(addOn);
        await ctx.SaveChangesAsync();

        var booking = await svc.CreateBookingAsync(BookingTestData.TenantId,
            new CreateBookingCommand(
                OfferingId: offeringId, StartAt: BookingTestData.At(startHour), StaffId: null,
                CustomerId: null, CustomerDeviceId: "device-update", CustomerNote: "Note cũ",
                AddOns: [new AddOnLine(addOn.Id, 1)], AttributionId: null),
            key);
        await svc.ConfirmAsync(BookingTestData.TenantId, booking.Id);
        await svc.AssignStaffAsync(BookingTestData.TenantId, booking.Id, staffId);
        return (booking, staffId, offeringId, addOn.Id);
    }

    [Fact]
    public async Task Update_OfferingChanged_RefreshesSnapshotItemsAndTotal()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
        var ctx = scope.Context;
        var svc = BuildService(ctx);
        var (booking, staffId, oldOfferingId, _) = await CreateBookingAsync(ctx, svc);

        // Dịch vụ mới (giá/duration khác) + add-on mới + staff hiện tại phải có skill dịch vụ mới (SRS §11.2).
        var newOffering = new AppointmentOffering(BookingTestData.TenantId, "Massage 90m", OfferingType.Service, 90, 450_000m);
        ctx.AppointmentOfferings.Add(newOffering);
        var newAddOn = new AddOn(BookingTestData.TenantId, "Nhiệt đá", 80_000m);
        ctx.AddOns.Add(newAddOn);
        ctx.StaffServices.Add(new StaffServiceEntity(BookingTestData.TenantId, staffId, newOffering.Id));
        await ctx.SaveChangesAsync();

        var updated = await svc.UpdateBookingAsync(BookingTestData.TenantId, booking.Id,
            new UpdateBookingCommand(
                OfferingId: newOffering.Id, StartAt: null, StaffId: null,
                AddOnIds: [newAddOn.Id], CustomerNote: null), default);

        Assert.Equal(newOffering.Id, updated.OfferingId);
        Assert.Equal("Massage 90m", updated.OfferingNameSnapshot);
        Assert.Equal(90, updated.OfferingDurationSnapshot);
        Assert.Equal(450_000m, updated.OfferingPriceSnapshot);
        Assert.Equal(530_000m, updated.EstimatedTotal);   // 450k + 80k
        Assert.Equal(BookingTestData.At(14), updated.StartAt);   // giờ giữ nguyên
        Assert.Equal(BookingTestData.At(14).AddMinutes(90), updated.EndAt);   // duration mới

        // BookingItems: 1 OFFERING mới + 1 ADD_ON mới (cũ bị xoá).
        var items = await ctx.BookingItems.IgnoreQueryFilters().Where(i => i.BookingId == booking.Id).ToListAsync();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.ItemType == "OFFERING" && i.OfferingId == newOffering.Id && i.Name == "Massage 90m");
        Assert.Contains(items, i => i.ItemType == "ADD_ON" && i.AddOnId == newAddOn.Id && i.Name == "Nhiệt đá");

        // BookingEvent audit.
        Assert.Contains(await ctx.BookingEvents.IgnoreQueryFilters().Where(e => e.BookingId == booking.Id).Select(e => e.EventType).ToListAsync(),
            e => e == "BookingUpdated");
    }

    [Fact]
    public async Task Update_AfterCheckIn_Allowed()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
        var ctx = scope.Context;
        var svc = BuildService(ctx);
        var (booking, _, _, _) = await CreateBookingAsync(ctx, svc);
        await svc.CheckInAsync(BookingTestData.TenantId, booking.Id);   // khách đã check-in

        var updated = await svc.UpdateBookingAsync(BookingTestData.TenantId, booking.Id,
            new UpdateBookingCommand(OfferingId: null, StartAt: null, StaffId: null, AddOnIds: null,
                CustomerNote: "Đổi giờ sang chiều"), default);

        Assert.Equal(BookingStatus.CheckedIn, updated.Status);
        Assert.Equal("Đổi giờ sang chiều", updated.CustomerNote);
    }

    [Fact]
    public async Task Update_AfterCompleted_Rejected()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
        var ctx = scope.Context;
        var svc = BuildService(ctx);
        var (booking, _, _, _) = await CreateBookingAsync(ctx, svc);
        await svc.CheckInAsync(BookingTestData.TenantId, booking.Id);
        await svc.StartServiceAsync(BookingTestData.TenantId, booking.Id);
        await svc.CompleteAsync(BookingTestData.TenantId, booking.Id, 300_000m);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => svc.UpdateBookingAsync(
            BookingTestData.TenantId, booking.Id,
            new UpdateBookingCommand(OfferingId: null, StartAt: null, StaffId: null, AddOnIds: null, CustomerNote: "Sửa sau hoàn tất"), default));
        Assert.Contains("Completed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_StaffChanged_RecordsReassign()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
        var ctx = scope.Context;
        var svc = BuildService(ctx);
        var (booking, _, offeringId, _) = await CreateBookingAsync(ctx, svc);

        // Staff thứ 2 có schedule — gán skill cho offering của booking (SRS §11.2: kỹ năng theo dịch vụ).
        var (staff2Id, _) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId, staffName: "KTV B");
        ctx.StaffServices.Add(new StaffServiceEntity(BookingTestData.TenantId, staff2Id, offeringId));
        await ctx.SaveChangesAsync();
        var updated = await svc.UpdateBookingAsync(BookingTestData.TenantId, booking.Id,
            new UpdateBookingCommand(OfferingId: null, StartAt: null, StaffId: staff2Id, AddOnIds: null, CustomerNote: null), default);

        Assert.Equal(staff2Id, updated.StaffId);
        var assignments = await ctx.BookingStaffAssignments.IgnoreQueryFilters()
            .Where(a => a.BookingId == booking.Id).ToListAsync();
        Assert.Contains(assignments, a => a.AssignmentType == "REASSIGN" && a.StaffId == staff2Id);
    }

    [Fact]
    public async Task Update_NoOp_ReturnsSameBooking()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
        var ctx = scope.Context;
        var svc = BuildService(ctx);
        var (booking, _, _, _) = await CreateBookingAsync(ctx, svc);
        int eventsBefore = await ctx.BookingEvents.IgnoreQueryFilters().CountAsync(e => e.BookingId == booking.Id);

        var updated = await svc.UpdateBookingAsync(BookingTestData.TenantId, booking.Id,
            new UpdateBookingCommand(OfferingId: booking.OfferingId, StartAt: booking.StartAt, StaffId: booking.StaffId, AddOnIds: null, CustomerNote: null), default);

        Assert.Equal(booking.Id, updated.Id);
        Assert.Equal(eventsBefore, await ctx.BookingEvents.IgnoreQueryFilters().CountAsync(e => e.BookingId == booking.Id));   // không event mới
    }

    [Fact]
    public async Task Update_ConflictingTime_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        scope.TenantProvider!.SetTenant(BookingTestData.TenantId.Value);
        var ctx = scope.Context;
        var svc = BuildService(ctx);
        await BookingTestData.EnsureConfigAsync(ctx, BookingTestData.TenantId, enabled: true);
        var (staffId, offeringId) = await BookingTestData.SeedStaffOfferingAsync(ctx, BookingTestData.TenantId);

        // Booking A: 14:00-15:00 (staff 1). Booking B: 16:00-17:00 (CÙNG staff 1) — create-with-staff → StaffAssigned.
        var bookingA = await svc.CreateBookingAsync(BookingTestData.TenantId,
            new CreateBookingCommand(OfferingId: offeringId, StartAt: BookingTestData.At(14), StaffId: staffId,
                CustomerId: null, CustomerDeviceId: "device-a", CustomerNote: null, AddOns: [], AttributionId: null),
            "key-conflict-a");
        var bookingB = await svc.CreateBookingAsync(BookingTestData.TenantId,
            new CreateBookingCommand(OfferingId: offeringId, StartAt: BookingTestData.At(16), StaffId: staffId,
                CustomerId: null, CustomerDeviceId: "device-b", CustomerNote: null, AddOns: [], AttributionId: null),
            "key-conflict-b");
        Assert.Equal(staffId, bookingA.StaffId);
        Assert.Equal(bookingA.StaffId, bookingB.StaffId);

        // Chuyển B về 14:30 → trùng A (14:00-15:00) → conflict.
        var ex = await Assert.ThrowsAsync<BookingConflictException>(() => svc.UpdateBookingAsync(
            BookingTestData.TenantId, bookingB.Id,
            new UpdateBookingCommand(OfferingId: null, StartAt: BookingTestData.At(14, 30), StaffId: null, AddOnIds: null, CustomerNote: null), default));
        Assert.Contains("Khung giờ", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
