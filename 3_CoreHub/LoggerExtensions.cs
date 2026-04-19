using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub;

public static class LoggerExtensions
{
    public static void LogMigrationSuccess(this ILogger logger)
    {
        logger.LogInformation("Migration completed successfully");
    }

    public static void LogIdentityInitialized(this ILogger logger)
    {
        logger.LogInformation("Identity system initialized");
    }

    public static void LogMigrationFailed(this ILogger logger, Exception exception, int AttemptCount)
    {
        logger.LogError(exception, "Migration failed after {AttemptCount} attempts", AttemptCount);
    }

    public static void LogMigrationRetry(this ILogger logger, int AttemptCount, int MaxAttempts)
    {
        logger.LogWarning("Migration retry attempt {AttemptCount} of {MaxAttempts}", AttemptCount, MaxAttempts);
    }
}
