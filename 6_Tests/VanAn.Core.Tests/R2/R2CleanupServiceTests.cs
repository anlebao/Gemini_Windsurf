using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.R2;

/// <summary>
/// R2 Cleanup Service unit tests.
/// Uses Moq for IR2StorageService + IVehicleSessionRepository.
/// Tests stats, single-tenant cleanup, all-tenants cleanup, edge cases.
/// </summary>
public class R2CleanupServiceTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid TenantGuid2 = Guid.NewGuid();

    private readonly Mock<IR2StorageService> _r2Storage = new();
    private readonly Mock<IVehicleSessionRepository> _sessionRepo = new();
    private readonly R2CleanupService _service;

    public R2CleanupServiceTests()
    {
        _service = new R2CleanupService(_r2Storage.Object, _sessionRepo.Object,
            NullLogger<R2CleanupService>.Instance);
    }

    private static VehicleSession CreateExpiredSession(Guid tenantId, string plateKey, string customerKey)
    {
        var session = new VehicleSession(
            new TenantId(tenantId),
            "51F-12345",
            plateKey,
            customerKey,
            Guid.NewGuid(),
            "hash-test-" + Guid.NewGuid(),
            "123456");
        session.Checkout(Guid.NewGuid());
        // Simulate old checkout by reflection (CheckedOutAt is protected set)
        var prop = typeof(VehicleSession).GetProperty("CheckedOutAt");
        prop?.SetValue(session, DateTime.UtcNow.AddDays(-60));
        return session;
    }

    [Fact]
    public async Task GetTenantStatsAsync_ReturnsCorrectCounts()
    {
        // Arrange
        _r2Storage.Setup(r => r.ListObjectsByPrefixAsync(
                IR2StorageService.GetPlatePrefix(TenantGuid), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<S3ObjectInfo>
            {
                new("plates/t1/a.jpg", 50000, DateTime.UtcNow.AddDays(-10)),
                new("plates/t1/b.jpg", 30000, DateTime.UtcNow.AddDays(-5))
            });
        _r2Storage.Setup(r => r.ListObjectsByPrefixAsync(
                IR2StorageService.GetCustomerPrefix(TenantGuid), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<S3ObjectInfo>
            {
                new("customers/t1/c.jpg", 20000, DateTime.UtcNow.AddDays(-3))
            });

        // Act
        var stats = await _service.GetTenantStatsAsync(TenantGuid);

        // Assert
        Assert.Equal(2, stats.PlatePhotoCount);
        Assert.Equal(1, stats.CustomerPhotoCount);
        Assert.Equal(100000, stats.TotalSizeBytes);
        Assert.NotNull(stats.OldestPhotoDate);
    }

    [Fact]
    public async Task CleanupTenantAsync_DeletesExpiredPhotos_AndClearsDbKeys()
    {
        // Arrange
        var session1 = CreateExpiredSession(TenantGuid, "plates/t1/a.jpg", "customers/t1/c.jpg");
        var session2 = CreateExpiredSession(TenantGuid, "plates/t1/b.jpg", "");

        _sessionRepo.Setup(r => r.GetExpiredSessionsAsync(TenantGuid, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VehicleSession> { session1, session2 });

        _r2Storage.Setup(r => r.ListObjectsByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<S3ObjectInfo>());

        _r2Storage.Setup(r => r.DeleteObjectsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // 3 keys: a.jpg, c.jpg, b.jpg

        _sessionRepo.Setup(r => r.ClearPhotoKeysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        // Act
        var result = await _service.CleanupTenantAsync(TenantGuid, TimeSpan.FromDays(30));

        // Assert
        Assert.Equal(2, result.SessionsProcessed);
        Assert.Equal(3, result.PhotosDeleted);
        Assert.Empty(result.Errors);

        _r2Storage.Verify(r => r.DeleteObjectsAsync(
            It.Is<IEnumerable<string>>(keys => keys.Count() == 3),
            It.IsAny<CancellationToken>()), Times.Once);
        _sessionRepo.Verify(r => r.ClearPhotoKeysAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Count() == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CleanupTenantAsync_NoExpiredSessions_ReturnsZero()
    {
        // Arrange
        _sessionRepo.Setup(r => r.GetExpiredSessionsAsync(TenantGuid, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VehicleSession>());

        // Act
        var result = await _service.CleanupTenantAsync(TenantGuid, TimeSpan.FromDays(30));

        // Assert
        Assert.Equal(0, result.SessionsProcessed);
        Assert.Equal(0, result.PhotosDeleted);
        _r2Storage.Verify(r => r.DeleteObjectsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CleanupAllTenantsAsync_ProcessesAllTenants()
    {
        // Arrange
        _sessionRepo.Setup(r => r.GetTenantsWithExpiredSessionsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { TenantGuid, TenantGuid2 });

        _sessionRepo.Setup(r => r.GetExpiredSessionsAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid tid, DateTime _, CancellationToken _) =>
                new List<VehicleSession> { CreateExpiredSession(tid, $"plates/{tid}/a.jpg", "") });

        _r2Storage.Setup(r => r.ListObjectsByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<S3ObjectInfo>());
        _r2Storage.Setup(r => r.DeleteObjectsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _sessionRepo.Setup(r => r.ClearPhotoKeysAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CleanupAllTenantsAsync(TimeSpan.FromDays(30));

        // Assert
        Assert.Equal(2, result.SessionsProcessed);
        Assert.Equal(2, result.PhotosDeleted);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task CleanupAllTenantsAsync_NoTenants_ReturnsZero()
    {
        // Arrange
        _sessionRepo.Setup(r => r.GetTenantsWithExpiredSessionsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        // Act
        var result = await _service.CleanupAllTenantsAsync(TimeSpan.FromDays(30));

        // Assert
        Assert.Equal(0, result.SessionsProcessed);
        Assert.Equal(0, result.PhotosDeleted);
    }

    [Fact]
    public async Task CleanupTenantAsync_R2DeleteError_RecordsError()
    {
        // Arrange
        var session = CreateExpiredSession(TenantGuid, "plates/t1/a.jpg", "");
        _sessionRepo.Setup(r => r.GetExpiredSessionsAsync(TenantGuid, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VehicleSession> { session });

        _r2Storage.Setup(r => r.ListObjectsByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<S3ObjectInfo>());

        _r2Storage.Setup(r => r.DeleteObjectsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("R2 connection failed"));

        // Act
        var result = await _service.CleanupTenantAsync(TenantGuid, TimeSpan.FromDays(30));

        // Assert
        Assert.Equal(0, result.SessionsProcessed);
        Assert.Single(result.Errors);
        Assert.Contains("R2 connection failed", result.Errors[0]);
    }
}
