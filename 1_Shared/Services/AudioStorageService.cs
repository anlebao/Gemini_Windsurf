using VanAn.Shared.Domain;
using Microsoft.Extensions.Logging;

namespace VanAn.Shared.Services
{
    public interface IAudioStorageService
    {
        Task<AudioFile> SaveAudioAsync(byte[] audioData, string fileName, string orderId);
        Task<bool> DeleteAudioAsync(string audioId);
        Task<AudioFile?> GetAudioAsync(string audioId);
        Task<List<AudioFile>> GetExpiredAudioFilesAsync();
        Task<bool> CleanupExpiredAudioFilesAsync();
    }

    public partial class AudioStorageService : IAudioStorageService
    {
        private readonly ILogger<AudioStorageService> _logger;
        private readonly Dictionary<string, AudioFile> _audioFiles; // In-memory store for demo
        private readonly string _storagePath;

        public AudioStorageService(ILogger<AudioStorageService> logger)
        {
            _logger = logger;
            _audioFiles = [];
            _storagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audio");

            // Ensure storage directory exists
            if (!Directory.Exists(_storagePath))
            {
                Directory.CreateDirectory(_storagePath);
            }
        }

        public async Task<AudioFile> SaveAudioAsync(byte[] audioData, string fileName, string orderId)
        {
            try
            {
                string audioId = Guid.NewGuid().ToString();
                string filePath = Path.Combine(_storagePath, $"{audioId}_{fileName}");

                // Save file to disk
                await File.WriteAllBytesAsync(filePath, audioData);

                AudioFile audioFile = new()
                {
                    Id = audioId,
                    FileName = fileName,
                    FilePath = filePath,
                    OrderId = orderId,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24)
                };

                // Store in memory dictionary
                _audioFiles[audioId] = audioFile;

                LogAudioFileSaved(fileName, orderId);

                return audioFile;
            }
            catch (Exception ex)
            {
                LogAudioFileSaveError(ex, fileName);
                throw;
            }
        }

        public async Task<bool> DeleteAudioAsync(string audioId)
        {
            try
            {
                if (_audioFiles.TryGetValue(audioId, out AudioFile? audioFile))
                {
                    // Delete file from disk
                    string filePath = Path.Combine(_storagePath, audioFile.FileName);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    _audioFiles.Remove(audioId);
                    LogAudioFileDeleted(audioId);
                    await Task.CompletedTask;
                    return true;
                }

                await Task.CompletedTask;
                return false;
            }
            catch (Exception ex)
            {
                LogAudioFileDeleteError(ex, audioId);
                throw;
            }
        }

        public async Task<AudioFile?> GetAudioAsync(string audioId)
        {
            try
            {
                await Task.CompletedTask;
                return _audioFiles.TryGetValue(audioId, out AudioFile? audioFile) ? audioFile : null;
            }
            catch (Exception ex)
            {
                LogAudioFileGetError(ex, audioId);
                return null;
            }
        }

        public async Task<List<AudioFile>> GetExpiredAudioFilesAsync()
        {
            try
            {
                await Task.CompletedTask;
                DateTime now = DateTime.UtcNow;
                return _audioFiles.Values
                    .Where(f => f.ExpiresAt <= now)
                    .ToList();
            }
            catch (Exception ex)
            {
                LogExpiredAudioFilesError(ex);
                return [];
            }
        }

        public async Task<bool> CleanupExpiredAudioFilesAsync()
        {
            try
            {
                List<AudioFile> expiredFiles = await GetExpiredAudioFilesAsync();
                int deletedCount = 0;

                foreach (AudioFile file in expiredFiles)
                {
                    if (await DeleteAudioAsync(file.Id))
                    {
                        deletedCount++;
                    }
                }

                LogExpiredAudioFilesCleaned(deletedCount);

                return deletedCount > 0;
            }
            catch (Exception ex)
            {
                LogCleanupError(ex);
                return false;
            }
        }

        // High-Performance Logging Methods
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Audio file saved: {FileName} for order: {OrderId}")]
        private partial void LogAudioFileSaved(string fileName, string orderId);

        [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Error saving audio file: {FileName}")]
        private partial void LogAudioFileSaveError(Exception ex, string fileName);

        [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Deleted audio file: {AudioId}")]
        private partial void LogAudioFileDeleted(string audioId);

        [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Error deleting audio file: {AudioId}")]
        private partial void LogAudioFileDeleteError(Exception ex, string audioId);

        [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Error getting audio file: {AudioId}")]
        private partial void LogAudioFileGetError(Exception ex, string audioId);

        [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Error getting expired audio files")]
        private partial void LogExpiredAudioFilesError(Exception ex);

        [LoggerMessage(EventId = 7, Level = LogLevel.Information, Message = "Cleaned up {Count} expired audio files")]
        private partial void LogExpiredAudioFilesCleaned(int count);

        [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Error cleaning up expired audio files")]
        private partial void LogCleanupError(Exception ex);
    }
}
