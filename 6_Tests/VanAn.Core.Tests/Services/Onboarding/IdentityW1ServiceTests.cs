using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OtpNet;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Aggregates.UserAggregate;
using Xunit;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using UserTenant = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTenant;
using UserTrustLevel = VanAn.Shared.Domain.Aggregates.UserAggregate.UserTrustLevel;

namespace VanAn.Core.Tests.Services.Onboarding;

/// <summary>
/// W1 (2026-11-10, user duyệt): ID-P1 trust fields + downgrade hook + MFA-1 service.
/// VerifyAsync populate · RejectPendingAsync downgrade · MfaService TOTP/trusted/email-fallback.
/// </summary>
public class IdentityW1ServiceTests
{
    private readonly Mock<IUserManagementService> _userServiceMock = new();
    private readonly Mock<IPermissionGroupService> _permissionGroupServiceMock = new();
    private readonly Mock<IRoleAssignmentService> _roleAssignmentServiceMock = new();
    private readonly IDataProtectionProvider _dataProtection = DataProtectionProvider.Create("VanAn.Core.Tests.IdentityW1");

    private TenantOnboardingService CreateOnboardingSut(IVanAnDbContext dbContext)
        => new(
            Mock.Of<ITenantManagementService>(),
            _userServiceMock.Object,
            _permissionGroupServiceMock.Object,
            _roleAssignmentServiceMock.Object,
            dbContext,
            null,
            NullLogger<TenantOnboardingService>.Instance);

    private void SetupUserMocks(DemoUser owner)
    {
        _userServiceMock.Setup(s => s.CreateUserAsync(
                It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(owner);
        _roleAssignmentServiceMock.Setup(s => s.AssignRoleToUserAsync(
                It.IsAny<Guid>(), It.IsAny<TenantId>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _roleAssignmentServiceMock.Setup(s => s.AssignUserToGroupAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _permissionGroupServiceMock.Setup(s => s.CreateGroupAsync(
                It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantId tid, string name, string? desc, CancellationToken _) =>
                new PermissionGroup(tid, name, desc));
    }

    private static CrawlListingDto Listing(string? taxCode = "0106463914") => new(
        Name: "Cafe Identity W1",
        TaxCode: taxCode,
        Address: "123 Lê Lợi, Q1, HCM",
        CrawledPhone: null,
        ContactName: "Nguyễn Văn A",
        IndustryCode: "F&B",
        SourceSite: "OCR-SELF",
        SourceUrl: "ocr://test",
        CrawledAt: DateTime.UtcNow);

    // ── ID-P1: VerifyAsync populate ────────────────────────────────────────

    [Fact]
    public async Task VerifyAsync_ContactVerifiedTrue_SetsPhoneVerifiedChannelAndBusiness()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var owner = DemoUser.Create(new TenantId(Guid.NewGuid()), "owner@w1.vn", "hash", "Owner", UserRole.Owner);
        scope.Context.Users.Add(owner);
        await scope.Context.SaveChangesAsync();
        SetupUserMocks(owner);

        var sut = CreateOnboardingSut(scope.Context);
        var tenantId = await sut.OnboardUnverifiedAsync(Listing());

        await sut.VerifyAsync(tenantId, new VerifyTenantRequest(
            OwnerUsername: "owner@w1.vn",
            OwnerPassword: "hash",
            OwnerDisplayName: "Owner",
            OwnerPhone: "+84 901 234 567",
            ContactVerified: true,
            VerifiedChannel: "email"));

        var saved = await scope.Context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == owner.Id);
        saved.Should().NotBeNull();
        saved!.PhoneNumber.Should().Be("0901234567");           // normalize + decrypt qua converter
        saved.PhoneVerifiedAt.Should().NotBeNull();
        saved.VerifiedChannel.Should().Be("email");
        saved.TrustLevel.Should().Be(UserTrustLevel.BusinessVerified);
    }

    [Fact]
    public async Task VerifyAsync_ContactVerifiedFalse_BusinessOnly_NoPhoneVerified()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var owner = DemoUser.Create(new TenantId(Guid.NewGuid()), "owner@w1.vn", "hash", "Owner", UserRole.Owner);
        scope.Context.Users.Add(owner);
        await scope.Context.SaveChangesAsync();
        SetupUserMocks(owner);

        var sut = CreateOnboardingSut(scope.Context);
        var tenantId = await sut.OnboardUnverifiedAsync(Listing());

        await sut.VerifyAsync(tenantId, new VerifyTenantRequest(
            OwnerUsername: "owner@w1.vn",
            OwnerPassword: "hash",
            OwnerDisplayName: "Owner",
            OwnerPhone: "0901234567"));

        var saved = await scope.Context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == owner.Id);
        saved!.PhoneVerifiedAt.Should().BeNull();
        saved.VerifiedChannel.Should().BeNull();
        saved.TrustLevel.Should().Be(UserTrustLevel.BusinessVerified);
    }

    // ── ID-P1: RejectPendingAsync downgrade hook (REVIEW B3) ───────────────

    [Fact]
    public async Task RejectPendingAsync_DowngradesOwnerBusinessToContactVerified()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateOnboardingSut(scope.Context);
        var tenantId = await sut.OnboardUnverifiedAsync(Listing());

        var owner = DemoUser.Create(new TenantId(tenantId), "owner@w1.vn", "hash", "Owner", UserRole.Owner);
        owner.MarkBusinessVerified();
        scope.Context.Users.Add(owner);
        scope.Context.UserTenants.Add(new UserTenant(owner.Id, tenantId, UserRole.Owner));
        await scope.Context.SaveChangesAsync();

        var merchantService = new MerchantSignupService(
            sut,
            new PlatformOnboardingFlagsService(scope.Context, NullLogger<PlatformOnboardingFlagsService>.Instance),
            scope.Context,
            _dataProtection,
            new ConfigurationBuilder().Build(),
            NullLogger<MerchantSignupService>.Instance);

        await merchantService.RejectPendingAsync(tenantId, "test reject", Guid.NewGuid());

        var saved = await scope.Context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == owner.Id);
        saved!.TrustLevel.Should().Be(UserTrustLevel.ContactVerified);
    }

    [Fact]
    public async Task RejectPendingAsync_NoOwnerUser_NoCrash()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateOnboardingSut(scope.Context);
        var tenantId = await sut.OnboardUnverifiedAsync(Listing());

        var merchantService = new MerchantSignupService(
            sut,
            new PlatformOnboardingFlagsService(scope.Context, NullLogger<PlatformOnboardingFlagsService>.Instance),
            scope.Context,
            _dataProtection,
            new ConfigurationBuilder().Build(),
            NullLogger<MerchantSignupService>.Instance);

        await merchantService.RejectPendingAsync(tenantId, "test reject", Guid.NewGuid());

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId));
        tenant!.Status.Should().Be(TenantStatus.Inactive);
    }

    // ── ID-P1: TenantManagementService.DeactivateAsync downgrade (RV planning phát hiện gap — wire thật) ──

    [Fact]
    public async Task TenantManagementDeactivate_DowngradesOwnerBusinessToContactVerified()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var tenantId = new TenantId(Guid.NewGuid());
        var tenant = Tenant.CreateCompany(tenantId, "RV Deactivate Test", new TenantSettings("a@b.vn", null, null));
        scope.Context.Tenants.Add(tenant);

        var owner = DemoUser.Create(tenantId, "owner@deact.vn", "hash", "Owner", UserRole.Owner);
        owner.MarkBusinessVerified();
        scope.Context.Users.Add(owner);
        scope.Context.UserTenants.Add(new UserTenant(owner.Id, tenantId.Value, UserRole.Owner));
        await scope.Context.SaveChangesAsync();

        var svc = new TenantManagementService(
            scope.Context,
            Mock.Of<VanAn.CoreHub.Infrastructure.IAccountingDbContext>(),
            Mock.Of<VanAn.CoreHub.Services.INotificationService>(),
            Mock.Of<VanAn.CoreHub.Services.IShopInstanceService>(),
            null,
            NullLogger<TenantManagementService>.Instance);

        await svc.DeactivateAsync(tenantId, "rv test deactivate");

        var savedOwner = await scope.Context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == owner.Id);
        savedOwner!.TrustLevel.Should().Be(UserTrustLevel.ContactVerified);
        var savedTenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId);
        savedTenant!.Status.Should().Be(TenantStatus.Inactive);
    }
}

/// <summary>MFA-1 (2026-11-10): MfaService — TOTP + trusted device + email fallback.</summary>
public class MfaServiceTests
{
    private static (MfaService Svc, VanAnDbContext Ctx, SqliteConnection Conn) Create(Mock<IEmailService>? email = null)
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var ctx = new VanAnDbContext(new DbContextOptionsBuilder<VanAnDbContext>().UseSqlite(conn).Options);
        ctx.Database.EnsureCreated();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mfa:MaxAttempts"] = "3",
                ["Mfa:LockoutMinutes"] = "15",
            })
            .Build();

        var svc = new MfaService(
            ctx,
            new MemoryCache(new MemoryCacheOptions()),
            config,
            (email ?? new Mock<IEmailService>()).Object,
            NullLogger<MfaService>.Instance);
        return (svc, ctx, conn);
    }

    private static async Task<DemoUser> SeedUser(VanAnDbContext ctx, bool withEmail = true, Guid? id = null)
    {
        var user = DemoUser.Create(new TenantId(Guid.NewGuid()), "admin@w1.vn", "hash", "Admin", UserRole.Owner);
        if (id.HasValue)
            user.GetType().GetProperty("Id")?.SetValue(user, id.Value);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user;
    }

    private static string ComputeTotp(string secret)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.ComputeTotp();
    }

    [Fact]
    public void GenerateSetup_ReturnsBase32SecretAndOtpauthUri()
    {
        var (svc, _, _) = Create();
        var setup = svc.GenerateSetup("user-id", "admin@w1.vn");
        setup.Secret.Should().NotBeNullOrEmpty();
        setup.Secret.Length.Should().Be(32); // 20 bytes base32
        setup.OtpauthUri.Should().StartWith("otpauth://totp/VanAn:");
        setup.OtpauthUri.Should().Contain("secret=");
    }

    [Fact]
    public async Task Enable_WithCorrectCode_EnablesMfa()
    {
        var (svc, ctx, _) = Create();
        var user = await SeedUser(ctx);
        var setup = svc.GenerateSetup(user.Id.ToString(), user.Username);

        var ok = await svc.EnableAsync(user.Id.ToString(), setup.Secret, ComputeTotp(setup.Secret));

        ok.Should().BeTrue();
        var reloaded = await ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        reloaded.MfaEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Enable_WithWrongCode_DoesNotEnable()
    {
        var (svc, ctx, _) = Create();
        var user = await SeedUser(ctx);
        var setup = svc.GenerateSetup(user.Id.ToString(), user.Username);

        var ok = await svc.EnableAsync(user.Id.ToString(), setup.Secret, "000000");

        ok.Should().BeFalse();
        var reloaded = await ctx.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        reloaded.MfaEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyLogin_NotEnabled_ReturnsNotEnabled()
    {
        var (svc, ctx, _) = Create();
        var user = await SeedUser(ctx);
        (await svc.VerifyLoginAsync(user.Id.ToString(), null, null)).Should().Be(MfaVerifyResult.NotEnabled);
    }

    [Fact]
    public async Task VerifyLogin_CorrectCode_Success_WrongCode_Invalid_ThenLock()
    {
        var (svc, ctx, _) = Create();
        var user = await SeedUser(ctx);
        var setup = svc.GenerateSetup(user.Id.ToString(), user.Username);
        await svc.EnableAsync(user.Id.ToString(), setup.Secret, ComputeTotp(setup.Secret));

        (await svc.VerifyLoginAsync(user.Id.ToString(), ComputeTotp(setup.Secret), null))
            .Should().Be(MfaVerifyResult.Success);

        (await svc.VerifyLoginAsync(user.Id.ToString(), "000000", null)).Should().Be(MfaVerifyResult.Invalid);
        (await svc.VerifyLoginAsync(user.Id.ToString(), "000000", null)).Should().Be(MfaVerifyResult.Invalid);
        (await svc.VerifyLoginAsync(user.Id.ToString(), "000000", null)).Should().Be(MfaVerifyResult.Locked);
        // locked — kể cả code đúng
        (await svc.VerifyLoginAsync(user.Id.ToString(), ComputeTotp(setup.Secret), null))
            .Should().Be(MfaVerifyResult.Locked);
    }

    [Fact]
    public async Task TrustedToken_IssueValidateRevoke()
    {
        var (svc, ctx, _) = Create();
        var user = await SeedUser(ctx);
        var setup = svc.GenerateSetup(user.Id.ToString(), user.Username);
        await svc.EnableAsync(user.Id.ToString(), setup.Secret, ComputeTotp(setup.Secret));

        var token = await svc.IssueTrustedTokenAsync(user.Id.ToString());
        (await svc.IsTrustedAsync(user.Id.ToString(), token)).Should().BeTrue();
        (await svc.IsTrustedAsync(user.Id.ToString(), "wrong-token")).Should().BeFalse();
        (await svc.VerifyLoginAsync(user.Id.ToString(), null, token)).Should().Be(MfaVerifyResult.Success); // skip MFA

        await svc.RevokeTrustAsync(user.Id.ToString());
        (await svc.IsTrustedAsync(user.Id.ToString(), token)).Should().BeFalse();
    }

    [Fact]
    public async Task EmailFallback_SendAndVerify_SingleUse()
    {
        var emailMock = new Mock<IEmailService>();
        string? sentHtml = null;
        emailMock.Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((to, subj, html, _) => sentHtml = html)
            .ReturnsAsync(true);

        var (svc, ctx, _) = Create(emailMock);
        var user = await SeedUser(ctx, withEmail: true);
        user.GetType().GetProperty("Email")?.SetValue(user, "admin@w1.vn");
        await ctx.SaveChangesAsync();

        var sent = await svc.SendEmailFallbackOtpAsync(user.Id.ToString());
        sent.Should().BeTrue();
        sentHtml.Should().NotBeNull();

        var code = System.Text.RegularExpressions.Regex.Match(sentHtml!, @"\d{6}").Value;
        code.Should().HaveLength(6);

        (await svc.VerifyEmailFallbackOtpAsync(user.Id.ToString(), code)).Should().BeTrue();
        (await svc.VerifyEmailFallbackOtpAsync(user.Id.ToString(), code)).Should().BeFalse(); // single-use
    }

    [Fact]
    public async Task EmailFallback_NoEmail_ReturnsFalse()
    {
        var (svc, ctx, _) = Create();
        var user = await SeedUser(ctx, withEmail: false);
        (await svc.SendEmailFallbackOtpAsync(user.Id.ToString())).Should().BeFalse();
    }
}