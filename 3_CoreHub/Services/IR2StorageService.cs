namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// R2/S3-compatible blob storage service for Guard QR photos (Issue #126).
    /// Presigned URL pattern: Guard app uploads directly to R2, photos accessed via presigned GET.
    /// </summary>
    public interface IR2StorageService
    {
        /// <summary>
        /// Generate a presigned PUT URL for direct upload from client.
        /// </summary>
        /// <param name="key">Object key (e.g. "plates/{tenantId}/{guid}.jpg")</param>
        /// <param name="contentType">MIME type (e.g. "image/jpeg")</param>
        /// <param name="ttlMinutes">URL expiry in minutes (default 15)</param>
        /// <returns>Presigned PUT URL</returns>
        string GetPresignedUploadUrl(string key, string contentType, int ttlMinutes = 15);

        /// <summary>
        /// Generate a presigned GET URL for photo display.
        /// </summary>
        /// <param name="key">Object key</param>
        /// <param name="ttlMinutes">URL expiry in minutes (default 60)</param>
        /// <returns>Presigned GET URL</returns>
        string GetPresignedDownloadUrl(string key, int ttlMinutes = 60);

        /// <summary>
        /// Generate a unique object key for a photo.
        /// </summary>
        /// <param name="prefix">"plates" or "customers"</param>
        /// <param name="tenantId">Tenant GUID</param>
        /// <returns>Object key (e.g. "plates/{tenantId}/{guid}.jpg")</returns>
        string GenerateKey(string prefix, Guid tenantId);

        /// <summary>
        /// #130: Upload photo to R2 server-side (Gateway → R2, no CORS needed).
        /// Replaces direct browser→R2 presigned URL upload which requires R2 CORS config.
        /// </summary>
        /// <param name="key">Object key (from GenerateKey)</param>
        /// <param name="base64Data">Base64-encoded JPEG data (without data: prefix)</param>
        /// <param name="contentType">MIME type (e.g. "image/jpeg")</param>
        /// <returns>True on success</returns>
        Task<bool> UploadObjectAsync(string key, string base64Data, string contentType);
    }
}
