using Microsoft.Extensions.Logging;

namespace VanAn.Shared.Logging
{
    public static partial class SharedLoggerMessages
    {
        [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Updating shop config for shop: {ShopId}")]
        public static partial void LogShopConfigUpdate(this ILogger logger, Guid ShopId);

        [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Error updating shop config for shop: {ShopId}")]
        public static partial void LogShopConfigUpdateError(this ILogger logger, Exception ex, Guid ShopId);

        [LoggerMessage(EventId = 2003, Level = LogLevel.Information, Message = "Getting shop config for shop: {ShopId}")]
        public static partial void LogShopConfigGet(this ILogger logger, Guid ShopId);

        [LoggerMessage(EventId = 2004, Level = LogLevel.Error, Message = "Error getting shop config for shop: {ShopId}")]
        public static partial void LogShopConfigGetError(this ILogger logger, Exception ex, Guid ShopId);

        [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "Voice command processed successfully")]
        public static partial void LogVoiceCommandProcessed(this ILogger logger);

        [LoggerMessage(EventId = 2006, Level = LogLevel.Error, Message = "Error converting text to speech: {Text}")]
        public static partial void LogTextToSpeechError(this ILogger logger, Exception ex, string Text);

        [LoggerMessage(EventId = 2007, Level = LogLevel.Information, Message = "Text to speech conversion completed for: {Text}")]
        public static partial void LogTextToSpeechCompleted(this ILogger logger, string Text);
    }
}
