using System.Net.Http;
using System.Net.Http.Headers;
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
        private static readonly HttpClient s_httpClient = new();

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
                // R2 requires region "auto" in SigV4 signing (matches boto3 region_name='auto').
                // Using USEast1 or null causes "Access Key Id does not exist" or 401 Unauthorized.
                AuthenticationRegion = "auto"
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
        /// Uses presigned PUT URL + HttpClient to avoid R2's lack of support for
        /// STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER (which AWS SDK uses for streamed uploads).
        /// Throws on failure so caller can surface the actual error message.
        /// </summary>
        public async Task<bool> UploadObjectAsync(string key, string base64Data, string contentType)
        {
            // Strip data: prefix if present (e.g. "data:image/jpeg;base64,")
            var base64 = base64Data;
            var commaIdx = base64Data.IndexOf(',');
            if (commaIdx >= 0 && base64Data.StartsWith("data:"))
                base64 = base64Data[(commaIdx + 1)..];

            var bytes = Convert.FromBase64String(base64);
            var ct = contentType ?? "image/jpeg";

            // Generate presigned PUT URL (SigV4 signed, no payload signing — R2 compatible)
            var presignedUrl = _s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = key,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(15),
                ContentType = ct
            });

            // Upload via HttpClient with full bytes in memory (no streaming → no trailer error)
            using var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(ct);
            var response = await s_httpClient.PutAsync(presignedUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"R2 upload failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
            }

            _logger.LogInformation("Uploaded photo to R2: {Key} ({Size} bytes)", key, bytes.Length);
            return true;
        }

        /// <summary>
        /// R2 Cleanup: List all objects under a prefix (e.g. "plates/{tenantId}/").
        /// Handles R2 pagination (1000 keys per page) via ContinuationToken loop.
        /// </summary>
        public async Task<List<S3ObjectInfo>> ListObjectsByPrefixAsync(string prefix, CancellationToken ct = default)
        {
            var results = new List<S3ObjectInfo>();
            string? continuationToken = null;

            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = prefix,
                    ContinuationToken = continuationToken
                };

                var response = await _s3Client.ListObjectsV2Async(request, ct);

                foreach (var s3Obj in response.S3Objects)
                {
                    results.Add(new S3ObjectInfo(s3Obj.Key, s3Obj.Size, s3Obj.LastModified));
                }

                continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
            }
            while (continuationToken != null);

            _logger.LogDebug("Listed {Count} objects under prefix {Prefix}", results.Count, prefix);
            return results;
        }

        /// <summary>
        /// R2 Cleanup: Batch delete objects by keys.
        /// R2/S3 DeleteObjects API accepts max 1000 keys per call — chunks automatically.
        /// </summary>
        public async Task<int> DeleteObjectsAsync(IEnumerable<string> keys, CancellationToken ct = default)
        {
            var keyList = keys as List<string> ?? keys.ToList();
            if (keyList.Count == 0)
                return 0;

            var deleted = 0;
            const int batchSize = 1000;

            for (int i = 0; i < keyList.Count; i += batchSize)
            {
                var batch = keyList.Skip(i).Take(batchSize).Select(k => new KeyVersion { Key = k }).ToList();

                var request = new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = batch
                };

                var response = await _s3Client.DeleteObjectsAsync(request, ct);
                deleted += response.DeletedObjects.Count;

                if (response.DeleteErrors?.Count > 0)
                {
                    foreach (var err in response.DeleteErrors)
                    {
                        _logger.LogWarning("R2 delete error for key {Key}: {Code} - {Message}", err.Key, err.Code, err.Message);
                    }
                }
            }

            _logger.LogInformation("Deleted {Deleted} objects from R2 (requested {Requested})", deleted, keyList.Count);
            return deleted;
        }
    }
}
