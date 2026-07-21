using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.UserAggregate;
using Xunit;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.Integration.Tests;

/// <summary>
/// Integration tests for tenant onboarding end-to-end flow.
///
/// Phase 3.6: Product seeding removed from onboarding — tests updated to verify
/// tenant + owner + permission groups only (no seed data in DB).
///
/// Validates:
///   - SC1: SystemAdmin POST creates tenant with 201
///   - SC2: Tenant exists in database
///   - SC3: Owner user exists with Owner role and BCrypt password hash
///   - SC4: Default permission groups created (was: F&B seed data)
///   - SC5: Owner assigned to "Quản lý" permission group
///   - SC6: Result warnings include QuickSetup-deferred notice
///
/// Uses GatewayWebApplicationFactory (SQLite in-memory). Database queries bypass
/// the global TenantId query filter because the test tenant provider returns a
/// fixed test tenant ID, while onboarding creates a brand-new tenant.
/// </summary>
[Trait("Category", "TenantOnboarding")]
public class TenantOnboardingIntegrationTests : IClassFixture<GatewayWebApplicationFactory>
{
    private const string EndpointUrl = "/api/v1/onboarding/tenants";

    // JWT settings must match Gateway appsettings.Development.json
    private const string JwtSecret = "VanAn-Dev-Secret-Key-2026-@#$%^&*()";
    private const string JwtIssuer = "VanAnShopERP";
    private const string JwtAudience = "VanAnApi";

    private readonly GatewayWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TenantOnboardingIntegrationTests(GatewayWebApplicationFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string MintSystemAdminJwt()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = creds,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["role"] = "SystemAdmin",
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static OnboardTenantRequest BuildValidRequest(string suffix = "") => new(
        Name: $"Vạn An F&B Test{suffix}",
        BusinessType: BusinessType.HouseholdBusiness,
        HKDGroup: HKDGroup.Group1,
        ContactEmail: $"test{suffix}@vanan.vn",
        ContactPhone: "0901234567",
        Address: "123 Test Street",
        TaxCode: "1234567890",
        IndustryCode: "F&B",
        OwnerUsername: $"owner{suffix}{Guid.NewGuid():N}@test.vn",
        OwnerPassword: "Password123!",
        OwnerDisplayName: $"Chủ Quán Test{suffix}");

    // ── Full Flow Test ────────────────────────────────────────────────────────

    [Fact(DisplayName = "Phase3.6: Full onboarding creates tenant, owner, permission groups (no seed data)")]
    public async Task Onboard_Creates_Tenant_Owner_PermissionGroups()
    {
        // Arrange
        var request = BuildValidRequest();
        var token = MintSystemAdminJwt();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);

        // Act
        var response = await _client.PostAsJsonAsync(EndpointUrl, request);

        // Assert HTTP
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TenantOnboardingResult>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.TenantId);
        Assert.NotEqual(Guid.Empty, result.OwnerUserId);
        // Phase 3.6: Seed counts are 0 (seeding deferred to QuickSetup)
        Assert.Equal(0, result.ProductsCreated);
        Assert.Equal(0, result.IngredientsCreated);
        Assert.Equal(0, result.RecipesCreated);
        Assert.Equal(0, result.ShopsCreated);
        Assert.Equal(4, result.PermissionGroupsCreated);
        // Phase 3.6: Warnings include QuickSetup-deferred notice
        Assert.NotEmpty(result.Warnings);

        var tenantIdGuid = result.TenantId;

        // Assert database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();

        // SC2: Tenant exists
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == new TenantId(tenantIdGuid));
        Assert.NotNull(tenant);
        Assert.Equal(request.Name, tenant.Name);
        Assert.Equal(request.BusinessType, tenant.BusinessType);

        // SC3: Owner user exists, active, has Owner role, and BCrypt password is valid
        var owner = await db.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == result.OwnerUserId);
        Assert.NotNull(owner);
        Assert.Equal(request.OwnerUsername, owner.Username);
        Assert.Equal(request.OwnerDisplayName, owner.DisplayName);
        Assert.Equal(UserRole.Owner, owner.Role);
        Assert.True(owner.IsActive);
        Assert.True(BCrypt.Net.BCrypt.Verify(request.OwnerPassword, owner.PasswordHash),
            "Owner password hash should be verifiable with the plain password supplied in the request.");

        // Owner role assignment (UserTenant mapping)
        var userTenant = await db.UserTenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(ut => ut.UserId == owner.Id && ut.TenantId == new TenantId(tenantIdGuid) && ut.IsActive);
        Assert.NotNull(userTenant);
        Assert.Equal(UserRole.Owner, userTenant.Role);

        // SC4 (Phase 3.6): NO seed data created for the new tenant (seeding deferred to QuickSetup)
        // Shop entity removed 2026-07-21 — no Shops table to check.
        var productCount = await db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(p => p.TenantId == new TenantId(tenantIdGuid));
        Assert.Equal(0, productCount);

        var ingredientCount = await db.Ingredients
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(i => i.TenantId == new TenantId(tenantIdGuid));
        Assert.Equal(0, ingredientCount);

        var recipeCount = await db.Recipes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(r => r.TenantId == new TenantId(tenantIdGuid));
        Assert.Equal(0, recipeCount);

        // SC5: Default permission groups created
        var groups = await db.PermissionGroups
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(g => g.TenantId == new TenantId(tenantIdGuid))
            .ToListAsync();
        Assert.Equal(4, groups.Count);
        Assert.Contains(groups, g => g.Name == "Quản lý");
        Assert.Contains(groups, g => g.Name == "Thu ngân");
        Assert.Contains(groups, g => g.Name == "Bếp");
        Assert.Contains(groups, g => g.Name == "Kho");

        // SC6: Owner assigned to "Quản lý" group
        var quanLyGroup = groups.Single(g => g.Name == "Quản lý");
        var ownerGroupAssignment = await db.UserPermissionGroups
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(upg => upg.UserId == owner.Id && upg.GroupId == quanLyGroup.Id && upg.IsActive);
        Assert.NotNull(ownerGroupAssignment);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    [Fact(DisplayName = "Phase3.6: Onboarding is isolated per tenant (two tenants get separate tenants/owners)")]
    public async Task Onboard_TwoTenants_TenantsAreIsolated()
    {
        var token = MintSystemAdminJwt();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);

        var response1 = await _client.PostAsJsonAsync(EndpointUrl, BuildValidRequest("A"));
        var response2 = await _client.PostAsJsonAsync(EndpointUrl, BuildValidRequest("B"));

        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, response2.StatusCode);

        var result1 = await response1.Content.ReadFromJsonAsync<TenantOnboardingResult>();
        var result2 = await response2.Content.ReadFromJsonAsync<TenantOnboardingResult>();
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1.TenantId, result2.TenantId);
        Assert.NotEqual(result1.OwnerUserId, result2.OwnerUserId);

        // Phase 3.6: No products seeded (seeding deferred to QuickSetup)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
        var tenant1ProductCount = await db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(p => p.TenantId == new TenantId(result1.TenantId));
        var tenant2ProductCount = await db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(p => p.TenantId == new TenantId(result2.TenantId));

        Assert.Equal(0, tenant1ProductCount);
        Assert.Equal(0, tenant2ProductCount);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
