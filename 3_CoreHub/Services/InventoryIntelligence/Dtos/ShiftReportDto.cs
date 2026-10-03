using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence.Dtos
{
    /// <summary>Phần 1 — thông tin ca (SRS §5.1).</summary>
    public sealed record ShiftDto(
        Guid Id,
        ShiftType ShiftType,
        ShiftStatus Status,
        DateTime StartTime,
        DateTime? EndTime,
        Guid StaffUserId,
        Guid? AcknowledgedBy,
        DateTime? AcknowledgedAt,
        string? HandoverNotes);

    /// <summary>Phần 2 — kiểm kê đầu/cuối ca (SRS §5.1).</summary>
    public sealed record InventoryCountDto(
        Guid IngredientId,
        string IngredientName,
        string Unit,
        CountType CountType,
        decimal Quantity,
        decimal? MidShiftStockIn);

    /// <summary>Phần 3-4 — tiêu hao lý thuyết vs thực tế + variance (SRS §5.1).</summary>
    public sealed record ConsumptionDto(
        Guid IngredientId,
        string IngredientName,
        string Unit,
        decimal TheoreticalQuantity,
        decimal ActualQuantity,
        decimal Variance,
        decimal VariancePercent,
        VarianceClassification Classification);

    /// <summary>Phần 5 — tiền mặt (SRS §5.1).</summary>
    public sealed record CashSectionDto(decimal? CashCount, decimal? PosCashTotal, decimal? Difference, bool IsMatched);

    /// <summary>Phần 6 — cảnh báo (SRS §5.1).</summary>
    public sealed record ShiftAlertDto(
        Guid Id,
        string AlertCode,
        AlertSeverity Severity,
        string Message,
        Guid? IngredientId,
        decimal? VarianceValue,
        decimal? VariancePercent,
        bool IsResolved);

    /// <summary>Đơn hàng trong ca (context cho phần 3).</summary>
    public sealed record OrderSummaryDto(Guid OrderId, string OrderType, decimal TotalPrice, DateTime CreatedAt, int ItemCount);

    /// <summary>Báo cáo cuối ca đầy đủ — 6 phần (SRS §5.1).</summary>
    public sealed record ShiftReportDto
    {
        public required ShiftDto Shift { get; init; }
        public required IReadOnlyList<InventoryCountDto> OpeningCounts { get; init; }
        public required IReadOnlyList<InventoryCountDto> ClosingCounts { get; init; }
        public required IReadOnlyList<ConsumptionDto> Consumptions { get; init; }
        public required CashSectionDto Cash { get; init; }
        public required IReadOnlyList<ShiftAlertDto> Alerts { get; init; }
        public required IReadOnlyList<OrderSummaryDto> Orders { get; init; }
    }
}
