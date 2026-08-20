using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Guard;

/// <summary>
/// PHASE-1: VehicleSession Plate-as-metadata tests.
/// Plate is optional — photo + QR token are primary verifiers.
/// Verifies: null plate OK, empty plate normalized to null, plate still accepted.
/// </summary>
public class VehicleSessionPlateOptionalTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();

    [Fact]
    public void Constructor_WithNullPlateNumber_SucceedsAndStoresNull()
    {
        // Act
        var session = new VehicleSession(
            new TenantId(TenantGuid),
            plateNumber: null,
            platePhotoKey: "plates/t1/a.jpg",
            customerPhotoKey: null,
            issuedBy: Guid.NewGuid(),
            qrTokenHash: "hash-" + Guid.NewGuid(),
            shortCode: "123456");

        // Assert
        Assert.Null(session.PlateNumber);
        Assert.Equal("plates/t1/a.jpg", session.PlatePhotoKey);
        Assert.Equal(VehicleSessionStatus.Issued, session.Status);
    }

    [Fact]
    public void Constructor_WithEmptyPlateNumber_NormalizesToNull()
    {
        // Act
        var session = new VehicleSession(
            new TenantId(TenantGuid),
            plateNumber: "",
            platePhotoKey: "plates/t1/a.jpg",
            customerPhotoKey: null,
            issuedBy: Guid.NewGuid(),
            qrTokenHash: "hash-" + Guid.NewGuid(),
            shortCode: "123456");

        // Assert — empty should normalize to null (PHASE-1 normalization)
        Assert.Null(session.PlateNumber);
    }

    [Fact]
    public void Constructor_WithWhitespacePlateNumber_NormalizesToNull()
    {
        // Act
        var session = new VehicleSession(
            new TenantId(TenantGuid),
            plateNumber: "   ",
            platePhotoKey: "plates/t1/a.jpg",
            customerPhotoKey: null,
            issuedBy: Guid.NewGuid(),
            qrTokenHash: "hash-" + Guid.NewGuid(),
            shortCode: "123456");

        // Assert — whitespace should normalize to null
        Assert.Null(session.PlateNumber);
    }

    [Fact]
    public void Constructor_WithValidPlateNumber_StoresPlate()
    {
        // Act
        var session = new VehicleSession(
            new TenantId(TenantGuid),
            plateNumber: "51F-12345",
            platePhotoKey: "plates/t1/a.jpg",
            customerPhotoKey: null,
            issuedBy: Guid.NewGuid(),
            qrTokenHash: "hash-" + Guid.NewGuid(),
            shortCode: "123456");

        // Assert — plate preserved for stats
        Assert.Equal("51F-12345", session.PlateNumber);
    }

    [Fact]
    public void Constructor_WithNullPlatePhotoKey_Throws()
    {
        // Act + Assert — photo remains required (PHASE-1: only plate became optional)
        Assert.Throws<ArgumentException>(() => new VehicleSession(
            new TenantId(TenantGuid),
            plateNumber: null,
            platePhotoKey: "",
            customerPhotoKey: null,
            issuedBy: Guid.NewGuid(),
            qrTokenHash: "hash-" + Guid.NewGuid(),
            shortCode: "123456"));
    }

    [Fact]
    public void Constructor_WithNullQrTokenHash_Throws()
    {
        Assert.Throws<ArgumentException>(() => new VehicleSession(
            new TenantId(TenantGuid),
            plateNumber: null,
            platePhotoKey: "plates/t1/a.jpg",
            customerPhotoKey: null,
            issuedBy: Guid.NewGuid(),
            qrTokenHash: "",
            shortCode: "123456"));
    }

    [Fact]
    public void Constructor_WithNullShortCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => new VehicleSession(
            new TenantId(TenantGuid),
            plateNumber: null,
            platePhotoKey: "plates/t1/a.jpg",
            customerPhotoKey: null,
            issuedBy: Guid.NewGuid(),
            qrTokenHash: "hash-" + Guid.NewGuid(),
            shortCode: ""));
    }
}
