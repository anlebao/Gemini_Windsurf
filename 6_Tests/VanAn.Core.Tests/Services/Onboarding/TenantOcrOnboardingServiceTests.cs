using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.Domain.Aggregates.UserAggregate;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using Xunit;

namespace VanAn.Core.Tests.Services.Onboarding;

/// <summary>
/// OCR Tenant Onboarding S2 (2026-10-25): service tests cho TenantOcrOnboardingService.
/// 2 case (D4): A = có GPKD (MST bắt buộc, dedup chặn cứng) · B = shop không GPKD
/// (HKDGroup bắt buộc, dedup gợi ý). Flow Pending → Verify (D3/D5) + provenance CrawlSource.
/// Dùng real SQLite in-memory (VanAnDbContextTestFactory) — giống OnboardUnverifiedTests.
/// </summary>
public class TenantOcrOnboardingServiceTests
{
    private readonly Mock<IUserManagementService> _userServiceMock = new();
    private readonly Mock<IPermissionGroupService> _permissionGroupServiceMock = new();
    private readonly Mock<IRoleAssignmentService> _roleAssignmentServiceMock = new();

    private static readonly Guid AdminId = Guid.NewGuid();

    private TenantOcrOnboardingService CreateSut(IVanAnDbContext dbContext, out TenantOnboardingService onboardingService)
    {
        onboardingService = new TenantOnboardingService(
            Mock.Of<ITenantManagementService>(),
            _userServiceMock.Object,
            _permissionGroupServiceMock.Object,
            _roleAssignmentServiceMock.Object,
            dbContext,
            null,  // IOutboxRepository — null for tests
            NullLogger<TenantOnboardingService>.Instance);

        return new TenantOcrOnboardingService(
            onboardingService,
            dbContext,
            NullLogger<TenantOcrOnboardingService>.Instance);
    }

    private void SetupUserMocks(Guid tenantId)
    {
        var fakeUser = DemoUser.Create(new TenantId(tenantId), "owner@abc.vn", "hashed", "Owner User", UserRole.Owner);
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

    private static OcrOnboardingRequest CaseARequest(string? mst = "0106463914") => new(
        Case: OcrOnboardingCase.CompanyWithLicense,
        Name: "Công ty TNHH ABC",
        Address: "123 Lê Lợi, Q1, HCM",
        BusinessType: BusinessType.Company,
        HkdGroup: null,
        ProductLines:
        [
            new OcrProductLineDto("Cà phê sữa đá", 25000, 1),
            new OcrProductLineDto("Bánh mì trứng", 20000, 2)
        ],
        Latitude: 10.7765,
        Longitude: 106.6951,
        GpdkInfo: new OcrGpdkInfo(mst, "Công ty TNHH ABC", "123 Lê Lợi, Q1, HCM", "TNHH", "Nguyễn Văn A"),
        SignName: "ABC Cafe",
        ContactPhone: "0901112223",
        PhotoUrls: ["https://cloudinary.com/gpkd-abc.jpg"]);

    private static OcrOnboardingRequest CaseBRequest(string? name = "Shop Cà Phê ABC") => new(
        Case: OcrOnboardingCase.SmallShopNoLicense,
        Name: name,
        Address: "12 Nguyễn Huệ, Q1, HCM",
        BusinessType: BusinessType.HouseholdBusiness,
        HkdGroup: HKDGroup.Group2,
        ProductLines:
        [
            new OcrProductLineDto("Trà đào", 30000, 1)
        ],
        Latitude: 10.7705,
        Longitude: 106.7031,
        SignName: name,
        ContactPhone: "0901112223");

    // ── Case A (có GPKD) ──────────────────────────────────────────────────

    [Fact(DisplayName = "CaseA_CreatesActiveTenant_WithOwnerCredentials_AndProvenance")]
    public async Task CaseA_CreatesActiveTenant_WithOwnerCredentials_AndProvenance()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _);
        SetupUserMocks(Guid.NewGuid());

        var result = await sut.ExecuteAsync(CaseARequest(), AdminId);

        result.TenantId.Should().NotBe(Guid.Empty);
        result.OwnerUsername.Should().Be("owner0106463914");
        result.OwnerPassword.Should().NotBeNullOrEmpty();
        result.Slug.Should().NotBeNullOrEmpty();

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(result.TenantId));
        tenant.Should().NotBeNull();
        tenant!.Status.Should().Be(TenantStatus.Active);
        tenant.BusinessType.Should().Be(BusinessType.Company);
        tenant.Settings.TaxCode.Should().Be("0106463914");
        tenant.Settings.Latitude.Should().Be(10.7765);
        tenant.Settings.Longitude.Should().Be(106.6951);
        // M3: ContactPhone từ owner consent trên wizard
        tenant.Settings.ContactPhone.Should().Be("0901112223");

        // Provenance: CrawlSource OCR-GPKD + payload chứa ảnh
        var crawlSource = await scope.Context.CrawlSources.IgnoreQueryFilters()
            .Where(c => c.TenantId == new TenantId(result.TenantId))
            .ToListAsync();
        crawlSource.Should().ContainSingle(c => c.SourceSite == "OCR-GPKD");
        crawlSource[0].RawJson.Should().Contain("cloudinary.com/gpkd-abc.jpg");
    }

    [Fact(DisplayName = "CaseA_MissingMst_Throws")]
    public async Task CaseA_MissingMst_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _);

        var request = CaseARequest(mst: null);

        var act = () => sut.ExecuteAsync(request, AdminId);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Mã số thuế*");
    }

    [Fact(DisplayName = "CaseA_InvalidMst_Throws")]
    public async Task CaseA_InvalidMst_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _);

        var request = CaseARequest(mst: "ABC123");

        var act = () => sut.ExecuteAsync(request, AdminId);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*10 hoặc 13*");
    }

    [Fact(DisplayName = "CaseA_DuplicateMst_ReturnsExisting_DoesNotCreate")]
    public async Task CaseA_DuplicateMst_ReturnsExisting_DoesNotCreate()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out var onboardingService);

        // Seed tenant sẵn với cùng MST
        var existingId = await onboardingService.OnboardUnverifiedAsync(new CrawlListingDto(
            Name: "Công ty TNHH ABC", TaxCode: "0106463914", Address: "123 Lê Lợi",
            CrawledPhone: null, ContactName: null, IndustryCode: null,
            SourceSite: "doanhnghiep.vn", SourceUrl: "https://example.com/1", CrawledAt: DateTime.UtcNow));

        var result = await sut.ExecuteAsync(CaseARequest(), AdminId);

        // Chặn cứng: trả tenant cũ, KHÔNG tạo tenant mới
        result.TenantId.Should().Be(existingId);
        result.ExistingDuplicateTenantId.Should().Be(existingId);
        result.OwnerUsername.Should().BeEmpty();

        var count = await scope.Context.Tenants.IgnoreQueryFilters()
            .CountAsync(t => t.Settings.TaxCode == "0106463914");
        count.Should().Be(1);
    }

    // ── Case B (shop không GPKD) ──────────────────────────────────────────

    [Fact(DisplayName = "CaseB_CreatesPendingThenActiveHkdTenant_WithGroup2 (S0 end-to-end)")]
    public async Task CaseB_CreatesActiveHkdTenant_WithGroup2()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _);
        SetupUserMocks(Guid.NewGuid());

        var result = await sut.ExecuteAsync(CaseBRequest(), AdminId);

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(result.TenantId));
        tenant.Should().NotBeNull();
        tenant!.Status.Should().Be(TenantStatus.Active);
        // S0: Pending HKD — BusinessType + HKDGroup + Type=HKD (sổ sách TT152)
        tenant.BusinessType.Should().Be(BusinessType.HouseholdBusiness);
        tenant.HKDGroup.Should().Be(HKDGroup.Group2);
        tenant.Type.Should().Be(TenantType.HKD);
        // Case B: không có MST
        tenant.Settings.TaxCode.Should().BeNull();
        // GPS sysadmin
        tenant.Settings.Latitude.Should().Be(10.7705);
        tenant.Settings.Longitude.Should().Be(106.7031);

        // Provenance: OCR-SIGN
        var crawlSource = await scope.Context.CrawlSources.IgnoreQueryFilters()
            .Where(c => c.TenantId == new TenantId(result.TenantId))
            .ToListAsync();
        crawlSource.Should().ContainSingle(c => c.SourceSite == "OCR-SIGN");
    }

    [Fact(DisplayName = "CaseB_MissingHkdGroup_Throws")]
    public async Task CaseB_MissingHkdGroup_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _);

        var request = CaseBRequest() with { HkdGroup = null };

        var act = () => sut.ExecuteAsync(request, AdminId);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*HKDGroup*");
    }

    [Fact(DisplayName = "CaseB_WrongBusinessType_Throws")]
    public async Task CaseB_WrongBusinessType_Throws()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _);

        var request = CaseBRequest() with { BusinessType = BusinessType.Company };

        var act = () => sut.ExecuteAsync(request, AdminId);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*HouseholdBusiness*");
    }

    [Fact(DisplayName = "CaseB_DuplicateNameAddress_HintsButStillCreates")]
    public async Task CaseB_DuplicateNameAddress_HintsButStillCreates()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _);
        SetupUserMocks(Guid.NewGuid());
        SetupUserMocks(Guid.NewGuid());

        // Tạo tenant Case B đầu tiên
        var first = await sut.ExecuteAsync(CaseBRequest(), AdminId);

        // Tạo tenant thứ 2 cùng tên + địa chỉ → gợi ý duplicate nhưng VẪN tạo
        var second = await sut.ExecuteAsync(CaseBRequest(), AdminId);

        second.ExistingDuplicateTenantId.Should().Be(first.TenantId);
        second.TenantId.Should().NotBe(first.TenantId);

        var count = await scope.Context.Tenants.IgnoreQueryFilters()
            .CountAsync(t => t.Name.ToLower() == "shop cà phê abc");
        count.Should().Be(2);
    }

    [Fact(DisplayName = "CaseB_PhotoUrls_InCrawlSourcePayload")]
    public async Task CaseB_PhotoUrls_InCrawlSourcePayload()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context, out _);
        SetupUserMocks(Guid.NewGuid());

        var request = CaseBRequest() with { PhotoUrls = ["https://cloudinary.com/sign-abc.jpg", "https://cloudinary.com/menu-abc.jpg"] };
        var result = await sut.ExecuteAsync(request, AdminId);

        var crawlSource = await scope.Context.CrawlSources.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == new TenantId(result.TenantId));
        crawlSource.Should().NotBeNull();
        crawlSource!.RawJson.Should().Contain("cloudinary.com/sign-abc.jpg");
        crawlSource.RawJson.Should().Contain("cloudinary.com/menu-abc.jpg");
    }
}