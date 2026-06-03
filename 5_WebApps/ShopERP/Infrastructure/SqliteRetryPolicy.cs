using Microsoft.Data.Sqlite;

namespace VanAn.ShopERP.Infrastructure
{
    /// <summary>
    /// SQLite retry policy with exponential backoff
    /// Handles BUSY (5) and LOCKED (6) error codes
    /// </summary>
    public static class SqliteRetryPolicy
    {
        private const int MaxRetries = 5;
        private const int BaseDelayMs = 30;

        /// <summary>
        /// Executes operation with retry policy for SQLite concurrency issues
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            ILogger? logger = null,
            int maxRetries = MaxRetries)
        {
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) // BUSY or LOCKED
                {
                    if (attempt == maxRetries)
                    {
                        logger?.LogWarning(ex, "SQLite lock after {Retries} attempts", maxRetries);
                        throw new InvalidOperationException($"Database locked after {maxRetries} retry attempts", ex);
                    }

                    TimeSpan delay = TimeSpan.FromMilliseconds(BaseDelayMs * Math.Pow(2, attempt));
                    logger?.LogDebug(ex, "SQLite busy, retry {Attempt}/{MaxRetries} after {Delay}ms",
                        attempt + 1, maxRetries, delay.TotalMilliseconds);

                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Operation failed on attempt {Attempt}", attempt + 1);
                    throw;
                }
            }

            throw new InvalidOperationException("Retry loop exited unexpectedly");
        }

        /// <summary>
        /// Executes void operation with retry policy
        /// </summary>
        public static async Task ExecuteWithRetryAsync(
            Func<Task> operation,
            ILogger? logger = null,
            int maxRetries = MaxRetries)
        {
            await ExecuteWithRetryAsync(async () =>
            {
                await operation();
                return true;
            }, logger, maxRetries);
        }

        /// <summary>
        /// Gets delay for retry attempt
        /// </summary>
        public static TimeSpan GetRetryDelay(int attempt)
        {
            return TimeSpan.FromMilliseconds(BaseDelayMs * Math.Pow(2, attempt));
        }
    }
}
