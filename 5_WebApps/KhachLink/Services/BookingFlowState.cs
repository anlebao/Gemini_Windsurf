using VanAn.KhachLink.Models;

namespace VanAn.KhachLink.Services;

/// <summary>
/// Scoped state cho flow đặt lịch 4 màn hình (P4.3-4.6). Giữ lựa chọn giữa các màn
/// (pattern CheckoutFlowState hiện có). ID của khách (anonymous session + device) sinh
/// 1 lần, persist trong localStorage để attribution session ổn định (§7.4).
/// </summary>
public class BookingFlowState
{
    public string? QrToken { get; set; }

    public QrResolveResultDto? Qr { get; set; }
    public BookingCatalogDto? Catalog { get; set; }

    public BookingOfferingDto? Offering { get; set; }
    public List<BookingAddOnDto> SelectedAddOns { get; set; } = [];

    public DateTime? SelectedSlotStart { get; set; }   // local time of chosen slot
    public Guid? SelectedStaffId { get; set; }
    public string? SelectedStaffName { get; set; }
    public string? StaffFilterName { get; set; }       // "Bất kỳ ai" | staff name đã chọn filter

    public string? Note { get; set; }
    public List<string> QuickTags { get; set; } = [];

    /// <summary>Idempotency-Key (SRS §21.1) — sinh 1 lần cho cả flow; retry sau mất mạng không tạo booking trùng (§29).</summary>
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");

    public string? AnonymousSessionId { get; set; }
    public string? DeviceId { get; set; }

    public CreateBookingResultDto? CreatedBooking { get; set; }

    public bool HasSelectedSlot => SelectedSlotStart.HasValue;

    public decimal ComputeTotal(decimal offeringPrice)
        => offeringPrice + SelectedAddOns.Sum(a => a.Price);

    /// <summary>Deposit amount hiển thị (server-authoritative khi create — booking.SetDepositRequirement).</summary>
    public decimal? ComputeDepositAmount(decimal estimatedTotal)
    {
        if (Catalog is null || !Catalog.DepositPolicy.HasDeposit)
            return null;
        return Catalog.DepositPolicy.Policy switch
        {
            "Fixed" => Catalog.DepositPolicy.FixedAmount,
            "Percentage" when Catalog.DepositPolicy.Percentage is > 0
                => Math.Round(estimatedTotal * Catalog.DepositPolicy.Percentage.Value / 100m, 0, MidpointRounding.AwayFromZero),
            _ => null
        };
    }

    public void ResetSelections()
    {
        Offering = null;
        SelectedAddOns = [];
        SelectedSlotStart = null;
        SelectedStaffId = null;
        SelectedStaffName = null;
        StaffFilterName = null;
        Note = null;
        QuickTags = [];
        CreatedBooking = null;
        IdempotencyKey = Guid.NewGuid().ToString("N");
    }
}
