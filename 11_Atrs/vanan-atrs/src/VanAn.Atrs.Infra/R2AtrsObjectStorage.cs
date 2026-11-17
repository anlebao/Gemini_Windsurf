using System.Net.Http.Headers;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VanAn.Atrs.Infra;

/// <summary>
/// Cloudflare R2 storage (S3-compatible) cho ATRS evidence attachments.
/// Config: Atrs:R2:Endpoint / AccessKey / SecretKey / BucketName (compose env Atrs__R2__*).
/// Reuse chung bucket với guard photos nhưng prefix "atrs-evidence/" — không cần bucket mới.
/// </summary>
public sealed class R2AtrsObjectStorage : IAtrsObjectStorage, IDisposable
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly ILogger<R2AtrsObjectStorage> _logger;
    private static readonly HttpClient s_http = new();

    public R2AtrsObjectStorage(IConfiguration configuration, ILogger<R2AtrsObjectStorage> logger)
    {
        var endpoint = configuration["Atrs:R2:Endpoint"];
        var accessKey = configuration["Atrs:R2:AccessKey"];
        var secretKey = configuration["Atrs:R2:SecretKey"];
        _bucket = configuration["Atrs:R2:BucketName"] ?? "vanan-guard-photos";
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("Atrs:R2:Endpoint/AccessKey/SecretKey chưa cấu hình.");

        _s3 = new AmazonS3Client(accessKey, secretKey, new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto"
        });
        _logger = logger;
    }

    public bool IsConfigured => true;

    public string GetPresignedPutUrl(string key, string contentType, int ttlMinutes = 15)
    {
        var url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(ttlMinutes),
            ContentType = contentType
        });
        _logger.LogDebug("ATRS R2 presigned PUT {Key}", key);
        return url;
    }

    public string GetPresignedGetUrl(string key, int ttlMinutes = 15)
    {
        var url = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(ttlMinutes)
        });
        return url;
    }

    /// <summary>Presigned PUT + HttpClient byte[] — R2 không hỗ trợ streaming trailer signing.</summary>
    public async Task UploadAsync(string key, byte[] data, string contentType, CancellationToken ct = default)
    {
        var presignedUrl = GetPresignedPutUrl(key, contentType);
        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var response = await s_http.PutAsync(presignedUrl, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("ATRS R2 upload {Key} failed {Status}: {Body}", key, (int)response.StatusCode, body);
            throw new InvalidOperationException($"R2 upload failed ({(int)response.StatusCode}).");
        }
    }

    public void Dispose() => _s3.Dispose();
}
