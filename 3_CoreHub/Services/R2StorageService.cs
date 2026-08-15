using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Cloudflare R2 storage service (S3-compatible API).
    /// Photos stored in R2 bucket "vanan-guard-photos" with presigned URL access.
    /// Free tier: 10GB storage + unlimited egress.
    /// </summary>
    public class R2StorageService : IR2StorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly ILogger<R2StorageService> _logger;

        public R2StorageService(IConfiguration configuration, ILogger<R2StorageService> logger)
        {
            var endpoint = configuration["R2:Endpoint"]
                ?? throw new InvalidOperationException("R2:Endpoint configuration is required.");
            var accessKey = configuration["R2:AccessKey"]
                ?? throw new InvalidOperationException("R2:AccessKey configuration is required.");
            var secretKey = configuration["R2:SecretKey"]
                ?? throw new InvalidOperationException("R2:SecretKey configuration is required.");
            _bucketName = configuration["R2:BucketName"]
                ?? throw new InvalidOperationException("R2:BucketName configuration is required.");

            var config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true,
                RegionEndpoint = Amazon.RegionEndpoint.USEast1 // R2 ignores region, but SDK requires one
            };

            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
            _logger = logger;
        }

        public string GetPresignedUploadUrl(string key, string contentType, int ttlMinutes = 15)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(ttlMinutes),
                ContentType = contentType
            };

            var url = _s3Client.GetPreSignedURL(request);
            _logger.LogDebug("Generated presigned PUT URL for key {Key} (TTL {Ttl}min)", key, ttlMinutes);
            return url;
        }

        public string GetPresignedDownloadUrl(string key, int ttlMinutes = 60)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.GET,
                Expires = DateTime.UtcNow.AddMinutes(ttlMinutes)
            };

            var url = _s3Client.GetPreSignedURL(request);
            _logger.LogDebug("Generated presigned GET URL for key {Key} (TTL {Ttl}min)", key, ttlMinutes);
            return url;
        }

        public string GenerateKey(string prefix, Guid tenantId)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Prefix is required.", nameof(prefix));
            if (prefix != "plates" && prefix != "customers")
                throw new ArgumentException("Prefix must be 'plates' or 'customers'.", nameof(prefix));

            return $"{prefix}/{tenantId}/{Guid.NewGuid()}.jpg";
        }

        /// <summary>
        /// #130: Upload photo to R2 server-side (Gateway → R2, no CORS needed).
        /// Replaces direct browser→R2 presigned URL upload which fails without R2 CORS config.
        /// </summary>
        public async Task<bool> UploadObjectAsync(string key, string base64Data, string contentType)
        {
            try
            {
                // Strip data: prefix if present (e.g. "data:image/jpeg;base64,")
                var base64 = base64Data;
                var commaIdx = base64Data.IndexOf(',');
                if (commaIdx >= 0 && base64Data.StartsWith("data:"))
                    base64 = base64Data[(commaIdx + 1)..];

                var bytes = Convert.FromBase64String(base64);
                using var stream = new MemoryStream(bytes);

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    InputStream = stream,
                    ContentType = contentType ?? "image/jpeg"
                };

                var response = await _s3Client.PutObjectAsync(request);
                _logger.LogInformation("Uploaded photo to R2: {Key} ({Size} bytes, ETag: {ETag})",
                    key, bytes.Length, response.ETag);
                return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload photo to R2: {Key}", key);
                return false;
            }
        }
    }
}
