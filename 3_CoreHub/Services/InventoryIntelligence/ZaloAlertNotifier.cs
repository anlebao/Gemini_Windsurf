using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Phase 4 (2026-10-05): Zalo notifier — CONFIG-ONLY stub (quyết định user 2026-10-05).
    /// Contract + payload sẵn sàng; implement HTTP (ZNS API — cần OA approved) khi có access token.
    /// KHÔNG echo token vào log/conversation (secrets governance).
    /// </summary>
    public sealed class ZaloAlertNotifier(ILogger<ZaloAlertNotifier> logger) : IAlertNotifier
    {
        private readonly ILogger<ZaloAlertNotifier> _logger = logger;

        public Task NotifyAsync(IReadOnlyList<ShiftAlert> alerts, VaIIeTenantConfig config, CancellationToken ct = default)
        {
            if (!config.ZaloEnabled)
            {
                return Task.CompletedTask;
            }
            if (string.IsNullOrWhiteSpace(config.ZaloAccessToken) || string.IsNullOrWhiteSpace(config.ZaloRecipientId))
            {
                _logger.LogWarning("Zalo alert enabled nhưng chưa cấu hình AccessToken/RecipientId — bỏ qua (config-only MVP)");
                return Task.CompletedTask;
            }

            string message = BuildMessage(alerts);
            // CONFIG-ONLY: chưa gọi API thật. Khi có token → POST ZNS api.openapi.zalo.me (cần OA approved).
            _logger.LogInformation("ZaloAlertNotifier (config-only): sẽ gửi {Count} alert(s) tới recipient {Recipient} — {Message}",
                alerts.Count, MaskRecipient(config.ZaloRecipientId), message.ReplaceLineEndings(" "));
            return Task.CompletedTask;
        }

        /// <summary>Payload tin nhắn (testable): danh sách alert Critical/Warning kèm mã.</summary>
        public static string BuildMessage(IReadOnlyList<ShiftAlert> alerts)
        {
            IEnumerable<string> lines = alerts.Select(a =>
                $"[{(a.Severity == AlertSeverity.Critical ? "CRITICAL" : "WARNING")}] {a.AlertCode}: {a.Message}");
            return $"Cảnh báo Vạn An ({alerts.Count})\n" + string.Join("\n", lines);
        }

        private static string MaskRecipient(string recipient)
            => recipient.Length <= 4 ? "****" : recipient[..2] + "****" + recipient[^2..];
    }
}
