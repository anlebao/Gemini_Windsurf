using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using VanAn.CoreHub.Services;
using VanAn.Gateway.Realtime;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): Gateway realtime identity layer — the three token validators,
/// the composite resolver, group naming and the default-deny authorizer lookup.
/// </summary>
public class RealtimeGatewayP3Tests
{
    private static readonly Guid DeviceId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    // === T5: a guest device guid is accepted from the query string (SignalR handshake path) ===
    [Fact(DisplayName = "T5: DeviceTokenValidator_QueryString_ReturnsGuestIdentity")]
    public async Task DeviceTokenValidator_QueryString_ReturnsGuestIdentity()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?customerDeviceId={DeviceId}");

        var identity = await new DeviceTokenValidator().ValidateAsync(context);

        Assert.NotNull(identity);
        Assert.Equal(DeviceId, identity!.UserId);
        Assert.Equal(RealtimeIdentityKind.Device, identity.Kind);
        Assert.True(identity.IsGuest);
        Assert.Equal(RealtimeParticipantRole.Guest, identity.RoleCode);
    }

    // === T6: the same guid is accepted from the header (HTTP endpoint path) ===
    [Fact(DisplayName = "T6: DeviceTokenValidator_Header_ReturnsGuestIdentity")]
    public async Task DeviceTokenValidator_Header_ReturnsGuestIdentity()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Customer-Device-Id"] = DeviceId.ToString();

        var identity = await new DeviceTokenValidator().ValidateAsync(context);

        Assert.NotNull(identity);
        Assert.Equal(DeviceId, identity!.UserId);
        Assert.Equal(RealtimeIdentityKind.Device, identity.Kind);
    }

    // === T7: a missing or malformed device guid is not an identity ===
    [Theory(DisplayName = "T7: DeviceTokenValidator_InvalidInput_ReturnsNull")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task DeviceTokenValidator_InvalidInput_ReturnsNull(string? raw)
    {
        var context = new DefaultHttpContext();
        if (raw != null)
            context.Request.Headers["X-Customer-Device-Id"] = raw;

        Assert.Null(await new DeviceTokenValidator().ValidateAsync(context));
    }

    // === T8: the resolver returns the first validator that accepts, in registration order ===
    [Fact(DisplayName = "T8: IdentityResolver_ReturnsFirstAcceptingValidator")]
    public async Task IdentityResolver_ReturnsFirstAcceptingValidator()
    {
        var customer = new RealtimeIdentity(CustomerId, RealtimeIdentityKind.Customer);
        var device = new RealtimeIdentity(DeviceId, RealtimeIdentityKind.Device);

        // Customer registered first — a signed-in customer carrying a device guid stays the customer.
        var resolver = new RealtimeIdentityResolver(
            new IRealtimeTokenValidator[]
            {
                new FakeValidator("Customer", customer),
                new FakeValidator("Device", device)
            },
            NullLogger<RealtimeIdentityResolver>.Instance);

        var identity = await resolver.ResolveAsync(new DefaultHttpContext());

        Assert.NotNull(identity);
        Assert.Equal(CustomerId, identity!.UserId);
        Assert.Equal(RealtimeIdentityKind.Customer, identity.Kind);
    }

    // === T9: no validator accepts → no identity (caller turns this into 401) ===
    [Fact(DisplayName = "T9: IdentityResolver_NoValidatorAccepts_ReturnsNull")]
    public async Task IdentityResolver_NoValidatorAccepts_ReturnsNull()
    {
        var resolver = new RealtimeIdentityResolver(
            new IRealtimeTokenValidator[] { new FakeValidator("Customer", null), new FakeValidator("Device", null) },
            NullLogger<RealtimeIdentityResolver>.Instance);

        Assert.Null(await resolver.ResolveAsync(new DefaultHttpContext()));
    }

    // === T10: group names carry the subject type so subjects can never collide ===
    [Fact(DisplayName = "T10: RealtimeGroups_NamesAreSubjectScoped")]
    public void RealtimeGroups_NamesAreSubjectScoped()
    {
        var id = Guid.NewGuid();

        Assert.Equal($"msg_Order_{id}", RealtimeGroups.Messaging(RealtimeSubjectType.Order, id));
        Assert.Equal($"loc_Order_{id}", RealtimeGroups.Tracking(RealtimeSubjectType.Order, id));

        // Same guid, different subject type → different group.
        Assert.NotEqual(RealtimeGroups.Messaging(RealtimeSubjectType.Order, id),
                        RealtimeGroups.Messaging(RealtimeSubjectType.Shop, id));

        // The string overload used by Leave* matches what Join* produced.
        Assert.Equal(RealtimeGroups.Messaging(RealtimeSubjectType.Shop, id),
                     RealtimeGroups.Messaging(RealtimeSubjectType.Shop.ToString(), id.ToString()));
    }

    // === T11: an unregistered subject type is denied, not defaulted open ===
    [Fact(DisplayName = "T11: AuthorizerLookup_UnregisteredSubject_IsDenied")]
    public async Task AuthorizerLookup_UnregisteredSubject_IsDenied()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var allowed = await RealtimeAuthorizerLookup.CanAccessAsync(
            provider, RealtimeSubjectType.Shop, Guid.NewGuid(), Guid.NewGuid());

        Assert.False(allowed);
    }

    // === T12: a registered authorizer is consulted for its own subject type ===
    [Fact(DisplayName = "T12: AuthorizerLookup_RegisteredSubject_Delegates")]
    public async Task AuthorizerLookup_RegisteredSubject_Delegates()
    {
        var subjectId = Guid.NewGuid();
        var allowedUser = FakeAuthorizer.AllowedUser;

        using var provider = new ServiceCollection()
            .AddKeyedScoped<IRealtimeParticipantAuthorizer>(RealtimeSubjectType.Order, (_, _) => new FakeAuthorizer())
            .BuildServiceProvider();

        Assert.True(await RealtimeAuthorizerLookup.CanAccessAsync(provider, RealtimeSubjectType.Order, subjectId, allowedUser));
        Assert.False(await RealtimeAuthorizerLookup.CanAccessAsync(provider, RealtimeSubjectType.Order, subjectId, Guid.NewGuid()));

        // The Shop key has no authorizer even though Order does.
        Assert.False(await RealtimeAuthorizerLookup.CanAccessAsync(provider, RealtimeSubjectType.Shop, subjectId, allowedUser));
    }

    // === T13: staff JWT — absent/garbage rejected, a real ShopERP-shaped token resolves ===
    [Fact(DisplayName = "T13: StaffJwtValidator_ValidatesBearerToken")]
    public async Task StaffJwtValidator_ValidatesBearerToken()
    {
        const string secret = "unit-test-secret-key-that-is-long-enough-for-hs256";
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Jwt:Secret"]).Returns(secret);
        config.Setup(c => c["Jwt:Issuer"]).Returns("VanAnShopERP");
        config.Setup(c => c["Jwt:Audience"]).Returns("VanAnApi");

        var validator = new StaffJwtValidator(config.Object, NullLogger<StaffJwtValidator>.Instance);

        // No credential at all.
        Assert.Null(await validator.ValidateAsync(new DefaultHttpContext()));

        // Garbage bearer token.
        var garbage = new DefaultHttpContext();
        garbage.Request.Headers["Authorization"] = "Bearer not.a.jwt";
        Assert.Null(await validator.ValidateAsync(garbage));

        // A token minted with the same secret/issuer/audience as ShopERP.
        var token = MintToken(secret, CustomerId);
        var valid = new DefaultHttpContext();
        valid.Request.Headers["Authorization"] = $"Bearer {token}";

        var identity = await validator.ValidateAsync(valid);

        Assert.NotNull(identity);
        Assert.Equal(CustomerId, identity!.UserId);
        Assert.Equal(RealtimeIdentityKind.Staff, identity.Kind);
    }

    // === T14/T15: customer token — absent means "not my credential", valid forwards to ShopERP /me ===
    [Fact(DisplayName = "T14: CustomerTokenValidator_NoToken_ReturnsNullWithoutCallingShopERP")]
    public async Task CustomerTokenValidator_NoToken_ReturnsNullWithoutCallingShopERP()
    {
        var factory = new FakeHttpClientFactory(CustomerId);
        var validator = new CustomerTokenValidator(factory, NullLogger<CustomerTokenValidator>.Instance);

        Assert.Null(await validator.ValidateAsync(new DefaultHttpContext()));
        Assert.Equal(0, factory.CallCount);
    }

    [Fact(DisplayName = "T15: CustomerTokenValidator_ValidToken_ReturnsCustomerIdentity")]
    public async Task CustomerTokenValidator_ValidToken_ReturnsCustomerIdentity()
    {
        var factory = new FakeHttpClientFactory(CustomerId);
        var validator = new CustomerTokenValidator(factory, NullLogger<CustomerTokenValidator>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["X-Customer-Token"] = "valid-token";

        var identity = await validator.ValidateAsync(context);

        Assert.NotNull(identity);
        Assert.Equal(CustomerId, identity!.UserId);
        Assert.Equal(RealtimeIdentityKind.Customer, identity.Kind);
        Assert.Equal(RealtimeParticipantRole.Customer, identity.RoleCode);
        Assert.Equal(1, factory.CallCount);
    }

    private static string MintToken(string secret, Guid subjectId)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            issuer: "VanAnShopERP",
            audience: "VanAnApi",
            claims: new[] { new Claim("sub", subjectId.ToString()), new Claim(ClaimTypes.Role, "Owner") },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256));

        return handler.WriteToken(token);
    }

    private sealed class FakeValidator(string name, RealtimeIdentity? identity) : IRealtimeTokenValidator
    {
        public string Name => name;

        public Task<RealtimeIdentity?> ValidateAsync(HttpContext httpContext, CancellationToken ct = default)
            => Task.FromResult(identity);
    }

    /// <summary>Allows exactly one user id; used to prove the keyed lookup reaches the right authorizer.</summary>
    private sealed class FakeAuthorizer : IRealtimeParticipantAuthorizer
    {
        public static readonly Guid AllowedUser = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public Task<bool> CanAccessAsync(RealtimeSubjectType subjectType, Guid subjectId, Guid userId, CancellationToken ct = default)
            => Task.FromResult(userId == AllowedUser);
    }

    /// <summary>Fake ShopERP /me endpoint — returns the configured customer id for any token.</summary>
    private sealed class FakeHttpClientFactory(Guid customerId) : IHttpClientFactory
    {
        public int CallCount { get; private set; }

        public HttpClient CreateClient(string name)
            => new(new FakeHandler(this, customerId)) { BaseAddress = new Uri("http://shoperp/") };

        private sealed class FakeHandler(FakeHttpClientFactory owner, Guid customerId) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner.CallCount++;
                var json = $$"""{"customerId":"{{customerId}}"}""";
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
