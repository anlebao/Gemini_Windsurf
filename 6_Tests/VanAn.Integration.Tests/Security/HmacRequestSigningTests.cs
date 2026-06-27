using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.Gateway.Middleware;
using VanAn.Gateway.Services;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Integration.Tests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace VanAn.Integration.Tests.Security;

/// <summary>
/// Wave 14 — Integration tests for HMAC Request Signing pipeline.
///
/// Tests cover:
///   W14-S1: Missing headers → 401
///   W14-S2: Invalid (expired) timestamp → 401
///   W14-S3: Nonce replay → 401 (same nonce used twice)
///   W14-S4: Wrong HMAC signature → 401
///   W14-S5: Valid signature → middleware passes (no 401)
///   W14-S6: Revoked API key → 401
///   W14-S7: Rate limit — 5 consecutive failures → key blocked
///   W14-S8: ApiKeyManagementService — create key returns raw secret
///   W14-S9: ApiKeyManagementService — list keys by tenant
///   W14-S10: ApiKeyManagementService — revoke key sets IsActive=false
/// </summary>
[Trait("Category", "Security")]
public class HmacRequestSigningTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;
    private const string TestSecret = "test-shared-secret-wave14-hmac-sha256";
    private const string TestPath = "/api/products";

    public HmacRequestSigningTests(ITestOutputHelper output) : base()
    {
        _output = output;
    }

    // ── Middleware unit-style tests (pure logic, no HTTP server) ─────────────

    [Fact(DisplayName = "W14-S1: Missing HMAC headers returns 401")]
    public async Task MissingHeaders_Returns401()
    {
        // Arrange — TestPath IS in the protected paths list; no HMAC headers added
        var (middleware, httpContext) = BuildMiddleware([TestPath], passThroughNext: false);
        // No HMAC headers added

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal(401, httpContext.Response.StatusCode);
        _output.WriteLine("W14-S1 PASS: Missing headers → 401");
    }

    [Fact(DisplayName = "W14-S2: Expired timestamp returns 401")]
    public async Task ExpiredTimestamp_Returns401()
    {
        // Arrange
        var keyId = Guid.NewGuid();
        var (middleware, httpContext) = BuildMiddleware(
            new[] { TestPath },
            passThroughNext: false,
            keys: [BuildKeyRecord(keyId, TestSecret)]);

        // Timestamp 120 seconds in the past (outside ±60s window)
        long oldTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 120;
        AddHmacHeaders(httpContext, keyId, TestSecret, oldTimestamp);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal(401, httpContext.Response.StatusCode);
        _output.WriteLine("W14-S2 PASS: Expired timestamp → 401");
    }

    [Fact(DisplayName = "W14-S3: Nonce replay returns 401")]
    public async Task NonceReplay_Returns401()
    {
        // Arrange — first request with a valid nonce
        var keyId = Guid.NewGuid();
        string reusedNonce = Guid.NewGuid().ToString();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var keys = new List<ApiKeyRecord> { BuildKeyRecord(keyId, TestSecret) };

        // Simulate the nonce already being in the cache (previous request)
        cache.Set($"nonce:{keyId}:{reusedNonce}", true, TimeSpan.FromSeconds(120));

        var (middleware, httpContext) = BuildMiddlewareWithCache(cache, [TestPath], keys);
        AddHmacHeaders(httpContext, keyId, TestSecret, now, nonce: reusedNonce);

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal(401, httpContext.Response.StatusCode);
        _output.WriteLine("W14-S3 PASS: Nonce replay → 401");
    }

    [Fact(DisplayName = "W14-S4: Wrong HMAC signature returns 401")]
    public async Task WrongSignature_Returns401()
    {
        // Arrange
        var keyId = Guid.NewGuid();
        var (middleware, httpContext) = BuildMiddleware(
            [TestPath],
            passThroughNext: false,
            keys: [BuildKeyRecord(keyId, TestSecret)]);

        // Correct headers but wrong secret used for signing
        AddHmacHeaders(httpContext, keyId, "wrong-secret-entirely", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal(401, httpContext.Response.StatusCode);
        _output.WriteLine("W14-S4 PASS: Wrong signature → 401");
    }

    [Fact(DisplayName = "W14-S5: Valid HMAC signature passes middleware")]
    public async Task ValidSignature_PassesMiddleware()
    {
        // Arrange
        var keyId = Guid.NewGuid();
        bool nextCalled = false;

        var (middleware, httpContext) = BuildMiddleware(
            [TestPath],
            passThroughNext: true,
            keys: [BuildKeyRecord(keyId, TestSecret)],
            onNext: () => nextCalled = true);

        AddHmacHeaders(httpContext, keyId, TestSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert — next was called (middleware did not short-circuit)
        Assert.True(nextCalled, "Middleware should have called next() for a valid signature");
        Assert.Equal(200, httpContext.Response.StatusCode);
        _output.WriteLine("W14-S5 PASS: Valid signature → middleware passes");
    }

    [Fact(DisplayName = "W14-S6: Revoked API key returns 401")]
    public async Task RevokedKey_Returns401()
    {
        // Arrange — key record missing from lookup (simulates revoked/expired key returning null)
        var keyId = Guid.NewGuid();
        var (middleware, httpContext) = BuildMiddleware(
            [TestPath],
            passThroughNext: false,
            keys: []); // empty: key not found → revoked/inactive

        AddHmacHeaders(httpContext, keyId, TestSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        Assert.Equal(401, httpContext.Response.StatusCode);
        _output.WriteLine("W14-S6 PASS: Revoked/missing key → 401");
    }

    [Fact(DisplayName = "W14-S7: 5 consecutive failures block the API key for 15 minutes")]
    public async Task FiveFailures_BlocksKey()
    {
        // Arrange — wrong signature repeated 5 times
        var keyId = Guid.NewGuid();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var keys = new List<ApiKeyRecord> { BuildKeyRecord(keyId, TestSecret) };

        for (int i = 0; i < 5; i++)
        {
            var (middleware, httpContext) = BuildMiddlewareWithCache(cache, [TestPath], keys);
            AddHmacHeaders(httpContext, keyId, "wrong-secret", DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                nonce: Guid.NewGuid().ToString()); // fresh nonce each time
            await middleware.InvokeAsync(httpContext);
            Assert.Equal(401, httpContext.Response.StatusCode);
        }

        // 6th attempt — should be blocked even with correct signature
        var (lastMiddleware, lastContext) = BuildMiddlewareWithCache(cache, [TestPath], keys);
        AddHmacHeaders(lastContext, keyId, TestSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await lastMiddleware.InvokeAsync(lastContext);

        // Assert — 6th request is blocked
        Assert.Equal(401, lastContext.Response.StatusCode);
        _output.WriteLine("W14-S7 PASS: 5 failures → key blocked → 6th attempt still 401");
    }

    // ── ApiKeyManagementService tests (database-backed) ────────────────────────

    [Fact(DisplayName = "W14-S8: CreateKey generates unique secret and persists key")]
    public async Task CreateKey_PersistsAndReturnsSecret()
    {
        // Arrange
        var repo = new ApiKeyRepository(_dbContext);
        var service = new ApiKeyManagementService(repo, NullLogger<ApiKeyManagementService>.Instance);
        var tenantId = TestTenantId.Value;

        // Act
        var (key, rawSecret) = await service.CreateKeyAsync(tenantId, "Test Key", expirationDays: 30);

        // Assert
        Assert.NotNull(key);
        Assert.False(string.IsNullOrEmpty(rawSecret), "Raw secret must be returned on creation");
        Assert.Equal(tenantId, key.TenantId);
        Assert.Equal("Test Key", key.Name);
        Assert.True(key.IsActive);
        Assert.True(key.ExpiresAt > DateTime.UtcNow);

        // Verify persisted in DB
        var fromDb = await _dbContext.ApiKeys.FindAsync(key.Id);
        Assert.NotNull(fromDb);
        Assert.Equal("Test Key", fromDb.Name);
        _output.WriteLine($"W14-S8 PASS: Key {key.Id} created, secret length={rawSecret.Length}");
    }

    [Fact(DisplayName = "W14-S9: ListKeys returns only keys for the requesting tenant")]
    public async Task ListKeys_ReturnsOnlyTenantKeys()
    {
        // Arrange
        var repo = new ApiKeyRepository(_dbContext);
        var service = new ApiKeyManagementService(repo, NullLogger<ApiKeyManagementService>.Instance);

        var tenantA = TestTenantId.Value;
        var tenantB = Guid.Parse("99999999-9999-9999-9999-999999999999");

        await service.CreateKeyAsync(tenantA, "Key A1");
        await service.CreateKeyAsync(tenantA, "Key A2");
        await service.CreateKeyAsync(tenantB, "Key B1");

        // Act
        var keysA = await service.ListKeysAsync(tenantA);
        var keysB = await service.ListKeysAsync(tenantB);

        // Assert
        Assert.Equal(2, keysA.Count);
        Assert.Single(keysB);
        Assert.All(keysA, k => Assert.Equal(tenantA, k.TenantId));
        _output.WriteLine($"W14-S9 PASS: Tenant A has {keysA.Count} keys, Tenant B has {keysB.Count} key(s)");
    }

    [Fact(DisplayName = "W14-S10: RevokeKey sets IsActive=false and records RevokedAt")]
    public async Task RevokeKey_SetsInactive()
    {
        // Arrange
        var repo = new ApiKeyRepository(_dbContext);
        var service = new ApiKeyManagementService(repo, NullLogger<ApiKeyManagementService>.Instance);
        var tenantId = TestTenantId.Value;

        var (key, _) = await service.CreateKeyAsync(tenantId, "Revoke Me");
        Assert.True(key.IsActive);

        // Act
        var revoked = await service.RevokeKeyAsync(key.Id, tenantId);

        // Assert
        Assert.False(revoked.IsActive);
        Assert.NotNull(revoked.RevokedAt);

        // Verify FindActiveKey returns null for revoked key
        var found = await service.FindActiveKeyAsync(key.Id);
        Assert.Null(found);
        _output.WriteLine($"W14-S10 PASS: Key {key.Id} revoked; FindActiveKey returns null");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static ApiKeyRecord BuildKeyRecord(Guid keyId, string secret)
        => new(keyId, Guid.NewGuid(), "TestKey", secret);

    private static void AddHmacHeaders(
        DefaultHttpContext ctx,
        Guid keyId,
        string secret,
        long timestamp,
        string? nonce = null,
        string body = "")
    {
        nonce ??= Guid.NewGuid().ToString();
        string path = ctx.Request.Path.ToString();
        string method = ctx.Request.Method;

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string bodyHash = Convert.ToHexString(SHA256.HashData(bodyBytes)).ToLowerInvariant();
        string signingString = $"{method.ToUpperInvariant()}\n{path}\n{keyId}\n{timestamp}\n{nonce}\n{bodyHash}";
        string signature = HmacSigningMiddleware.ComputeHmacSha256Base64(signingString, secret);

        ctx.Request.Headers["X-VanAn-KeyId"] = keyId.ToString();
        ctx.Request.Headers["X-VanAn-Timestamp"] = timestamp.ToString();
        ctx.Request.Headers["X-VanAn-Nonce"] = nonce;
        ctx.Request.Headers["X-VanAn-Signature"] = signature;
    }

    private static (HmacSigningMiddleware middleware, DefaultHttpContext ctx) BuildMiddleware(
        IEnumerable<string> protectedPaths,
        bool passThroughNext,
        IEnumerable<ApiKeyRecord>? keys = null,
        Action? onNext = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return BuildMiddlewareWithCache(cache, protectedPaths, keys, passThroughNext, onNext);
    }

    private static (HmacSigningMiddleware middleware, DefaultHttpContext ctx) BuildMiddlewareWithCache(
        IMemoryCache cache,
        IEnumerable<string> protectedPaths,
        IEnumerable<ApiKeyRecord>? keys = null,
        bool passThroughNext = false,
        Action? onNext = null)
    {
        var keyList = keys?.ToList() ?? [];

        var options = new HmacSigningOptions
        {
            ProtectedPaths = protectedPaths.Select(p => new PathString(p)).ToList()
        };

        var lookup = new StaticApiKeyLookup(keyList);

        RequestDelegate next = ctx =>
        {
            onNext?.Invoke();
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new HmacSigningMiddleware(
            next,
            cache,
            NullLogger<HmacSigningMiddleware>.Instance,
            options);

        var services = new ServiceCollection();
        services.AddSingleton<IHmacApiKeyLookup>(lookup);
        var sp = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = sp,
            Request = { Method = "GET", Path = TestPath }
        };
        // Provide a writable body so middleware JSON writes don't throw
        httpContext.Response.Body = new System.IO.MemoryStream();

        return (middleware, httpContext);
    }

    /// <summary>Simple in-memory IHmacApiKeyLookup for tests.</summary>
    private sealed class StaticApiKeyLookup(List<ApiKeyRecord> keys) : IHmacApiKeyLookup
    {
        private readonly List<ApiKeyRecord> _keys = keys;

        public Task<ApiKeyRecord?> FindActiveKeyAsync(Guid keyId, CancellationToken ct = default)
            => Task.FromResult(_keys.FirstOrDefault(k => k.Id == keyId));

        public Task RecordUsageAsync(Guid keyId, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
