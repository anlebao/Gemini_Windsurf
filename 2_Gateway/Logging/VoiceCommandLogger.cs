namespace VanAn.Gateway.Logging
{
    public static partial class VoiceCommandLogger
    {
        [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "Error processing audio command for order: {OrderId}")]
        public static partial void AudioCommandError(ILogger logger, Exception ex, string orderId);

        [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Invalid argument in text command: {CommandText}")]
        public static partial void TextCommandArgumentError(ILogger logger, Exception ex, string commandText);

        [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Invalid operation in text command: {CommandText}")]
        public static partial void TextCommandOperationError(ILogger logger, Exception ex, string commandText);

        [LoggerMessage(EventId = 1004, Level = LogLevel.Error, Message = "Error processing text command: {CommandText}")]
        public static partial void TextCommandError(ILogger logger, Exception ex, string commandText);

        [LoggerMessage(EventId = 1005, Level = LogLevel.Error, Message = "Invalid argument in text to speech: {Text}")]
        public static partial void TextToSpeechArgumentError(ILogger logger, Exception ex, string text);

        [LoggerMessage(EventId = 1006, Level = LogLevel.Error, Message = "Text to speech service unavailable: {Text}")]
        public static partial void TextToSpeechUnavailableError(ILogger logger, Exception ex, string text);

        [LoggerMessage(EventId = 1007, Level = LogLevel.Error, Message = "Error converting text to speech: {Text}")]
        public static partial void TextToSpeechError(ILogger logger, Exception ex, string text);

        [LoggerMessage(EventId = 1008, Level = LogLevel.Warning, Message = "Attempted path traversal attack: {AudioId}")]
        public static partial void PathTraversalAttack(ILogger logger, string audioId);

        [LoggerMessage(EventId = 1009, Level = LogLevel.Error, Message = "Failed to get audio file")]
        public static partial void GetAudioFileError(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 1010, Level = LogLevel.Error, Message = "Error cleaning up expired audio files")]
        public static partial void CleanupError(ILogger logger, Exception ex);

        [LoggerMessage(EventId = 1011, Level = LogLevel.Error, Message = "Error occurred while processing voice command")]
        public static partial void LogVoiceCommandError(ILogger logger, Exception ex);
    }
}
