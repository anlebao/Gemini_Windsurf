using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Infrastructure;
using VanAn.Integration.Tests.Infrastructure;
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;
using Xunit;

namespace VanAn.Integration.Tests.KhachLink
{
    /// <summary>
    /// Directory "Tìm hiểu" redirect: tests for TenantStoreController Search endpoint
    /// verifying KhachLinkDomain field is populated from KhachLinkInstance data.
    /// Uses GatewayWebApplicationFactory (SQLite in-memory, EnsureCreated schema).
    /// </summary>
    [Trait("Category", "Integration")]
    public class TenantStoreSearchTests : IClassFixture<GatewayWebApplicationFactory>
    {
        private readonly GatewayWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public TenantStoreSearchTests(GatewayWebApplicationFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        /// <summary>Seed a Tenant + optional KhachLinkInstance directly into the test DB.</summary>
        private async Task<Tenant> SeedTenantAsync(string name, string? slug = null)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();

            var tenantId = new TenantId(Guid.NewGuid());
            var settings = slug != null
                ? TenantSettings.Empty().WithSlug(slug)
                : TenantSettings.Empty();
            var tenant = Tenant.CreateCompany(tenantId, name, settings);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            return tenant;
        }

        private async Task SeedKhachLinkInstanceAsync(Guid ownerTenantId, KhachLinkProfile profile, string domain)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IVanAnDbContext>();
            var instance = new KhachLinkInstance(
                $"Test-{profile}",
                profile,
                domain,
                ownerTenantId);
            db.KhachLinkInstances.Add(instance);
            await db.SaveChangesAsync();
        }

        [Fact(DisplayName = "TSS-1: Search returns KhachLinkDomain when tenant has FullCommerce instance")]
        public async Task Search_ReturnsKhachLinkDomain_WhenTenantHasInstance()
        {
            // Arrange
            var tenant = await SeedTenantAsync("Test Bakery", "test-bakery");
            await SeedKhachLinkInstanceAsync(tenant.Id.Value, KhachLinkProfile.FullCommerce, "testbakery.khachvip.online");

            // Act — no name param to avoid EF.Functions.ILike (PostgreSQL-only, fails on SQLite test DB)
            var response = await _client.GetAsync("/api/tenants/search");

            // Assert — Issue #166: response is now TenantSearchResultDto wrapper
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<SearchResultWrapper>();
            Assert.NotNull(result);
            var match = result!.Results.FirstOrDefault(s => s.Id == tenant.Id.Value);
            Assert.NotNull(match);
            Assert.Equal("testbakery.khachvip.online", match!.KhachLinkDomain);
        }

        [Fact(DisplayName = "TSS-2: Search returns null KhachLinkDomain when tenant has no instance")]
        public async Task Search_ReturnsNullKhachLinkDomain_WhenTenantHasNoInstance()
        {
            // Arrange
            var tenant = await SeedTenantAsync("Solo Shop", "solo-shop");

            // Act — no name param to avoid EF.Functions.ILike (PostgreSQL-only, fails on SQLite test DB)
            var response = await _client.GetAsync("/api/tenants/search");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<SearchResultWrapper>();
            Assert.NotNull(result);
            var match = result!.Results.FirstOrDefault(s => s.Id == tenant.Id.Value);
            Assert.NotNull(match);
            Assert.Null(match!.KhachLinkDomain);
        }

        [Fact(DisplayName = "TSS-3: Search returns null KhachLinkDomain when instance is Directory profile")]
        public async Task Search_ReturnsNullKhachLinkDomain_WhenInstanceIsDirectory()
        {
            // Arrange
            var tenant = await SeedTenantAsync("Dir Tenant", "dir-tenant");
            await SeedKhachLinkInstanceAsync(tenant.Id.Value, KhachLinkProfile.Directory, "dir.khachvip.online");

            // Act — no name param to avoid EF.Functions.ILike (PostgreSQL-only, fails on SQLite test DB)
            var response = await _client.GetAsync("/api/tenants/search");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<SearchResultWrapper>();
            Assert.NotNull(result);
            var match = result!.Results.FirstOrDefault(s => s.Id == tenant.Id.Value);
            Assert.NotNull(match);
            Assert.Null(match!.KhachLinkDomain);
        }

        // Issue #166 comment: Gateway now returns wrapper { Results, SuggestedKeywords, MatchStrategy }
        private class SearchResultWrapper
        {
            public List<StoreSearchResult> Results { get; set; } = new();
            public List<string> SuggestedKeywords { get; set; } = new();
            public string MatchStrategy { get; set; } = "all";
        }

        private class StoreSearchResult
        {
            public Guid Id { get; set; }
            public string? KhachLinkDomain { get; set; }
        }
    }
}
