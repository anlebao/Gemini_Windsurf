using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Phase 4 (2026-10-05): Telegram notifier — CONFIG-ONLY stub (quyết định user 2026-10-05).
    /// Contract + payload sẵn sàng; implement HTTP (sendMessage) khi có bot token.
    /// KHÔNG echo token vào log/conversation (secrets governance).
    /// </summary>
    public sealed class TelegramAlertNotifier(ILogger<TelegramAlertNotifier> logger) : IAlertNotifier
    {
        private readonly ILogger<TelegramAlertNotifier> _logger = logger;

        public Task NotifyAsync(IReadOnlyList<ShiftAlert> alerts, VaIIeTenantConfig config, CancellationToken ct = default)
        {
            if (!config.TelegramEnabled)
            {
                return Task.CompletedTask;
            }
            if (string.IsNullOrWhiteSpace(config.TelegramBotToken) || string.IsNullOrWhiteSpace(config.TelegramChatId))
            {
                _logger.LogWarning("Telegram alert enabled nhưng chưa cấu hình BotToken/ChatId — bỏ qua (config-only MVP)");
                return Task.CompletedTask;
            }

            string message = BuildMessage(alerts);
            // CONFIG-ONLY: chưa gọi API thật. Khi có token → POST https://api.telegram.org/bot{token}/sendMessage
            // payload: { "chat_id": config.TelegramChatId, "text": message }
            _logger.LogInformation("TelegramAlertNotifier (config-only): sẽ gửi {Count} alert(s) tới chat {ChatId} — {Message}",
                alerts.Count, MaskChatId(config.TelegramChatId), message.ReplaceLineEndings(" "));
            return Task.CompletedTask;
        }

        /// <summary>Payload tin nhắn (testable): danh sách alert Critical/Warning kèm mã.</summary>
        public static string BuildMessage(IReadOnlyList<ShiftAlert> alerts)
        {
            IEnumerable<string> lines = alerts.Select(a =>
                $"{(a.Severity == AlertSeverity.Critical ? "🔴" : "🟡")} [{a.AlertCode}] {a.Message}");
            return $"⚠️ Cảnh báo Vạn An ({alerts.Count})\n" + string.Join("\n", lines);
        }

        private static string MaskChatId(string chatId)
            => chatId.Length <= 4 ? "****" : chatId[..2] + "****" + chatId[^2..];
    }
}
