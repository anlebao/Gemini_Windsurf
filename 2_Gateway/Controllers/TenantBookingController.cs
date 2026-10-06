using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Booking;
using VanAn.Shared.Domain;
// Namespace "Booking" xung đột type Booking (lesson P1 — CS0118) → alias cho domain entity.
using BookingEntity = VanAn.Shared.Domain.Booking;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Booking P4.2 (SRS §20 — tenant operations). JWT tenant-scoped — TenantId từ claim "tenant_id".
///
///   GET  /api/tenant/booking/bookings?status=&from=&to=              → queue (P5 `/booking/queue`)
///   GET  /api/tenant/booking/bookings/{id}                            → chi tiết
///   POST /api/tenant/booking/bookings/{id}/confirm|reject|assign-staff|change-staff|check-in|start|complete|no-show|cancel
///   GET  /api/tenant/booking/availability?date=&offeringId=&staffId= → who-is-available matrix (§14)
///   GET  /api/tenant/booking/staff                                    → staff list
///   GET  /api/tenant/booking/staff/{staffId}/schedules                → working schedules + overrides
///   GET  /api/tenant/booking/bookings/{id}/financial-status           → deposit/payment/invoice state (§16)
///
/// Tất cả transition idempotent (§21.2-21.3). Mọi query filter TenantId (lesson a21f97f2 — không leak cross-tenant).
/// </summary>
[ApiController]
[Route("api/tenant/booking")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class TenantBookingController(
    IBookingService bookingService,
    IStaffService staffService,
    IAvailabilityService availabilityService,
    IBookingFinancialService financialService,
    VanAnDbContext dbContext,
    ILogger<TenantBookingController> logger) : ControllerBase
{
    private readonly IBookingService _bookingService = bookingService;
    private readonly IStaffService _staffService = staffService;
    private readonly IAvailabilityService _availabilityService = availabilityService;
    private readonly IBookingFinancialService _financialService = financialService;
    private readonly VanAnDbContext _dbContext = dbContext;
    private readonly ILogger<TenantBookingController> _logger = logger;

    // ── Queue / detail ──────────────────────────────────────────────────────

    /// <summary>Queue booking theo status + khoảng thời gian (mặc định 7 ngày tới).</summary>
    [HttpGet("bookings")]
    public async Task<ActionResult<List<BookingQueueItemDto>>> GetQueue(
        [FromQuery] BookingStatus? status = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });

        DateTime fromUtc = from?.ToUniversalTime() ?? DateTime.UtcNow.Date;
        DateTime toUtc = to?.ToUniversalTime() ?? fromUtc.AddDays(7);

        var bookings = await _bookingService.GetQueueAsync(tenantId, status, fromUtc, toUtc, ct);
        return Ok(bookings.Select(BookingQueueItemDto.From).ToList());
    }

    [HttpGet("bookings/{bookingId:guid}")]
    public async Task<ActionResult<BookingQueueItemDto>> GetDetail(Guid bookingId, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });

        BookingEntity? booking = await _bookingService.GetBookingAsync(tenantId, bookingId, ct);
        if (booking is null)
            return NotFound(new { message = "Không tìm thấy lịch hẹn." });
        return Ok(BookingQueueItemDto.From(booking));
    }

    // ── Transitions (idempotent §21.2-21.3) ─────────────────────────────────

    [HttpPost("bookings/{bookingId:guid}/confirm")]
    public async Task<ActionResult<BookingQueueItemDto>> Confirm(Guid bookingId, CancellationToken ct = default)
        => await TransitionAsync(bookingId, () => _bookingService.ConfirmAsync(ResolveTenantId(), bookingId, actorId: null, ct), ct);

    [HttpPost("bookings/{bookingId:guid}/reject")]
    public async Task<ActionResult<BookingQueueItemDto>> Reject(Guid bookingId, [FromBody] RejectBookingRequest? request, CancellationToken ct = default)
        => await TransitionAsync(bookingId, () => _bookingService.RejectAsync(ResolveTenantId(), bookingId, request?.Reason, actorId: null, ct), ct);

    [HttpPost("bookings/{bookingId:guid}/assign-staff")]
    public async Task<ActionResult<BookingQueueItemDto>> AssignStaff(Guid bookingId, [FromBody] AssignStaffRequest request, CancellationToken ct = default)
    {
        if (request is null || request.StaffId == Guid.Empty)
            return BadRequest(new { message = "Thiếu staffId." });
        return await TransitionAsync(bookingId, () => _bookingService.AssignStaffAsync(ResolveTenantId(), bookingId, request.StaffId, actorId: null, ct), ct);
    }

    [HttpPost("bookings/{bookingId:guid}/change-staff")]
    public async Task<ActionResult<BookingQueueItemDto>> ChangeStaff(Guid bookingId, [FromBody] AssignStaffRequest request, CancellationToken ct = default)
    {
        if (request is null || request.StaffId == Guid.Empty)
            return BadRequest(new { message = "Thiếu staffId." });
        return await TransitionAsync(bookingId, () => _bookingService.ChangeStaffAsync(ResolveTenantId(), bookingId, request.StaffId, actorId: null, ct), ct);
    }

    [HttpPost("bookings/{bookingId:guid}/check-in")]
    public async Task<ActionResult<BookingQueueItemDto>> CheckIn(Guid bookingId, CancellationToken ct = default)
        => await TransitionAsync(bookingId, () => _bookingService.CheckInAsync(ResolveTenantId(), bookingId, actorId: null, ct), ct);

    [HttpPost("bookings/{bookingId:guid}/start")]
    public async Task<ActionResult<BookingQueueItemDto>> Start(Guid bookingId, CancellationToken ct = default)
        => await TransitionAsync(bookingId, () => _bookingService.StartServiceAsync(ResolveTenantId(), bookingId, actorId: null, ct), ct);

    [HttpPost("bookings/{bookingId:guid}/complete")]
    public async Task<ActionResult<BookingQueueItemDto>> Complete(Guid bookingId, [FromBody] CompleteBookingRequest? request, CancellationToken ct = default)
        => await TransitionAsync(bookingId, () => _bookingService.CompleteAsync(ResolveTenantId(), bookingId, request?.ActualTotal ?? 0m, actorId: null, ct), ct);

    [HttpPost("bookings/{bookingId:guid}/no-show")]
    public async Task<ActionResult<BookingQueueItemDto>> NoShow(Guid bookingId, [FromBody] RejectBookingRequest? request, CancellationToken ct = default)
        => await TransitionAsync(bookingId, () => _bookingService.MarkNoShowAsync(ResolveTenantId(), bookingId, request?.Reason, actorId: null, ct), ct);

    [HttpPost("bookings/{bookingId:guid}/cancel")]
    public async Task<ActionResult<BookingQueueItemDto>> Cancel(Guid bookingId, [FromBody] RejectBookingRequest? request, CancellationToken ct = default)
        => await TransitionAsync(bookingId, () => _bookingService.CancelAsync(ResolveTenantId(), bookingId, request?.Reason, actorId: null, ct), ct);

    // ── Availability / staff / schedules ────────────────────────────────────

    /// <summary>Who-is-available matrix (SRS §14 — staff × slot trong ngày kèm lý do unavailable tối thiểu).</summary>
    [HttpGet("availability")]
    public async Task<ActionResult<List<StaffAvailabilityDto>>> GetAvailability(
        [FromQuery] string date,
        [FromQuery] Guid? offeringId = null,
        [FromQuery] Guid? staffId = null,
        CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });

        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateOnly day))
            return BadRequest(new { message = "Ngày không hợp lệ (định dạng yyyy-MM-dd)." });

        var matrix = await _availabilityService.GetStaffAvailabilityMatrixAsync(tenantId, day, offeringId, ct);
        if (staffId is not null)
            matrix = matrix.Where(m => m.StaffId == staffId.Value).ToList();
        return Ok(matrix.Select(m => new StaffAvailabilityDto(m.StaffId, m.StaffName, m.SlotStartAt, m.SlotEndAt, m.IsAvailable, m.UnavailableReason)).ToList());
    }

    [HttpGet("staff")]
    public async Task<ActionResult<List<StaffDto>>> GetStaff(CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });

        var staff = await _staffService.ListStaffAsync(tenantId, activeOnly: false, ct);
        return Ok(staff.Select(s => new StaffDto(s.Id, s.DisplayName, s.Role, s.AvatarUrl, s.IsActive)).ToList());
    }

    [HttpGet("staff/{staffId:guid}/schedules")]
    public async Task<ActionResult<StaffScheduleDto>> GetSchedules(Guid staffId, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });

        var schedules = await _staffService.ListWorkingSchedulesAsync(tenantId, staffId, ct);
        var overrides = await _staffService.ListOverridesAsync(tenantId, staffId, DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(60), ct);
        return Ok(new StaffScheduleDto(
            StaffId: staffId,
            Schedules: schedules.Select(s => new WorkingScheduleDto(s.Id, s.Weekday, s.StartTime, s.EndTime, s.BreakStart, s.BreakEnd)).ToList(),
            Overrides: overrides.Select(o => new ScheduleOverrideDto(o.Id, o.Date, o.OverrideType.ToString(), o.StartTime, o.EndTime, o.Note)).ToList()));
    }

    /// <summary>Financial status của booking (SRS §16 — deposit transaction + payment/invoice state).</summary>
    [HttpGet("bookings/{bookingId:guid}/financial-status")]
    public async Task<ActionResult<FinancialStatusDto>> GetFinancialStatus(Guid bookingId, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });

        BookingEntity? booking = await _bookingService.GetBookingAsync(tenantId, bookingId, ct);
        if (booking is null)
            return NotFound(new { message = "Không tìm thấy lịch hẹn." });

        PaymentTransaction? deposit = await _financialService.GetDepositAsync(tenantId, bookingId, ct);
        return Ok(new FinancialStatusDto(
            BookingId: booking.Id,
            DepositRequired: booking.DepositRequired,
            DepositAmount: booking.DepositAmount,
            DepositType: booking.DepositType?.ToString(),
            DepositStatus: deposit?.Status.ToString(),
            DepositTransactionId: deposit?.Id,
            PaymentStatus: booking.PaymentStatus.ToString(),
            InvoiceStatus: booking.InvoiceStatus.ToString(),
            InvoiceTrigger: booking.InvoiceTrigger.ToString(),
            EstimatedTotal: booking.EstimatedTotal,
            ActualTotal: booking.ActualTotal));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private TenantId ResolveTenantId()
    {
        string? tenantClaim = User.FindFirst("tenant_id")?.Value
            ?? User.FindFirst("TenantId")?.Value;
        return Guid.TryParse(tenantClaim, out Guid tenantId) ? new TenantId(tenantId) : new TenantId(Guid.Empty);
    }

    private async Task<ActionResult<BookingQueueItemDto>> TransitionAsync(Guid bookingId, Func<Task<BookingEntity>> action, CancellationToken ct)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });

        try
        {
            BookingEntity updated = await action();
            _logger.LogInformation("Booking transition: booking={BookingId} tenant={TenantId} status={Status}",
                updated.Id, updated.TenantId.Value, updated.Status);
            return Ok(BookingQueueItemDto.From(updated));
        }
        catch (BookingConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record BookingQueueItemDto(
    Guid BookingId,
    string PublicBookingCode,
    BookingStatus Status,
    BookingSubState SubState,
    string OfferingNameSnapshot,
    DateTime StartAt,
    DateTime EndAt,
    decimal EstimatedTotal,
    decimal? ActualTotal,
    Guid? StaffId,
    Guid? CustomerId,
    string? CustomerDeviceId,
    string? CustomerNote,
    bool DepositRequired,
    decimal? DepositAmount,
    string? DepositType,
    PaymentStatus PaymentStatus,
    BookingInvoiceStatus InvoiceStatus,
    int Version,
    DateTime CreatedAt,
    DateTime? CompletedAt)
{
    public static BookingQueueItemDto From(BookingEntity b) => new(
        b.Id,
        b.PublicBookingCode,
        b.Status,
        b.SubState,
        b.OfferingNameSnapshot,
        b.StartAt,
        b.EndAt,
        b.EstimatedTotal,
        b.ActualTotal,
        b.StaffId,
        b.CustomerId,
        b.CustomerDeviceId,
        b.CustomerNote,
        b.DepositRequired,
        b.DepositAmount,
        b.DepositType?.ToString(),
        b.PaymentStatus,
        b.InvoiceStatus,
        b.Version,
        b.CreatedAt,
        b.CompletedAt);
}

public record RejectBookingRequest(string? Reason);

public record AssignStaffRequest(Guid StaffId);

public record CompleteBookingRequest(decimal ActualTotal);

public record StaffAvailabilityDto(Guid StaffId, string StaffName, DateTime SlotStartAt, DateTime SlotEndAt, bool IsAvailable, string? UnavailableReason);

public record StaffDto(Guid Id, string DisplayName, string? Role, string? AvatarUrl, bool IsActive);

public record WorkingScheduleDto(Guid Id, int Weekday, TimeSpan StartTime, TimeSpan EndTime, TimeSpan? BreakStart, TimeSpan? BreakEnd);

public record ScheduleOverrideDto(Guid Id, DateTime Date, string OverrideType, TimeSpan? StartTime, TimeSpan? EndTime, string? Note);

public record StaffScheduleDto(Guid StaffId, List<WorkingScheduleDto> Schedules, List<ScheduleOverrideDto> Overrides);

public record FinancialStatusDto(
    Guid BookingId,
    bool DepositRequired,
    decimal? DepositAmount,
    string? DepositType,
    string? DepositStatus,
    Guid? DepositTransactionId,
    string PaymentStatus,
    string InvoiceStatus,
    string InvoiceTrigger,
    decimal EstimatedTotal,
    decimal? ActualTotal);
