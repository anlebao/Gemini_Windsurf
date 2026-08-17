using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Services;
using Xunit;

namespace VanAn.Core.Tests.DynamicCors;

/// <summary>
/// Dynamic CORS Sprint 1: Unit tests for DynamicCorsCacheHostedService.
/// Tests pre-warm, DB failure handling, normalization in snapshot.
/// </summary>
public class DynamicCorsCacheHostedServiceTests
{
    private static (DynamicCorsCacheHostedService service, IMemoryCache cache) CreateService(
        Func<CancellationToken, Task<List<string>>> getDomainsImpl)
    {
        var instanceServiceMock = new Mock<IKhachLinkInstanceService>();
        instanceServiceMock
            .Setup(x => x.GetActiveCustomDomainsAsync(It.IsAny<CancellationToken>()))
            .Returns(getDomainsImpl);

        var services = new ServiceCollection();
        services.AddSingleton(instanceServiceMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var service = new DynamicCorsCacheHostedService(
            scopeFactory, cache, NullLogger<DynamicCorsCacheHostedService>.Instance);

        return (service, cache);
    }

    [Fact]
    public async Task PreWarm_OnStartup_PopulatesCache()
    {
        var (service, cache) = CreateService(ct =>
            Task.FromResult(new List<string> { "timlathay.com", "sanjob.com" }));

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(500, CancellationToken.None); // allow pre-warm to complete
        await service.StopAsync(CancellationToken.None);

        // Verify cache was populated
        Assert.True(cache.TryGetValue<HashSet<string>>(DynamicCorsService.SnapshotKey, out var snapshot));
        Assert.Contains("https://timlathay.com", snapshot!);
        Assert.Contains("https://sanjob.com", snapshot!);
    }

    [Fact]
    public async Task DBFailure_NoCrash_StaleCacheRetained()
    {
        var (service, cache) = CreateService(ct =>
            throw new InvalidOperationException("DB connection failed"));

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(500, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        // No exception thrown — service handled DB failure gracefully
        // Cache should NOT have snapshot (DB failed, nothing to cache)
        Assert.False(cache.TryGetValue<HashSet<string>>(DynamicCorsService.SnapshotKey, out _));
    }

    [Fact]
    public async Task NormalizationInSnapshot_LowercaseHttpsPrefix()
    {
        var (service, cache) = CreateService(ct =>
            Task.FromResult(new List<string> { "TIMLATHAY.COM" }));

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(500, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.True(cache.TryGetValue<HashSet<string>>(DynamicCorsService.SnapshotKey, out var snapshot));
        // Normalized to lowercase https:// prefix
        Assert.Contains("https://timlathay.com", snapshot!);
    }
}
