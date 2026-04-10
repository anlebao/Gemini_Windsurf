using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

public interface IVoiceCommandService
{
    Task<bool> ProcessVoiceCommandAsync(string command, string deviceId);
    Task<IEnumerable<string>> GetSupportedCommandsAsync();
}

public class VoiceCommandService : IVoiceCommandService
{
    private readonly ILogger<VoiceCommandService> _logger;

    public VoiceCommandService(ILogger<VoiceCommandService> logger)
    {
        _logger = logger;
    }

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
