using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services;

public class AudioCleanupService
{
    private readonly ILogger<AudioCleanupService> _logger;
    private readonly IServiceProvider _serviceProvider;
    
    private static readonly Action<ILogger, Exception?> LogServiceStarting = 
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "AudioCleanup"), "Audio Cleanup Service is starting.");
    
    private static readonly Action<ILogger, Exception?> LogServiceStopping = 
        LoggerMessage.Define(LogLevel.Information, new EventId(2, "AudioCleanup"), "Audio Cleanup Service is stopping.");
    
    private static readonly Action<ILogger, Exception?> LogCleanupSuccess = 
        LoggerMessage.Define(LogLevel.Information, new EventId(3, "AudioCleanup"), "Expired audio files cleaned up successfully.");
    
    private static readonly Action<ILogger, Exception?> LogNoFilesFound = 
        LoggerMessage.Define(LogLevel.Debug, new EventId(4, "AudioCleanup"), "No expired audio files found for cleanup.");
    
    private static readonly Action<ILogger, Exception?> LogCleanupError = 
        LoggerMessage.Define(LogLevel.Error, new EventId(5, "AudioCleanup"), "Error occurred during audio cleanup.");

    public AudioCleanupService(
        ILogger<AudioCleanupService> logger,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task PerformCleanupAsync()
    {
        LogServiceStarting(_logger, null);
        
        try
        {
            await PerformCleanupInternalAsync().ConfigureAwait(false);
            LogCleanupSuccess(_logger, null);
        }
        catch (Exception ex)
        {
            LogCleanupError(_logger, ex);
            throw;
        }
        
        LogServiceStopping(_logger, null);
    }

    private async Task PerformCleanupInternalAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var audioStorageService = scope.ServiceProvider.GetRequiredService<IAudioStorageService>();

        var cleaned = await audioStorageService.CleanupExpiredAudioFilesAsync().ConfigureAwait(false);
        
        if (cleaned)
        {
            LogCleanupSuccess(_logger, null);
        }
        else
        {
            LogNoFilesFound(_logger, null);
        }
    }
}
