using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services
{
    public interface IVoiceCommandService
    {
        Task<bool> ProcessVoiceCommandAsync(string command, string deviceId);
        Task<IEnumerable<string>> GetSupportedCommandsAsync();
    }

    /// <summary>
    /// Voice command service — processes voice/text commands for kitchen operations.
    /// NOTE: For order voice notes, KhachLink UI uses PUT /api/v1/orders/{orderId}/note
    /// (OrdersController → OrderService.UpdateOrderVoiceNoteAsync) which saves to Order.VoiceNoteText.
    /// This service handles generic voice commands (e.g. "đặt hàng", "thanh toán") and is
    /// currently a stub — logs the command and returns success. Implement command parsing
    /// when voice command navigation is needed.
    /// </summary>
    public class VoiceCommandService(ILogger<VoiceCommandService> logger) : IVoiceCommandService
    {
        private readonly ILogger<VoiceCommandService> _logger = logger;

        public async Task<bool> ProcessVoiceCommandAsync(string command, string deviceId)
        {
            await Task.Delay(10);
            _logger.LogInformation("Processing voice command: {Command} for device: {DeviceId}", command, deviceId);
            return true;
        }

        public async Task<IEnumerable<string>> GetSupportedCommandsAsync()
        {
            await Task.Delay(10);
            return new List<string> { "đặt hàng", "giỏ hàng", "thanh toán", "trang chủ" };
        }
    }
}
