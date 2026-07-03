using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VanAn.CoreHub.Infrastructure;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using VanAn.Shared.DTOs;
using Xunit;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Integration.Tests;

/// <summary>
/// Integration tests for Wave 7 — GET /api/hkd-books endpoints.
///
/// Tests validate:
///   - SC2: GET /api/hkd-books/{templateCode} returns HKDBookDto with NumericValues
///   - SC3: GET /api/hkd-books returns list of available templates
///   - SC5: Endpoint returns 200 with populated NumericValues (not empty)
///   - Multi-tenancy: unauthenticated request returns 401/302
///
/// Uses GatewayWebApplicationFactory (SQLite in-memory VanAnDbContext).
/// Seeds a Group1 tenant + accounting entries, mints JWT with tenant_id claim.
/// </summary>
[Trait("Category", "HKDBooks")]
public class HKDBooksEndpointTests : IClassFixture<GatewayWebApplicationFactory>
{
    // JWT settings must match Gateway appsettings.Development.json
    private const string JwtSecret = "VanAn-Dev-Secret-Key-2026-@#$%^&*()";
    private const string JwtIssuer = "VanAnShopERP";
    private const string JwtAudience = "VanAnApi";

    // Must match GatewayWebApplicationFactory.TestTenantProvider.TestTenantId
    // so the VanAnDbContext multi-tenancy query filter doesn't hide our seeded data.
    private static readonly Guid TestTenantGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
    private static readonly TenantId TestTenantId = new(TestTenantGuid);

    private readonly GatewayWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public HKDBooksEndpointTests(GatewayWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Mint a JWT with Owner role + tenant_id claim (same pattern as TenantOnboardingApiTests).
    /// </summary>
    private static string MintJwt(Guid tenantId)
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
                ["role"] = "Owner",
                ["tenant_id"] = tenantId.ToString()
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>
    /// Seed a Group1 tenant + revenue/expense entries for period 2026/06.
    /// S1a_HKD template calculates TotalRevenue = SUM_ACCOUNT("5", "Credit") and
    /// TotalExpense = SUM_ACCOUNT("6", "Debit").
    /// </summary>
    private async Task SeedGroup1TenantWithEntriesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();

        // Ensure schema exists (factory already calls EnsureCreated, but be safe)
        await db.Database.EnsureCreatedAsync();

        // Check if tenant already exists (test isolation — factory is shared)
        var existing = await db.Tenants.FirstOrDefaultAsync(t => t.Id == TestTenantId);
        if (existing != null) return;

        // Seed Group1 tenant (S1a_HKD is for Group1)
        var tenant = TenantAggregate.CreateHouseholdBusiness(TestTenantId, "Wave7 Test Tenant", HKDGroup.Group1);
        db.Tenants.Add(tenant);

        // Seed revenue entries (accountCode "511" → starts with "5" → Credit side)
        var period = new AccountingPeriod(2026, 6);
        var revenue1 = AccountingEntry.CreateRevenue(
            TestTenantId, period, new Money(1000m), "Revenue entry 1", accountCode: "511");
        var revenue2 = AccountingEntry.CreateRevenue(
            TestTenantId, period, new Money(500m), "Revenue entry 2", accountCode: "511");

        // Seed expense entries (accountCode "621" → starts with "6" → Debit side)
        var expense1 = AccountingEntry.CreateExpense(
            TestTenantId, period, new Money(400m), "Expense entry 1", accountCode: "621");
        var expense2 = AccountingEntry.CreateExpense(
            TestTenantId, period, new Money(100m), "Expense entry 2", accountCode: "621");

        db.AccountingEntries.AddRange(revenue1, revenue2, expense1, expense2);
        await db.SaveChangesAsync();
    }

    private void SetAuthHeader(Guid tenantId)
    {
        var token = MintJwt(tenantId);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
    }

    private void ClearAuthHeader()
    {
        _client.DefaultRequestHeaders.Authorization = null;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "W7-SC2/SC5: GET /api/hkd-books/S1a_HKD returns 200 with NumericValues populated")]
    public async Task GetHkdBook_S1a_ReturnsBookWithNumericValues()
    {
        // Arrange — seed data + auth
        await SeedGroup1TenantWithEntriesAsync();
        SetAuthHeader(TestTenantGuid);

        try
        {
            // Act — generate S1a_HKD book for period 2026/06
            var response = await _client.GetAsync("/api/hkd-books/S1a_HKD?year=2026&month=6");

            // Assert — 200 + NumericValues populated
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Assert.Fail($"Expected OK, got {response.StatusCode}. Response: {errorContent}");
            }
            var book = await response.Content.ReadFromJsonAsync<HKDBookDto>();
            Assert.NotNull(book);
            Assert.Equal("S1a_HKD", book!.BookTypeCode);
            Assert.Equal(2026, book.Year);
            Assert.Equal(6, book.Month);
            Assert.NotEmpty(book.NumericValues);
            // TotalRevenue should be 1500 (1000 + 500), TotalExpense should be 500 (400 + 100)
            Assert.True(book.NumericValues.ContainsKey("TotalRevenue"),
                "NumericValues should contain TotalRevenue");
            Assert.Equal(1500m, book.NumericValues["TotalRevenue"]);
            Assert.True(book.NumericValues.ContainsKey("TotalExpense"),
                "NumericValues should contain TotalExpense");
            Assert.Equal(500m, book.NumericValues["TotalExpense"]);
        }
        finally
        {
            ClearAuthHeader();
        }
    }

    [Fact(DisplayName = "W7-SC3: GET /api/hkd-books returns list of available templates")]
    public async Task GetHkdBooks_ReturnsAvailableTemplates()
    {
        // Arrange
        await SeedGroup1TenantWithEntriesAsync();
        SetAuthHeader(TestTenantGuid);

        try
        {
            // Act
            var response = await _client.GetAsync("/api/hkd-books");

            // Assert — 200 + list contains S1a_HKD (Group1 tenant)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var templates = await response.Content.ReadFromJsonAsync<List<HKDBookTemplateDto>>();
            Assert.NotNull(templates);
            Assert.NotEmpty(templates!);
            Assert.Contains(templates!, t => t.TemplateCode == "S1a_HKD");
        }
        finally
        {
            ClearAuthHeader();
        }
    }

    [Fact(DisplayName = "W7: GET /api/hkd-books without auth returns 401/302 (not 200 or 500)")]
    public async Task GetHkdBooks_NoAuth_Returns401Or302()
    {
        ClearAuthHeader();
        var response = await _client.GetAsync("/api/hkd-books");

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized ||
            response.StatusCode == HttpStatusCode.Redirect,
            $"Expected 401 or 302 (auth challenge), got {(int)response.StatusCode}");
    }

    [Fact(DisplayName = "W7: GET /api/hkd-books/{templateCode} with invalid period returns 400")]
    public async Task GetHkdBook_InvalidPeriod_Returns400()
    {
        await SeedGroup1TenantWithEntriesAsync();
        SetAuthHeader(TestTenantGuid);

        try
        {
            var response = await _client.GetAsync("/api/hkd-books/S1a_HKD?year=1999&month=6");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            ClearAuthHeader();
        }
    }
}
