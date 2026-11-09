using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.Core.Tests.Services.Onboarding;

/// <summary>
/// Self-Serve Merchant Signup (2026-11-09 — SRS FR-1..6, FR-22):
/// submit → Pending tenant + SelfServePayload (password protected),
/// verify-signup dùng credentials merchant, reject, status lookup, flags gate.
/// Dùng real SQLite in-memory + real IDataProtection (cùng provider instance).
/// </summary>
public class MerchantSignupServiceTests
{
    private readonly Mock<IUserManagementService> _userServiceMock = new();
    private readonly Mock<IPermissionGroupService> _permissionGroupServiceMock = new();
    private readonly Mock<IRoleAssignmentService> _roleAssignmentServiceMock = new();
    private readonly IDataProtectionProvider _dataProtection = DataProtectionProvider.Create("VanAn.Core.Tests");

    private static readonly Guid AdminId = Guid.NewGuid();

    private MerchantSignupService CreateSut(
        IVanAnDbContext dbContext,
        out TenantOnboardingService onboardingService,
        out PlatformOnboardingFlagsService flagsService,
        IConfiguration? config = null)
    {
        onboardingService = new TenantOnboardingService(
            Mock.Of<ITenantManagementService>(),
            _userServiceMock.Object,
            _permissionGroupServiceMock.Object,
            _roleAssignmentServiceMock.Object,
            dbContext,
            null,
            NullLogger<TenantOnboardingService>.Instance);

        flagsService = new PlatformOnboardingFlagsService(dbContext, NullLogger<PlatformOnboardingFlagsService>.Instance);

        return new MerchantSignupService(
            onboardingService,
            flagsService,
            dbContext,
            _dataProtection,
            config ?? new ConfigurationBuilder().Build(),
            NullLogger<MerchantSignupService>.Instance);
    }

    private void SetupUserMocks()
    {
        var fakeUser = DemoUser.Create(new TenantId(Guid.NewGuid()), "owner@test.vn", "hashed", "Owner", UserRole.Owner);
        _userServiceMock.Setup(s => s.CreateUserAsync(
                It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeUser);
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

    private static MerchantSignupRequest CaseARequest(string? mst = "0106463914") => new(
        Case: SignupCase.CompanyWithLicense,
        Name: "Công ty TNHH ABC",
        Address: "123 Lê Lợi, Q1, HCM",
        BusinessType: BusinessType.Company,
        HkdGroup: null,
        GpdkInfo: new OcrGpdkInfo(mst, "Công ty TNHH ABC", "123 Lê Lợi, Q1, HCM", "TNHH", "Nguyễn Văn A"),
        SignName: null,
        Latitude: 10.7765,
        Longitude: 106.6951,
        PhotoUrls: ["https://r2.example.com/onboarding/gpkd.jpg"],
        ProductLines: [new OcrProductLineDto("Cà phê sữa đá", 25000, 1)],
        ContactName: "Nguyễn Văn A",
        ContactPhone: "0901 112 233",
        ContactEmail: "a@abc.vn",
        OwnerUsername: "chuquanabc",
        OwnerPassword: "matkhau123",
        TurnstileToken: null,
        HoneypotWebsite: null,
        Src: "audit");

    private static MerchantSignupRequest CaseBRequest(string name = "Quán Cà Phê Nhỏ") => new(
        Case: SignupCase.SmallShopNoLicense,
        Name: name,
        Address: "12 Nguyễn Huệ, Q1, HCM",
        BusinessType: BusinessType.HouseholdBusiness,
        HkdGroup: HKDGroup.Group2,
        GpdkInfo: null,
        SignName: name,
        Latitude: null,
        Longitude: null,
        PhotoUrls: ["https://r2.example.com/onboarding/sign.jpg"],
        ProductLines: [new OcrProductLineDto("Trà đào", 30000, 1)],
        ContactName: "Chủ Quán",
        ContactPhone: "0912-345-678",
        ContactEmail: null,
        OwnerUsername: "ownerquanNho",
        OwnerPassword: "matkhau456",
        TurnstileToken: null,
        HoneypotWebsite: null,
        Src: "qr");

    private static MerchantSignupRequest RongRequest() => new(
        Case: SignupCase.StreetVendorUnregistered,
        Name: "Bán hàng rong sáng",
        Address: null,
        BusinessType: BusinessType.Company,   // client gửi sai — service phải force HKD
        HkdGroup: HKDGroup.Group3,            // client gửi sai — force Group1
        GpdkInfo: null,
        SignName: "Xe bán mì",
        Latitude: 10.8,
        Longitude: 106.7,
        PhotoUrls: null,
        ProductLines: null,
        ContactName: "Anh Bán Rong",
        ContactPhone: "0987654321",
        ContactEmail: null,
        OwnerUsername: "banrong01",
        OwnerPassword: "matkhau789",
        TurnstileToken: null,
        HoneypotWebsite: null,
        Src: "directory");

    // ── Submit ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "CaseA_Submit_CreatesPendingTenant_PayloadProtected")]
    public async Task CaseA_Submit_CreatesPendingTenant_PayloadProtected()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        var result = await sut.SubmitAsync(CaseARequest(), "1.2.3.4", "test-agent");

        result.SignupId.Should().NotBe(Guid.Empty);
        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(result.SignupId));
        tenant.Should().NotBeNull();
        tenant!.Status.Should().Be(TenantStatus.Pending);
        tenant.BusinessType.Should().Be(BusinessType.Company);
        tenant.Settings.TaxCode.Should().Be("0106463914");
        tenant.Settings.Latitude.Should().Be(10.7765);

        var source = await scope.Context.CrawlSources.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == new TenantId(result.SignupId));
        source.Should().NotBeNull();
        source!.SourceSite.Should().Be("OCR-SELF-GPKD");
        // Payload đầy đủ: ảnh + menu + username + protected password + src
        source.RawJson.Should().Contain("r2.example.com/onboarding/gpkd.jpg");
        source.RawJson.Should().Contain("chuquanabc");
        source.RawJson.Should().Contain("OwnerPasswordProtected");
        source.RawJson.Should().Contain("\"Src\":\"audit\"");
        // KHÔNG chứa plaintext password
        source.RawJson.Should().NotContain("matkhau123");
        source.RawJson.Should().NotContain("0901 112 233"); // phone normalized → "0901112233"
        source.RawJson.Should().Contain("0901112233");
    }

    [Fact(DisplayName = "CaseA_DuplicateMst_StillGeneric_OkButMarkedDuplicate")]
    public async Task CaseA_DuplicateMst_StillGeneric_OkButMarkedDuplicate()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out var onboarding, out _);

        var existingId = await onboarding.OnboardUnverifiedAsync(new CrawlListingDto(
            Name: "Công ty TNHH ABC", TaxCode: "0106463914", Address: "123 Lê Lợi",
            CrawledPhone: null, ContactName: null, IndustryCode: null,
            SourceSite: "doanhnghiep.vn", SourceUrl: "https://example.com/1", CrawledAt: DateTime.UtcNow));

        var result = await sut.SubmitAsync(CaseARequest(), null, null);

        // Generic 200-style result — không tiết lộ duplicate
        result.SignupId.Should().NotBe(Guid.Empty);
        result.SignupId.Should().NotBe(existingId);

        var newTenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(result.SignupId));
        newTenant!.PotentialDuplicateOf.Should().Be(existingId);
    }

    [Fact(DisplayName = "CaseA_InvalidMst_Throws")]
    public async Task CaseA_InvalidMst_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        var act = () => sut.SubmitAsync(CaseARequest(mst: "ABC"), null, null);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*10 hoặc 13*");
    }

    [Fact(DisplayName = "CaseB_RequiresHkdGroup_Throws")]
    public async Task CaseB_RequiresHkdGroup_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        var request = CaseBRequest() with { HkdGroup = null };
        var act = () => sut.SubmitAsync(request, null, null);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*nhóm thuế*");
    }

    [Fact(DisplayName = "CaseB_Submit_CreatesHkdPending_DuplicateWarningOnNameMatch")]
    public async Task CaseB_Submit_CreatesHkdPending_DuplicateWarningOnNameMatch()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        // Seed tenant trùng tên trước → DuplicateWarning
        await sut.SubmitAsync(CaseBRequest(), null, null);
        var second = await sut.SubmitAsync(CaseBRequest(), null, null);

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(second.SignupId));
        tenant!.BusinessType.Should().Be(BusinessType.HouseholdBusiness);
        tenant.HKDGroup.Should().Be(HKDGroup.Group2);
        tenant.Type.Should().Be(TenantType.HKD);
        tenant.Status.Should().Be(TenantStatus.Pending);

        var source = await scope.Context.CrawlSources.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == new TenantId(second.SignupId));
        source!.RawJson.Should().Contain("\"DuplicateWarning\":true");
    }

    [Fact(DisplayName = "StreetVendor_ForcesHkdGroup1_Unregistered_IgnoresClientValues")]
    public async Task StreetVendor_ForcesHkdGroup1_Unregistered_IgnoresClientValues()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        var result = await sut.SubmitAsync(RongRequest(), null, null);

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(result.SignupId));
        tenant!.BusinessType.Should().Be(BusinessType.HouseholdBusiness);
        tenant.HKDGroup.Should().Be(HKDGroup.Group1);
        tenant.Type.Should().Be(TenantType.HKD);

        var source = await scope.Context.CrawlSources.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == new TenantId(result.SignupId));
        source!.SourceSite.Should().Be("OCR-SELF-RONG");
        source.RawJson.Should().Contain("\"Unregistered\":true");
        source.RawJson.Should().Contain("\"Src\":\"directory\"");
    }

    [Fact(DisplayName = "SelfServeClosed_Throws_ServiceUnavailable")]
    public async Task SelfServeClosed_Throws_ServiceUnavailable()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out var flags);

        await flags.SetFlagsAsync(
            new UpdateOnboardingFlagsRequest(false, true, true, true), AdminId);

        var act = () => sut.SubmitAsync(CaseBRequest(), null, null);
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("self-serve-closed");
    }

    [Fact(DisplayName = "InvalidUsername_Throws")]
    public async Task InvalidUsername_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        var request = CaseBRequest() with { OwnerUsername = "Ab@C!" };
        var act = () => sut.SubmitAsync(request, null, null);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Tên đăng nhập*");
    }

    [Fact(DisplayName = "InvalidSrc_DefaultsToDirect")]
    public async Task InvalidSrc_DefaultsToDirect()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        var result = await sut.SubmitAsync(CaseBRequest() with { Src = "<script>" }, null, null);

        var source = await scope.Context.CrawlSources.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == new TenantId(result.SignupId));
        source!.RawJson.Should().Contain("\"Src\":\"direct\"");
    }

    // ── Verify-signup ─────────────────────────────────────────────────────

    [Fact(DisplayName = "VerifySignup_UsesMerchantPassword_TenantActive")]
    public async Task VerifySignup_UsesMerchantPassword_TenantActive()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);
        SetupUserMocks();

        var submit = await sut.SubmitAsync(CaseBRequest(), null, null);
        var result = await sut.VerifySignupAsync(submit.SignupId, new VerifySignupRequest(), AdminId);

        result.UsedMerchantPassword.Should().BeTrue();
        result.Slug.Should().NotBeNullOrEmpty();
        result.OwnerUsername.Should().Be("ownerquanNho".ToLowerInvariant());

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(result.TenantId));
        tenant!.Status.Should().Be(TenantStatus.Active);
        // ContactPhone từ payload (consented) — M3 pattern
        tenant.Settings.ContactPhone.Should().Be("0912345678");

        // Password merchant protected trong payload — unprotect ra đúng plaintext gốc
        var source = await scope.Context.CrawlSources.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == new TenantId(result.TenantId));
        var listing = System.Text.Json.JsonSerializer.Deserialize<CrawlListingDto>(source!.RawJson)!;
        var plain = _dataProtection.CreateProtector("VanAn.SelfServeSignup.v1")
            .Unprotect(listing.SelfServe!.OwnerPasswordProtected);
        plain.Should().Be("matkhau456");
    }

    [Fact(DisplayName = "VerifySignup_PasswordOverride_UsedMerchantPasswordFalse")]
    public async Task VerifySignup_PasswordOverride_UsedMerchantPasswordFalse()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);
        SetupUserMocks();

        var submit = await sut.SubmitAsync(CaseBRequest(), null, null);
        var result = await sut.VerifySignupAsync(submit.SignupId,
            new VerifySignupRequest(OwnerPassword: "password-moi-123"), AdminId);

        result.UsedMerchantPassword.Should().BeFalse();
    }

    [Fact(DisplayName = "VerifySignup_NonSelfServeTenant_Throws")]
    public async Task VerifySignup_NonSelfServeTenant_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out var onboarding, out _);

        // Crawl tenant thường — không có SelfServe payload
        var crawlTenantId = await onboarding.OnboardUnverifiedAsync(new CrawlListingDto(
            Name: "Công ty Crawl", TaxCode: "0100000001", Address: null,
            CrawledPhone: null, ContactName: null, IndustryCode: null,
            SourceSite: "doanhnghiep.vn", SourceUrl: "https://example.com/2", CrawledAt: DateTime.UtcNow));

        var act = () => sut.VerifySignupAsync(crawlTenantId, new VerifySignupRequest(), AdminId);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*self-serve*");
    }

    [Fact(DisplayName = "VerifySignup_AlreadyActive_Throws409")]
    public async Task VerifySignup_AlreadyActive_Throws409()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);
        SetupUserMocks();

        var submit = await sut.SubmitAsync(CaseBRequest(), null, null);
        await sut.VerifySignupAsync(submit.SignupId, new VerifySignupRequest(), AdminId);

        var act = () => sut.VerifySignupAsync(submit.SignupId, new VerifySignupRequest(), AdminId);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Pending*");
    }

    [Fact(DisplayName = "VerifySignup_DuplicateBlocked_Throws409")]
    public async Task VerifySignup_DuplicateBlocked_Throws409()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out var onboarding, out _);

        // Seed tenant cùng MST trước
        await onboarding.OnboardUnverifiedAsync(new CrawlListingDto(
            Name: "Công ty TNHH ABC", TaxCode: "0106463914", Address: null,
            CrawledPhone: null, ContactName: null, IndustryCode: null,
            SourceSite: "doanhnghiep.vn", SourceUrl: "https://example.com/3", CrawledAt: DateTime.UtcNow));

        var submit = await sut.SubmitAsync(CaseARequest(), null, null);
        var act = () => sut.VerifySignupAsync(submit.SignupId, new VerifySignupRequest(), AdminId);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*duplicate*");
    }

    // ── Reject ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "RejectPending_DeactivatesTenant")]
    public async Task RejectPending_DeactivatesTenant()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        var submit = await sut.SubmitAsync(CaseBRequest(), null, null);
        await sut.RejectPendingAsync(submit.SignupId, "Thông tin không hợp lệ", AdminId);

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(submit.SignupId));
        tenant!.Status.Should().Be(TenantStatus.Inactive);
    }

    [Fact(DisplayName = "RejectPending_NonPending_Throws")]
    public async Task RejectPending_NonPending_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out var onboarding, out _);

        var tenantId = await onboarding.OnboardUnverifiedAsync(new CrawlListingDto(
            Name: "Công ty X", TaxCode: "0109999999", Address: null,
            CrawledPhone: null, ContactName: null, IndustryCode: null,
            SourceSite: "doanhnghiep.vn", SourceUrl: "https://example.com/4", CrawledAt: DateTime.UtcNow));
        // tenant này Pending — verify trước để thành Active
        SetupUserMocks();
        await onboarding.VerifyAsync(tenantId, new VerifyTenantRequest("ownerx", "pass12345", "Owner X", ApprovedByUserId: AdminId));

        var act = () => sut.RejectPendingAsync(tenantId, "lý do", AdminId);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Pending*");
    }

    // ── Status lookup ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Status_PhoneMatch_ReturnsPending_ThenActive")]
    public async Task Status_PhoneMatch_ReturnsPending_ThenActive()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);
        SetupUserMocks();

        var submit = await sut.SubmitAsync(CaseBRequest(), null, null);

        var pending = await sut.GetStatusAsync(submit.SignupId, "0912 345 678");
        pending.Status.Should().Be("Pending");

        await sut.VerifySignupAsync(submit.SignupId, new VerifySignupRequest(), AdminId);
        var active = await sut.GetStatusAsync(submit.SignupId, "0912345678");
        active.Status.Should().Be("Active");
        active.Slug.Should().NotBeNullOrEmpty();
    }

    [Fact(DisplayName = "Status_WrongPhone_ReturnsNotFound")]
    public async Task Status_WrongPhone_ReturnsNotFound()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        var submit = await sut.SubmitAsync(CaseBRequest(), null, null);
        var result = await sut.GetStatusAsync(submit.SignupId, "0999999999");
        result.Status.Should().Be("NotFound");
    }

    [Fact(DisplayName = "Status_UnknownId_ReturnsNotFound")]
    public async Task Status_UnknownId_ReturnsNotFound()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        var result = await sut.GetStatusAsync(Guid.NewGuid(), "0912345678");
        result.Status.Should().Be("NotFound");
    }

    // ── Detail ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "SignupDetail_ContainsSelfServe_NoPasswordBlob")]
    public async Task SignupDetail_ContainsSelfServe_NoPasswordBlob()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _, out _);

        var submit = await sut.SubmitAsync(CaseBRequest(), null, null);
        var detail = await sut.GetSignupDetailAsync(submit.SignupId);

        detail.Should().NotBeNull();
        detail!.HasSelfServePayload.Should().BeTrue();
        detail.SelfServe.Should().NotBeNull();
        detail.SelfServe!.OwnerUsername.Should().Be("ownerquannho"); // normalized lowercase
        detail.SelfServe.ContactPhone.Should().Be("0912345678");
        detail.SelfServe.ProductLines.Should().ContainSingle(p => p.Name == "Trà đào");
        detail.SelfServe.PhotoUrls.Should().Contain(u => u.Contains("sign.jpg"));
        // Serialization check: KHÔNG field nào chứa protected blob
        var json = System.Text.Json.JsonSerializer.Serialize(detail);
        json.Should().NotContain("OwnerPasswordProtected");
        json.Should().NotContain("matkhau456");
    }

    [Fact(DisplayName = "SignupDetail_CrawlTenant_HasSelfServePayloadFalse")]
    public async Task SignupDetail_CrawlTenant_HasSelfServePayloadFalse()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out var onboarding, out _);

        var tenantId = await onboarding.OnboardUnverifiedAsync(new CrawlListingDto(
            Name: "Công ty Crawl", TaxCode: "0108888888", Address: null,
            CrawledPhone: null, ContactName: null, IndustryCode: null,
            SourceSite: "doanhnghiep.vn", SourceUrl: "https://example.com/5", CrawledAt: DateTime.UtcNow));

        var detail = await sut.GetSignupDetailAsync(tenantId);
        detail.Should().NotBeNull();
        detail!.HasSelfServePayload.Should().BeFalse();
        detail.SelfServe.Should().BeNull();
    }

    // ── Normalize helpers ─────────────────────────────────────────────────

    [Theory]
    [InlineData("0901 112 233", "0901112233")]
    [InlineData("+84901112233", "0901112233")]
    [InlineData("84901112233", "0901112233")]
    [InlineData("0912-345-678", "0912345678")]
    public void NormalizePhone_VariousFormats(string input, string expected)
        => MerchantSignupService.NormalizePhone(input).Should().Be(expected);

    [Theory]
    [InlineData("0106463914", true)]
    [InlineData("0106463914-001", true)]
    [InlineData("123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidTaxCode_Checks(string? input, bool expected)
        => MerchantSignupService.IsValidTaxCode(input).Should().Be(expected);
}
