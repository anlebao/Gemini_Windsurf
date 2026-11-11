using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.CoreHub.Services.Otp;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;

namespace VanAn.Core.Tests.Services.Onboarding;

/// <summary>
/// W2 / P4 (2026-11-10, user duyệt): MerchantSignupOtpService — email-first (Brevo 0đ),
/// CSPRNG + single-use + attempt lock + purpose binding + session token cho submit.
/// </summary>
public class MerchantSignupOtpServiceTests
{
    private static (MerchantSignupOtpService Svc, Mock<IEmailService> Email, MemoryCache Cache) Create(
        int maxAttempts = 3, int lockoutHours = 24, int dailyLimit = 5, bool requireOtp = true, int cooldownSeconds = 0)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MerchantSignup:RequireOtp"] = requireOtp.ToString(),
                ["MerchantSignup:OtpChannel"] = "email",
                ["MerchantSignup:OtpFallbackChannels"] = "email",
                ["MerchantSignup:OtpMaxAttempts"] = maxAttempts.ToString(),
                ["MerchantSignup:OtpLockoutHours"] = lockoutHours.ToString(),
                ["MerchantSignup:OtpDailyLimitPerContact"] = dailyLimit.ToString(),
                ["MerchantSignup:OtpResendCooldownSeconds"] = cooldownSeconds.ToString(),
            })
            .Build();

        var email = new Mock<IEmailService>();
        email.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var brevo = new BrevoEmailOtpChannel(email.Object, NullLogger<BrevoEmailOtpChannel>.Instance);
        var fallback = new FallbackOtpChannel(brevo, config, NullLogger<FallbackOtpChannel>.Instance);
        var cache = new MemoryCache(new MemoryCacheOptions());

        var svc = new MerchantSignupOtpService(
            cache,
            config,
            fallback,
            NullLogger<MerchantSignupOtpService>.Instance);
        return (svc, email, cache);
    }

    private static string? PeekCode(MemoryCache cache, string contact)
        => cache.TryGetValue($"{MerchantSignupOtpService.CodeCacheKeyPrefix}:{contact}", out string? code) ? code : null;

    [Fact]
    public void RequiresOtp_ReflectsConfig()
    {
        var (on, _, _) = Create(requireOtp: true);
        on.RequiresOtp.Should().BeTrue();
        var (off, _, _) = Create(requireOtp: false);
        off.RequiresOtp.Should().BeFalse();
    }

    [Fact]
    public async Task SendOtp_EmailChannel_ReturnsSent_AndStoresCode()
    {
        var (svc, email, cache) = Create();
        var result = await svc.SendOtpAsync("merchant@test.vn");
        result.Sent.Should().BeTrue();
        email.Verify(e => e.SendEmailAsync("merchant@test.vn", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        var code = PeekCode(cache, "merchant@test.vn");
        code.Should().HaveLength(6);
        code!.All(char.IsDigit).Should().BeTrue();
    }

    [Fact]
    public async Task SendOtp_ResendCooldown_ReturnsRetryAfter()
    {
        var (svc, _, _) = Create(cooldownSeconds: 60);
        await svc.SendOtpAsync("merchant@test.vn");
        var second = await svc.SendOtpAsync("merchant@test.vn");
        second.Sent.Should().BeTrue();
        second.RetryAfterSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SendOtp_ExceedsDailyLimit_Fails()
    {
        var (svc, _, _) = Create(dailyLimit: 2);
        await svc.SendOtpAsync("merchant@test.vn");
        await svc.SendOtpAsync("merchant@test.vn");
        var third = await svc.SendOtpAsync("merchant@test.vn");
        third.Sent.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyOtp_CorrectCode_ReturnsSessionToken_SingleUse()
    {
        var (svc, _, cache) = Create();
        await svc.SendOtpAsync("merchant@test.vn");
        var code = PeekCode(cache, "merchant@test.vn");

        var ok = await svc.VerifyOtpAsync("merchant@test.vn", code!);
        ok.Verified.Should().BeTrue();
        ok.SessionToken.Should().NotBeNullOrEmpty();

        var replay = await svc.VerifyOtpAsync("merchant@test.vn", code!);
        replay.Verified.Should().BeFalse(); // single-use
    }

    [Fact]
    public async Task VerifyOtp_WrongCodeMaxAttempts_Locks()
    {
        var (svc, _, cache) = Create(maxAttempts: 3);
        await svc.SendOtpAsync("merchant@test.vn");
        var code = PeekCode(cache, "merchant@test.vn");

        (await svc.VerifyOtpAsync("merchant@test.vn", "000000")).Verified.Should().BeFalse();
        (await svc.VerifyOtpAsync("merchant@test.vn", "111111")).Verified.Should().BeFalse();
        var third = await svc.VerifyOtpAsync("merchant@test.vn", "222222");
        third.Verified.Should().BeFalse();
        third.LockedMinutes.Should().BeGreaterThan(0);

        // locked — kể cả code đúng
        (await svc.VerifyOtpAsync("merchant@test.vn", code!)).Verified.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeSession_ValidOnce_ThenGone()
    {
        var (svc, _, cache) = Create();
        await svc.SendOtpAsync("merchant@test.vn");
        var code = PeekCode(cache, "merchant@test.vn");
        var ok = await svc.VerifyOtpAsync("merchant@test.vn", code!);

        var session = await svc.ConsumeSessionAsync(ok.SessionToken!);
        session.Should().NotBeNull();
        session!.Contact.Should().Be("merchant@test.vn");
        session.Purpose.Should().Be("merchant-signup");

        (await svc.ConsumeSessionAsync(ok.SessionToken!)).Should().BeNull(); // consumed
    }

    [Fact]
    public async Task ConsumeSession_InvalidOrWrongPurpose_ReturnsNull()
    {
        var (svc, _, _) = Create();
        (await svc.ConsumeSessionAsync("not-a-token")).Should().BeNull();
    }

    [Fact]
    public async Task VerifyOtp_ContactMismatch_Fails()
    {
        var (svc, _, cache) = Create();
        await svc.SendOtpAsync("a@test.vn");
        var code = PeekCode(cache, "a@test.vn");
        var ok = await svc.VerifyOtpAsync("b@test.vn", code!);
        ok.Verified.Should().BeFalse(); // code gắn contact a — verify với b fail
    }

    [Fact]
    public async Task Channel_Fallback_PrimaryFails_ReturnsFalse()
    {
        var emailFail = new Mock<IEmailService>();
        emailFail.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MerchantSignup:OtpChannel"] = "email",
                ["MerchantSignup:OtpFallbackChannels"] = "email",
            })
            .Build();
        var brevo = new BrevoEmailOtpChannel(emailFail.Object, NullLogger<BrevoEmailOtpChannel>.Instance);
        var fallback = new FallbackOtpChannel(brevo, config, NullLogger<FallbackOtpChannel>.Instance);

        var sent = await fallback.SendOtpAsync("a@test.vn", "123456");
        sent.Should().BeFalse(); // email fail → không còn kênh khác
    }

    // ── Submit integration: gate OTP + payload provenance ──────────────────

    private static MerchantSignupRequest CaseBRequest(string contactEmail = "merchant@test.vn") => new(
        Case: SignupCase.SmallShopNoLicense,
        Name: "Quán OTP Test",
        Address: "12 Nguyễn Huệ, Q1",
        BusinessType: BusinessType.HouseholdBusiness,
        HkdGroup: HKDGroup.Group2,
        GpdkInfo: null,
        SignName: "Quán OTP Test",
        Latitude: null,
        Longitude: null,
        PhotoUrls: null,
        ProductLines: [new OcrProductLineDto("Trà đào", 30000, 1)],
        ContactName: "Chủ Quán",
        ContactPhone: "0912-345-678",
        ContactEmail: contactEmail,
        OwnerUsername: "ownerotptest",
        OwnerPassword: "matkhau123",
        TurnstileToken: null,
        HoneypotWebsite: null);

    [Fact]
    public async Task Submit_RequireOtp_WithoutSessionToken_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var (otpSvc, _, _) = Create(requireOtp: true);
        var onboardingMock = new Mock<ITenantOnboardingService>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MerchantSignup:RequireOtp"] = "true" })
            .Build();

        var svc = new MerchantSignupService(
            onboardingMock.Object,
            new PlatformOnboardingFlagsService(scope.Context, NullLogger<PlatformOnboardingFlagsService>.Instance),
            scope.Context,
            DataProtectionProvider.Create("W2-Tests"),
            config,
            NullLogger<MerchantSignupService>.Instance,
            metering: null,
            otpService: otpSvc);

        var act = () => svc.SubmitAsync(CaseBRequest(), "1.2.3.4", "test");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Xác minh liên hệ bắt buộc*");
    }

    [Fact]
    public async Task Submit_RequireOtp_ValidSession_StoresOtpProvenance()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var (otpSvc, _, cache) = Create(requireOtp: true);
        const string contactEmail = "merchant@test.vn";

        CrawlListingDto? captured = null;
        var onboardingMock = new Mock<ITenantOnboardingService>();
        onboardingMock.Setup(o => o.OnboardUnverifiedAsync(
                It.IsAny<CrawlListingDto>(), It.IsAny<CancellationToken>(), It.IsAny<BusinessType>(), It.IsAny<HKDGroup?>()))
            .ReturnsAsync((CrawlListingDto l, CancellationToken _, BusinessType _, HKDGroup? _) =>
            {
                captured = l;
                return Guid.NewGuid();
            });
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MerchantSignup:RequireOtp"] = "true" })
            .Build();

        var svc = new MerchantSignupService(
            onboardingMock.Object,
            new PlatformOnboardingFlagsService(scope.Context, NullLogger<PlatformOnboardingFlagsService>.Instance),
            scope.Context,
            DataProtectionProvider.Create("W2-Tests"),
            config,
            NullLogger<MerchantSignupService>.Instance,
            metering: null,
            otpService: otpSvc);

        // verify OTP trước
        await otpSvc.SendOtpAsync(contactEmail);
        var code = PeekCode(cache, contactEmail);
        var ok = await otpSvc.VerifyOtpAsync(contactEmail, code!);

        var result = await svc.SubmitAsync(CaseBRequest(contactEmail) with { OtpSessionToken = ok.SessionToken }, "1.2.3.4", "test");

        result.SignupId.Should().NotBe(Guid.Empty);
        captured.Should().NotBeNull();
        captured!.SelfServe!.OtpVerified.Should().BeTrue();
        captured.SelfServe.OtpChannel.Should().Be("email");
        captured.SelfServe.OtpVerifiedContact.Should().Be(contactEmail);
        captured.SelfServe.OtpVerifiedAt.Should().NotBeNull();
    }
}