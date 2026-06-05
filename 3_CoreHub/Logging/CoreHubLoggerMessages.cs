using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Logging
{
    public static class CoreHubLoggerMessages
    {
        public static void LogOrderCreation(this ILogger logger, Guid ProductId, int Quantity)
        {
            logger.LogInformation("Creating new order for ProductId: {ProductId}, Quantity: {Quantity}", ProductId, Quantity);
        }

        public static void LogOrderCreated(this ILogger logger, Guid OrderId)
        {
            logger.LogInformation("Order created successfully with OrderId: {OrderId}", OrderId);
        }

        public static void LogOrderCreationFailed(this ILogger logger, Exception ex)
        {
            logger.LogError(ex, "Failed to create order");
        }

        public static void LogDatabaseMigrationSuccess(this ILogger logger)
        {
            logger.LogInformation("Database migration completed successfully");
        }

        public static void LogDatabaseMigrationFailed(this ILogger logger, Exception ex)
        {
            logger.LogError(ex, "Database migration failed");
        }

        public static void LogDatabaseSeedingSuccess(this ILogger logger)
        {
            logger.LogInformation("Database seeding completed successfully");
        }

        public static void LogDatabaseSeedingFailed(this ILogger logger, Exception ex)
        {
            logger.LogError(ex, "Database seeding failed but application will continue");
        }
    }
}
