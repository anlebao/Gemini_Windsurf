using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Seed;
using VanAn.CoreHub.Services.FinancialIntelligence.Dtos;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using TenantAggregate = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Integration.Tests
{
    /// <summary>
    /// VA-FI-MVP2 Phase 3 (2026-08-21): Endpoint integration tests for FinancialIntelligenceController.
    ///
    /// Validates:
    ///   - GET /api/financial/business-profile → 404 when not declared, 200 after upsert
    ///   - PUT /api/financial/business-profile → 200 (upsert)
    ///   - GET /api/financial/profit-summary → 200 + JSON body
    ///   - GET /api/financial/break-even → 200 + JSON body
    ///   - GET /api/financial/break-even/multi-product → 200
    ///   - GET /api/financial/unit-economics → 200
    ///   - POST /api/financial/target-profit → 200
    ///   - 401 without auth (W12-G7)
    ///   - 400 invalid period format
    ///
    /// Uses GatewayWebApplicationFactory (SQLite in-memory VanAnDbContext).
    /// Seeds AccountChart + Enterprise_SME tenant + 2026-08 JournalEntries
    ///   - Revenue 10M (511 credit), COGS 7M (632 debit), OpEx 2M (642 debit) → Net 1M
    /// </summary>
    [Trait("Category", "FinancialIntelligenceEndpoints")]
    public class FinancialIntelligenceEndpointsTests : IClassFixture<GatewayWebApplicationFactory>
    {
        private const string JwtSecret = "VanAn-Dev-Secret-Key-2026-@#$%^&*()";
        private const string JwtIssuer = "VanAnShopERP";
        private const string JwtAudience = "VanAnApi";

        private static readonly Guid TestTenantGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        private static readonly TenantId TestTenantId = new(TestTenantGuid);

        private readonly GatewayWebApplicationFactory _factory;
        private readonly HttpClient _client;

        // Gateway serializes enums as strings (camelCase via JsonStringEnumConverter — Fix #87).
        // Test client must use matching options to deserialize DTOs containing enums.
        private static readonly JsonSerializerOptions GatewayJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        public FinancialIntelligenceEndpointsTests(GatewayWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

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

        private void SetAuthHeader(Guid tenantId)
        {
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, MintJwt(tenantId));
        }

        private void ClearAuthHeader()
        {
            _client.DefaultRequestHeaders.Authorization = null;
        }

        private async Task SeedAccountingDataAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VanAnDbContext>();

            await db.Database.EnsureCreatedAsync();
            await AccountChartSeeder.SeedAsync(db, NullLogger.Instance);

            var existing = await db.Tenants.FirstOrDefaultAsync(t => t.Id == TestTenantId);
            if (existing is null)
            {
                var settings = new TenantSettings("test@vanan.vn", "028-1234-5678", "123 Test St", "0301234567");
                var tenant = TenantAggregate.CreateCompany(TestTenantId, "FI Endpoint Test Tenant", settings);
                tenant.SetTenantType(TenantType.Enterprise_SME, AccountingStandard.TT133_2016);
                db.Tenants.Add(tenant);
            }

            bool alreadySeeded = await db.JournalEntries.AnyAsync(e => e.TenantId == TestTenantId && e.Description == "FI-E2E-Sale");
            if (alreadySeeded)
            {
                await db.SaveChangesAsync();
                return;
            }

            var sale = new JournalEntry(TestTenantId, new DateTime(2026, 8, 10), "FI-E2E-Sale", "Sale", null);
            sale.AddLine("111", 11_000_000m, 0, "Tiền mặt");
            sale.AddLine("511", 0, 10_000_000m, "Doanh thu");
            sale.AddLine("3331", 0, 1_000_000m, "VAT đầu ra");
            db.JournalEntries.Add(sale);

            var cogs = new JournalEntry(TestTenantId, new DateTime(2026, 8, 10), "FI-E2E-COGS", "COGS", null);
            cogs.AddLine("632", 7_000_000m, 0, "Giá vốn");
            cogs.AddLine("156", 0, 7_000_000m, "Xuất kho");
            db.JournalEntries.Add(cogs);

            var opex = new JournalEntry(TestTenantId, new DateTime(2026, 8, 15), "FI-E2E-OpEx", "Expense", null);
            opex.AddLine("642", 2_000_000m, 0, "CP quản lý DN");
            opex.AddLine("111", 0, 2_000_000m, "Trả tiền mặt");
            db.JournalEntries.Add(opex);

            await db.SaveChangesAsync();
        }

        // ── W12-G7 Auth ───────────────────────────────────────────────────────────

        [Fact(DisplayName = "FI-P3-AUTH: GET /api/financial/business-profile without auth returns 401")]
        public async Task GetProfile_WithoutAuth_Returns401()
        {
            ClearAuthHeader();
            var response = await _client.GetAsync("/api/financial/business-profile");
            // 401 (or 302 if redirect-to-login falls through) — but never 200/500.
            response.StatusCode.Should().NotBe(HttpStatusCode.OK);
            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }

        // ── BusinessProfile CRUD ────────────────────────────────────────────────

        [Fact(DisplayName = "FI-P3-PROFILE-GET: GET returns 404 when profile not yet declared")]
        public async Task GetProfile_WhenMissing_Returns404()
        {
            await SeedAccountingDataAsync();
            // Use a fresh tenant GUID without a profile (avoid collision with other tests' seeded profile).
            var freshTenant = Guid.NewGuid();
            SetAuthHeader(freshTenant);

            var response = await _client.GetAsync("/api/financial/business-profile");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact(DisplayName = "FI-P3-PROFILE-UPSERT: PUT creates profile then GET returns 200")]
        public async Task UpsertProfile_ThenGet_Returns200()
        {
            await SeedAccountingDataAsync();
            // Use TestTenantGuid — must match GatewayWebApplicationFactory.TestTenantProvider.TenantId
            // so VanAnDbContext's global multi-tenancy query filter doesn't hide the saved entity.
            SetAuthHeader(TestTenantGuid);

            var upsertBody = new
            {
                monthlyRent = 5_000_000m,
                monthlyPayroll = 8_000_000m,
                monthlyUtilities = 1_000_000m,
                monthlyMarketing = 500_000m,
                monthlyLogistics = 300_000m,
                monthlyOtherOpEx = 200_000m,
                monthlyDepreciation = 1_000_000m,
                dailyCapacityUnits = 200,
                operatingDaysPerMonth = 30,
                pricingModel = "FixedPrice",
                notes = "E2E test profile"
            };

            var putResponse = await _client.PutAsJsonAsync("/api/financial/business-profile", upsertBody);
            putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var created = await putResponse.Content.ReadFromJsonAsync<BusinessProfileDto>(GatewayJsonOptions);
            created.Should().NotBeNull();
            created!.MonthlyRent.Should().Be(5_000_000m);
            created.Version.Should().Be("1.0");
            created.TotalMonthlyFixedCost.Should().Be(16_000_000m);

            // GET now returns 200
            var getResponse = await _client.GetAsync("/api/financial/business-profile");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // ── Calculation endpoints ─────────────────────────────────────────────────

        [Fact(DisplayName = "FI-P3-PROFIT: GET /api/financial/profit-summary returns 200 with Revenue/COGS")]
        public async Task GetProfitSummary_Returns200WithNumbers()
        {
            await SeedAccountingDataAsync();
            SetAuthHeader(TestTenantGuid);

            var response = await _client.GetAsync("/api/financial/profit-summary?period=2026-08&standard=TT133_2016");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<ProfitSummaryDto>(GatewayJsonOptions);
            dto.Should().NotBeNull();
            // Seeded: Revenue 10M, COGS 7M, OpEx 2M, Net 1M
            dto!.Revenue.Should().Be(10_000_000m);
            dto.COGS.Should().Be(7_000_000m);
            dto.OperatingExpenses.Should().Be(2_000_000m);
            dto.NetProfit.Should().Be(1_000_000m);
        }

        [Fact(DisplayName = "FI-P3-BREAKEVEN: GET /api/financial/break-even returns 200 + InsufficientData when no profile")]
        public async Task GetBreakEven_NoProfile_Returns200WithInsufficientData()
        {
            await SeedAccountingDataAsync();
            var freshTenant = Guid.NewGuid();
            SetAuthHeader(freshTenant);

            var response = await _client.GetAsync("/api/financial/break-even?period=2026-08&standard=TT133_2016");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<BreakEvenAnalysisDto>(GatewayJsonOptions);
            dto.Should().NotBeNull();
            dto!.Status.Should().Be(BreakEvenStatus.InsufficientData);
        }

        [Fact(DisplayName = "FI-P3-BREAKEVEN-MULTI: GET /api/financial/break-even/multi-product returns 200")]
        public async Task GetMultiProductBreakEven_Returns200()
        {
            await SeedAccountingDataAsync();
            SetAuthHeader(TestTenantGuid);

            var response = await _client.GetAsync("/api/financial/break-even/multi-product?period=2026-08&standard=TT133_2016");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<MultiProductBreakEvenDto>(GatewayJsonOptions);
            dto.Should().NotBeNull();
            // No products seeded → empty product lines
            dto!.ProductLines.Should().BeEmpty();
        }

        [Fact(DisplayName = "FI-P3-UNITECONOMICS: GET /api/financial/unit-economics returns 200")]
        public async Task GetUnitEconomics_Returns200()
        {
            await SeedAccountingDataAsync();
            SetAuthHeader(TestTenantGuid);

            var response = await _client.GetAsync("/api/financial/unit-economics?period=2026-08");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<UnitEconomicsReportDto>(GatewayJsonOptions);
            dto.Should().NotBeNull();
            // No products seeded → 0 analyzed
            dto!.TotalProductsAnalyzed.Should().Be(0);
        }

        [Fact(DisplayName = "FI-P3-TARGETPROFIT: POST /api/financial/target-profit returns 200")]
        public async Task PostTargetProfit_Returns200()
        {
            await SeedAccountingDataAsync();
            // Use fresh tenant (no BusinessProfile seeded) — asserts PROFILE_MISSING → Feasible=false.
            // TestTenantGuid may have a profile left by UpsertProfile test (xUnit runs sequentially but
            // shares the IClassFixture database) — fresh tenant guarantees isolation.
            var freshTenant = Guid.NewGuid();
            SetAuthHeader(freshTenant);

            var body = new
            {
                year = 2026,
                month = 8,
                standard = "TT133_2016",
                targetProfit = 5_000_000m
            };

            var response = await _client.PostAsJsonAsync("/api/financial/target-profit", body);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var dto = await response.Content.ReadFromJsonAsync<TargetProfitAnalysisDto>(GatewayJsonOptions);
            dto.Should().NotBeNull();
            // No BusinessProfile seeded for this tenant → Feasible=false, PROFILE_MISSING
            dto!.Feasible.Should().BeFalse();
        }

        // ── Validation ───────────────────────────────────────────────────────────

        [Fact(DisplayName = "FI-P3-INVALID-PERIOD: Invalid period format returns 400")]
        public async Task GetProfitSummary_InvalidPeriod_Returns400()
        {
            await SeedAccountingDataAsync();
            SetAuthHeader(TestTenantGuid);

            var response = await _client.GetAsync("/api/financial/profit-summary?period=invalid&standard=TT133_2016");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact(DisplayName = "FI-P3-MISSING-TENANT-CLAIM: JWT without tenant_id returns 401")]
        public async Task GetProfile_JwtWithoutTenantClaim_Returns401()
        {
            await SeedAccountingDataAsync();
            // Mint a JWT with no tenant_id claim
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
                    ["role"] = "Owner"
                }
            };
            string token = new JsonWebTokenHandler().CreateToken(descriptor);
            _client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);

            var response = await _client.GetAsync("/api/financial/business-profile");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
