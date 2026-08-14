using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Guard;

/// <summary>
/// #126 Sprint 6: VehicleSession domain entity unit tests.
/// Pure domain tests — no infrastructure, no mocks.
/// Covers: Create, Claim, Checkout, Flag, Void state transitions + invariants.
/// </summary>
public class VehicleSessionTests
{
    private static readonly Guid TenantGuid = Guid.NewGuid();
    private static readonly Guid GuardGuid = Guid.NewGuid();
    private static readonly Guid CustomerGuid = Guid.NewGuid();
    private const string Plate = "51F-12345";
    private const string PlateKey = "plates/test.jpg";
    private const string CustomerKey = "customers/test.jpg";
    private const string Hash = "abc123hash";
    private const string Code = "123456";

    private static VehicleSession CreateSession() => new(
        new TenantId(TenantGuid), Plate, PlateKey, CustomerKey,
        GuardGuid, Hash, Code, null);

    [Fact]
    public void Create_SetsStatusToIssued()
    {
        var s = CreateSession();
        Assert.Equal(VehicleSessionStatus.Issued, s.Status);
    }

    [Fact]
    public void Create_SetsIdToVehicleSessionIdValue()
    {
        var s = CreateSession();
        Assert.Equal(s.VehicleSessionId.Value, s.Id);
    }

    [Fact]
    public void Create_SetsCoreFields()
    {
        var s = CreateSession();
        Assert.Equal(Plate, s.PlateNumber);
        Assert.Equal(PlateKey, s.PlatePhotoKey);
        Assert.Equal(CustomerKey, s.CustomerPhotoKey);
        Assert.Equal(GuardGuid, s.IssuedBy);
        Assert.Equal(Hash, s.QrTokenHash);
        Assert.Equal(Code, s.ShortCode);
        Assert.Null(s.CustomerId);
    }

    [Fact]
    public void Claim_FromIssued_TransitionsToClaimed()
    {
        var s = CreateSession();
        s.Claim(CustomerGuid);
        Assert.Equal(VehicleSessionStatus.Claimed, s.Status);
        Assert.Equal(CustomerGuid, s.CustomerId);
        Assert.Equal(CustomerGuid, s.ClaimedBy);
        Assert.NotNull(s.ClaimedAt);
    }

    [Fact]
    public void Claim_FromClaimed_ThrowsInvalidOperationException()
    {
        var s = CreateSession();
        s.Claim(CustomerGuid);
        Assert.Throws<InvalidOperationException>(() => s.Claim(Guid.NewGuid()));
    }

    [Fact]
    public void Claim_FromVoided_ThrowsInvalidOperationException()
    {
        var s = CreateSession();
        s.Void();
        Assert.Throws<InvalidOperationException>(() => s.Claim(CustomerGuid));
    }

    [Fact]
    public void Claim_FromCheckedOut_ThrowsInvalidOperationException()
    {
        var s = CreateSession();
        s.Checkout(GuardGuid);
        Assert.Throws<InvalidOperationException>(() => s.Claim(CustomerGuid));
    }

    [Fact]
    public void Checkout_FromClaimed_TransitionsToCheckedOut()
    {
        var s = CreateSession();
        s.Claim(CustomerGuid);
        s.Checkout(GuardGuid);
        Assert.Equal(VehicleSessionStatus.CheckedOut, s.Status);
        Assert.Equal(GuardGuid, s.CheckedOutBy);
        Assert.NotNull(s.CheckedOutAt);
    }

    [Fact]
    public void Checkout_FromIssued_TransitionsToCheckedOut()
    {
        var s = CreateSession();
        s.Checkout(GuardGuid);
        Assert.Equal(VehicleSessionStatus.CheckedOut, s.Status);
    }

    [Fact]
    public void Checkout_FromVoided_ThrowsInvalidOperationException()
    {
        var s = CreateSession();
        s.Void();
        Assert.Throws<InvalidOperationException>(() => s.Checkout(GuardGuid));
    }

    [Fact]
    public void Flag_FromIssued_TransitionsToFlagged()
    {
        var s = CreateSession();
        s.Flag("Mismatch", GuardGuid);
        Assert.Equal(VehicleSessionStatus.Flagged, s.Status);
        Assert.Equal("Mismatch", s.FlagReason);
    }

    [Fact]
    public void Flag_FromClaimed_TransitionsToFlagged()
    {
        var s = CreateSession();
        s.Claim(CustomerGuid);
        s.Flag("Suspicious", GuardGuid);
        Assert.Equal(VehicleSessionStatus.Flagged, s.Status);
    }

    [Fact]
    public void Void_FromIssued_TransitionsToVoided()
    {
        var s = CreateSession();
        s.Void();
        Assert.Equal(VehicleSessionStatus.Voided, s.Status);
        Assert.NotNull(s.VoidedAt);
    }

    [Fact]
    public void Void_FromCheckedOut_ThrowsInvalidOperationException()
    {
        var s = CreateSession();
        s.Checkout(GuardGuid);
        Assert.Throws<InvalidOperationException>(() => s.Void());
    }

    [Fact]
    public void Create_WithEmptyPlate_Throws()
    {
        Assert.Throws<ArgumentException>(() => new VehicleSession(
            new TenantId(TenantGuid), "", PlateKey, CustomerKey, GuardGuid, Hash, Code));
    }
}
