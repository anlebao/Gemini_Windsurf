using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Booking;
using VanAn.Shared.Domain;
// Namespace "Booking" xung đột type Booking (lesson P1 — CS0118) → alias cho domain entity.
using BookingEntity = VanAn.Shared.Domain.Booking;
// Services-layer exceptions (Exceptions.cs) — KHÁC VanAn.Shared.Domain.NotFoundException/ValidationException
// (RV P6 bug: catch không alias → services exception không được bắt → 500 thay vì 400/404 friendly).
using NotFoundException = VanAn.CoreHub.Services.NotFoundException;
using ValidationException = VanAn.CoreHub.Services.ValidationException;

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
    IOfferingService offeringService,
    IAvailabilityService availabilityService,
    IBookingFinancialService financialService,
    IQRAttributionService qrService,
    ICommissionService commissionService,
    VanAnDbContext dbContext,
    IConfiguration configuration,
    ILogger<TenantBookingController> logger) : ControllerBase
{
    private readonly IBookingService _bookingService = bookingService;
    private readonly IStaffService _staffService = staffService;
    private readonly IOfferingService _offeringService = offeringService;
    private readonly IAvailabilityService _availabilityService = availabilityService;
    private readonly IBookingFinancialService _financialService = financialService;
    private readonly IQRAttributionService _qrService = qrService;
    private readonly ICommissionService _commissionService = commissionService;
    private readonly VanAnDbContext _dbContext = dbContext;
    private readonly IConfiguration _configuration = configuration;
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

    /// <summary>
    /// Feature 3 (2026-10-08 — luồng đặt lịch từ ShopERP, Owner/Staff tạo thay khách):
    /// tenant-scoped create — KHÔNG cần QR/attribution. Idempotency-Key bắt buộc (§21.1).
    /// Q3: staffId != null → STAFF_ASSIGNED ngay (create-with-staff); staffId null → auto CONFIRMED.
    /// Khách: CustomerId null (chưa có tài khoản) — tên/SĐT lưu vào CustomerNote (MVP).
    /// </summary>
    [HttpPost("bookings")]
    public async Task<ActionResult<BookingQueueItemDto>> CreateBooking([FromBody] CreateTenantBookingRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });

        string idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { message = "Thiếu Idempotency-Key — bắt buộc cho tạo lịch hẹn (chống trùng khi retry)." });

        if (request.StartAt.Kind == DateTimeKind.Unspecified)
            request = request with { StartAt = DateTime.SpecifyKind(request.StartAt, DateTimeKind.Utc) };

        var command = new CreateBookingCommand(
            request.OfferingId,
            request.StartAt.ToUniversalTime(),
            request.StaffId,
            CustomerId: null,
            CustomerDeviceId: null,
            CustomerNote: BuildCustomerNote(request.CustomerName, request.CustomerPhone, request.CustomerNote),
            AddOns: (request.AddOnIds ?? []).Select(id => new AddOnLine(id, 1)).ToList());
        try
        {
            BookingEntity booking = await _bookingService.CreateBookingAsync(tenantId, command, idempotencyKey, ct);
            // Q3 auto-confirm: có staff → đã StaffAssigned; không staff → confirm luôn.
            if (booking.StaffId is null && booking.Status == BookingStatus.PendingConfirmation)
            {
                booking = await _bookingService.ConfirmAsync(tenantId, booking.Id, ct: ct);
            }
            _logger.LogInformation("Tenant booking created (ShopERP): booking={BookingId} code={Code} tenant={TenantId} staff={StaffId}",
                booking.Id, booking.PublicBookingCode, tenantId.Value, booking.StaffId);
            return StatusCode(StatusCodes.Status201Created, BookingQueueItemDto.From(booking));
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
    }

    [HttpGet("add-ons")]
    public async Task<ActionResult<List<BookingAddOnDto>>> GetAddOns(CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        var addOns = await _offeringService.ListAddOnsAsync(tenantId, true, ct);
        return Ok(addOns.Select(a => new BookingAddOnDto(a.Id, a.Name, a.Price)).ToList());
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

    // ── Staff CRUD + skills (P5.2 — SRS §11.1-11.2) ──────────────────────────

    [HttpPost("staff")]
    public async Task<ActionResult<StaffDto>> CreateStaff([FromBody] CreateStaffRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        try
        {
            Staff staff = await _staffService.CreateStaffAsync(tenantId, request.DisplayName, request.Role, request.StaffUserId, ct);
            return Ok(ToStaffDto(staff));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("staff/{staffId:guid}")]
    public async Task<ActionResult<StaffDto>> UpdateStaff(Guid staffId, [FromBody] UpdateStaffRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        try
        {
            Staff staff = await _staffService.UpdateStaffAsync(
                tenantId, staffId, request.DisplayName, request.Role, request.AvatarUrl, request.StaffUserId, request.IsActive, ct);
            return Ok(ToStaffDto(staff));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Skills hiện tại của staff (StaffService junction — SRS §11.2).</summary>
    [HttpGet("staff/{staffId:guid}/services")]
    public async Task<ActionResult<List<StaffServiceDto>>> GetStaffServices(Guid staffId, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        var junction = await _staffService.ListStaffServicesAsync(tenantId, staffId, ct);
        return Ok(junction.Select(j => new StaffServiceDto(j.OfferingId)).ToList());
    }

    /// <summary>Thay thế toàn bộ skills staff (idempotent replace — SRS §11.2).</summary>
    [HttpPost("staff/{staffId:guid}/services")]
    public async Task<IActionResult> SetStaffServices(Guid staffId, [FromBody] SetStaffServicesRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        try
        {
            await _staffService.SetStaffServicesAsync(tenantId, staffId, request.OfferingIds ?? [], ct);
            return Ok(new { message = "Đã cập nhật dịch vụ của nhân viên." });
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Catalog reads (P5.2 skill setup + queue display) ─────────────────────

    [HttpGet("categories")]
    public async Task<ActionResult<List<BookingCategoryDto>>> GetCategories(CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        var categories = await _offeringService.ListCategoriesAsync(tenantId, false, ct);
        return Ok(categories.Select(c => new BookingCategoryDto(c.Id, c.Name, c.DisplayOrder)).ToList());
    }

    [HttpGet("offerings")]
    public async Task<ActionResult<List<BookingOfferingDto>>> GetOfferings([FromQuery] bool activeOnly = false, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        var offerings = await _offeringService.ListOfferingsAsync(tenantId, null, activeOnly, ct);
        return Ok(offerings.Select(ToOfferingDto).ToList());
    }

    // ── Catalog CRUD (Fix 2026-10-09 — bug 2: không có UI tạo dịch vụ → staff skills rỗng) ──

    [HttpPost("categories")]
    public async Task<ActionResult<BookingCategoryDto>> CreateCategory([FromBody] UpsertCategoryRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Tên danh mục là bắt buộc." });
        try
        {
            var category = await _offeringService.CreateCategoryAsync(tenantId, request.Name.Trim(), request.DisplayOrder, ct);
            return StatusCode(StatusCodes.Status201Created, new BookingCategoryDto(category.Id, category.Name, category.DisplayOrder));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("categories/{categoryId:guid}")]
    public async Task<ActionResult<BookingCategoryDto>> UpdateCategory(Guid categoryId, [FromBody] UpsertCategoryRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Tên danh mục là bắt buộc." });
        try
        {
            var category = await _offeringService.UpdateCategoryAsync(tenantId, categoryId, request.Name.Trim(), request.DisplayOrder, request.IsActive, ct);
            return Ok(new BookingCategoryDto(category.Id, category.Name, category.DisplayOrder));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("offerings")]
    public async Task<ActionResult<BookingOfferingDto>> CreateOffering([FromBody] UpsertOfferingRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DurationMinutes <= 0 || request.Price < 0)
            return BadRequest(new { message = "Tên dịch vụ, thời lượng > 0 và giá >= 0 là bắt buộc." });
        if (!Enum.TryParse<OfferingType>(request.OfferingType, ignoreCase: true, out var type))
            return BadRequest(new { message = "OfferingType không hợp lệ (Service/Package)." });
        try
        {
            var offering = await _offeringService.CreateOfferingAsync(tenantId, new CreateOfferingCommand(
                request.DisplayName.Trim(), type, request.DurationMinutes, request.Price,
                request.CategoryId, request.RequiredSkillCode, request.Description), ct);
            return StatusCode(StatusCodes.Status201Created, ToOfferingDto(offering));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("offerings/{offeringId:guid}")]
    public async Task<ActionResult<BookingOfferingDto>> UpdateOffering(Guid offeringId, [FromBody] UpsertOfferingRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DurationMinutes <= 0 || request.Price < 0)
            return BadRequest(new { message = "Tên dịch vụ, thời lượng > 0 và giá >= 0 là bắt buộc." });
        if (!Enum.TryParse<OfferingType>(request.OfferingType, ignoreCase: true, out var type))
            return BadRequest(new { message = "OfferingType không hợp lệ (Service/Package)." });
        try
        {
            var offering = await _offeringService.UpdateOfferingAsync(tenantId, offeringId, new CreateOfferingCommand(
                request.DisplayName.Trim(), type, request.DurationMinutes, request.Price,
                request.CategoryId, request.RequiredSkillCode, request.Description), request.IsActive, ct);
            return Ok(ToOfferingDto(offering));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("add-ons")]
    public async Task<ActionResult<BookingAddOnDto>> CreateAddOn([FromBody] UpsertAddOnRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0)
            return BadRequest(new { message = "Tên add-on và giá >= 0 là bắt buộc." });
        try
        {
            var addOn = await _offeringService.CreateAddOnAsync(tenantId, request.Name.Trim(), request.Price, ct);
            return StatusCode(StatusCodes.Status201Created, new BookingAddOnDto(addOn.Id, addOn.Name, addOn.Price));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("add-ons/{addOnId:guid}")]
    public async Task<ActionResult<BookingAddOnDto>> UpdateAddOn(Guid addOnId, [FromBody] UpsertAddOnRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0)
            return BadRequest(new { message = "Tên add-on và giá >= 0 là bắt buộc." });
        try
        {
            var addOn = await _offeringService.UpdateAddOnAsync(tenantId, addOnId, request.Name.Trim(), request.Price, request.IsActive, ct);
            return Ok(new BookingAddOnDto(addOn.Id, addOn.Name, addOn.Price));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    private static BookingOfferingDto ToOfferingDto(AppointmentOffering o)
        => new(o.Id, o.CategoryId, o.OfferingType.ToString(), o.DisplayName, o.DurationMinutes, o.Price, o.Description);

    // ── Schedules (P5.3 — SRS §11.3) ─────────────────────────────────────────

    [HttpPost("staff/{staffId:guid}/schedules")]
    public async Task<ActionResult<WorkingScheduleDto>> UpsertSchedule(Guid staffId, [FromBody] UpsertScheduleRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        if (request.Weekday is < 0 or > 6)
            return BadRequest(new { message = "Weekday phải trong 0 (Chủ nhật)..6 (Thứ 7)." });
        if (!TryParseTime(request.StartTime, out TimeSpan start) || !TryParseTime(request.EndTime, out TimeSpan end))
            return BadRequest(new { message = "Giờ làm việc không hợp lệ (HH:mm)." });
        TimeSpan? breakStart = TryParseTimeOrNull(request.BreakStart);
        TimeSpan? breakEnd = TryParseTimeOrNull(request.BreakEnd);
        try
        {
            StaffWorkingSchedule schedule = await _staffService.UpsertWorkingScheduleAsync(
                tenantId, staffId, request.Weekday.Value, start, end, breakStart, breakEnd, ct);
            return Ok(new WorkingScheduleDto(schedule.Id, schedule.Weekday, schedule.StartTime, schedule.EndTime, schedule.BreakStart, schedule.BreakEnd));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("schedules/{scheduleId:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid scheduleId, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        await _staffService.DeleteWorkingScheduleAsync(tenantId, scheduleId, ct);
        return Ok(new { message = "Đã xóa lịch làm việc." });
    }

    [HttpPost("staff/{staffId:guid}/overrides")]
    public async Task<ActionResult<ScheduleOverrideDto>> AddOverride(Guid staffId, [FromBody] AddOverrideRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        if (!DateOnly.TryParseExact(request.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateOnly day))
            return BadRequest(new { message = "Ngày không hợp lệ (yyyy-MM-dd)." });
        if (!Enum.TryParse<StaffScheduleOverrideType>(request.OverrideType, ignoreCase: true, out var overrideType))
            return BadRequest(new { message = "Loại override không hợp lệ (Working/Leave/Unavailable/Break)." });
        try
        {
            StaffScheduleOverride overrideItem = await _staffService.AddOverrideAsync(
                tenantId, staffId, day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), overrideType,
                TryParseTimeOrNull(request.StartTime), TryParseTimeOrNull(request.EndTime), request.Note, ct);
            return Ok(new ScheduleOverrideDto(overrideItem.Id, overrideItem.Date, overrideItem.OverrideType.ToString(), overrideItem.StartTime, overrideItem.EndTime, overrideItem.Note));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("overrides/{overrideId:guid}")]
    public async Task<IActionResult> DeleteOverride(Guid overrideId, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        await _staffService.DeleteOverrideAsync(tenantId, overrideId, ct);
        return Ok(new { message = "Đã xóa override." });
    }

    // ── BookingTenantConfig (P5.6 — Q5 enable + deposit policy §16.1) ────────

    [HttpGet("config")]
    public async Task<ActionResult<BookingTenantConfigDto>> GetConfig(CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        BookingTenantConfig config = await _bookingService.GetConfigAsync(tenantId, ct);
        return Ok(ToConfigDto(config));
    }

    [HttpPut("config")]
    public async Task<ActionResult<BookingTenantConfigDto>> UpdateConfig([FromBody] UpdateBookingConfigRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        if (!Enum.TryParse<DepositPolicy>(request.DepositPolicy, ignoreCase: true, out var policy))
            return BadRequest(new { message = "DepositPolicy không hợp lệ (None/Fixed/Percentage)." });
        try
        {
            BookingTenantConfig config = await _bookingService.UpdateConfigAsync(
                tenantId, request.IsEnabled, policy, request.DepositFixedAmount, request.DepositPercentage,
                request.CancelReschedulePolicy, request.EinvoiceMode, ct);
            return Ok(ToConfigDto(config));
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── QR channels (P5.6 — SRS §26.1) ───────────────────────────────────────

    [HttpPost("qr-channels")]
    public async Task<ActionResult<QrChannelCreatedDto>> CreateQrChannel([FromBody] CreateQrChannelRequest request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        if (string.IsNullOrWhiteSpace(request.QrToken))
            return BadRequest(new { message = "Thiếu qrToken." });
        try
        {
            QRChannel channel = await _qrService.CreateQrChannelAsync(
                tenantId, request.QrToken, request.SalesmanId, request.CampaignId, request.AttributionExpiryAt, ct);
            // Raw token trả về ĐÚNG 1 lần tại creation — UI hiển thị link đặt lịch; sau đó chỉ hash được lưu (§7.2).
            return Ok(new QrChannelCreatedDto(
                channel.Id, channel.QrTokenHash, request.QrToken, channel.SalesmanId, channel.CampaignId,
                channel.IsActive, channel.RevokedAt, channel.CreatedAt));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("qr-channels")]
    public async Task<ActionResult<List<QrChannelDto>>> GetQrChannels(CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        var channels = await _dbContext.QRChannels.IgnoreQueryFilters()
            .Where(q => q.TenantId == tenantId)
            .OrderByDescending(q => q.CreatedAt)
            .Select(q => new QrChannelDto(q.Id, q.QrTokenHash, q.SalesmanId, q.CampaignId, q.IsActive, q.RevokedAt, q.CreatedAt))
            .ToListAsync(ct);
        return Ok(channels);
    }

    /// <summary>
    /// Q1 (2026-10-08 — issue #188 bug 2): chi tiết QR cho tenant — decrypt EncryptedToken → link đặt lịch
    /// + QR PNG render lại (QRCoder). Chỉ tenant sở hữu + channel active. Revoked/legacy → link/QR null.
    /// </summary>
    [HttpGet("qr-channels/{qrId:guid}")]
    public async Task<ActionResult<QrChannelDetailDto>> GetQrChannelDetail(Guid qrId, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });

        // Fix 2026-10-09 (bug: khách không truy cập link QR): khachvip.online DNS → 161.118.212.110 (chết).
        // Domain KhachLink PWA hoạt động = diemthuong2.khachvip.online (ShopERP env ExternalUrls__KhachLink).
        string khachLinkBase = _configuration["KhachLink:BaseUrl"]
            ?? _configuration["ExternalUrls:KhachLink"]
            ?? "https://diemthuong2.khachvip.online";
        QrChannelDetailResult detail;
        try
        {
            detail = await _qrService.GetQrChannelDetailAsync(tenantId, qrId, khachLinkBase, ct);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        return Ok(new QrChannelDetailDto(
            detail.QrId, detail.BookingLink, detail.QrCodePngBase64, detail.IsActive, detail.RevokedAt));
    }

    [HttpPost("qr-channels/{qrId:guid}/revoke")]
    public async Task<IActionResult> RevokeQrChannel(Guid qrId, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        await _qrService.RevokeQrAsync(tenantId, qrId, ct);
        return Ok(new { message = "Đã thu hồi mã QR." });
    }

    /// <summary>Salesmen (active CommunityRole Salesman) trong tenant — cho QR channel + commission filter.</summary>
    [HttpGet("salesmen")]
    public async Task<ActionResult<List<SalesmanDto>>> GetSalesmen(CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        var salesmen = await (from r in _dbContext.CommunityRoles.IgnoreQueryFilters()
                              join c in _dbContext.Customers.IgnoreQueryFilters() on r.CustomerId equals c.Id into gj
                              from c in gj.DefaultIfEmpty()
                              where r.TenantId == tenantId && r.RoleType == CommunityRoleType.Salesman && r.IsActive
                              select new SalesmanDto(r.CustomerId, c != null ? c.FullName : "CTV", r.CreatedAt))
            .ToListAsync(ct);
        return Ok(salesmen);
    }

    // ── Commission ledger (P5.6 — D3 ledger hợp nhất, SRS §17) ───────────────

    [HttpGet("commission/ledger")]
    public async Task<ActionResult<List<CommissionLedgerDto>>> GetCommissionLedger(
        [FromQuery] Guid? salesmanId = null, [FromQuery] Guid? bookingId = null, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        var entries = await _commissionService.GetLedgerAsync(tenantId, salesmanId, bookingId, ct);
        var results = new List<CommissionLedgerDto>();
        foreach (var entry in entries)
        {
            string? bookingCode = null;
            if (entry.BookingId is not null)
                bookingCode = await _dbContext.Bookings.IgnoreQueryFilters()
                    .Where(b => b.Id == entry.BookingId.Value)
                    .Select(b => b.PublicBookingCode)
                    .FirstOrDefaultAsync(ct);
            results.Add(new CommissionLedgerDto(
                entry.Id, entry.SourceType.ToString(), entry.BookingId, bookingCode, entry.SalesmanId,
                entry.BaseAmount, entry.GrossCommissionAmount, entry.TaxWithheldAmount, entry.NetCommissionAmount,
                entry.TaxRuleVersion, entry.WithholdingReasonCode, entry.State.ToString(), entry.WalletTransactionId,
                entry.FinalizedAt, entry.PaidAt));
        }
        return Ok(results);
    }

    [HttpPost("commission/ledger/{entryId:guid}/pay")]
    public async Task<ActionResult<CommissionLedgerDto>> PayCommission(Guid entryId, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        try
        {
            CommissionLedgerEntry entry = await _commissionService.PayCommissionAsync(tenantId, entryId, ct);
            return Ok(new CommissionLedgerDto(
                entry.Id, entry.SourceType.ToString(), entry.BookingId, null, entry.SalesmanId,
                entry.BaseAmount, entry.GrossCommissionAmount, entry.TaxWithheldAmount, entry.NetCommissionAmount,
                entry.TaxRuleVersion, entry.WithholdingReasonCode, entry.State.ToString(), entry.WalletTransactionId,
                entry.FinalizedAt, entry.PaidAt));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // ── Deposits (P5.6 — SRS §16) ────────────────────────────────────────────

    /// <summary>Bookings cần/đã đặt cọc (deposit tracking page).</summary>
    [HttpGet("deposits")]
    public async Task<ActionResult<List<DepositItemDto>>> GetDeposits(CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        var bookings = await _dbContext.Bookings.IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && b.DepositRequired)
            .OrderBy(b => b.StartAt)
            .ToListAsync(ct);

        var results = new List<DepositItemDto>();
        foreach (var booking in bookings)
        {
            PaymentTransaction? deposit = await _financialService.GetDepositAsync(tenantId, booking.Id, ct);
            results.Add(new DepositItemDto(
                booking.Id, booking.PublicBookingCode, booking.OfferingNameSnapshot, booking.StartAt,
                booking.DepositAmount, booking.DepositType?.ToString(), booking.PaymentStatus.ToString(),
                deposit?.Id, deposit?.Status.ToString(), booking.CustomerDeviceId, booking.Status.ToString()));
        }
        return Ok(results);
    }

    /// <summary>Xác nhận đã nhận cọc (get-or-create PENDING → PAID + outbox DepositPaid §16.6).</summary>
    [HttpPost("bookings/{bookingId:guid}/deposit/received")]
    public async Task<ActionResult<FinancialStatusDto>> MarkDepositReceived(Guid bookingId, [FromBody] DepositReceivedRequest? request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        try
        {
            PaymentTransaction? existing = await _financialService.GetDepositAsync(tenantId, bookingId, ct);
            PaymentTransaction tx;
            if (existing is null)
            {
                BookingEntity? booking = await _bookingService.GetBookingAsync(tenantId, bookingId, ct);
                if (booking is null)
                    return NotFound(new { message = "Không tìm thấy lịch hẹn." });
                decimal amount = request?.Amount ?? booking.DepositAmount ?? booking.EstimatedTotal;
                tx = await _financialService.CaptureDepositAsync(tenantId, bookingId, amount, "CASH", request?.ProviderRef, ct);
                if (tx.Status != PaymentStatus.Paid)
                    tx = await _financialService.ConfirmDepositAsync(tenantId, bookingId, tx.Id, request?.ProviderRef, ct);
            }
            else
            {
                tx = existing.Status == PaymentStatus.Paid
                    ? existing
                    : await _financialService.ConfirmDepositAsync(tenantId, bookingId, existing.Id, request?.ProviderRef, ct);
            }
            return Ok(new FinancialStatusDto(bookingId, true, tx.Amount, tx.Type.ToString(), tx.Status.ToString(), tx.Id,
                tx.Status.ToString(), "Pending", "OnPayment", tx.Amount, null));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("bookings/{bookingId:guid}/deposit/refund")]
    public async Task<ActionResult<FinancialStatusDto>> RefundDeposit(Guid bookingId, [FromBody] DepositRefundRequest? request, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        try
        {
            PaymentTransaction tx = await _financialService.RefundDepositAsync(tenantId, bookingId, request?.ProviderRef, ct);
            return Ok(new FinancialStatusDto(bookingId, true, tx.Amount, tx.Type.ToString(), tx.Status.ToString(), tx.Id,
                tx.Status.ToString(), "Pending", "OnPayment", tx.Amount, null));
        }
        catch (ValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (NotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    // ── Eligible staff cho assignment (P5.1 — SRS §13: capability + available) ─

    /// <summary>Staff có skill phù hợp offering + availability tại slot booking (server re-check §12).</summary>
    [HttpGet("bookings/{bookingId:guid}/eligible-staff")]
    public async Task<ActionResult<List<EligibleStaffDto>>> GetEligibleStaff(Guid bookingId, CancellationToken ct = default)
    {
        TenantId tenantId = ResolveTenantId();
        if (tenantId.Value == Guid.Empty)
            return Unauthorized(new { error = "Missing tenant_id claim" });
        BookingEntity? booking = await _bookingService.GetBookingAsync(tenantId, bookingId, ct);
        if (booking is null)
            return NotFound(new { message = "Không tìm thấy lịch hẹn." });

        var eligible = await _staffService.ListEligibleStaffAsync(tenantId, booking.OfferingId, true, ct);
        var results = new List<EligibleStaffDto>();
        foreach (var staff in eligible)
        {
            AvailabilityCheckResult check = await _availabilityService.ValidateSlotAsync(tenantId, booking.OfferingId, staff.Id, booking.StartAt, ct);
            results.Add(new EligibleStaffDto(staff.Id, staff.DisplayName, check.IsAvailable, check.UnavailableReason));
        }
        return Ok(results.OrderByDescending(s => s.IsAvailable).ThenBy(s => s.StaffName).ToList());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private TenantId ResolveTenantId()
    {
        string? tenantClaim = User.FindFirst("tenant_id")?.Value
            ?? User.FindFirst("TenantId")?.Value;
        return Guid.TryParse(tenantClaim, out Guid tenantId) ? new TenantId(tenantId) : new TenantId(Guid.Empty);
    }

    private static bool TryParseTime(string? value, out TimeSpan result)
        => TimeSpan.TryParseExact(value, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out result);

    private static TimeSpan? TryParseTimeOrNull(string? value)
        => TryParseTime(value, out TimeSpan result) ? result : null;

    private static StaffDto ToStaffDto(Staff s) => new(s.Id, s.DisplayName, s.Role, s.AvatarUrl, s.IsActive);

    /// <summary>Gom tên/SĐT/ghi chú khách vào CustomerNote (CustomerId null — khách chưa có tài khoản, MVP).</summary>
    private static string? BuildCustomerNote(string? customerName, string? customerPhone, string? customerNote)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(customerName)) parts.Add($"KH: {customerName.Trim()}");
        if (!string.IsNullOrWhiteSpace(customerPhone)) parts.Add($"SĐT: {customerPhone.Trim()}");
        if (!string.IsNullOrWhiteSpace(customerNote)) parts.Add(customerNote.Trim());
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static BookingTenantConfigDto ToConfigDto(BookingTenantConfig c) => new(
        c.IsEnabled, c.DepositPolicy.ToString(), c.DepositFixedAmount, c.DepositPercentage, c.CancelReschedulePolicy, c.EinvoiceMode);

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
    DateTime? CompletedAt,
    Guid? OrderId)   // RV P6 P2: D2 hook link — queue/chi tiết hiển thị Order tạo từ booking
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
        b.CompletedAt,
        b.OrderId);
}

public record RejectBookingRequest(string? Reason);

public record AssignStaffRequest(Guid StaffId);

public record CompleteBookingRequest(decimal ActualTotal);

/// <summary>Feature 3 (2026-10-08): tạo lịch hẹn từ ShopERP — staff/owner đặt thay khách.</summary>
public record CreateTenantBookingRequest(
    Guid OfferingId, List<Guid>? AddOnIds, DateTime StartAt, Guid? StaffId,
    string? CustomerName = null, string? CustomerPhone = null, string? CustomerNote = null);

/// <summary>Catalog CRUD (Fix 2026-10-09 — bug 2: UI quản lý dịch vụ đặt lịch).</summary>
public record UpsertCategoryRequest(string Name, int DisplayOrder = 0, bool IsActive = true);

public record UpsertOfferingRequest(
    string DisplayName, string OfferingType, int DurationMinutes, decimal Price,
    Guid? CategoryId = null, string? RequiredSkillCode = null, string? Description = null, bool IsActive = true);

public record UpsertAddOnRequest(string Name, decimal Price, bool IsActive = true);

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

// ── P5 DTOs (Staff CRUD / catalog / schedules / config / QR / commission / deposits / eligible) ──

public record CreateStaffRequest(string DisplayName, string? Role = null, Guid? StaffUserId = null);

public record UpdateStaffRequest(string DisplayName, string? Role, string? AvatarUrl, Guid? StaffUserId, bool IsActive);

public record StaffServiceDto(Guid OfferingId);

public record SetStaffServicesRequest(List<Guid>? OfferingIds);

public record UpsertScheduleRequest(int? Weekday, string? StartTime, string? EndTime, string? BreakStart, string? BreakEnd);

public record AddOverrideRequest(string Date, string OverrideType, string? StartTime = null, string? EndTime = null, string? Note = null);

public record BookingTenantConfigDto(
    bool IsEnabled, string DepositPolicy, decimal? DepositFixedAmount, decimal? DepositPercentage,
    string? CancelReschedulePolicy, string? EinvoiceMode);

public record UpdateBookingConfigRequest(
    bool IsEnabled, string DepositPolicy, decimal? DepositFixedAmount = null, decimal? DepositPercentage = null,
    string? CancelReschedulePolicy = null, string? EinvoiceMode = null);

public record CreateQrChannelRequest(string QrToken, Guid? SalesmanId = null, Guid? CampaignId = null, DateTime? AttributionExpiryAt = null);

public record QrChannelDto(Guid Id, string QrTokenHash, Guid? SalesmanId, Guid? CampaignId, bool IsActive, DateTime? RevokedAt, DateTime CreatedAt);

/// <summary>Create response — RawToken trả về ĐÚNG 1 lần (UI cần hiển thị link đặt lịch; DB chỉ lưu hash §7.2).</summary>
public record QrChannelCreatedDto(
    Guid Id, string QrTokenHash, string RawToken, Guid? SalesmanId, Guid? CampaignId,
    bool IsActive, DateTime? RevokedAt, DateTime CreatedAt);

/// <summary>Q1 (2026-10-08 — issue #188 bug 2): chi tiết QR — link đặt lịch + QR PNG (render lại sau khi tạo).</summary>
public record QrChannelDetailDto(
    Guid Id, string? BookingLink, string? QrCodePngBase64, bool IsActive, DateTime? RevokedAt);

public record SalesmanDto(Guid CustomerId, string Name, DateTime CreatedAt);

public record CommissionLedgerDto(
    Guid EntryId, string SourceType, Guid? BookingId, string? BookingCode, Guid SalesmanId,
    decimal BaseAmount, decimal GrossCommissionAmount, decimal TaxWithheldAmount, decimal NetCommissionAmount,
    string? TaxRuleVersion, string? WithholdingReasonCode, string State, Guid? WalletTransactionId,
    DateTime? FinalizedAt, DateTime? PaidAt);

public record DepositItemDto(
    Guid BookingId, string PublicBookingCode, string OfferingName, DateTime StartAt,
    decimal? DepositAmount, string? DepositType, string PaymentStatus,
    Guid? DepositTransactionId, string? DepositStatus, string? CustomerDeviceId, string BookingStatus);

public record DepositReceivedRequest(decimal? Amount, string? ProviderRef = null);

public record DepositRefundRequest(string? ProviderRef = null);

public record EligibleStaffDto(Guid StaffId, string StaffName, bool IsAvailable, string? UnavailableReason);
