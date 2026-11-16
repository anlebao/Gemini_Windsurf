using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VanAn.Atrs.Domain;
using VanAn.Atrs.Infra;
using Xunit;

namespace VanAn.Atrs.Tests.Services;

/// <summary>
/// WebAuthnService — challenge lifecycle (one-time, TTL, origin/RP binding), rate guard,
/// revocation, no-private-key. Fido2 crypto verify (attestation/assertion) là responsibility
/// của lib (đã battle-tested) — test ở đây focus wrapper logic + storage contract.
/// </summary>
public sealed class WebAuthnServiceTests : IDisposable
{
    private readonly AtrsDbContext _db;
    private readonly WebAuthnService _service;

    public WebAuthnServiceTests()
    {
        var options = new DbContextOptionsBuilder<AtrsDbContext>()
            .UseInMemoryDatabase($"webauthn-{Guid.NewGuid()}")
            .Options;
        _db = new AtrsDbContext(options);
        _service = new WebAuthnService(
            _db,
            Options.Create(new WebAuthnOptions
            {
                RpId = "timlathay.com",
                Origins = ["https://vanan.timlathay.com", "https://commienphi.timlathay.com"],
                ChallengeTtlMinutes = 5
            }));
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static readonly Guid Subject = Guid.NewGuid();

    [Fact]
    public async Task BeginRegistration_StoresChallenge_WithBinding()
    {
        var result = await _service.BeginRegistrationAsync(Subject, deviceRecordId: null,
            "https://vanan.timlathay.com", "timlathay.com");

        Assert.False(string.IsNullOrEmpty(result.Challenge));
        Assert.NotEqual(default, result.ExpiresAt);

        var stored = await _db.WebAuthnChallenges.SingleAsync();
        Assert.Equal(WebAuthnChallengePurpose.Registration, stored.Purpose);
        Assert.Equal(Subject, stored.TrustSubjectId);
        Assert.Equal("https://vanan.timlathay.com", stored.Origin);
        Assert.Equal("timlathay.com", stored.RpId);
        Assert.Null(stored.ConsumedAt);
        Assert.False(stored.IsExpired(DateTime.UtcNow));
        // Options JSON đầy đủ để verify stage (challenge + rp.id bên trong)
        using var doc = JsonDocument.Parse(stored.OptionsJson);
        Assert.True(doc.RootElement.TryGetProperty("challenge", out _));
    }

    [Fact]
    public async Task BeginRegistration_OriginNotInAllowlist_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.BeginRegistrationAsync(Subject, null, "https://evil.example.com", "timlathay.com"));
    }

    [Fact]
    public async Task FinishRegistration_UnknownChallenge_Rejected()
    {
        var result = await _service.FinishRegistrationAsync(Subject, "unknown-challenge",
            "https://vanan.timlathay.com", "timlathay.com", JsonDocument.Parse("{}").RootElement, null);

        Assert.False(result.Success);
        Assert.Equal("InvalidChallenge", result.Reason);
    }

    [Fact]
    public async Task FinishRegistration_ExpiredChallenge_Rejected()
    {
        var begin = await _service.BeginRegistrationAsync(Subject, null, "https://vanan.timlathay.com", "timlathay.com");
        var stored = await _db.WebAuthnChallenges.SingleAsync();
        // Hết hạn: dời ExpiresAt về quá khứ
        _db.WebAuthnChallenges.Remove(stored);
        await _db.SaveChangesAsync();
        _db.WebAuthnChallenges.Add(new WebAuthnChallenge(
            stored.Id, Subject, WebAuthnChallengePurpose.Registration, stored.Challenge,
            stored.Origin, stored.RpId, stored.DeviceRecordId, DateTime.UtcNow.AddMinutes(-1), stored.OptionsJson));
        await _db.SaveChangesAsync();

        var result = await _service.FinishRegistrationAsync(Subject, begin.Challenge,
            "https://vanan.timlathay.com", "timlathay.com", JsonDocument.Parse("{}").RootElement, null);

        Assert.False(result.Success);
        Assert.Equal("InvalidChallenge", result.Reason);
    }

    [Fact]
    public async Task FinishRegistration_OriginMismatch_Rejected()
    {
        var begin = await _service.BeginRegistrationAsync(Subject, null, "https://vanan.timlathay.com", "timlathay.com");

        // Challenge tạo ở origin A, finish gọi từ origin B → replay cross-origin bị chặn
        var result = await _service.FinishRegistrationAsync(Subject, begin.Challenge,
            "https://commienphi.timlathay.com", "timlathay.com", JsonDocument.Parse("{}").RootElement, null);

        Assert.False(result.Success);
        Assert.Equal("InvalidChallenge", result.Reason);
    }

    [Fact]
    public async Task Challenge_IsOneTime_ConsumedAfterUse()
    {
        await _service.BeginRegistrationAsync(Subject, null, "https://vanan.timlathay.com", "timlathay.com");
        var stored = await _db.WebAuthnChallenges.SingleAsync();
        Assert.Null(stored.ConsumedAt);

        await _service.FinishRegistrationAsync(Subject, stored.Challenge,
            "https://vanan.timlathay.com", "timlathay.com", JsonDocument.Parse("{}").RootElement, null);

        var after = await _db.WebAuthnChallenges.SingleAsync();
        Assert.NotNull(after.ConsumedAt);
    }

    [Fact]
    public async Task RateGuard_MaxActiveChallenges_Throws()
    {
        for (var i = 0; i < 5; i++)
            await _service.BeginRegistrationAsync(Subject, null, "https://vanan.timlathay.com", "timlathay.com");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.BeginRegistrationAsync(Subject, null, "https://vanan.timlathay.com", "timlathay.com"));
    }

    [Fact]
    public async Task ListCredentials_ExposesNoPublicKeyOrPrivateKey()
    {
        // Entity: không có field private key — contract bất biến (FR-004, NFR-001)
        var entityProps = typeof(DeviceCredential).GetProperties().Select(p => p.Name);
        Assert.DoesNotContain("PrivateKey", entityProps);
        Assert.DoesNotContain("PrivateKeyJson", entityProps);

        var dtoProps = typeof(VanAn.Atrs.Contracts.DeviceCredentialSummaryDto).GetProperties().Select(p => p.Name);
        Assert.DoesNotContain("PublicKey", dtoProps);
        Assert.DoesNotContain("PrivateKey", dtoProps);

        var list = await _service.ListCredentialsAsync(Subject);
        Assert.Empty(list);
    }

    [Fact]
    public async Task RevokeCredential_NotFound_False()
    {
        var ok = await _service.RevokeCredentialAsync(Subject, Guid.NewGuid());
        Assert.False(ok);
    }

    [Fact]
    public async Task RevokeAllForDevice_UnknownDevice_Zero()
    {
        var count = await _service.RevokeAllForDeviceAsync(Subject, Guid.NewGuid());
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BeginAuthentication_StoresAuthenticationChallenge()
    {
        var result = await _service.BeginAuthenticationAsync(Subject, "https://vanan.timlathay.com", "timlathay.com");

        Assert.False(string.IsNullOrEmpty(result.Challenge));
        var stored = await _db.WebAuthnChallenges.SingleAsync();
        Assert.Equal(WebAuthnChallengePurpose.Authentication, stored.Purpose);
    }

    [Fact]
    public async Task FinishAuthentication_UnknownChallenge_Rejected()
    {
        var result = await _service.FinishAuthenticationAsync(Subject, "nope",
            "https://vanan.timlathay.com", "timlathay.com", JsonDocument.Parse("{}").RootElement);
        Assert.False(result.Success);
        Assert.Equal("InvalidChallenge", result.Reason);
    }

    [Theory]
    [InlineData("timlathay.com", "https://vanan.timlathay.com", true)]
    [InlineData("timlathay.com", "https://commienphi.timlathay.com", true)]
    [InlineData("khachvip.online", "https://diemthuong2.khachvip.online", true)]
    [InlineData("timlathay.com", "https://evil.example.com", false)]  // không phải subdomain — bị chặn
    [InlineData("evil.com", "https://vanan.timlathay.com", false)]
    [InlineData("", "https://vanan.timlathay.com", false)]
    public void IsRpIdValidForOrigin_SuffixRule(string rpId, string origin, bool expected)
    {
        Assert.Equal(expected, WebAuthnService.IsRpIdValidForOrigin(rpId, origin));
    }

    [Fact]
    public async Task BeginRegistration_RpIdMismatchOrigin_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.BeginRegistrationAsync(Subject, null, "https://vanan.timlathay.com", "khachvip.online"));
    }
}