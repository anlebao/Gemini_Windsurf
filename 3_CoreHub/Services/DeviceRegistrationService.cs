using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// DeviceRegistrationService — enforces max 3 active devices per Customer (v1.2).
    /// F3 fix 2026-07-26: Sprint 0 SC18 claimed max-3 enforcement but no production code existed.
    /// This service implements the application-layer constraint.
    /// Community entities are PG-only (v1.3) — queries run against PostgreSQL via IVanAnDbContext.
    /// </summary>
    public class DeviceRegistrationService : IDeviceRegistrationService
    {
        private const int MaxActiveDevicesPerCustomer = 3;

        private readonly IVanAnDbContext _dbContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<DeviceRegistrationService> _logger;

        public DeviceRegistrationService(
            IVanAnDbContext dbContext,
            ITenantProvider tenantProvider,
            ILogger<DeviceRegistrationService> logger)
        {
            _dbContext = dbContext;
            _tenantProvider = tenantProvider;
            _logger = logger;
        }

        public async Task<DeviceRegistrationResult> RegisterDeviceAsync(
            Guid customerId,
            string deviceToken,
            string fingerprintHash,
            string fingerprintSignals,
            string userAgent,
            string platform,
            string ipAddress)
        {
            var tenantId = new TenantId(_tenantProvider.TenantId);

            _logger.LogInformation(
                "Registering device for Customer={CustomerId} Fingerprint={FingerprintHash}",
                customerId, fingerprintHash);

            // Count active devices for this customer (application-layer enforce max 3)
            var activeDeviceCount = await _dbContext.DeviceRegistrations
                .CountAsync(d => d.CustomerId == customerId && d.IsActive);

            var device = new DeviceRegistration(
                tenantId,
                customerId,
                deviceToken,
                fingerprintHash,
                fingerprintSignals,
                userAgent,
                platform,
                ipAddress);

            FraudFlag? fraudFlag = null;

            if (activeDeviceCount >= MaxActiveDevicesPerCustomer)
            {
                // Device 4+ → create with IsActive=false + FraudFlag
                device.Deactivate(); // set IsActive=false
                _dbContext.DeviceRegistrations.Add(device);

                fraudFlag = new FraudFlag(
                    tenantId,
                    FraudEntityType.DeviceRegistration,
                    device.Id,
                    customerId,
                    FraudFlagType.DeviceLimitExceeded,
                    riskScore: 0,
                    riskFactors: $"{{\"activeDeviceCount\":{activeDeviceCount},\"max\":{MaxActiveDevicesPerCustomer}}}",
                    description: $"Customer {customerId} exceeded max {MaxActiveDevicesPerCustomer} active devices (has {activeDeviceCount}). New device registered as inactive.");
                _dbContext.FraudFlags.Add(fraudFlag);

                _logger.LogWarning(
                    "Device limit exceeded for Customer={CustomerId}: {ActiveCount} active devices. " +
                    "New device {DeviceId} created as inactive + FraudFlag {FraudFlagId} raised.",
                    customerId, activeDeviceCount, device.Id, fraudFlag.Id);
            }
            else
            {
                _dbContext.DeviceRegistrations.Add(device);
                _logger.LogInformation(
                    "Device {DeviceId} registered for Customer={CustomerId} (active count now {Count})",
                    device.Id, customerId, activeDeviceCount + 1);
            }

            await _dbContext.SaveChangesAsync();

            return new DeviceRegistrationResult(device, fraudFlag);
        }
    }
}
