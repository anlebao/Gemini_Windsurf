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
    }
}
