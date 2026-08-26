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
/// Crawl-to-Onboard Phase 8 (2026-08-26): Service tests for OnboardUnverifiedAsync + VerifyAsync.
/// Uses real SQLite in-memory (VanAnDbContextTestFactory) — OnboardUnverifiedAsync heavily uses dbContext
/// (duplicate detection, CrawlSource audit, tenant save).
/// Verifies corrections: H5 (first canonical duplicate), M3 (CrawledPhone internal, ContactPhone null).
/// </summary>
public class OnboardUnverifiedTests
{
    private readonly Mock<IUserManagementService> _userServiceMock = new();
    private readonly Mock<IPermissionGroupService> _permissionGroupServiceMock = new();
    private readonly Mock<IRoleAssignmentService> _roleAssignmentServiceMock = new();

    private static CrawlListingDto DefaultListing(string? taxCode = "0106463914", string? crawledPhone = "0901234567") => new(
        Name: "Cafe ABC",
        TaxCode: taxCode,
        Address: "123 Lê Lợi, Q1, HCM",
        CrawledPhone: crawledPhone,
        ContactName: "Nguyễn Văn A",
        IndustryCode: "F&B",
        SourceSite: "trangvangvietnam",
        SourceUrl: "https://trangvangvietnam.com/listing/123",
        CrawledAt: DateTime.UtcNow);

    private TenantOnboardingService CreateSut(IVanAnDbContext dbContext)
        => new(
            Mock.Of<ITenantManagementService>(),
            _userServiceMock.Object,
            _permissionGroupServiceMock.Object,
            _roleAssignmentServiceMock.Object,
            dbContext,
            null,  // IOutboxRepository — null for tests
            NullLogger<TenantOnboardingService>.Instance);

    // ── OnboardUnverifiedAsync ─────────────────────────────────────────────

    [Fact(DisplayName = "OnboardUnverifiedAsync_CreatesPendingTenant_WithMaskedPhone")]
    public async Task OnboardUnverifiedAsync_CreatesPendingTenant_WithMaskedPhone()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        var tenantId = await sut.OnboardUnverifiedAsync(DefaultListing());

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId));
        tenant.Should().NotBeNull();
        tenant!.Status.Should().Be(TenantStatus.Pending);
        // M3: ContactPhone=null (SĐT section hidden on Pending profile)
        tenant.Settings.ContactPhone.Should().BeNull();
        // CrawledPhone stored internal (NOT displayed)
        tenant.Settings.CrawledPhone.Should().Be("0901234567");
    }

    [Fact(DisplayName = "OnboardUnverifiedAsync_DoesNotCreateUser")]
    public async Task OnboardUnverifiedAsync_DoesNotCreateUser()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        await sut.OnboardUnverifiedAsync(DefaultListing());

        _userServiceMock.Verify(
            s => s.CreateUserAsync(
                It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "OnboardUnverifiedAsync_MarksDuplicate_WhenTaxCodeExists (correction H5)")]
    public async Task OnboardUnverifiedAsync_MarksDuplicate_WhenTaxCodeExists()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        // First tenant (canonical) — same MST
        var canonicalId = await sut.OnboardUnverifiedAsync(DefaultListing(taxCode: "0106463914"));

        // Second tenant — same MST → should be marked duplicate of canonical
        var duplicateId = await sut.OnboardUnverifiedAsync(DefaultListing(taxCode: "0106463914"));

        var duplicate = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(duplicateId));
        duplicate.Should().NotBeNull();
        duplicate!.PotentialDuplicateOf.Should().Be(canonicalId);
    }

    [Fact(DisplayName = "OnboardUnverifiedAsync_SavesCrawlSourceAudit")]
    public async Task OnboardUnverifiedAsync_SavesCrawlSourceAudit()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        var tenantId = await sut.OnboardUnverifiedAsync(DefaultListing());

        var crawlSource = await scope.Context.CrawlSources
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == new TenantId(tenantId))
            .ToListAsync();
        crawlSource.Should().HaveCount(1);
        crawlSource[0].SourceSite.Should().Be("trangvangvietnam");
        crawlSource[0].SourceUrl.Should().Be("https://trangvangvietnam.com/listing/123");
    }

    [Fact(DisplayName = "OnboardUnverifiedAsync_PendingSlugFormat")]
    public async Task OnboardUnverifiedAsync_PendingSlugFormat()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        var tenantId = await sut.OnboardUnverifiedAsync(DefaultListing(taxCode: "0106463914"));

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId));
        tenant!.Settings.Slug.Should().StartWith("pending-0106463914-");
    }

    // ── VerifyAsync ────────────────────────────────────────────────────────

    [Fact(DisplayName = "VerifyAsync_CreatesOwnerUser_AndPermissionGroups")]
    public async Task VerifyAsync_CreatesOwnerUser_AndPermissionGroups()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);
        var tenantId = await sut.OnboardUnverifiedAsync(DefaultListing());

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

        var req = new VerifyTenantRequest(
            OwnerUsername: "owner@abc.vn",
            OwnerPassword: "Password123!",
            OwnerDisplayName: "Owner User",
            OwnerPhone: "0909876543",
            OwnerEmail: "owner@abc.vn");

        var result = await sut.VerifyAsync(tenantId, req);

        _userServiceMock.Verify(s => s.CreateUserAsync(
            It.Is<TenantId>(t => t == new TenantId(tenantId)),
            It.Is<string>(u => u == "owner@abc.vn"),
            It.Is<string>(p => p == "Password123!"),
            It.Is<string>(d => d == "Owner User"),
            It.Is<UserRole>(r => r == UserRole.Owner),
            It.IsAny<CancellationToken>()), Times.Once);

        result.OwnerUserId.Should().Be(fakeUser.Id);
        result.PermissionGroupsCreated.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact(DisplayName = "VerifyAsync_TransitionsTenantToActive")]
    public async Task VerifyAsync_TransitionsTenantToActive()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);
        var tenantId = await sut.OnboardUnverifiedAsync(DefaultListing());

        SetupUserMocks(tenantId);

        var req = new VerifyTenantRequest("owner@abc.vn", "Password123!", "Owner User");
        await sut.VerifyAsync(tenantId, req);

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId));
        tenant!.Status.Should().Be(TenantStatus.Active);
    }

    [Fact(DisplayName = "VerifyAsync_UnmasksPhone_CopiesCrawledToContact")]
    public async Task VerifyAsync_UnmasksPhone_CopiesCrawledToContact()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);
        var tenantId = await sut.OnboardUnverifiedAsync(DefaultListing());

        SetupUserMocks(tenantId);

        // Verify with owner-provided phone (consented per M3)
        var req = new VerifyTenantRequest(
            "owner@abc.vn", "Password123!", "Owner User",
            OwnerPhone: "0909876543");
        await sut.VerifyAsync(tenantId, req);

        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId));
        // M3: ContactPhone = owner-provided (consented), NOT from CrawledPhone
        tenant!.Settings.ContactPhone.Should().Be("0909876543");
    }

    [Fact(DisplayName = "VerifyAsync_UpdatesSlug_ToCleanSlug")]
    public async Task VerifyAsync_UpdatesSlug_ToCleanSlug()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);
        var tenantId = await sut.OnboardUnverifiedAsync(DefaultListing());

        SetupUserMocks(tenantId);

        var req = new VerifyTenantRequest(
            "owner@abc.vn", "Password123!", "Owner User",
            Slug: "cafe-abc");
        var result = await sut.VerifyAsync(tenantId, req);

        result.PublishedSlug.Should().Be("cafe-abc");
        var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId));
        tenant!.Settings.Slug.Should().Be("cafe-abc");
    }

    [Fact(DisplayName = "VerifyAsync_Throws_WhenPotentialDuplicateOfNotNull (correction H4)")]
    public async Task VerifyAsync_Throws_WhenPotentialDuplicateOfNotNull()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        // Create canonical + duplicate (same MST)
        await sut.OnboardUnverifiedAsync(DefaultListing(taxCode: "0106463914"));
        var duplicateId = await sut.OnboardUnverifiedAsync(DefaultListing(taxCode: "0106463914"));

        SetupUserMocks(duplicateId);

        var req = new VerifyTenantRequest("owner@abc.vn", "Password123!", "Owner User");
        var act = () => sut.VerifyAsync(duplicateId, req);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*duplicate*");
    }

    [Fact(DisplayName = "VerifyAsync_Throws_WhenTenantNotFound")]
    public async Task VerifyAsync_Throws_WhenTenantNotFound()
    {
        using var scope = VanAnDbContextTestFactory.Create();
        var sut = CreateSut(scope.Context);

        var req = new VerifyTenantRequest("owner@abc.vn", "Password123!", "Owner User");
        var act = () => sut.VerifyAsync(Guid.NewGuid(), req);

        await act.Should().ThrowAsync<KeyNotFoundException>();
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
}
