using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.CoreHub.Services.Claims;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.CoreHub.Tests.TestInfrastructure;
using VerifyResult = VanAn.CoreHub.Services.Onboarding.VerifyResult;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;

namespace VanAn.Core.Tests.Services.Claims;

/// <summary>
/// Crawl-to-Onboard Phase 8 (2026-08-26): Service tests for TenantClaimService.
/// Tests claim lifecycle: Submit → Approve (Verify) / Reject.
/// Uses real SQLite in-memory (TenantClaimService queries dbContext for tenant + claim).
/// </summary>
public class TenantClaimServiceTests
{
    private readonly Mock<ITenantOnboardingService> _onboardingMock = new();

    private static CrawlListingDto DefaultListing(string? taxCode = "0106463914") => new(
        Name: "Cafe ABC",
        TaxCode: taxCode,
        Address: "123 Lê Lợi",
        CrawledPhone: "0901234567",
        ContactName: "Nguyễn Văn A",
        IndustryCode: "F&B",
        SourceSite: "trangvangvietnam",
        SourceUrl: "https://trangvangvietnam.com/123",
        CrawledAt: DateTime.UtcNow);

    private async Task<(TestContextScope scope, Guid tenantId, TenantClaimService sut)> SetupAsync()
    {
        var scope = VanAnDbContextTestFactory.Create();
        // Create a Pending tenant via OnboardUnverifiedAsync (real service — needs dbContext)
        var onboarding = new TenantOnboardingService(
            Mock.Of<ITenantManagementService>(),
            Mock.Of<IUserManagementService>(),
            Mock.Of<IPermissionGroupService>(),
            Mock.Of<IRoleAssignmentService>(),
            scope.Context,
            null,
            NullLogger<TenantOnboardingService>.Instance);
        var tenantId = await onboarding.OnboardUnverifiedAsync(DefaultListing());

        var sut = new TenantClaimService(scope.Context, _onboardingMock.Object, NullLogger<TenantClaimService>.Instance);
        return (scope, tenantId, sut);
    }

    private static SubmitClaimRequest DefaultClaimRequest() => new(
        ClaimantName: "Nguyễn Văn A",
        ClaimantPhone: "0909876543",
        ClaimantEmail: "owner@abc.vn",
        GpkdImageUrl: "https://cloudinary.com/gpkd.jpg",
        TaxCodeSubmitted: "0106463914");

    // ── SubmitClaimAsync ───────────────────────────────────────────────────

    [Fact(DisplayName = "SubmitClaimAsync_CreatesClaimRequest_WithSubmittedStatus")]
    public async Task SubmitClaimAsync_CreatesClaimRequest_WithSubmittedStatus()
    {
        var (scope, tenantId, sut) = await SetupAsync();
        using (scope)
        {
            var claimId = await sut.SubmitClaimAsync(tenantId, DefaultClaimRequest());

            var claim = await scope.Context.TenantClaimRequests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == claimId);
            claim.Should().NotBeNull();
            claim!.Status.Should().Be(TenantClaimRequest.ClaimStatus.Submitted);
            claim.ClaimantName.Should().Be("Nguyễn Văn A");
            claim.ClaimantPhone.Should().Be("0909876543");
            claim.GpkdImageUrl.Should().Be("https://cloudinary.com/gpkd.jpg");
        }
    }

    [Fact(DisplayName = "SubmitClaimAsync_OnActiveTenant_Throws")]
    public async Task SubmitClaimAsync_OnActiveTenant_Throws()
    {
        var (scope, tenantId, sut) = await SetupAsync();
        using (scope)
        {
            // Manually transition tenant to Active (bypass Verify to avoid user creation)
            var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId));
            tenant!.Verify();
            await scope.Context.SaveChangesAsync();

            var act = () => sut.SubmitClaimAsync(tenantId, DefaultClaimRequest());
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*verified*");
        }
    }

    [Fact(DisplayName = "SubmitClaimAsync_OnNonPendingTenant_Throws")]
    public async Task SubmitClaimAsync_OnNonPendingTenant_Throws()
    {
        var (scope, tenantId, sut) = await SetupAsync();
        using (scope)
        {
            // Manually transition tenant to Inactive
            var tenant = await scope.Context.Tenants.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantId));
            tenant!.Deactivate("test");
            await scope.Context.SaveChangesAsync();

            var act = () => sut.SubmitClaimAsync(tenantId, DefaultClaimRequest());
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    [Fact(DisplayName = "SubmitClaimAsync_OnUnknownTenant_Throws")]
    public async Task SubmitClaimAsync_OnUnknownTenant_Throws()
    {
        var (scope, _, sut) = await SetupAsync();
        using (scope)
        {
            var act = () => sut.SubmitClaimAsync(Guid.NewGuid(), DefaultClaimRequest());
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }
    }

    // ── ApproveClaimAsync ──────────────────────────────────────────────────

    [Fact(DisplayName = "ApproveClaimAsync_VerifiesTenant_AndApprovesClaim")]
    public async Task ApproveClaimAsync_VerifiesTenant_AndApprovesClaim()
    {
        var (scope, tenantId, sut) = await SetupAsync();
        using (scope)
        {
            var claimId = await sut.SubmitClaimAsync(tenantId, DefaultClaimRequest());

            var verifyResult = new VerifyResult(tenantId, Guid.NewGuid(), 4, "cafe-abc");
            _onboardingMock.Setup(o => o.VerifyAsync(
                    It.IsAny<Guid>(), It.IsAny<VerifyTenantRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(verifyResult);

            var adminConfig = new VerifyTenantRequest(
                "owner@abc.vn", "Password123!", "Owner User",
                OwnerPhone: "0909876543");

            var result = await sut.ApproveClaimAsync(claimId, adminConfig, sysAdminUserId: Guid.NewGuid());

            result.Should().Be(verifyResult);
            _onboardingMock.Verify(o => o.VerifyAsync(
                It.Is<Guid>(g => g == tenantId),
                It.IsAny<VerifyTenantRequest>(),
                It.IsAny<CancellationToken>()), Times.Once);

            // Verify claim status transitioned to Approved
            var claim = await scope.Context.TenantClaimRequests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == claimId);
            claim!.Status.Should().Be(TenantClaimRequest.ClaimStatus.Approved);
            claim.ReviewedAt.Should().NotBeNull();
        }
    }

    [Fact(DisplayName = "ApproveClaimAsync_OnAlreadyApproved_Throws")]
    public async Task ApproveClaimAsync_OnAlreadyApproved_Throws()
    {
        var (scope, tenantId, sut) = await SetupAsync();
        using (scope)
        {
            var claimId = await sut.SubmitClaimAsync(tenantId, DefaultClaimRequest());

            var verifyResult = new VerifyResult(tenantId, Guid.NewGuid(), 4, "cafe-abc");
            _onboardingMock.Setup(o => o.VerifyAsync(
                    It.IsAny<Guid>(), It.IsAny<VerifyTenantRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(verifyResult);

            var adminConfig = new VerifyTenantRequest("owner@abc.vn", "Password123!", "Owner User");
            await sut.ApproveClaimAsync(claimId, adminConfig, Guid.NewGuid());

            // Second approve should throw
            var act = () => sut.ApproveClaimAsync(claimId, adminConfig, Guid.NewGuid());
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    // ── RejectClaimAsync ───────────────────────────────────────────────────

    [Fact(DisplayName = "RejectClaimAsync_SetsRejectedStatus_AndReason")]
    public async Task RejectClaimAsync_SetsRejectedStatus_AndReason()
    {
        var (scope, tenantId, sut) = await SetupAsync();
        using (scope)
        {
            var claimId = await sut.SubmitClaimAsync(tenantId, DefaultClaimRequest());

            await sut.RejectClaimAsync(claimId, "GPKD không rõ nét", Guid.NewGuid());

            var claim = await scope.Context.TenantClaimRequests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == claimId);
            claim!.Status.Should().Be(TenantClaimRequest.ClaimStatus.Rejected);
            claim.RejectionReason.Should().Be("GPKD không rõ nét");
            claim.ReviewedAt.Should().NotBeNull();
        }
    }

    [Fact(DisplayName = "RejectClaimAsync_OnUnknownClaim_Throws")]
    public async Task RejectClaimAsync_OnUnknownClaim_Throws()
    {
        var (scope, _, sut) = await SetupAsync();
        using (scope)
        {
            var act = () => sut.RejectClaimAsync(Guid.NewGuid(), "reason", Guid.NewGuid());
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }
    }

    // ── ListPendingClaimsAsync ─────────────────────────────────────────────

    [Fact(DisplayName = "ListPendingClaimsAsync_ReturnsOnlySubmittedClaims")]
    public async Task ListPendingClaimsAsync_ReturnsOnlySubmittedClaims()
    {
        var (scope, tenantId, sut) = await SetupAsync();
        using (scope)
        {
            var claim1Id = await sut.SubmitClaimAsync(tenantId, DefaultClaimRequest());
            var claim2Id = await sut.SubmitClaimAsync(tenantId, DefaultClaimRequest() with { ClaimantName = "Trần Văn B" });

            // Reject claim2 → should not appear in pending list
            await sut.RejectClaimAsync(claim2Id, "test", Guid.NewGuid());

            var pending = await sut.ListPendingClaimsAsync();

            pending.Should().HaveCount(1);
            pending[0].Id.Should().Be(claim1Id);
            pending[0].ClaimantName.Should().Be("Nguyễn Văn A");
            pending[0].TenantName.Should().Be("Cafe ABC");
        }
    }

    [Fact(DisplayName = "ListPendingClaimsAsync_ReturnsEmpty_WhenNoSubmittedClaims")]
    public async Task ListPendingClaimsAsync_ReturnsEmpty_WhenNoSubmittedClaims()
    {
        var (scope, _, sut) = await SetupAsync();
        using (scope)
        {
            var pending = await sut.ListPendingClaimsAsync();
            pending.Should().BeEmpty();
        }
    }

    // ── GetClaimAsync ──────────────────────────────────────────────────────

    [Fact(DisplayName = "GetClaimAsync_ReturnsClaim_WhenExists")]
    public async Task GetClaimAsync_ReturnsClaim_WhenExists()
    {
        var (scope, tenantId, sut) = await SetupAsync();
        using (scope)
        {
            var claimId = await sut.SubmitClaimAsync(tenantId, DefaultClaimRequest());

            var dto = await sut.GetClaimAsync(claimId);

            dto.Should().NotBeNull();
            dto!.Id.Should().Be(claimId);
            dto.TenantName.Should().Be("Cafe ABC");
        }
    }

    [Fact(DisplayName = "GetClaimAsync_ReturnsNull_WhenNotFound")]
    public async Task GetClaimAsync_ReturnsNull_WhenNotFound()
    {
        var (scope, _, sut) = await SetupAsync();
        using (scope)
        {
            var dto = await sut.GetClaimAsync(Guid.NewGuid());
            dto.Should().BeNull();
        }
    }
}
