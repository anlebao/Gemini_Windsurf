using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;
using VanAn.Voice.Api.Auth;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Tests;

/// <summary>
/// DeviceTokenValidator transport coverage (zero-barrier guest calling, 2026-11-25):
/// the guest's device GUID arrives via ?customerDeviceId= (SignalR WS handshake can't
/// set headers) or the X-Customer-Device-Id header (TURN/missed-calls REST fetches).
/// The identity is self-asserted — same trust model as guest chat — so downstream
/// gates (verified context, VoiceGuestCalling flag, per-IP rate limit) carry the weight.
/// </summary>
public class DeviceTokenValidatorTests
{
    private static readonly Guid DeviceId = Guid.NewGuid();
    private static readonly DeviceTokenValidator Validator = new();

    [Fact]
    public async Task Device_id_via_query_resolves_device_identity()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?customerDeviceId={DeviceId}");

        var identity = await Validator.ValidateAsync(context);

        identity.Should().NotBeNull();
        identity!.Kind.Should().Be(VoiceIdentityKind.Device);
        identity.UserId.Should().Be(DeviceId);
        identity.TenantId.Should().BeNull();
    }

    [Fact]
    public async Task Device_id_via_header_resolves_device_identity()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Customer-Device-Id"] = DeviceId.ToString();

        var identity = await Validator.ValidateAsync(context);

        identity.Should().NotBeNull();
        identity!.Kind.Should().Be(VoiceIdentityKind.Device);
        identity.UserId.Should().Be(DeviceId);
    }

    [Fact]
    public async Task Missing_device_id_returns_null()
    {
        (await Validator.ValidateAsync(new DefaultHttpContext())).Should().BeNull();
    }

    [Fact]
    public async Task Non_guid_device_id_returns_null()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?customerDeviceId=not-a-guid");

        (await Validator.ValidateAsync(context)).Should().BeNull();
    }

    [Fact]
    public async Task Empty_guid_device_id_returns_null()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?customerDeviceId={Guid.Empty}");

        (await Validator.ValidateAsync(context)).Should().BeNull();
    }
}
