using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Core.Tests.DynamicCors;

/// <summary>
/// Dynamic CORS Sprint 1: Unit tests for DynamicCorsService.
/// Tests static origins, dynamic snapshot, cache miss, normalization, edge cases.
/// Each test uses a fresh MemoryCache — no cross-test cache pollution.
/// </summary>
public class DynamicCorsServiceTests
{
    private static DynamicCorsService CreateService(
        IMemoryCache cache,
        string[]? staticOrigins = null,
        HashSet<string>? snapshot = null)
    {
        if (snapshot is not null)
            cache.Set(DynamicCorsService.SnapshotKey, snapshot, TimeSpan.FromMinutes(5));

        var configData = new Dictionary<string, string?>();
        if (staticOrigins is not null && staticOrigins.Length > 0)
        {
            for (int i = 0; i < staticOrigins.Length; i++)
                configData[$"Cors:StaticOrigins:{i}"] = staticOrigins[i];
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        return new DynamicCorsService(cache, config, NullLogger<DynamicCorsService>.Instance);
    }

    [Fact]
    public void StaticOrigin_Allowed()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = CreateService(cache, staticOrigins: ["https://app2.khachvip.online"]);

        Assert.True(svc.IsOriginAllowed("https://app2.khachvip.online"));
    }

    [Fact]
    public void UnknownOrigin_Rejected_WhenCacheEmpty()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = CreateService(cache, staticOrigins: [], snapshot: null);

        Assert.False(svc.IsOriginAllowed("https://evil.com"));
    }

    [Fact]
    public void RegistryOrigin_Allowed_WhenInSnapshot()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var snapshot = new HashSet<string> { "https://timlathay.com" };
        var svc = CreateService(cache, staticOrigins: [], snapshot: snapshot);

        Assert.True(svc.IsOriginAllowed("https://timlathay.com"));
    }

    [Fact]
    public void OriginNotInSnapshot_Rejected()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var snapshot = new HashSet<string> { "https://timlathay.com" };
        var svc = CreateService(cache, staticOrigins: [], snapshot: snapshot);

        Assert.False(svc.IsOriginAllowed("https://evil.com"));
    }

    [Fact]
    public void CacheMiss_StartupRace_RejectsConservatively()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        // No snapshot in cache — simulates startup race before HostedService warms cache
        var svc = CreateService(cache, staticOrigins: [], snapshot: null);

        Assert.False(svc.IsOriginAllowed("https://timlathay.com"));
    }

    [Fact]
    public void OriginNormalization_TrailingSlash_Allowed()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var snapshot = new HashSet<string> { "https://timlathay.com" };
        var svc = CreateService(cache, staticOrigins: [], snapshot: snapshot);

        Assert.True(svc.IsOriginAllowed("https://timlathay.com/"));
    }

    [Fact]
    public void OriginNormalization_CaseInsensitive_Allowed()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var snapshot = new HashSet<string> { "https://timlathay.com" };
        var svc = CreateService(cache, staticOrigins: [], snapshot: snapshot);

        Assert.True(svc.IsOriginAllowed("https://TIMLATHAY.COM"));
    }

    [Fact]
    public void EmptyOrigin_Rejected()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = CreateService(cache, staticOrigins: ["https://app2.khachvip.online"]);

        Assert.False(svc.IsOriginAllowed(""));
    }

    [Fact]
    public void NullOrigin_Rejected()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = CreateService(cache, staticOrigins: ["https://app2.khachvip.online"]);

        Assert.False(svc.IsOriginAllowed(null!));
    }

    [Fact]
    public void StaticOrigins_CachedNeverExpire()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = CreateService(cache, staticOrigins: ["https://app2.khachvip.online"]);

        // First call loads static origins from config into cache
        Assert.True(svc.IsOriginAllowed("https://app2.khachvip.online"));

        // Second call should still work (cached with NeverRemove)
        Assert.True(svc.IsOriginAllowed("https://app2.khachvip.online"));
    }
}
