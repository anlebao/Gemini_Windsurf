using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using Xunit;

using Microsoft.Extensions.DependencyInjection;
namespace VanAn.Core.Tests.Community
{
    /// <summary>
    /// DeviceRegistrationService tests (F3 fix 2026-07-26).
    /// Verifies max 3 active devices per Customer enforcement — the constraint
    /// that Sprint 0 SC18 claimed but never implemented.
    /// Uses real VanAnDbContext with SQLite in-memory.
    /// </summary>
    public class DeviceRegistrationServiceTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly VanAnDbContext _dbContext;
        private readonly DeviceRegistrationService _service;
        private readonly StubTenantProvider _tenantProvider;

        public DeviceRegistrationServiceTests()
        {
            _connection = new SqliteConnection($"DataSource=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared");
            _connection.Open();
            var efServiceProvider = new ServiceCollection().AddEntityFrameworkSqlite().BuildServiceProvider();
            var options = new DbContextOptionsBuilder<VanAnDbContext>()
                .UseInternalServiceProvider(efServiceProvider).UseSqlite(_connection)
                .Options;
            _tenantProvider = new StubTenantProvider(Guid.NewGuid());
            _dbContext = new VanAnDbContext(options, _tenantProvider);
            _dbContext.Database.EnsureCreated();

            _service = new DeviceRegistrationService(
                _dbContext,
                _tenantProvider,
                NullLogger<DeviceRegistrationService>.Instance);
        }

        public void Dispose()
        {
            _dbContext?.Dispose();
            _connection?.Dispose();
        }

        private static readonly string Hash64 = new string('b', 64);

        private static string UniqueToken() => Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N").Substring(0, 32);

        private Task<DeviceRegistrationResult> RegisterAsync(Guid customerId)
            => _service.RegisterDeviceAsync(customerId, UniqueToken(), Hash64, "{}", "Mozilla/5.0", "Web", "127.0.0.1");

        [Fact(DisplayName = "F3-1: RegisterDevice_FirstDevice_IsActiveTrue_NoFraudFlag")]
        public async Task RegisterDevice_FirstDevice_IsActiveTrue_NoFraudFlag()
        {
            var customerId = Guid.NewGuid();
            var result = await RegisterAsync(customerId);

            Assert.True(result.DeviceRegistration.IsActive);
            Assert.Null(result.FraudFlag);
        }

        [Fact(DisplayName = "F3-2: RegisterDevice_ThirdDevice_IsActiveTrue_NoFraudFlag")]
        public async Task RegisterDevice_ThirdDevice_IsActiveTrue_NoFraudFlag()
        {
            var customerId = Guid.NewGuid();
            await RegisterAsync(customerId);
            await RegisterAsync(customerId);
            var result = await RegisterAsync(customerId);

            Assert.True(result.DeviceRegistration.IsActive);
            Assert.Null(result.FraudFlag);

            var activeCount = await _dbContext.DeviceRegistrations
                .CountAsync(d => d.CustomerId == customerId && d.IsActive);
            Assert.Equal(3, activeCount);
        }

        [Fact(DisplayName = "F3-3: RegisterDevice_FourthDevice_IsActiveFalse_FraudFlagCreated")]
        public async Task RegisterDevice_FourthDevice_IsActiveFalse_FraudFlagCreated()
        {
            var customerId = Guid.NewGuid();
            await RegisterAsync(customerId);
            await RegisterAsync(customerId);
            await RegisterAsync(customerId);
            var result = await RegisterAsync(customerId);

            Assert.False(result.DeviceRegistration.IsActive);
            Assert.NotNull(result.FraudFlag);
            Assert.Equal(FraudFlagType.DeviceLimitExceeded, result.FraudFlag!.FlagType);
            Assert.Equal(FraudFlagStatus.Pending, result.FraudFlag.Status);
            Assert.Equal(customerId, result.FraudFlag.CustomerId);
            Assert.Equal(FraudEntityType.DeviceRegistration, result.FraudFlag.EntityType);
            Assert.Equal(result.DeviceRegistration.Id, result.FraudFlag.EntityId);
        }

        [Fact(DisplayName = "F3-4: RegisterDevice_FifthDevice_StillInactive_FraudFlagCreated")]
        public async Task RegisterDevice_FifthDevice_StillInactive_FraudFlagCreated()
        {
            var customerId = Guid.NewGuid();
            for (int i = 0; i < 4; i++)
                await RegisterAsync(customerId);

            var result = await RegisterAsync(customerId);

            Assert.False(result.DeviceRegistration.IsActive);
            Assert.NotNull(result.FraudFlag);

            var activeCount = await _dbContext.DeviceRegistrations
                .CountAsync(d => d.CustomerId == customerId && d.IsActive);
            Assert.Equal(3, activeCount); // only first 3 are active
        }

        [Fact(DisplayName = "F3-5: RegisterDevice_DeactivatedDevice_DoesNotCountTowardLimit")]
        public async Task RegisterDevice_DeactivatedDevice_DoesNotCountTowardLimit()
        {
            var customerId = Guid.NewGuid();
            var r1 = await RegisterAsync(customerId);
            await RegisterAsync(customerId);
            await RegisterAsync(customerId);

            // Deactivate one device
            r1.DeviceRegistration.Deactivate();
            _dbContext.DeviceRegistrations.Update(r1.DeviceRegistration);
            await _dbContext.SaveChangesAsync();

            // Now only 2 active — new device should be active (3rd active)
            var result = await RegisterAsync(customerId);
            Assert.True(result.DeviceRegistration.IsActive);
            Assert.Null(result.FraudFlag);
        }

        [Fact(DisplayName = "F3-6: RegisterDevice_DifferentCustomers_IndependentCounts")]
        public async Task RegisterDevice_DifferentCustomers_IndependentCounts()
        {
            var customer1 = Guid.NewGuid();
            var customer2 = Guid.NewGuid();

            await RegisterAsync(customer1);
            await RegisterAsync(customer1);
            await RegisterAsync(customer1);

            // Customer 2 should have independent count
            var result = await RegisterAsync(customer2);
            Assert.True(result.DeviceRegistration.IsActive);
            Assert.Null(result.FraudFlag);
        }

        private sealed class StubTenantProvider : ITenantProvider
        {
            public StubTenantProvider(Guid tenantId) => TenantId = tenantId;
            public Guid TenantId { get; }
            public string? CurrentUser => "test";
            public bool HasTenant => true;
            public void SetTenant(Guid tenantId) { }
        }
    }
}
