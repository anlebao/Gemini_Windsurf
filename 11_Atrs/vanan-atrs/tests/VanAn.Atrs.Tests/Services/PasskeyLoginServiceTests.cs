using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using VanAn.Atrs.Contracts;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

/// <summary>
/// ATRS-21 — discoverable passkey login (subject-unknown ceremony).
/// Crypto verify (MakeAssertion) là responsibility của Fido2 lib — test ở đây focus
/// challenge storage contract (Guid.Empty sentinel), credential resolution gates,
/// anti-oracle consume semantics. Happy-path crypto cover ở RV (virtual authenticator).
/// Token issuance branches cover ở <see cref="PasskeyLoginTokenResolverTests"/>.
/// </summary>
public sealed class PasskeyLoginServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly WebAuthnService _service;
    private readonly Mock<IPasskeyLoginTokenIssuer> _issuer = new();

    private const string Origin = "https://vanan.timlathay.com";
    private const string RpId = "timlathay.com";

    public PasskeyLoginServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"passkey-login-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new WebAuthnService(
            _db,
            Options.Create(new WebAuthnOptions
            {
                RpId = RpId,
                Origins = [Origin],
                ChallengeTtlMinutes = 5,
                ResidentKey = "Required"
            }),
            _issuer.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── T1: BeginPasskeyLogin — discoverable challenge, subject sentinel ─────

    [Fact]
    public async Task BeginPasskeyLogin_StoresChallenge_WithEmptySubject_AndDiscoverableOptions()
    {
        var result = await _service.BeginPasskeyLoginAsync(Origin, RpId);

        Assert.False(string.IsNullOrEmpty(result.Challenge));

        var stored = await _db.WebAuthnChallenges.SingleAsync();
        Assert.Equal(WebAuthnChallengePurpose.PasskeyLogin, stored.Purpose);
        Assert.Equal(Guid.Empty, stored.TrustSubjectId); // sentinel — subject unknown at begin
        Assert.Equal(Origin, stored.Origin);
        Assert.Equal(RpId, stored.RpId);
        Assert.Null(stored.ConsumedAt);

        // Options phải là discoverable: allowedCredentials rỗng + UV required.
        var optionsJson = stored.OptionsJson;
        Assert.Contains("\"allowCredentials\":[]", optionsJson.Replace(" ", ""));
        Assert.Contains("required", optionsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BeginPasskeyLogin_OriginNotAllowed_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.BeginPasskeyLoginAsync("https://evil.example.com", RpId));
        Assert.Empty(_db.WebAuthnChallenges);
    }

    [Fact]
    public async Task BeginPasskeyLogin_RpIdMismatchOrigin_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.BeginPasskeyLoginAsync(Origin, "evil.com"));
    }

    // ── T3: FinishPasskeyLogin — challenge gates (anti-oracle) ──────────────

    [Fact]
    public async Task FinishPasskeyLogin_UnknownChallenge_ReturnsInvalidChallenge()
    {
        var result = await _service.FinishPasskeyLoginAsync(
            "nonexistent", Origin, RpId, FakeAssertion(), "device-1");
        Assert.False(result.Success);
        Assert.Equal("InvalidChallenge", result.ReasonCode);
    }

    [Fact]
    public async Task FinishPasskeyLogin_ChallengeWrongOrigin_ReturnsInvalidChallenge()
    {
        var challenge = await BeginLoginChallengeAsync();
        var result = await _service.FinishPasskeyLoginAsync(
            challenge, "https://other.timlathay.com", RpId, FakeAssertion(), "device-1");
        Assert.Equal("InvalidChallenge", result.ReasonCode);
    }

    [Fact]
    public async Task FinishPasskeyLogin_ChallengeWrongRpId_ReturnsInvalidChallenge()
    {
        var challenge = await BeginLoginChallengeAsync();
        var result = await _service.FinishPasskeyLoginAsync(
            challenge, Origin, "other.com", FakeAssertion(), "device-1");
        Assert.Equal("InvalidChallenge", result.ReasonCode);
    }

    [Fact]
    public async Task FinishPasskeyLogin_ConsumedChallenge_CannotReplay()
    {
        var challenge = await BeginLoginChallengeAsync();
        // Lần 1: assertion rác → consume challenge.
        var first = await _service.FinishPasskeyLoginAsync(challenge, Origin, RpId, FakeAssertion(), "device-1");
        Assert.False(first.Success);
        // Lần 2: replay cùng challenge → InvalidChallenge.
        var second = await _service.FinishPasskeyLoginAsync(challenge, Origin, RpId, FakeAssertion(), "device-1");
        Assert.Equal("InvalidChallenge", second.ReasonCode);
    }

    [Fact]
    public async Task FinishPasskeyLogin_SubjectScopedAuthenticationChallenge_NotAccepted()
    {
        // Challenge purpose Authentication (subject-scoped) KHÔNG được dùng cho login.
        var subject = Guid.NewGuid();
        var begin = await _service.BeginAuthenticationAsync(subject, Origin, RpId);
        var result = await _service.FinishPasskeyLoginAsync(
            begin.Challenge, Origin, RpId, FakeAssertion(), "device-1");
        Assert.Equal("InvalidChallenge", result.ReasonCode);
    }

    // ── T4/T5: credential resolution gates ──────────────────────────────────

    [Fact]
    public async Task FinishPasskeyLogin_UnknownCredentialId_ReturnsUnknownCredential_AndConsumes()
    {
        var challenge = await BeginLoginChallengeAsync();
        var result = await _service.FinishPasskeyLoginAsync(
            challenge, Origin, RpId, FakeAssertion(credentialIdB64: "AAAAAAAAAAAAAAAAAAAAAA"), "device-1");
        Assert.False(result.Success);
        Assert.Equal("UnknownCredential", result.ReasonCode);
        Assert.Null(result.CustomerToken);
        var stored = await _db.WebAuthnChallenges.SingleAsync();
        Assert.NotNull(stored.ConsumedAt); // consumed — không replay được
    }

    [Fact]
    public async Task FinishPasskeyLogin_RevokedCredential_ReturnsRevokedCredential()
    {
        var subject = Guid.NewGuid();
        var credIdBytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var credIdB64 = Base64UrlEncode(credIdBytes);
        var cred = new DeviceCredential(Guid.NewGuid(), subject, credIdB64,
            publicKey: Base64UrlEncode(new byte[32]), signCount: 0, rpId: RpId,
            deviceRecordId: null, aaguid: null, transportsJson: null,
            backupEligible: false, backupState: false);
        cred.Revoke();
        _db.DeviceCredentials.Add(cred);
        await _db.SaveChangesAsync();

        var challenge = await BeginLoginChallengeAsync();
        var result = await _service.FinishPasskeyLoginAsync(
            challenge, Origin, RpId, FakeAssertion(credIdB64), "device-1");
        Assert.False(result.Success);
        Assert.Equal("RevokedCredential", result.ReasonCode);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private async Task<string> BeginLoginChallengeAsync()
    {
        var result = await _service.BeginPasskeyLoginAsync(Origin, RpId);
        return result.Challenge;
    }

    /// <summary>Assertion JSON tối thiểu deserialize được thành AuthenticatorAssertionRawResponse.</summary>
    private static JsonElement FakeAssertion(string credentialIdB64 = "AAAAAAAAAAAAAAAAAAAAAA")
        => JsonSerializer.SerializeToElement(new
        {
            id = credentialIdB64,
            rawId = credentialIdB64,
            type = "public-key",
            response = new
            {
                authenticatorData = "AA",
                clientDataJSON = "e30",
                signature = "AA",
                userHandle = "AAAAAAAAAAAAAAAAAAAAAA"
            }
        });

    private static string Base64UrlEncode(byte[] bytes)
        => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(bytes);
}

/// <summary>
/// ATRS-21 — PasskeyLoginTokenResolver: subject → customerId → ShopERP token.
/// 3 branches: Customer link → by-id; DeviceCluster/khác + deviceId → by-device;
/// không deviceId → fail graceful. Audit passkey.login.* events.
/// </summary>
public sealed class PasskeyLoginTokenResolverTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly Mock<IPasskeyLoginTokenIssuer> _issuer = new();
    private readonly PasskeyLoginTokenResolver _resolver;

    private static readonly Guid CustomerId = Guid.NewGuid();
    private const string DeviceId = "dev-abc-123";

    public PasskeyLoginTokenResolverTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"passkey-resolver-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _resolver = new PasskeyLoginTokenResolver(_db, _issuer.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Guid> SeedSubjectWithLink(TrustIdentityType type, Guid identityId, Guid? tenantId = null)
    {
        var subjectId = Guid.NewGuid();
        _db.TrustSubjects.Add(new TrustSubject(subjectId, null));
        _db.TrustSubjectLinks.Add(new TrustSubjectLink(Guid.NewGuid(), subjectId, type, identityId, tenantId));
        await _db.SaveChangesAsync();
        return subjectId;
    }

    // T8: subject có Customer link → by-id
    [Fact]
    public async Task Resolve_CustomerLink_CallsById_ReturnsToken()
    {
        var subjectId = await SeedSubjectWithLink(TrustIdentityType.Customer, CustomerId);

        _issuer.Setup(i => i.IssueByCustomerIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasskeyTokenResult(true, null, "tok-123", CustomerId, false));

        var result = await _resolver.ResolveAsync(subjectId, DeviceId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("tok-123", result.CustomerToken);
        _issuer.Verify(i => i.IssueByCustomerIdAsync(CustomerId, It.IsAny<CancellationToken>()), Times.Once);
        _issuer.Verify(i => i.IssueByDeviceAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // T9: chỉ DeviceCluster link + deviceId → by-device
    [Fact]
    public async Task Resolve_DeviceClusterOnly_WithDeviceId_CallsByDevice()
    {
        var subjectId = await SeedSubjectWithLink(TrustIdentityType.DeviceCluster, Guid.NewGuid());

        _issuer.Setup(i => i.IssueByDeviceAsync(DeviceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasskeyTokenResult(true, null, "tok-dev", Guid.NewGuid(), true));

        var result = await _resolver.ResolveAsync(subjectId, DeviceId, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.CustomerCreated);
        _issuer.Verify(i => i.IssueByDeviceAsync(DeviceId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    // T10: không link customer + không deviceId → fail graceful
    [Fact]
    public async Task Resolve_NoCustomerLink_NoDeviceId_FailsGraceful()
    {
        var subjectId = await SeedSubjectWithLink(TrustIdentityType.DeviceCluster, Guid.NewGuid());

        var result = await _resolver.ResolveAsync(subjectId, deviceId: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("NoCustomerIdentity", result.ReasonCode);
        _issuer.VerifyNoOtherCalls();
    }

    // T11: ShopERP fail → passthrough failure + audit fail event
    [Fact]
    public async Task Resolve_IssuerFails_ReturnsFailure_AndAudits()
    {
        var subjectId = await SeedSubjectWithLink(TrustIdentityType.Customer, CustomerId);

        _issuer.Setup(i => i.IssueByCustomerIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasskeyTokenResult(false, "TokenIssuanceFailed", null, null, false));

        var result = await _resolver.ResolveAsync(subjectId, DeviceId, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("TokenIssuanceFailed", result.ReasonCode);

        var evt = await _db.AccountSecurityEvents.SingleAsync();
        Assert.Equal(subjectId, evt.TrustSubjectId);
        Assert.Equal("passkey.login.fail", evt.EventType);
        Assert.Equal(SecurityEventOutcome.Failure, evt.Outcome);
    }

    // T12: success → audit success event
    [Fact]
    public async Task Resolve_Success_AuditsLoginSuccess()
    {
        var subjectId = await SeedSubjectWithLink(TrustIdentityType.Customer, CustomerId);

        _issuer.Setup(i => i.IssueByCustomerIdAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasskeyTokenResult(true, null, "tok", CustomerId, false));

        await _resolver.ResolveAsync(subjectId, DeviceId, CancellationToken.None);

        var evt = await _db.AccountSecurityEvents.SingleAsync();
        Assert.Equal("passkey.login.success", evt.EventType);
        Assert.Equal(SecurityEventOutcome.Success, evt.Outcome);
    }
}
