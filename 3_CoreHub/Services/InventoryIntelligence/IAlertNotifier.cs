using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Phase 4 (2026-10-05): kênh thông báo alert — CONFIG-ONLY (quyết định user 2026-10-05).
    /// MVP: xây contract + config UI cho Telegram &amp; Zalo; KHÔNG gọi API thật (chưa có token).
    /// Khi có token → implement HTTP call trong TelegramAlertNotifier/ZaloAlertNotifier (hook đã sẵn).
    /// In-app alert luôn hoạt động (SRS §4.3) — notifier là kênh phụ, fail-safe (không fail đóng ca).
    /// </summary>
    public interface IAlertNotifier
    {
        /// <summary>Gửi danh sách cảnh báo qua các kênh đã cấu hình per-tenant (không throw — fail-safe).</summary>
        Task NotifyAsync(IReadOnlyList<ShiftAlert> alerts, VaIIeTenantConfig config, CancellationToken ct = default);
    }
}
