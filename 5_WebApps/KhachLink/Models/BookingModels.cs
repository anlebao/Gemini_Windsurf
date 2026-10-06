using System.Text.Json.Serialization;

namespace VanAn.KhachLink.Models;

// ── QR resolve (P4.1 — GET /api/public/booking/qr/{token}) ──────────────────

public class QrResolveResultDto
{
    public Guid QrId { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public Guid? SalesmanId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid AttributionSessionId { get; set; }
    public bool IsQualified { get; set; }
    public bool IsNewSession { get; set; }
}

// ── Catalog (GET /api/public/booking/tenants/{id}/services) ─────────────────

public class BookingCatalogDto
{
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public List<BookingCategoryDto> Categories { get; set; } = [];
    public List<BookingOfferingDto> Offerings { get; set; } = [];
    public List<BookingAddOnDto> AddOns { get; set; } = [];
    public BookingDepositPolicyDto DepositPolicy { get; set; } = new();
    public string? CancelPolicy { get; set; }
}

public class BookingCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public class BookingOfferingDto
{
    public Guid Id { get; set; }
    public Guid? CategoryId { get; set; }
    public string OfferingType { get; set; } = "Service";
    public string DisplayName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }

    [JsonIgnore]
    public bool IsPackage => OfferingType == "Package";
}

public class BookingAddOnDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class BookingDepositPolicyDto
{
    public string Policy { get; set; } = "None"; // None | Fixed | Percentage
    public decimal? FixedAmount { get; set; }
    public decimal? Percentage { get; set; }

    [JsonIgnore]
    public bool HasDeposit => Policy == "Fixed" || Policy == "Percentage";
}

// ── Availability (GET /api/public/booking/availability) ──────────────────────

public class AvailableSlotDto
{
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public Guid StaffId { get; set; }
    public string StaffName { get; set; } = string.Empty;
}

// ── Create booking (POST /api/public/booking/bookings) ───────────────────────

public class CreateBookingRequestDto
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

public class PublicAddOnLineDto
{
    public Guid AddOnId { get; set; }
    public int Quantity { get; set; }
}

public class CreateBookingResultDto
{
    public string PublicBookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OfferingName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal EstimatedTotal { get; set; }
    public bool DepositRequired { get; set; }
    public decimal? DepositAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}

// ── Status polling (GET /api/public/booking/bookings/{token}) ────────────────

public class PublicBookingStatusDto
{
    public string PublicBookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SubState { get; set; } = "None";
    public Guid? StaffId { get; set; }
    public string? StaffName { get; set; }
    public string OfferingName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal EstimatedTotal { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string InvoiceStatus { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? OrderId { get; set; }

    [JsonIgnore]
    public bool IsTerminal => Status is "Completed" or "Cancelled" or "Rejected" or "NoShow";
}

/// <summary>API error message (server trả message thân thiện theo §6.5 — không lộ mã kỹ thuật).</summary>
public class BookingApiError
{
    public string Message { get; set; } = string.Empty;
}
