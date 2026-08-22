using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using Xunit;

namespace VanAn.Tests.Services;

/// <summary>
/// Wave 5 — W5-T10: Service tests for TenantManagementService.
/// Uses SQLite in-memory via VanAnDbContextTestFactory.
/// Minimum 8 cases required per exit criteria.
/// </summary>
public class TenantManagementServiceTests : IDisposable
{
    private readonly TestContextScope _scope;
    private readonly VanAnDbContext _db;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<IShopInstanceService> _shopInstanceMock;
    private readonly TenantManagementService _sut;

    public TenantManagementServiceTests()
    {
        _scope = VanAnDbContextTestFactory.Create();
        _db = _scope.Context;
        _notificationMock = new Mock<INotificationService>();
        _notificationMock.Setup(n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        // Phase 2 Scaling: IShopInstanceService mock for capacity check in AssignShopInstanceAsync
        _shopInstanceMock = new Mock<IShopInstanceService>();
        _shopInstanceMock.Setup(s => s.CountTenantsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _sut = new TenantManagementService(_db, _db, _notificationMock.Object, _shopInstanceMock.Object, NullLogger<TenantManagementService>.Instance);
    }

    public void Dispose() => _scope.Dispose();

    // ── 1. CreateTenant ───────────────────────────────────────────────────────

    [Fact(DisplayName = "W5-S1: CreateTenant persists tenant with Active status")]
    public async Task CreateTenant_PersistsTenant_WithActiveStatus()
    {
        var req = new CreateTenantRequest("Corp ABC", BusinessType.Company, null, "ceo@abc.vn", null, null, null);

        Tenant result = await _sut.CreateTenantAsync(req);

        result.Should().NotBeNull();
        result.Name.Should().Be("Corp ABC");
        result.Status.Should().Be(TenantStatus.Active);
        result.Settings.ContactEmail.Should().Be("ceo@abc.vn");

        // Verify persisted
        Tenant? fromDb = await _sut.GetTenantByIdAsync(result.Id);
        fromDb.Should().NotBeNull();
    }

    [Fact(DisplayName = "W5-S2: CreateTenant sends welcome email when ContactEmail provided")]
    public async Task CreateTenant_SendsWelcomeEmail_WhenContactEmailProvided()
    {
        var req = new CreateTenantRequest("ABC Co", BusinessType.Company, null, "owner@abc.vn", null, null, null);

        await _sut.CreateTenantAsync(req);

        _notificationMock.Verify(
            n => n.SendEmailAsync("owner@abc.vn", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact(DisplayName = "W5-S3: CreateTenant skips email when no ContactEmail")]
    public async Task CreateTenant_SkipsEmail_WhenNoContactEmail()
    {
        var req = new CreateTenantRequest("No Email Corp", BusinessType.Company, null, null, null, null, null);

        await _sut.CreateTenantAsync(req);

        _notificationMock.Verify(
            n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact(DisplayName = "W5-S4: CreateTenant for HouseholdBusiness sets correct HKDGroup")]
    public async Task CreateTenant_HouseholdBusiness_SetsHKDGroup()
    {
        var req = new CreateTenantRequest("HKD Lan", BusinessType.HouseholdBusiness, HKDGroup.Group2, null, null, null, null);

        Tenant result = await _sut.CreateTenantAsync(req);

        result.IsHouseholdBusiness().Should().BeTrue();
        result.HKDGroup.Should().Be(HKDGroup.Group2);
    }

    // ── 2. GetTenantById ──────────────────────────────────────────────────────

    [Fact(DisplayName = "W5-S5: GetTenantById returns null for unknown ID")]
    public async Task GetTenantById_ReturnsNull_ForUnknownId()
    {
        Tenant? result = await _sut.GetTenantByIdAsync(new TenantId(Guid.NewGuid()));

        result.Should().BeNull();
    }

    // ── 3. ListTenants ────────────────────────────────────────────────────────

    [Fact(DisplayName = "W5-S6: ListTenants returns all tenants across lifecycle states")]
    public async Task ListTenants_ReturnsAllTenants()
    {
        await _sut.CreateTenantAsync(new CreateTenantRequest("T1", BusinessType.Company, null, null, null, null, null));
        await _sut.CreateTenantAsync(new CreateTenantRequest("T2", BusinessType.Company, null, null, null, null, null));

        IReadOnlyList<Tenant> list = await _sut.ListTenantsAsync();

        list.Should().HaveCount(2);
    }

    // ── 4. SuspendAsync ───────────────────────────────────────────────────────

    [Fact(DisplayName = "W5-S7: SuspendAsync changes tenant status to Suspended")]
    public async Task SuspendAsync_ChangesTenantToSuspended()
    {
        Tenant tenant = await _sut.CreateTenantAsync(new CreateTenantRequest("Corp", BusinessType.Company, null, null, null, null, null));

        await _sut.SuspendAsync(tenant.Id, "payment overdue");

        Tenant? updated = await _sut.GetTenantByIdAsync(tenant.Id);
        updated!.Status.Should().Be(TenantStatus.Suspended);
    }

    [Fact(DisplayName = "W5-S8: SuspendAsync throws KeyNotFoundException for unknown tenant")]
    public async Task SuspendAsync_ThrowsKeyNotFound_ForUnknownTenant()
    {
        Func<Task> act = () => _sut.SuspendAsync(new TenantId(Guid.NewGuid()), "reason");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── 5. DeactivateAsync ────────────────────────────────────────────────────

    [Fact(DisplayName = "W5-S9: DeactivateAsync sets tenant status to Inactive")]
    public async Task DeactivateAsync_SetsTenantToInactive()
    {
        Tenant tenant = await _sut.CreateTenantAsync(new CreateTenantRequest("Corp", BusinessType.Company, null, null, null, null, null));

        await _sut.DeactivateAsync(tenant.Id, "closing");

        Tenant? updated = await _sut.GetTenantByIdAsync(tenant.Id);
        updated!.Status.Should().Be(TenantStatus.Inactive);
    }

    // ── 6. UpdateProfileAsync ─────────────────────────────────────────────────

    [Fact(DisplayName = "W5-S10: UpdateProfileAsync updates name and settings")]
    public async Task UpdateProfileAsync_UpdatesNameAndSettings()
    {
        Tenant tenant = await _sut.CreateTenantAsync(new CreateTenantRequest("Old Name", BusinessType.Company, null, null, null, null, null));

        await _sut.UpdateProfileAsync(tenant.Id, new UpdateTenantProfileRequest("New Name", "new@email.vn", null, null, "MST-123"));

        Tenant? updated = await _sut.GetTenantByIdAsync(tenant.Id);
        updated!.Name.Should().Be("New Name");
        updated.Settings.ContactEmail.Should().Be("new@email.vn");
        updated.Settings.TaxCode.Should().Be("MST-123");
    }
}
