using VanAn.CoreHub.Services.Onboarding;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Core.Tests.Services.Onboarding
{
    /// <summary>
    /// Unit tests for Wave 1 onboarding DTOs.
    /// Verifies immutability (record), shape, and default/sentinel values.
    /// </summary>
    public class OnboardingDtoTests
    {
        // ── OnboardTenantRequest ─────────────────────────────────────────────────

        [Fact]
        public void OnboardTenantRequest_ShouldHold_AllProperties()
        {
            var req = new OnboardTenantRequest(
                Name: "Quán Vạn An",
                BusinessType: BusinessType.HouseholdBusiness,
                HKDGroup: HKDGroup.Group1,
                ContactEmail: "owner@vanan.vn",
                ContactPhone: "0901234567",
                Address: "123 Nguyễn Huệ, Q1, TP.HCM",
                TaxCode: "0123456789",
                IndustryCode: "F&B",
                OwnerUsername: "owner@vanan.vn",
                OwnerPassword: "Secret@123",
                OwnerDisplayName: "Nguyễn Văn A");

            Assert.Equal("Quán Vạn An", req.Name);
            Assert.Equal(BusinessType.HouseholdBusiness, req.BusinessType);
            Assert.Equal(HKDGroup.Group1, req.HKDGroup);
            Assert.Equal("owner@vanan.vn", req.ContactEmail);
            Assert.Equal("0901234567", req.ContactPhone);
            Assert.Equal("123 Nguyễn Huệ, Q1, TP.HCM", req.Address);
            Assert.Equal("0123456789", req.TaxCode);
            Assert.Equal("F&B", req.IndustryCode);
            Assert.Equal("owner@vanan.vn", req.OwnerUsername);
            Assert.Equal("Secret@123", req.OwnerPassword);
            Assert.Equal("Nguyễn Văn A", req.OwnerDisplayName);
        }

        [Fact]
        public void OnboardTenantRequest_NullableFields_ShouldAllowNull()
        {
            var req = new OnboardTenantRequest(
                Name: "MinimalTenant",
                BusinessType: BusinessType.Company,
                HKDGroup: null,
                ContactEmail: null,
                ContactPhone: null,
                Address: null,
                TaxCode: null,
                IndustryCode: "SPA",
                OwnerUsername: "admin",
                OwnerPassword: "pass",
                OwnerDisplayName: "Admin");

            Assert.Null(req.HKDGroup);
            Assert.Null(req.ContactEmail);
            Assert.Null(req.ContactPhone);
            Assert.Null(req.Address);
            Assert.Null(req.TaxCode);
        }

        [Fact]
        public void OnboardTenantRequest_IsRecord_SupportsValueEquality()
        {
            var req1 = new OnboardTenantRequest("T", BusinessType.Company, null, null, null, null, null, "F&B", "u", "p", "d");
            var req2 = new OnboardTenantRequest("T", BusinessType.Company, null, null, null, null, null, "F&B", "u", "p", "d");
            Assert.Equal(req1, req2);
        }

        // ── TenantOnboardingResult ───────────────────────────────────────────────

        [Fact]
        public void TenantOnboardingResult_ShouldHold_AllProperties()
        {
            var tenantId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var warnings = new List<string> { "Some warning" };

            var result = new TenantOnboardingResult(
                TenantId: tenantId,
                OwnerUserId: ownerId,
                ProductsCreated: 10,
                IngredientsCreated: 20,
                RecipesCreated: 5,
                ShopsCreated: 1,
                PermissionGroupsCreated: 4,
                Warnings: warnings);

            Assert.Equal(tenantId, result.TenantId);
            Assert.Equal(ownerId, result.OwnerUserId);
            Assert.Equal(10, result.ProductsCreated);
            Assert.Equal(20, result.IngredientsCreated);
            Assert.Equal(5, result.RecipesCreated);
            Assert.Equal(1, result.ShopsCreated);
            Assert.Equal(4, result.PermissionGroupsCreated);
            Assert.Single(result.Warnings);
            Assert.Equal("Some warning", result.Warnings[0]);
        }

        [Fact]
        public void TenantOnboardingResult_TenantId_ShouldNotBeEmpty()
        {
            var result = new TenantOnboardingResult(
                Guid.NewGuid(), Guid.NewGuid(), 0, 0, 0, 0, 0, []);
            Assert.NotEqual(Guid.Empty, result.TenantId);
        }

        [Fact]
        public void TenantOnboardingResult_IsRecord_SupportsValueEquality()
        {
            var id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var r1 = new TenantOnboardingResult(id1, id2, 1, 2, 3, 1, 4, []);
            var r2 = new TenantOnboardingResult(id1, id2, 1, 2, 3, 1, 4, []);
            Assert.Equal(r1, r2);
        }

        // ── IndustrySeedResult ───────────────────────────────────────────────────

        [Fact]
        public void IndustrySeedResult_Empty_ShouldHaveZeroCounts()
        {
            var empty = IndustrySeedResult.Empty;
            Assert.Equal(0, empty.ProductsCreated);
            Assert.Equal(0, empty.IngredientsCreated);
            Assert.Equal(0, empty.RecipesCreated);
            Assert.Equal(0, empty.ShopsCreated);
            Assert.Empty(empty.Warnings);
        }

        [Fact]
        public void IndustrySeedResult_ShouldHold_AllProperties()
        {
            var result = new IndustrySeedResult(
                ProductsCreated: 15,
                IngredientsCreated: 30,
                RecipesCreated: 8,
                ShopsCreated: 2,
                Warnings: ["W1", "W2"]);

            Assert.Equal(15, result.ProductsCreated);
            Assert.Equal(30, result.IngredientsCreated);
            Assert.Equal(8, result.RecipesCreated);
            Assert.Equal(2, result.ShopsCreated);
            Assert.Equal(2, result.Warnings.Count);
        }

        [Fact]
        public void IndustrySeedResult_IsRecord_SupportsValueEquality()
        {
            var r1 = new IndustrySeedResult(1, 2, 3, 1, []);
            var r2 = new IndustrySeedResult(1, 2, 3, 1, []);
            Assert.Equal(r1, r2);
        }
    }
}
