using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Booking;
using VanAn.Shared.Domain;
// Namespace "Booking" xung đột type Booking (lesson P1 — CS0118) → alias cho domain entity.
using BookingEntity = VanAn.Shared.Domain.Booking;
// Services-layer exceptions (Exceptions.cs) — KHÁC VanAn.Shared.Domain.NotFoundException/ValidationException
// (RV P6 bug: catch không alias → services exception không được bắt → 500 thay vì 404/400 friendly).
using NotFoundException = VanAn.CoreHub.Services.NotFoundException;
using ValidationException = VanAn.CoreHub.Services.ValidationException;

namespace VanAn.Gateway.Controllers;

/// <summary>
/// Booking P4.1 (SRS §20 — public booking API). Anonymous + rate-limited (§25).
///
/// Endpoints (baseline §20, map vào conventions hiện hữu):
///   GET  /api/public/booking/qr/{qrToken}?anonymousSessionId=     → resolve QR → tenant branding + attribution session
///   GET  /api/public/booking/tenants/{tenantId}/services          → catalog (categories/offerings/add-ons + deposit policy)
///   GET  /api/public/booking/availability?tenantId=&offeringId=&date=&staffId= → available slots (§11.5)
///   POST /api/public/booking/bookings                             → create (Idempotency-Key §21.1)
///   GET  /api/public/booking/bookings/{publicBookingToken}        → status polling + ETag (§10.3)
///   POST /api/public/booking/bookings/{publicBookingToken}/cancel → cancel
///
/// Security (§25): KHÔNG trả internal IDs không cần / commission / staff conflicts / private data khách khác.
/// Error messages thân thiện theo §6.5 (không trả mã kỹ thuật cho customer).
/// </summary>
[ApiController]
[Route("api/public/booking")]
[EnableRateLimiting("booking-public")]
public class PublicBookingController(
    IQRAttributionService qrService,
    IOfferingService offeringService,
    IAvailabilityService availabilityService,
    IBookingService bookingService,
    VanAnDbContext dbContext,
    ILogger<PublicBookingController> logger) : ControllerBase
{
    private readonly IQRAttributionService _qrService = qrService;
    private readonly IOfferingService _offeringService = offeringService;
    private readonly IAvailabilityService _availabilityService = availabilityService;
    private readonly IBookingService _bookingService = bookingService;
    private readonly VanAnDbContext _dbContext = dbContext;
    private readonly ILogger<PublicBookingController> _logger = logger;

    /// <summary>
    /// Resolve QR token → tenant + salesman + attribution session (SRS §7.3-7.6).
    /// Khách không đăng nhập — anonymousSessionId từ localStorage (zero-friction §4.1).
    /// </summary>
    [HttpGet("qr/{qrToken}")]
    [AllowAnonymous]
    public async Task<ActionResult<QrResolveResponse>> ResolveQr(
        string qrToken,
        [FromQuery] string? anonymousSessionId,
        CancellationToken ct = default)
    {
        string sessionId = string.IsNullOrWhiteSpace(anonymousSessionId)
            ? Guid.NewGuid().ToString("N")
            : anonymousSessionId;

        QrResolveResult result;
        try
        {
            result = await _qrService.ResolveQrAsync(qrToken, sessionId, ct);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        // Tenant branding cho Screen 1 (§5.2).
        string tenantName = await GetTenantNameAsync(result.TenantId, ct);

        _logger.LogInformation("Public QR resolved: token={Token} tenant={TenantId} qualified={Qualified} newSession={New}",
            qrToken[..Math.Min(8, qrToken.Length)], result.TenantId.Value, result.IsQualified, result.IsNewSession);

        return Ok(new QrResolveResponse(
            QrId: result.QrId,
            TenantId: result.TenantId.Value,
            TenantName: tenantName,
            SalesmanId: result.SalesmanId,
            CampaignId: result.CampaignId,
            AttributionSessionId: result.AttributionSessionId,
            IsQualified: result.IsQualified,
            IsNewSession: result.IsNewSession));
    }

    /// <summary>
    /// Catalog booking (SRS §8.2-8.3, Screen 1). Chỉ trả offering/add-on active + deposit policy hiển thị (§16.1).
    /// Feature-flag Q5: tenant chưa enable → 404 friendly.
    /// </summary>
    [HttpGet("tenants/{tenantId:guid}/services")]
    [AllowAnonymous]
    public async Task<ActionResult<BookingCatalogResponse>> GetServices(Guid tenantId, CancellationToken ct = default)
    {
        var tid = new TenantId(tenantId);

        bool tenantExists = await _dbContext.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tid, ct);
        if (!tenantExists)
            return NotFound(new { message = "Cửa hàng không tồn tại hoặc chưa mở đặt lịch hẹn." });

        // Q5 feature-flag (D5): chỉ tenant enabled mới resolve catalog.
        BookingTenantConfig config;
        try
        {
            config = await _bookingService.GetConfigAsync(tid, ct);
        }
        catch (NotFoundException)
        {
            return NotFound(new { message = "Cửa hàng không tồn tại hoặc chưa mở đặt lịch hẹn." });
        }
        if (!config.IsEnabled)
            return NotFound(new { message = "Cửa hàng không tồn tại hoặc chưa mở đặt lịch hẹn." });

        string tenantName = await GetTenantNameAsync(tid, ct);

        var categories = await _offeringService.ListCategoriesAsync(tid, true, ct);
        var offerings = await _offeringService.ListOfferingsAsync(tid, null, true, ct);
        var addOns = await _offeringService.ListAddOnsAsync(tid, true, ct);

        return Ok(new BookingCatalogResponse
        {
            TenantId = tenantId,
            TenantName = tenantName,
            Categories = categories.Select(c => new BookingCategoryDto(c.Id, c.Name, c.DisplayOrder)).ToList(),
            Offerings = offerings.Select(o => new BookingOfferingDto(
                o.Id, o.CategoryId, o.OfferingType.ToString(), o.DisplayName,
                o.DurationMinutes, o.Price, o.Description)).ToList(),
            AddOns = addOns.Select(a => new BookingAddOnDto(a.Id, a.Name, a.Price)).ToList(),
            DepositPolicy = new BookingDepositPolicyDto(
                config.DepositPolicy.ToString(),
                config.DepositFixedAmount,
                config.DepositPercentage),
            CancelPolicy = config.CancelReschedulePolicy
        });
    }

    /// <summary>Available slots (SRS §11.5 — chỉ trả available, KHÔNG trả lý do unavailable cho khách, Risk 3).</summary>
    [HttpGet("availability")]
    [AllowAnonymous]
    public async Task<ActionResult<List<AvailableSlotResponse>>> GetAvailability(
        [FromQuery] Guid tenantId,
        [FromQuery] Guid offeringId,
        [FromQuery] string date,
        [FromQuery] Guid? staffId = null,
        CancellationToken ct = default)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly day))
            return BadRequest(new { message = "Ngày không hợp lệ (định dạng yyyy-MM-dd)." });

        var slots = await _availabilityService.GetAvailableSlotsAsync(new TenantId(tenantId), offeringId, day, staffId, ct);
        return Ok(slots.Select(s => new AvailableSlotResponse(s.StartAt, s.EndAt, s.StaffId, s.StaffName)).ToList());
    }

    /// <summary>
    /// Create booking (SRS §21.1 — Idempotency-Key bắt buộc; retry cùng key không tạo booking trùng).
    /// Conflict → 409 message thân thiện (§6.5); KHÔNG báo thành công trước server persist (§29).
    /// </summary>
    [HttpPost("bookings")]
    [AllowAnonymous]
    public async Task<ActionResult<CreateBookingResponse>> CreateBooking(
        [FromBody] CreatePublicBookingRequest request,
        CancellationToken ct = default)
    {
        string? idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { message = "Thiếu mã chống trùng (Idempotency-Key). Vui lòng thử lại." });

        var command = new CreateBookingCommand(
            request.OfferingId,
            request.StartAt,
            request.StaffId,
            CustomerId: null, // zero-friction anonymous (§4.1) — identity qua CustomerDeviceId
            request.CustomerDeviceId,
            request.CustomerNote,
            request.AddOns?.Select(a => new AddOnLine(a.AddOnId, a.Quantity)).ToList() ?? [],
            request.AttributionId);

        try
        {
            BookingEntity booking = await _bookingService.CreateBookingAsync(new TenantId(request.TenantId), command, idempotencyKey, ct);

            _logger.LogInformation("Public booking created: code={Code} booking={BookingId} tenant={TenantId}",
                booking.PublicBookingCode, booking.Id, booking.TenantId.Value);

            return Ok(new CreateBookingResponse(
                booking.PublicBookingCode,
                booking.Status.ToString(),
                booking.OfferingNameSnapshot,
                booking.StartAt,
                booking.EndAt,
                booking.EstimatedTotal,
                booking.DepositRequired,
                booking.DepositAmount,
                booking.PaymentStatus.ToString()));
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

    /// <summary>
    /// Status polling (SRS §10). ETag theo Version (§10.3 — response nhẹ khi không đổi).
    /// KHÔNG trả internal booking Id / commission / conflicts (§25).
    /// </summary>
    [HttpGet("bookings/{publicBookingToken}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicBookingStatusResponse>> GetStatus(
        string publicBookingToken,
        CancellationToken ct = default)
    {
        BookingStatusDto? dto = await _bookingService.GetPublicStatusAsync(publicBookingToken, ct);
        if (dto is null)
            return NotFound(new { message = "Không tìm thấy lịch hẹn này." });

        string etag = $"\"{dto.Version}\"";
        if (Request.Headers.IfNoneMatch.Any(v => string.Equals(v, etag, StringComparison.Ordinal)))
            return StatusCode(StatusCodes.Status304NotModified);

        Response.Headers.ETag = etag;

        string? staffName = null;
        if (dto.StaffId is not null)
        {
            staffName = await _dbContext.Staffs.IgnoreQueryFilters()
                .Where(s => s.Id == dto.StaffId.Value)
                .Select(s => s.DisplayName)
                .FirstOrDefaultAsync(ct);
        }

        return Ok(new PublicBookingStatusResponse(
            dto.PublicBookingCode,
            dto.Status.ToString(),
            dto.SubState.ToString(),
            dto.StaffId,
            staffName,
            dto.OfferingNameSnapshot,
            dto.StartAt,
            dto.EndAt,
            dto.EstimatedTotal,
            dto.PaymentStatus.ToString(),
            dto.InvoiceStatus.ToString(),
            dto.Version,
            dto.CompletedAt,
            dto.OrderId));
    }

    /// <summary>Cancel booking (khách tự hủy — PendingConfirmation/Confirmed; idempotent-safe message).</summary>
    [HttpPost("bookings/{publicBookingToken}/cancel")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicBookingStatusResponse>> Cancel(
        string publicBookingToken,
        [FromBody] CancelPublicBookingRequest request,
        CancellationToken ct = default)
    {
        BookingStatusDto? dto = await _bookingService.GetPublicStatusAsync(publicBookingToken, ct);
        if (dto is null)
            return NotFound(new { message = "Không tìm thấy lịch hẹn này." });

        if (dto.Status is BookingStatus.Cancelled)
            return Ok(new { message = "Lịch hẹn đã được hủy trước đó." });

        try
        {
            // Tenant context: public token → cần tenant để gọi CancelAsync. Tra qua booking entity (public code unique).
            BookingEntity? booking = await _dbContext.Bookings.IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.PublicBookingCode == publicBookingToken, ct);
            if (booking is null)
                return NotFound(new { message = "Không tìm thấy lịch hẹn này." });

            BookingEntity updated = await _bookingService.CancelAsync(
                new TenantId(booking.TenantId.Value), booking.Id, request?.Reason, actorId: null, ct);

            return Ok(new { message = "Đã hủy lịch hẹn.", bookingCode = updated.PublicBookingCode, status = updated.Status.ToString() });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<string> GetTenantNameAsync(TenantId tenantId, CancellationToken ct)
    {
        string? name = await _dbContext.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct);
        return name ?? string.Empty;
    }
}

// ── Request / Response DTOs (public-safe — §25) ──────────────────────────────

public record QrResolveResponse(
    Guid QrId,
    Guid TenantId,
    string TenantName,
    Guid? SalesmanId,
    Guid? CampaignId,
    Guid AttributionSessionId,
    bool IsQualified,
    bool IsNewSession);

public class BookingCatalogResponse
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public List<BookingCategoryDto> Categories { get; set; } = [];
    public List<BookingOfferingDto> Offerings { get; set; } = [];
    public List<BookingAddOnDto> AddOns { get; set; } = [];
    public BookingDepositPolicyDto DepositPolicy { get; set; } = new("None", null, null);
    public string? CancelPolicy { get; set; }
}

public record BookingCategoryDto(Guid Id, string Name, int DisplayOrder);

public record BookingOfferingDto(
    Guid Id,
    Guid? CategoryId,
    string OfferingType,
    string DisplayName,
    int DurationMinutes,
    decimal Price,
    string? Description);

public record BookingAddOnDto(Guid Id, string Name, decimal Price);

public record BookingDepositPolicyDto(string Policy, decimal? FixedAmount, decimal? Percentage);

public record AvailableSlotResponse(DateTime StartAt, DateTime EndAt, Guid StaffId, string StaffName);

public class CreatePublicBookingRequest
{
    public Guid TenantId { get; set; }
    public Guid OfferingId { get; set; }
    public DateTime StartAt { get; set; }
    public Guid? StaffId { get; set; }
    public string? CustomerDeviceId { get; set; }
    public string? CustomerNote { get; set; }
    public List<PublicAddOnLineDto>? AddOns { get; set; }
    public Guid? AttributionId { get; set; }
}

public record PublicAddOnLineDto(Guid AddOnId, int Quantity);

public record CreateBookingResponse(
    string PublicBookingCode,
    string Status,
    string OfferingName,
    DateTime StartAt,
    DateTime EndAt,
    decimal EstimatedTotal,
    bool DepositRequired,
    decimal? DepositAmount,
    string PaymentStatus);

public record PublicBookingStatusResponse(
    string PublicBookingCode,
    string Status,
    string SubState,
    Guid? StaffId,
    string? StaffName,
    string OfferingName,
    DateTime StartAt,
    DateTime EndAt,
    decimal EstimatedTotal,
    string PaymentStatus,
    string InvoiceStatus,
    int Version,
    DateTime? CompletedAt,
    Guid? OrderId);

public class CancelPublicBookingRequest
{
    public string? Reason { get; set; }
}
