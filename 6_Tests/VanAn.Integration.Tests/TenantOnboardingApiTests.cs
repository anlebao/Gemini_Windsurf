using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VanAn.CoreHub.Services.Onboarding;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using Xunit;

namespace VanAn.Integration.Tests;

/// <summary>
/// Integration tests for Wave 4: POST /api/v1/onboarding/tenants endpoint.
///
/// Tests validate:
///   - SC1: Endpoint is available (not 404)
///   - SC2: Unauthenticated request returns 401/302 (auth challenge)
///   - SC3: Non-SystemAdmin JWT returns 403
///   - SC4: SystemAdmin JWT with valid request returns 201 + TenantOnboardingResult
///   - SC5: Invalid request body returns 400
///
/// Uses GatewayWebApplicationFactory (SQLite in-memory VanAnDbContext).
/// JWT tokens are minted locally using the same Dev secret from appsettings.Development.json.
/// </summary>
[Trait("Category", "TenantOnboarding")]
public class TenantOnboardingApiTests : IClassFixture<GatewayWebApplicationFactory>
{
    private const string EndpointUrl = "/api/v1/onboarding/tenants";

    // JWT settings must match Gateway appsettings.Development.json
    private const string JwtSecret = "VanAn-Dev-Secret-Key-2026-@#$%^&*()";
    private const string JwtIssuer = "VanAnShopERP";
    private const string JwtAudience = "VanAnApi";

    private readonly GatewayWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TenantOnboardingApiTests(GatewayWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Mint a short-lived JWT with the given role (same signing key as Gateway).
    ///
    /// Uses <see cref="JsonWebTokenHandler"/> (NOT the old JwtSecurityTokenHandler) to avoid
    /// DefaultOutboundClaimTypeMap transforming short claim names like "role" into long Microsoft
    /// schema URLs (e.g. http://schemas.microsoft.com/ws/2008/06/identity/claims/role).
    /// Gateway is configured with RoleClaimType="role" — the JWT payload must contain exactly "role".
    /// </summary>
    private static string MintJwt(string role, Guid? tenantId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = creds,
            // Claims dict is serialized as-is — no DefaultOutboundClaimTypeMap transformation
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["role"] = role,
            }
        };

        if (tenantId.HasValue)
            descriptor.Claims["tenant_id"] = tenantId.Value.ToString();

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static OnboardTenantRequest BuildValidRequest(string suffix = "") => new(
        Name: $"Test Tenant{suffix}",
        BusinessType: BusinessType.HouseholdBusiness,
        HKDGroup: null,
        ContactEmail: $"owner{suffix}@test.vn",
        ContactPhone: "0901234567",
        Address: "123 Test Street",
        TaxCode: null,
        IndustryCode: "F&B",
        OwnerUsername: $"owner{suffix}{Guid.NewGuid():N}",
        OwnerPassword: "Test@123456",
        OwnerDisplayName: $"Owner{suffix}");

    // ── SC2: No auth → 401/302 ────────────────────────────────────────────────

    [Fact(DisplayName = "W4-SC2: POST /tenants without auth returns 401/302 (not 200 or 500)")]
    public async Task CreateTenantOnboarding_NoAuth_Returns401Or302()
    {
        var response = await _client.PostAsJsonAsync(EndpointUrl, BuildValidRequest());

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Redirect,
            $"Expected 401 or 302 (auth challenge), got {(int)response.StatusCode}");
    }

    // ── SC3: Non-SystemAdmin JWT → 403 ───────────────────────────────────────

    [Fact(DisplayName = "W4-SC3: POST /tenants with Owner JWT returns 403 Forbidden")]
    public async Task CreateTenantOnboarding_OwnerRole_Returns403()
    {
        var token = MintJwt(role: "Owner", tenantId: Guid.NewGuid());
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);

        var response = await _client.PostAsJsonAsync(EndpointUrl, BuildValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    // ── SC4: SystemAdmin JWT + valid request → 201 ───────────────────────────

    [Fact(DisplayName = "W4-SC4: POST /tenants with SystemAdmin JWT returns 201 + TenantOnboardingResult")]
    public async Task CreateTenantOnboarding_SystemAdminRole_Returns201WithResult()
    {
        var token = MintJwt(role: "SystemAdmin");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);

        var request = BuildValidRequest();
        var response = await _client.PostAsJsonAsync(EndpointUrl, request);

        // Expect 201 Created
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<TenantOnboardingResult>();
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.TenantId);
        Assert.NotEqual(Guid.Empty, result.OwnerUserId);
        Assert.Equal(4, result.PermissionGroupsCreated);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    // ── SC5: Invalid body → 400 ───────────────────────────────────────────────

    [Fact(DisplayName = "W4-SC5: POST /tenants with unknown IndustryCode returns 400")]
    public async Task CreateTenantOnboarding_UnknownIndustryCode_Returns400()
    {
        var token = MintJwt(role: "SystemAdmin");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);

        var request = BuildValidRequest() with { IndustryCode = "UNKNOWN_INDUSTRY" };
        var response = await _client.PostAsJsonAsync(EndpointUrl, request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;
    }
}
