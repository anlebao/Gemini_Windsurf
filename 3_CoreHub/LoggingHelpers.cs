using Microsoft.Extensions.Logging;

// LoggerMessage definitions for high-performance logging
static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "✅ Database migration completed successfully")]
    public static partial void DatabaseMigrationSuccess(ILogger logger);
    
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "🔐 Vạn An Identity System Initialized")]
    public static partial void IdentitySystemInitialized(ILogger logger);
    
    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "❌ Database migration failed after {RetryCount} attempts")]
    public static partial void DatabaseMigrationFailed(ILogger logger, Exception ex, int retryCount);
    
    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "⚠️ Database migration attempt {RetryCount} failed, retrying in {DelaySeconds} seconds...")]
    public static partial void DatabaseMigrationRetry(ILogger logger, int retryCount, int delaySeconds);
}
