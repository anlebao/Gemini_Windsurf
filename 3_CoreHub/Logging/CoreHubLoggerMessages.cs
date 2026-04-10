using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Logging
{
    public static partial class CoreHubLoggerMessages
    {
        [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Creating new order for ProductId: {ProductId}, Quantity: {Quantity}")]
        public static partial void LogOrderCreation(this ILogger logger, Guid ProductId, int Quantity);

        [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Order created successfully with OrderId: {OrderId}")]
        public static partial void LogOrderCreated(this ILogger logger, Guid OrderId);

        [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Failed to create order")]
        public static partial void LogOrderCreationFailed(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Database migration completed successfully")]
        public static partial void LogDatabaseMigrationSuccess(this ILogger logger);

        [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "Database migration failed")]
        public static partial void LogDatabaseMigrationFailed(this ILogger logger, Exception ex);

        [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Database seeding completed successfully")]
        public static partial void LogDatabaseSeedingSuccess(this ILogger logger);

        [LoggerMessage(EventId = 1007, Level = LogLevel.Error, Message = "Database seeding failed but application will continue")]
        public static partial void LogDatabaseSeedingFailed(this ILogger logger, Exception ex);
    }
}
