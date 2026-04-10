using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub
{
    public static partial class LoggerExtensions
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "✅ Database migration completed successfully")]
        public static partial void LogMigrationSuccess(ILogger logger);
        
        [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "🔐 Vạn An Identity System Initialized")]
        public static partial void LogIdentityInitialized(ILogger logger);
        
        [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "❌ Database migration failed after {RetryCount} attempts")]
        public static partial void LogMigrationFailed(ILogger logger, Exception ex, int retryCount);
        
        [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "⚠️ Database migration attempt {RetryCount} failed, retrying in {DelaySeconds} seconds...")]
        public static partial void LogMigrationRetry(ILogger logger, int retryCount, int delaySeconds);
    }
}
