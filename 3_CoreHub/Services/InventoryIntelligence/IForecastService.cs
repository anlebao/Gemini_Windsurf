using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>Trạng thái dự báo 1 nguyên liệu.</summary>
    public enum ForecastStatus
    {
        /// <summary>Còn đủ hàng (DaysRemaining &gt; 2 × threshold).</summary>
        Ok = 0,

        /// <summary>Sắp cạn (DaysRemaining ≤ 2 × threshold).</summary>
        Warning = 1,

        /// <summary>Nguy cơ hết hàng (DaysRemaining ≤ threshold) hoặc hết hàng rồi.</summary>
        Critical = 2,

        /// <summary>Chưa đủ dữ liệu tiêu hao (0 ca đóng trong window).</summary>
        NoData = 3
    }

    /// <summary>1 dòng dự báo (Restock hoặc Stockout) — SRS §7.5.</summary>
    public sealed record ForecastItem(
        Guid IngredientId,
        string Name,
        string Unit,
        decimal CurrentStock,
        decimal AvgDailyConsumption,
        int DaysRemaining,
        int ThresholdDays,
        decimal SuggestedOrderQuantity,
        ForecastStatus Status);

    /// <summary>Tiêu hao 1 ngày của 1 nguyên liệu (trend — SRS §8.6).</summary>
    public sealed record DailyConsumption(DateTime Date, decimal Quantity);

    /// <summary>Báo cáo dự báo đầy đủ (Restock + Stockout + Trend + config snapshot).</summary>
    public sealed record ForecastReport(
        DateTime GeneratedAt,
        int WindowDays,
        int LeadTimeDays,
        int SafetyDays,
        IReadOnlyList<ForecastItem> RestockItems,
        IReadOnlyList<ForecastItem> StockoutItems,
        IReadOnlyDictionary<Guid, IReadOnlyList<DailyConsumption>> Trend);

    /// <summary>
    /// VA-IIE Phase 3 (2026-10-05): Restock Forecast + Stockout Forecast (SRS §7.5, §8.6).
    /// AvgDailyConsumption = Σ TheoreticalConsumption trong rolling window ÷ số ngày có ca đóng (min 1);
    /// StockoutDays = CurrentStock ÷ ADC; cảnh báo khi &lt; LeadTimeDays + SafetyDays;
    /// Restock suggestion = max(0, ADC × (LeadTime + Safety) − CurrentStock).
    /// Config per-tenant chung (VaIIeTenantConfig) — quyết định user 2026-10-05.
    /// Multi-tenancy: mọi query filter theo TenantId (bài học RV a21f97f2).
    /// </summary>
    public interface IForecastService
    {
        /// <summary>Get-or-create config forecast per-tenant (window/leadtime/safety).</summary>
        Task<VaIIeTenantConfig> GetConfigAsync(TenantId tenantId, CancellationToken ct = default);

        /// <summary>Cập nhật config forecast per-tenant.</summary>
        Task<VaIIeTenantConfig> UpdateConfigAsync(TenantId tenantId, int windowDays, int leadTimeDays, int safetyDays, CancellationToken ct = default);

        /// <summary>Cập nhật config notification bot per-tenant (Phase 4 — config-only: Telegram/Zalo).</summary>
        Task<VaIIeTenantConfig> UpdateNotificationConfigAsync(
            TenantId tenantId,
            bool telegramEnabled, string? telegramBotToken, string? telegramChatId,
            bool zaloEnabled, string? zaloAccessToken, string? zaloRecipientId,
            CancellationToken ct = default);

        /// <summary>Tính Restock + Stockout forecast cho tenant (pure read — không persist).</summary>
        Task<ForecastReport> GetForecastAsync(TenantId tenantId, CancellationToken ct = default);
    }
}
