using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Phase 4 (2026-10-05): dispatcher — gọi từng kênh đã cấu hình (Telegram/Zalo).
    /// Fail-safe: lỗi 1 kênh không làm fail các kênh khác / không fail đóng ca (SRS §4.3).
    /// </summary>
    public sealed class AlertNotifierDispatcher(
        TelegramAlertNotifier telegram,
        ZaloAlertNotifier zalo,
        ILogger<AlertNotifierDispatcher> logger) : IAlertNotifier
    {
        private readonly TelegramAlertNotifier _telegram = telegram;
        private readonly ZaloAlertNotifier _zalo = zalo;
        private readonly ILogger<AlertNotifierDispatcher> _logger = logger;

        public async Task NotifyAsync(IReadOnlyList<ShiftAlert> alerts, VaIIeTenantConfig config, CancellationToken ct = default)
        {
            if (alerts.Count == 0)
            {
                return;
            }
            await NotifyChannelAsync(() => _telegram.NotifyAsync(alerts, config, ct), "Telegram");
            await NotifyChannelAsync(() => _zalo.NotifyAsync(alerts, config, ct), "Zalo");
        }

        private async Task NotifyChannelAsync(Func<Task> send, string channel)
        {
            try
            {
                await send();
            }
            catch (Exception ex)
            {
                // Fail-safe: không throw — alert in-app vẫn hoạt động (SRS §4.3).
                _logger.LogWarning(ex, "AlertNotifier: kênh {Channel} thất bại — bỏ qua", channel);
            }
        }
    }
}
