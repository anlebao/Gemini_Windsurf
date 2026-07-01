using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using Xunit;

namespace VanAn.Core.Tests.Services.Onboarding
{
    /// <summary>
    /// Unit tests for Wave 3: TenantOnboardingService.
    /// Uses Moq to mock all service dependencies.
    /// Verifies orchestration flow: tenant → user → role → seed → groups → group assignment.
    /// </summary>
    public class TenantOnboardingServiceTests
    {
        private static readonly TenantId TestTenantId = new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
        private static readonly Guid TestUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        private readonly Mock<ITenantManagementService> _tenantServiceMock = new();
        private readonly Mock<IUserManagementService> _userServiceMock = new();
        private readonly Mock<IPermissionGroupService> _permissionGroupServiceMock = new();
        private readonly Mock<IRoleAssignmentService> _roleAssignmentServiceMock = new();
        private readonly Mock<IIndustrySeedStrategy> _seedStrategyMock = new();
        private readonly Mock<IVanAnDbContext> _dbContextMock = new();

        private readonly TenantOnboardingService _sut;

        public TenantOnboardingServiceTests()
        {
            // Setup default tenant mock — returns a Tenant with TestTenantId
            var fakeTenant = Tenant.CreateCompany(
                TestTenantId,
                "Test Tenant",
                new TenantSettings("owner@test.vn", null, null));
            _tenantServiceMock
                .Setup(s => s.CreateTenantAsync(It.IsAny<CreateTenantRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeTenant);

            // Setup default user mock
            var fakeUser = DemoUser.Create(TestTenantId, "owner@test.vn", "hashed", "Owner User", UserRole.Owner);
            fakeUser.GetType().GetProperty("Id")?.SetValue(fakeUser, TestUserId); // note: Id set via base entity
            _userServiceMock
                .Setup(s => s.CreateUserAsync(
                    It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeUser);

            // Setup role assignment
            _roleAssignmentServiceMock
                .Setup(s => s.AssignRoleToUserAsync(It.IsAny<Guid>(), It.IsAny<TenantId>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _roleAssignmentServiceMock
                .Setup(s => s.AssignUserToGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<TenantId>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Setup seed strategy mock
            _seedStrategyMock.Setup(s => s.IndustryCode).Returns("F&B");
            _seedStrategyMock.Setup(s => s.IndustryName).Returns("Food & Beverage");
            _seedStrategyMock
                .Setup(s => s.SeedAsync(It.IsAny<TenantId>(), It.IsAny<IVanAnDbContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IndustrySeedResult(8, 12, 14, 1, []));

            // Setup DbContext SaveChangesAsync
            _dbContextMock
                .Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            // Setup permission group mock — returns a new group each call
            _permissionGroupServiceMock
                .Setup(s => s.CreateGroupAsync(It.IsAny<TenantId>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TenantId tid, string name, string? desc, CancellationToken _) =>
                    new PermissionGroup(tid, name, desc));

            _sut = new TenantOnboardingService(
                _tenantServiceMock.Object,
                _userServiceMock.Object,
                _permissionGroupServiceMock.Object,
                _roleAssignmentServiceMock.Object,
                [_seedStrategyMock.Object],
                _dbContextMock.Object,
                NullLogger<TenantOnboardingService>.Instance);
        }

        private static OnboardTenantRequest DefaultRequest(string industryCode = "F&B") =>
            new("Quán Cà Phê ABC", BusinessType.HouseholdBusiness, HKDGroup.Group1,
                "owner@abc.vn", "0901234567", "123 Nguyễn Huệ", "1234567890",
                industryCode, "owner@abc.vn", "Password123!", "Nguyễn Văn A");

        // ── SC1: Implements ITenantOnboardingService ───────────────────────────

        [Fact(DisplayName = "W3-SC1: TenantOnboardingService implements ITenantOnboardingService")]
        public void TenantOnboardingService_ImplementsInterface()
        {
            _sut.Should().BeAssignableTo<ITenantOnboardingService>();
        }

        // ── SC2: One call creates tenant + user + seed ─────────────────────────

        [Fact(DisplayName = "W3-SC2: OnboardAsync calls CreateTenantAsync once")]
        public async Task OnboardAsync_CallsCreateTenantAsync_Once()
        {
            await _sut.OnboardAsync(DefaultRequest());

            _tenantServiceMock.Verify(
                s => s.CreateTenantAsync(It.Is<CreateTenantRequest>(r => r.Name == "Quán Cà Phê ABC"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact(DisplayName = "W3-SC2: OnboardAsync calls CreateUserAsync once with Owner role")]
        public async Task OnboardAsync_CallsCreateUserAsync_Once_WithOwnerRole()
        {
            await _sut.OnboardAsync(DefaultRequest());

            _userServiceMock.Verify(
                s => s.CreateUserAsync(
                    It.Is<TenantId>(t => t == TestTenantId),
                    It.Is<string>(u => u == "owner@abc.vn"),
                    It.Is<string>(p => p == "Password123!"),
                    It.Is<string>(d => d == "Nguyễn Văn A"),
                    It.Is<UserRole>(r => r == UserRole.Owner),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact(DisplayName = "W3-SC2: OnboardAsync calls seed strategy SeedAsync once")]
        public async Task OnboardAsync_CallsSeedStrategy_Once()
        {
            await _sut.OnboardAsync(DefaultRequest());

            _seedStrategyMock.Verify(
                s => s.SeedAsync(
                    It.Is<TenantId>(t => t == TestTenantId),
                    It.IsAny<IVanAnDbContext>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // ── SC3: Owner user assigned role Owner ────────────────────────────────

        [Fact(DisplayName = "W3-SC3: OnboardAsync assigns Owner role to owner user")]
        public async Task OnboardAsync_AssignsOwnerRole_ToOwnerUser()
        {
            await _sut.OnboardAsync(DefaultRequest());

            _roleAssignmentServiceMock.Verify(
                s => s.AssignRoleToUserAsync(
                    It.IsAny<Guid>(),
                    It.Is<TenantId>(t => t == TestTenantId),
                    It.Is<UserRole>(r => r == UserRole.Owner),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // ── SC4: Owner assigned to Quản lý group ──────────────────────────────

        [Fact(DisplayName = "W3-SC4: OnboardAsync assigns owner to Quản lý permission group")]
        public async Task OnboardAsync_AssignsOwner_ToQuanLyGroup()
        {
            await _sut.OnboardAsync(DefaultRequest());

            _roleAssignmentServiceMock.Verify(
                s => s.AssignUserToGroupAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.Is<TenantId>(t => t == TestTenantId),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // ── SC5: Creates at least 4 default permission groups ─────────────────

        [Fact(DisplayName = "W3-SC5: OnboardAsync creates at least 4 default permission groups")]
        public async Task OnboardAsync_CreatesAtLeastFour_PermissionGroups()
        {
            await _sut.OnboardAsync(DefaultRequest());

            _permissionGroupServiceMock.Verify(
                s => s.CreateGroupAsync(
                    It.Is<TenantId>(t => t == TestTenantId),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()),
                Times.AtLeast(4));
        }

        [Fact(DisplayName = "W3-SC5: OnboardAsync creates Quản lý group")]
        public async Task OnboardAsync_CreatesQuanLyGroup()
        {
            await _sut.OnboardAsync(DefaultRequest());

            _permissionGroupServiceMock.Verify(
                s => s.CreateGroupAsync(
                    It.IsAny<TenantId>(),
                    It.Is<string>(name => name == "Quản lý"),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // ── SC6: Returns TenantOnboardingResult with correct counts ───────────

        [Fact(DisplayName = "W3-SC6: OnboardAsync returns result with correct seed counts")]
        public async Task OnboardAsync_Returns_CorrectSeedCounts()
        {
            var result = await _sut.OnboardAsync(DefaultRequest());

            result.ProductsCreated.Should().Be(8);
            result.IngredientsCreated.Should().Be(12);
            result.RecipesCreated.Should().Be(14);
            result.ShopsCreated.Should().Be(1);
        }

        [Fact(DisplayName = "W3-SC6: OnboardAsync returns result with correct PermissionGroupsCreated count")]
        public async Task OnboardAsync_Returns_CorrectGroupsCreatedCount()
        {
            var result = await _sut.OnboardAsync(DefaultRequest());

            result.PermissionGroupsCreated.Should().BeGreaterThanOrEqualTo(4);
        }

        [Fact(DisplayName = "W3-SC6: OnboardAsync returns result with TenantId and OwnerUserId")]
        public async Task OnboardAsync_Returns_TenantIdAndOwnerUserId()
        {
            var result = await _sut.OnboardAsync(DefaultRequest());

            result.TenantId.Should().Be(TestTenantId.Value);
            result.OwnerUserId.Should().NotBeEmpty();
        }

        [Fact(DisplayName = "W3-SC6: OnboardAsync returns empty Warnings when seed has no warnings")]
        public async Task OnboardAsync_Returns_EmptyWarnings_WhenNoSeedWarnings()
        {
            var result = await _sut.OnboardAsync(DefaultRequest());

            result.Warnings.Should().BeEmpty();
        }

        [Fact(DisplayName = "W3-SC6: OnboardAsync propagates seed warnings to result")]
        public async Task OnboardAsync_Propagates_SeedWarnings()
        {
            _seedStrategyMock
                .Setup(s => s.SeedAsync(It.IsAny<TenantId>(), It.IsAny<IVanAnDbContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IndustrySeedResult(0, 0, 0, 0, ["Some seed warning"]));

            var result = await _sut.OnboardAsync(DefaultRequest());

            result.Warnings.Should().ContainSingle().Which.Should().Be("Some seed warning");
        }

        // ── Invalid industry code throws ───────────────────────────────────────

        [Fact(DisplayName = "W3-SC: OnboardAsync throws ArgumentException for unknown industry code")]
        public async Task OnboardAsync_Throws_ForUnknownIndustryCode()
        {
            var act = () => _sut.OnboardAsync(DefaultRequest("UNKNOWN"));

            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*UNKNOWN*");
        }

        [Fact(DisplayName = "W3-SC: OnboardAsync is case-insensitive for industry code lookup")]
        public async Task OnboardAsync_IsCaseInsensitive_ForIndustryCode()
        {
            var result = await _sut.OnboardAsync(DefaultRequest("f&b"));

            result.Should().NotBeNull();
        }

        // ── Cross-tenant safety: all operations use same TenantId ─────────────

        [Fact(DisplayName = "W3-SC: OnboardAsync uses same TenantId for all operations")]
        public async Task OnboardAsync_UsesSameTenantId_ForAllOperations()
        {
            await _sut.OnboardAsync(DefaultRequest());

            // Verify all tenant-scoped calls use the same TenantId returned by tenant creation
            _userServiceMock.Verify(s =>
                s.CreateUserAsync(It.Is<TenantId>(t => t == TestTenantId), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _seedStrategyMock.Verify(s =>
                s.SeedAsync(It.Is<TenantId>(t => t == TestTenantId), It.IsAny<IVanAnDbContext>(), It.IsAny<CancellationToken>()),
                Times.Once);
            _permissionGroupServiceMock.Verify(s =>
                s.CreateGroupAsync(It.Is<TenantId>(t => t == TestTenantId), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
                Times.AtLeast(4));
        }

        // ── SaveChangesAsync called after seed ─────────────────────────────────

        [Fact(DisplayName = "W3-SC: OnboardAsync calls SaveChangesAsync after seed")]
        public async Task OnboardAsync_CallsSaveChangesAsync_AfterSeed()
        {
            await _sut.OnboardAsync(DefaultRequest());

            _dbContextMock.Verify(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
