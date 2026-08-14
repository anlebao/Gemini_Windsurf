using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Guard;

/// <summary>
/// #126 Sprint 6: GuardService unit tests.
/// Uses Moq for IVehicleSessionRepository, IGuardScanLogRepository, IR2StorageService.
/// Tests all 9 business methods + state transition logic.
/// </summary>
public class GuardServiceTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid GuardGuid = Guid.NewGuid();
    private static readonly Guid CustomerGuid = Guid.NewGuid();
    private const string Plate = "51F-12345";
    private const string PlateKey = "plates/test.jpg";
    private const string CustomerKey = "customers/test.jpg";
    private const string Code = "123456";

    private readonly Mock<IVehicleSessionRepository> _sessionRepo = new();
    private readonly Mock<IGuardScanLogRepository> _scanLogRepo = new();
    private readonly Mock<IR2StorageService> _r2Storage = new();
    private readonly GuardService _service;

    public GuardServiceTests()
    {
        _r2Storage.Setup(r => r.GenerateKey(It.IsAny<string>(), It.IsAny<Guid>()))
            .Returns<string, Guid>((prefix, t) => $"{prefix}/{t}/test.jpg");
        _r2Storage.Setup(r => r.GetPresignedUploadUrl(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns("https://r2.example.com/presigned");
        _r2Storage.Setup(r => r.GetPresignedDownloadUrl(It.IsAny<string>(), It.IsAny<int>()))
            .Returns("https://r2.example.com/download");
        _service = new GuardService(_sessionRepo.Object, _scanLogRepo.Object, _r2Storage.Object,
            NullLogger<GuardService>.Instance);
    }

    private VehicleSession CreateSession(string? hash = null) => new(
        new TenantId(TenantGuid), Plate, PlateKey, CustomerKey,
        GuardGuid, hash ?? "somehash", Code);

    [Fact]
    public async Task PresignUploadAsync_ReturnsTwoPresignedUrls()
    {
        var result = await _service.PresignUploadAsync(TenantGuid, "image/jpeg");
        Assert.NotEmpty(result.PlatePhotoKey);
        Assert.NotEmpty(result.CustomerPhotoKey);
        Assert.NotEmpty(result.PlatePhotoUploadUrl);
        Assert.NotEmpty(result.CustomerPhotoUploadUrl);
    }

    [Fact]
    public async Task IssueAsync_ValidInput_CreatesSessionWithHashedToken()
    {
        VehicleSession? captured = null;
        _sessionRepo.Setup(r => r.AddAsync(It.IsAny<VehicleSession>(), It.IsAny<CancellationToken>()))
            .Callback<VehicleSession, CancellationToken>((s, _) => captured = s);
        _sessionRepo.Setup(r => r.GetByShortCodeAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleSession?)null);

        var result = await _service.IssueAsync(TenantGuid, GuardGuid,
            new IssueRequest(Plate, PlateKey, CustomerKey, null));

        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.NotEmpty(result.QrPayload);
        Assert.NotEmpty(result.ShortCode);
        Assert.NotNull(captured);
        Assert.Equal(VehicleSessionStatus.Issued, captured!.Status);
        _sessionRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimAsync_ByQrPayload_TransitionsToClaimed()
    {
        var session = CreateSession("testhash");
        _sessionRepo.Setup(r => r.GetByQrTokenHashAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _service.ClaimAsync(TenantGuid, CustomerGuid, new ClaimRequest("payload-with-tn", null));

        Assert.Equal(VehicleSessionStatus.Claimed, result.Status);
        Assert.Equal(CustomerGuid, session.CustomerId);
        _sessionRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClaimAsync_ByShortCode_TransitionsToClaimed()
    {
        var session = CreateSession("testhash");
        _sessionRepo.Setup(r => r.GetByShortCodeAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _service.ClaimAsync(TenantGuid, CustomerGuid, new ClaimRequest(null, Code));

        Assert.Equal(VehicleSessionStatus.Claimed, result.Status);
    }

    [Fact]
    public async Task ClaimAsync_AlreadyClaimed_Throws()
    {
        var session = CreateSession("testhash");
        session.Claim(CustomerGuid);
        _sessionRepo.Setup(r => r.GetByQrTokenHashAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ClaimAsync(TenantGuid, Guid.NewGuid(), new ClaimRequest("payload", null)));
    }

    [Fact]
    public async Task ClaimAsync_Voided_Throws()
    {
        var session = CreateSession("testhash");
        session.Void();
        _sessionRepo.Setup(r => r.GetByQrTokenHashAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ClaimAsync(TenantGuid, CustomerGuid, new ClaimRequest("payload", null)));
    }

    [Fact]
    public async Task ClaimAsync_AlreadyCheckedOut_Throws()
    {
        var session = CreateSession("testhash");
        session.Checkout(GuardGuid);
        _sessionRepo.Setup(r => r.GetByQrTokenHashAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ClaimAsync(TenantGuid, CustomerGuid, new ClaimRequest("payload", null)));
    }

    [Fact]
    public async Task ClaimAsync_NotFound_Throws()
    {
        _sessionRepo.Setup(r => r.GetByQrTokenHashAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleSession?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ClaimAsync(TenantGuid, CustomerGuid, new ClaimRequest("payload", null)));
    }

    [Fact]
    public async Task VerifyAsync_ValidQr_ReturnsSessionWithPhotos()
    {
        var session = CreateSession("testhash");
        _sessionRepo.Setup(r => r.GetByQrTokenHashAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _service.VerifyAsync(TenantGuid, GuardGuid, "payload");

        Assert.Equal(session.Id, result.SessionId);
        Assert.Equal(Plate, result.PlateNumber);
        Assert.NotEmpty(result.PlatePhotoUrl);
        Assert.NotEmpty(result.CustomerPhotoUrl);
        _scanLogRepo.Verify(r => r.AddAsync(It.IsAny<GuardScanLog>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyAsync_UnknownQr_ThrowsKeyNotFound()
    {
        _sessionRepo.Setup(r => r.GetByQrTokenHashAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleSession?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.VerifyAsync(TenantGuid, GuardGuid, "unknown-payload"));
    }

    [Fact]
    public async Task VerifyAsync_VoidedQr_Throws()
    {
        var session = CreateSession("testhash");
        session.Void();
        _sessionRepo.Setup(r => r.GetByQrTokenHashAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.VerifyAsync(TenantGuid, GuardGuid, "payload"));
    }

    [Fact]
    public async Task CheckoutAsync_ValidSession_TransitionsToCheckedOut()
    {
        var session = CreateSession("testhash");
        _sessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _service.CheckoutAsync(TenantGuid, GuardGuid, session.Id);

        Assert.Equal(VehicleSessionStatus.CheckedOut, result.Status);
        Assert.Equal(VehicleSessionStatus.CheckedOut, session.Status);
    }

    [Fact]
    public async Task CheckoutAsync_NotFound_Throws()
    {
        _sessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VehicleSession?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.CheckoutAsync(TenantGuid, GuardGuid, Guid.NewGuid()));
    }

    [Fact]
    public async Task FlagAsync_ValidSession_TransitionsToFlagged()
    {
        var session = CreateSession("testhash");
        _sessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _service.FlagAsync(TenantGuid, GuardGuid, session.Id, "Mismatch");

        Assert.Equal(VehicleSessionStatus.Flagged, result.Status);
        Assert.Equal("Mismatch", result.FlagReason);
    }

    [Fact]
    public async Task VoidAsync_ValidSession_TransitionsToVoided()
    {
        var session = CreateSession("testhash");
        _sessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _service.VoidAsync(TenantGuid, GuardGuid, session.Id);

        Assert.Equal(VehicleSessionStatus.Voided, result.Status);
    }

    [Fact]
    public async Task GetTodaySessionsAsync_PaginatesCorrectly()
    {
        var sessions = new List<VehicleSession> { CreateSession(), CreateSession() };
        _sessionRepo.Setup(r => r.GetTodaySessionsAsync(It.IsAny<Guid>(), It.IsAny<VehicleSessionStatus?>(),
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((sessions, 2));
        _sessionRepo.Setup(r => r.GetTodayStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((2, 0, 2));

        var result = await _service.GetTodaySessionsAsync(TenantGuid, null, 1, 20);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.CheckInCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetSessionAsync_ValidId_ReturnsDetail()
    {
        var session = CreateSession("testhash");
        _sessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _service.GetSessionAsync(TenantGuid, session.Id);

        Assert.Equal(Plate, result.PlateNumber);
        Assert.NotEmpty(result.PlatePhotoUrl);
    }

    [Fact]
    public async Task GetSessionStatusesAsync_ReturnsOwnedSessions()
    {
        var s1 = CreateSession("hash1");
        s1.Claim(CustomerGuid);
        var s2 = CreateSession("hash2");
        s2.Claim(CustomerGuid);
        _sessionRepo.Setup(r => r.GetByIdsForCustomerAsync(It.IsAny<Guid>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VehicleSession> { s1, s2 });

        var result = await _service.GetSessionStatusesAsync(CustomerGuid, new List<Guid> { s1.Id, s2.Id });

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(CustomerGuid, r.CustomerId));
    }
}
