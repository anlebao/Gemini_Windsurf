using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using VanAn.Gateway.Services;

namespace VanAn.Gateway.Middleware
{
    /// <summary>
    /// Wave 14: HMAC-SHA256 Request Signing Middleware.
    ///
    /// Validates three custom headers on every request to protected paths:
    ///   X-VanAn-KeyId       — API Key ID (Guid)
    ///   X-VanAn-Timestamp   — Unix seconds (UTC)
    ///   X-VanAn-Nonce       — Random UUID per request
    ///   X-VanAn-Signature   — Base64(HMAC-SHA256(signingString, sharedSecret))
    ///
    /// Signing string format:
    ///   {HTTP_METHOD}\n{Path}\n{KeyId}\n{Timestamp}\n{Nonce}\n{SHA256(Body)}
    ///
    /// Anti-replay:
    ///   - Timestamp window: 60 seconds
    ///   - Nonce dedup: IMemoryCache TTL 120s (covers the window + grace)
    ///
    /// Rate limiting:
    ///   - 5 consecutive failed signature attempts per KeyId → 15-minute block
    ///   - Failure counter is also stored in IMemoryCache
    ///
    /// Activation:
    ///   Apply only to paths listed in HmacProtectedPaths configuration.
    ///   Internal Blazor Server calls (ShopERP → CoreHub) use JWT and are excluded.
    /// </summary>
    public class HmacSigningMiddleware
    {
        private const int TimestampWindowSeconds = 60;
        private const int NonceCacheTtlSeconds = 120;
        private const int MaxFailedAttempts = 5;
        private const int BlockDurationMinutes = 15;

        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly ILogger<HmacSigningMiddleware> _logger;
        private readonly HmacSigningOptions _options;

        public HmacSigningMiddleware(
            RequestDelegate next,
            IMemoryCache cache,
            ILogger<HmacSigningMiddleware> logger,
            HmacSigningOptions options)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
            _options = options;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!IsProtectedPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            var lookup = context.RequestServices.GetRequiredService<IHmacApiKeyLookup>();
            var result = await ValidateSignatureAsync(context, lookup);

            if (!result.IsValid)
            {
                _logger.LogWarning(
                    "HMAC validation failed for {Path}: {Reason}",
                    context.Request.Path, result.FailureReason);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                try
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        error = "Unauthorized",
                        reason = result.FailureReason,
                        timestamp = DateTime.UtcNow
                    }));
                }
                catch (Exception writeEx)
                {
                    _logger.LogDebug(writeEx, "Could not write HMAC 401 response body");
                }
                return;
            }

            await _next(context);
        }

        // ── Validation pipeline ──────────────────────────────────────────────────

        private async Task<ValidationResult> ValidateSignatureAsync(
            HttpContext context, IHmacApiKeyLookup lookup)
        {
            // 1. Read required headers
            if (!TryReadHeaders(context.Request, out var keyIdStr, out var timestampStr, out var nonce, out var signature))
                return ValidationResult.Fail("Missing required HMAC headers (X-VanAn-KeyId, X-VanAn-Timestamp, X-VanAn-Nonce, X-VanAn-Signature)");

            if (!Guid.TryParse(keyIdStr, out var keyId))
                return ValidationResult.Fail("X-VanAn-KeyId must be a valid GUID");

            // 2. Rate limit check (block if too many failures)
            if (IsBlocked(keyId))
                return ValidationResult.Fail("API key temporarily blocked due to repeated signature failures");

            // 3. Timestamp window
            if (!long.TryParse(timestampStr, out var timestamp))
                return ValidationResult.Fail("X-VanAn-Timestamp must be a Unix timestamp (long)");

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - timestamp) > TimestampWindowSeconds)
            {
                IncrementFailure(keyId);
                return ValidationResult.Fail("Request timestamp outside acceptable window (±60s)");
            }

            // 4. Nonce replay check
            var nonceCacheKey = $"nonce:{keyId}:{nonce}";
            if (_cache.TryGetValue(nonceCacheKey, out _))
            {
                IncrementFailure(keyId);
                return ValidationResult.Fail("Nonce already used (replay detected)");
            }

            // 5. Lookup API key
            var keyRecord = await lookup.FindActiveKeyAsync(keyId);
            if (keyRecord is null)
            {
                IncrementFailure(keyId);
                return ValidationResult.Fail("API key not found, revoked, or expired");
            }

            // 6. Compute and verify HMAC
            string body = await ReadBodyAsync(context.Request);
            string bodyHash = ComputeSha256Hex(body);
            string signingString = BuildSigningString(
                context.Request.Method,
                context.Request.Path.ToString(),
                keyIdStr!,
                timestampStr!,
                nonce!,
                bodyHash);

            // Shared secret is stored as BCrypt hash — we need raw secret to verify HMAC.
            // Architecture decision: SecretHash here is the RAW secret (not BCrypt) because
            // HMAC requires the exact key material. BCrypt is one-way only.
            // We store the secret with light obfuscation; for production, use a proper KMS.
            // See docs/requirements/Van_An_Solution_SRS_Lightweight_Key_Management_Protocol.md
            string expectedSig = ComputeHmacSha256Base64(signingString, keyRecord.SecretHash);

            if (!CryptographicEquals(expectedSig, signature!))
            {
                IncrementFailure(keyId);
                return ValidationResult.Fail("Signature mismatch");
            }

            // 7. All checks passed — register nonce + record usage
            _cache.Set(nonceCacheKey, true, TimeSpan.FromSeconds(NonceCacheTtlSeconds));
            ClearFailures(keyId);
            await lookup.RecordUsageAsync(keyId);

            return ValidationResult.Success();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private bool IsProtectedPath(PathString path)
        {
            if (_options.ProtectedPaths.Count == 0)
                return false;
            return _options.ProtectedPaths.Any(p => path.StartsWithSegments(p));
        }

        private static bool TryReadHeaders(
            HttpRequest request,
            out string? keyId,
            out string? timestamp,
            out string? nonce,
            out string? signature)
        {
            keyId = request.Headers["X-VanAn-KeyId"].FirstOrDefault();
            timestamp = request.Headers["X-VanAn-Timestamp"].FirstOrDefault();
            nonce = request.Headers["X-VanAn-Nonce"].FirstOrDefault();
            signature = request.Headers["X-VanAn-Signature"].FirstOrDefault();
            return keyId != null && timestamp != null && nonce != null && signature != null;
        }

        private static async Task<string> ReadBodyAsync(HttpRequest request)
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            string body = await reader.ReadToEndAsync();
            request.Body.Position = 0;
            return body;
        }

        private static string ComputeSha256Hex(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string BuildSigningString(
            string method, string path, string keyId,
            string timestamp, string nonce, string bodyHash)
            => $"{method.ToUpperInvariant()}\n{path}\n{keyId}\n{timestamp}\n{nonce}\n{bodyHash}";

        public static string ComputeHmacSha256Base64(string data, string secret)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] hash = HMACSHA256.HashData(keyBytes, dataBytes);
            return Convert.ToBase64String(hash);
        }

        private static bool CryptographicEquals(string a, string b)
            => CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(a),
                Encoding.UTF8.GetBytes(b));

        // ── Rate limiting (IMemoryCache) ──────────────────────────────────────────

        private string FailureKey(Guid keyId) => $"hmac_fail:{keyId}";
        private string BlockKey(Guid keyId) => $"hmac_block:{keyId}";

        private bool IsBlocked(Guid keyId) => _cache.TryGetValue(BlockKey(keyId), out _);

        private void IncrementFailure(Guid keyId)
        {
            string fKey = FailureKey(keyId);
            int count = _cache.TryGetValue(fKey, out int existing) ? existing + 1 : 1;
            _cache.Set(fKey, count, TimeSpan.FromMinutes(BlockDurationMinutes));

            if (count >= MaxFailedAttempts)
            {
                _cache.Set(BlockKey(keyId), true, TimeSpan.FromMinutes(BlockDurationMinutes));
                _cache.Remove(fKey);
                _logger.LogWarning("API key {KeyId} blocked for {Minutes} minutes after {Max} failed attempts",
                    keyId, BlockDurationMinutes, MaxFailedAttempts);
            }
        }

        private void ClearFailures(Guid keyId)
        {
            _cache.Remove(FailureKey(keyId));
            _cache.Remove(BlockKey(keyId));
        }
    }

    // ── Supporting types ──────────────────────────────────────────────────────────

    public sealed class HmacSigningOptions
    {
        /// <summary>
        /// Path prefixes that require HMAC signing.
        /// Example: ["/api/products", "/api/orders"]
        /// Default: empty (middleware is passive until configured).
        /// </summary>
        public List<PathString> ProtectedPaths { get; set; } = [];
    }

    internal sealed record ValidationResult(bool IsValid, string? FailureReason)
    {
        internal static ValidationResult Success() => new(true, null);
        internal static ValidationResult Fail(string reason) => new(false, reason);
    }
}
