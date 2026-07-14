using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Cloudinary image storage implementation — G8 locked.
    /// Reads config from "Cloudinary" section: { CloudName, ApiKey, ApiSecret }.
    /// Validates: max 5MB, extensions .jpg/.jpeg/.png/.webp.
    /// </summary>
    public class CloudinaryImageStorageService : IImageStorageService
    {
        private readonly Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryImageStorageService> _logger;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public CloudinaryImageStorageService(IConfiguration configuration, ILogger<CloudinaryImageStorageService> logger)
        {
            _logger = logger;
            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            // If config is missing/empty, Cloudinary client is null-configured — uploads will no-op (return null).
            // This allows the app to boot without Cloudinary credentials in dev/test.
            var account = new Account(cloudName ?? string.Empty, apiKey ?? string.Empty, apiSecret ?? string.Empty);
            _cloudinary = new Cloudinary(account);
        }

        public async Task<string?> UploadAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("UploadAsync: empty file");
                return null;
            }

            if (file.Length > MaxFileSize)
            {
                _logger.LogWarning("UploadAsync: file exceeds 5MB limit ({Size})", file.Length);
                return null;
            }

            var ext = Path.GetExtension(file.FileName);
            if (!AllowedExtensions.Contains(ext))
            {
                _logger.LogWarning("UploadAsync: extension {Ext} not allowed", ext);
                return null;
            }

            try
            {
                var publicId = $"{folder}/{Guid.NewGuid():N}";
                await using var stream = file.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    PublicId = publicId,
                    Overwrite = false
                };

                ImageUploadResult result = await _cloudinary.UploadAsync(uploadParams);
                if (result?.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return result.SecureUrl?.ToString();
                }

                _logger.LogWarning("UploadAsync: Cloudinary returned {Status}", result?.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadAsync: Cloudinary upload failed");
                return null;
            }
        }

        public async Task<string?> UploadAsync(Stream stream, string fileName, string folder, CancellationToken cancellationToken = default)
        {
            if (stream == null || stream.Length == 0)
            {
                _logger.LogWarning("UploadAsync(stream): empty stream");
                return null;
            }

            if (stream.Length > MaxFileSize)
            {
                _logger.LogWarning("UploadAsync(stream): stream exceeds 5MB limit ({Size})", stream.Length);
                return null;
            }

            var ext = Path.GetExtension(fileName);
            if (!AllowedExtensions.Contains(ext))
            {
                _logger.LogWarning("UploadAsync(stream): extension {Ext} not allowed", ext);
                return null;
            }

            try
            {
                var publicId = $"{folder}/{Guid.NewGuid():N}";
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(fileName, stream),
                    PublicId = publicId,
                    Overwrite = false
                };

                ImageUploadResult result = await _cloudinary.UploadAsync(uploadParams);
                if (result?.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    return result.SecureUrl?.ToString();
                }

                _logger.LogWarning("UploadAsync(stream): Cloudinary returned {Status}", result?.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadAsync(stream): Cloudinary upload failed");
                return null;
            }
        }

        public async Task<bool> DeleteAsync(string publicId, CancellationToken cancellationToken = default)
        {
            try
            {
                var deleteParams = new DeletionParams(publicId);
                DeletionResult result = await _cloudinary.DestroyAsync(deleteParams);
                return result?.StatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteAsync: Cloudinary delete failed for {PublicId}", publicId);
                return false;
            }
        }
    }
}
