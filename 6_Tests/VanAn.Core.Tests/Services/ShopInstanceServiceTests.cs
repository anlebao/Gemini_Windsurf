using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;
using Xunit;
using Tenant = VanAn.Shared.Domain.Aggregates.TenantAggregate.Tenant;

namespace VanAn.Core.Tests.Services
{
    /// <summary>
    /// Phase 2 (Multi-VPS Checkout): Unit tests for ShopInstanceService.
    /// Uses real VanAnDbContext with SQLite in-memory (supports async EF operations).
    /// Verifies CRUD, validation, health check, and tenant count.
    /// </summary>
    public class ShopInstanceServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly VanAnDbContext _dbContext;
        private readonly ShopInstanceService _service;

        public ShopInstanceServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<VanAnDbContext>()
                .UseSqlite(_connection)
                .Options;
            _dbContext = new VanAnDbContext(options);
            _dbContext.Database.EnsureCreated();

            // Use a stub handler that returns 200 OK by default (health check tests override)
            _service = new ShopInstanceService(_dbContext, new HttpClient(new StubHandler(System.Net.HttpStatusCode.OK)), NullLogger<ShopInstanceService>.Instance);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            _connection?.Dispose();
        }

        /// <summary>Simple HttpMessageHandler stub returning a fixed status code or throwing.</summary>
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly Exception? _exception;

            public StubHandler(HttpStatusCode status) => _status = status;
            public StubHandler(Exception exception) => _exception = exception;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                if (_exception is not null)
                    return Task.FromException<HttpResponseMessage>(_exception);
                return Task.FromResult(new HttpResponseMessage(_status));
            }
        }

        private ShopInstanceService CreateServiceWithHttpHandler(StubHandler handler)
            => new(_dbContext, new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) }, NullLogger<ShopInstanceService>.Instance);

        [Fact]
        public async Task CreateAsync_WithValidInput_CreatesInstance()
        {
            var instance = await _service.CreateAsync("http://shoperp:5003", "VPS-1 HCM", 50, null);

            Assert.Equal("http://shoperp:5003", instance.BaseUrl);
            Assert.Equal("VPS-1 HCM", instance.Label);
            Assert.Equal(50, instance.MaxTenants);
            Assert.True(instance.IsActive);

            // Verify persisted
            var fromDb = await _dbContext.ShopInstances.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == instance.Id);
            Assert.NotNull(fromDb);
        }

        [Fact]
        public async Task CreateAsync_WithDuplicateBaseUrl_Throws()
        {
            await _service.CreateAsync("http://shoperp:5003", "Existing");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CreateAsync("http://shoperp:5003", "New Instance"));
        }

        [Fact]
        public async Task CreateAsync_WithInvalidUrl_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.CreateAsync("not-a-url", "VPS-1"));
        }

        [Fact]
        public async Task CreateAsync_WithNegativeMaxTenants_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.CreateAsync("http://shoperp:5003", "VPS-1", -1));
        }

        [Fact]
        public async Task GetByIdAsync_WithExistingId_ReturnsInstance()
        {
            var created = await _service.CreateAsync("http://shoperp:5003", "VPS-1");

            var result = await _service.GetByIdAsync(created.Id);

            Assert.NotNull(result);
            Assert.Equal("VPS-1", result!.Label);
        }

        [Fact]
        public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
        {
            var result = await _service.GetByIdAsync(Guid.NewGuid());

            Assert.Null(result);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllInstances()
        {
            await _service.CreateAsync("http://shoperp1:5003", "VPS-1");
            await _service.CreateAsync("http://shoperp2:5003", "VPS-2");

            var result = await _service.GetAllAsync();

            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetActiveAsync_ReturnsOnlyActiveInstances()
        {
            var active = await _service.CreateAsync("http://shoperp1:5003", "VPS-1");
            var inactive = await _service.CreateAsync("http://shoperp2:5003", "VPS-2");
            await _service.SetActiveAsync(inactive.Id, false);

            var result = await _service.GetActiveAsync();

            Assert.Single(result);
            Assert.Equal("VPS-1", result[0].Label);
        }

        [Fact]
        public async Task UpdateAsync_WithExistingId_UpdatesAndReturnsTrue()
        {
            var created = await _service.CreateAsync("http://shoperp:5003", "VPS-1");

            var result = await _service.UpdateAsync(created.Id, "VPS-1 Updated", 100);

            Assert.True(result);
            // Re-fetch to verify persisted
            var fromDb = await _service.GetByIdAsync(created.Id);
            Assert.Equal("VPS-1 Updated", fromDb!.Label);
            Assert.Equal(100, fromDb.MaxTenants);
        }

        [Fact]
        public async Task UpdateAsync_WithNonExistentId_ReturnsFalse()
        {
            var result = await _service.UpdateAsync(Guid.NewGuid(), "New", 50);

            Assert.False(result);
        }

        [Fact]
        public async Task SetActiveAsync_TogglesFlag()
        {
            var created = await _service.CreateAsync("http://shoperp:5003", "VPS-1");
            Assert.True(created.IsActive);

            var result = await _service.SetActiveAsync(created.Id, false);

            Assert.True(result);
            var fromDb = await _service.GetByIdAsync(created.Id);
            Assert.False(fromDb!.IsActive);
        }

        [Fact]
        public async Task CountTenantsAsync_ReturnsCorrectCount()
        {
            var shopInstance = await _service.CreateAsync("http://shoperp:5003", "VPS-1");
            var tenant1 = Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "Tenant 1");
            tenant1.AssignToShopInstance(shopInstance.Id);
            var tenant2 = Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "Tenant 2");
            tenant2.AssignToShopInstance(shopInstance.Id);
            var tenant3 = Tenant.CreateCompany(new TenantId(Guid.NewGuid()), "Tenant 3"); // no ShopInstance

            _dbContext.Tenants.AddRange(tenant1, tenant2, tenant3);
            await _dbContext.SaveChangesAsync();

            var count = await _service.CountTenantsAsync(shopInstance.Id);

            Assert.Equal(2, count);
        }

        [Fact]
        public async Task CheckHealthAsync_WithHealthyEndpoint_UpdatesStatus()
        {
            var created = await _service.CreateAsync("http://shoperp:5003", "VPS-1");
            var service = CreateServiceWithHttpHandler(new StubHandler(HttpStatusCode.OK));

            var result = await service.CheckHealthAsync(created.Id);

            Assert.Equal("Healthy", result.Status);
            var fromDb = await _service.GetByIdAsync(created.Id);
            Assert.Equal("Healthy", fromDb!.HealthStatus);
            Assert.NotNull(fromDb.LastHealthCheck);
        }

        [Fact]
        public async Task CheckHealthAsync_WithUnreachableEndpoint_SetsDownStatus()
        {
            var created = await _service.CreateAsync("http://nonexistent-host:9999", "VPS-Down");
            var service = CreateServiceWithHttpHandler(new StubHandler(new HttpRequestException("Connection refused")));

            var result = await service.CheckHealthAsync(created.Id);

            Assert.Equal("Down", result.Status);
            Assert.NotNull(result.ErrorMessage);
            var fromDb = await _service.GetByIdAsync(created.Id);
            Assert.Equal("Down", fromDb!.HealthStatus);
        }

        [Fact]
        public async Task CheckHealthAsync_WithNonExistentId_Throws()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _service.CheckHealthAsync(Guid.NewGuid()));
        }
    }
}
