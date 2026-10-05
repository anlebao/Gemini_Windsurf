using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Services.InventoryIntelligence;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.InventoryIntelligence
{
    /// <summary>
    /// VA-IIE Phase 4 (2026-10-05): Alert notifier — CONFIG-ONLY (quyết định user 2026-10-05).
    /// Telegram/Zalo stub xây payload; dispatcher fail-safe; không gọi API thật khi chưa có token.
    /// </summary>
    public class AlertNotifierTests
    {
        private static readonly TenantId TenantId = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        private static readonly Guid ShiftId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static List<ShiftAlert> BuildAlerts() =>
        [
            new ShiftAlert(TenantId, ShiftId, "STOCK_LOW", AlertSeverity.Warning, "Tồn kho thấp cà phê"),
            new ShiftAlert(TenantId, ShiftId, "CASH_MISMATCH", AlertSeverity.Critical, "Tiền mặt chênh 50.000")
        ];

        [Fact]
        public void TelegramBuildMessage_ContainsCodesAndSeverity()
        {
            string message = TelegramAlertNotifier.BuildMessage(BuildAlerts());

            Assert.Contains("🔴 [CASH_MISMATCH]", message);
            Assert.Contains("🟡 [STOCK_LOW]", message);
            Assert.Contains("Tiền mặt chênh 50.000", message);
            Assert.Contains("Cảnh báo Vạn An (2)", message);
        }

        [Fact]
        public void ZaloBuildMessage_ContainsCodes()
        {
            string message = ZaloAlertNotifier.BuildMessage(BuildAlerts());

            Assert.Contains("[CRITICAL] CASH_MISMATCH", message);
            Assert.Contains("[WARNING] STOCK_LOW", message);
            Assert.Contains("Cảnh báo Vạn An (2)", message);
        }

        [Fact]
        public async Task Dispatcher_DisabledChannels_NoThrow()
        {
            var dispatcher = new AlertNotifierDispatcher(
                new TelegramAlertNotifier(NullLogger<TelegramAlertNotifier>.Instance),
                new ZaloAlertNotifier(NullLogger<ZaloAlertNotifier>.Instance),
                NullLogger<AlertNotifierDispatcher>.Instance);
            var config = new VaIIeTenantConfig(TenantId); // cả 2 channel disabled

            await dispatcher.NotifyAsync(BuildAlerts(), config); // không throw
        }

        [Fact]
        public async Task Dispatcher_EnabledButMissingToken_NoThrow()
        {
            var dispatcher = new AlertNotifierDispatcher(
                new TelegramAlertNotifier(NullLogger<TelegramAlertNotifier>.Instance),
                new ZaloAlertNotifier(NullLogger<ZaloAlertNotifier>.Instance),
                NullLogger<AlertNotifierDispatcher>.Instance);
            var config = new VaIIeTenantConfig(TenantId);
            config.UpdateNotificationConfig(telegramEnabled: true, null, null, zaloEnabled: true, null, null);

            await dispatcher.NotifyAsync(BuildAlerts(), config); // log warning — không throw
        }

        [Fact]
        public async Task Dispatcher_EmptyAlerts_NoOp()
        {
            var dispatcher = new AlertNotifierDispatcher(
                new TelegramAlertNotifier(NullLogger<TelegramAlertNotifier>.Instance),
                new ZaloAlertNotifier(NullLogger<ZaloAlertNotifier>.Instance),
                NullLogger<AlertNotifierDispatcher>.Instance);
            var config = new VaIIeTenantConfig(TenantId);
            config.UpdateNotificationConfig(true, "token", "chat", true, "token", "recipient");

            await dispatcher.NotifyAsync([], config); // alerts.Count == 0 → return sớm
        }
    }
}
