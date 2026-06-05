using Microsoft.Extensions.Logging;

// Logger definitions for high-performance logging
internal static class Log
{
    public static void DatabaseMigrationSuccess(ILogger logger)
    {
        logger.LogInformation("Database migration completed successfully");
    }

    public static void IdentitySystemInitialized(ILogger logger)
    {
        logger.LogInformation("Vn An Identity System Initialized");
    }

    public static void DatabaseMigrationFailed(ILogger logger, Exception ex, int retryCount)
    {
        logger.LogError(ex, "Database migration failed after {RetryCount} attempts", retryCount);
    }

    public static void DatabaseMigrationRetry(ILogger logger, int retryCount, int delaySeconds)
    {
        logger.LogWarning("Database migration attempt {RetryCount} failed, retrying in {DelaySeconds} seconds...", retryCount, delaySeconds);
    }
}
