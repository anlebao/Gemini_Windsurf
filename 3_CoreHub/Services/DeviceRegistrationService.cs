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

        // === Admin methods (SystemAdmin cross-tenant) ===

        public async Task<DeviceRegistrationPagedResult> ListDevicesAsync(
            int page = 1,
            int pageSize = 20,
            Guid? customerId = null,
            string? fingerprintHash = null,
            bool? isActive = null,
            CancellationToken ct = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _dbContext.DeviceRegistrations
                .IgnoreQueryFilters()
                .AsNoTracking();

            if (customerId.HasValue)
                query = query.Where(d => d.CustomerId == customerId.Value);
            if (!string.IsNullOrWhiteSpace(fingerprintHash))
                query = query.Where(d => d.FingerprintHash == fingerprintHash);
            if (isActive.HasValue)
                query = query.Where(d => d.IsActive == isActive.Value);

            var total = await query.CountAsync(ct);

            var devices = await query
                .OrderByDescending(d => d.LastSeenAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            // Load customer names
            var customerIds = devices.Select(d => d.CustomerId).Distinct().ToList();
            var customers = await _dbContext.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.FullName })
                .ToDictionaryAsync(c => c.Id, c => c.FullName, ct);

            var items = devices.Select(d => new DeviceRegistrationDto
            {
                Id = d.Id,
                CustomerId = d.CustomerId,
                CustomerName = customers.TryGetValue(d.CustomerId, out var name) ? name : "Unknown",
                DeviceToken = d.DeviceToken,
                FingerprintHash = d.FingerprintHash,
                FirstSeenAt = d.FirstSeenAt,
                LastSeenAt = d.LastSeenAt,
                IsActive = d.IsActive,
                IsVerified = d.IsVerified,
                UserAgent = d.UserAgent,
                Platform = d.Platform,
                IpAddress = d.IpAddress,
                RiskScore = d.RiskScore
            }).ToList();

            return new DeviceRegistrationPagedResult { Total = total, Items = items };
        }

        public async Task<DeviceRegistrationDto?> GetDeviceByIdAsync(Guid id, CancellationToken ct = default)
        {
            var device = await _dbContext.DeviceRegistrations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id, ct);

            if (device == null) return null;

            var customerName = "Unknown";
            var customer = await _dbContext.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.Id == device.CustomerId)
                .Select(c => c.FullName)
                .FirstOrDefaultAsync(ct);
            if (customer != null) customerName = customer;

            return new DeviceRegistrationDto
            {
                Id = device.Id,
                CustomerId = device.CustomerId,
                CustomerName = customerName,
                DeviceToken = device.DeviceToken,
                FingerprintHash = device.FingerprintHash,
                FirstSeenAt = device.FirstSeenAt,
                LastSeenAt = device.LastSeenAt,
                IsActive = device.IsActive,
                IsVerified = device.IsVerified,
                UserAgent = device.UserAgent,
                Platform = device.Platform,
                IpAddress = device.IpAddress,
                RiskScore = device.RiskScore
            };
        }

        public async Task<bool> DeactivateDeviceAsync(Guid id, CancellationToken ct = default)
        {
            var device = await _dbContext.DeviceRegistrations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Id == id, ct);

            if (device == null) return false;

            device.Deactivate();
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Device {DeviceId} deactivated by admin", id);
            return true;
        }

        public async Task<bool> VerifyDeviceAsync(Guid id, CancellationToken ct = default)
        {
            var device = await _dbContext.DeviceRegistrations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Id == id, ct);

            if (device == null) return false;

            device.Verify();
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Device {DeviceId} verified (whitelisted) by admin", id);
            return true;
        }

        public async Task<bool> UpdateRiskScoreAsync(Guid id, int score, CancellationToken ct = default)
        {
            var device = await _dbContext.DeviceRegistrations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Id == id, ct);

            if (device == null) return false;

            device.UpdateRiskScore(score);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Device {DeviceId} risk score updated to {Score} by admin", id, score);
            return true;
        }

        public async Task<List<DeviceRegistrationDto>> GetDevicesByFingerprintAsync(string fingerprintHash, CancellationToken ct = default)
        {
            var devices = await _dbContext.DeviceRegistrations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d => d.FingerprintHash == fingerprintHash)
                .ToListAsync(ct);

            var customerIds = devices.Select(d => d.CustomerId).Distinct().ToList();
            var customers = await _dbContext.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.FullName })
                .ToDictionaryAsync(c => c.Id, c => c.FullName, ct);

            return devices.Select(d => new DeviceRegistrationDto
            {
                Id = d.Id,
                CustomerId = d.CustomerId,
                CustomerName = customers.TryGetValue(d.CustomerId, out var name) ? name : "Unknown",
                DeviceToken = d.DeviceToken,
                FingerprintHash = d.FingerprintHash,
                FirstSeenAt = d.FirstSeenAt,
                LastSeenAt = d.LastSeenAt,
                IsActive = d.IsActive,
                IsVerified = d.IsVerified,
                UserAgent = d.UserAgent,
                Platform = d.Platform,
                IpAddress = d.IpAddress,
                RiskScore = d.RiskScore
            }).ToList();
        }
    }
}
