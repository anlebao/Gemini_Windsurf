using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// IDeviceRegistrationService — device registration with max-3 active per Customer enforcement (v1.2).
    /// F3 fix 2026-07-26: Sprint 0 SC18 claimed max-3 enforcement but no production code existed.
    /// This service implements the application-layer constraint:
    ///   - Count active devices for Customer before insert.
    ///   - If < 3 active: create DeviceRegistration(IsActive=true).
    ///   - If >= 3 active: create DeviceRegistration(IsActive=false) + FraudFlag(FlagType=DeviceLimitExceeded).
    /// </summary>
    public interface IDeviceRegistrationService
    {
        /// <summary>
        /// Register a new device for a customer. Enforces max 3 active devices per customer.
        /// If customer already has 3 active devices, the new device is created with IsActive=false
        /// and a FraudFlag (DeviceLimitExceeded) is raised for admin review.
        /// </summary>
        /// <returns>The created DeviceRegistration + a FraudFlag if device limit was exceeded (null otherwise).</returns>
        Task<DeviceRegistrationResult> RegisterDeviceAsync(
            Guid customerId,
            string deviceToken,
            string fingerprintHash,
            string fingerprintSignals,
            string userAgent,
            string platform,
            string ipAddress);

        // === Admin methods (SystemAdmin cross-tenant) ===

        /// <summary>
        /// List devices with optional filters. Cross-tenant (IgnoreQueryFilters).
        /// </summary>
        Task<DeviceRegistrationPagedResult> ListDevicesAsync(
            int page = 1,
            int pageSize = 20,
            Guid? customerId = null,
            string? fingerprintHash = null,
            bool? isActive = null,
            CancellationToken ct = default);

        /// <summary>
        /// Get a single device by ID. Cross-tenant.
        /// </summary>
        Task<DeviceRegistrationDto?> GetDeviceByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Deactivate a device (IsActive = false). Cross-tenant.
        /// </summary>
        Task<bool> DeactivateDeviceAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Verify a device (IsVerified = true — whitelist, RiskScore reduced). Cross-tenant.
        /// </summary>
        Task<bool> VerifyDeviceAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Update device-level risk score. Cross-tenant.
        /// </summary>
        Task<bool> UpdateRiskScoreAsync(Guid id, int score, CancellationToken ct = default);

        /// <summary>
        /// Query devices by fingerprint hash (self-deal detection — who else uses this fingerprint?).
        /// Cross-tenant.
        /// </summary>
        Task<List<DeviceRegistrationDto>> GetDevicesByFingerprintAsync(string fingerprintHash, CancellationToken ct = default);
    }

/// <summary>
/// Paged result for device registration list.
/// </summary>
public class DeviceRegistrationPagedResult
{
    public int Total { get; set; }
    public List<DeviceRegistrationDto> Items { get; set; } = new();
}

/// <summary>
/// DTO for device registration (admin view).
/// </summary>
public class DeviceRegistrationDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public string FingerprintHash { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsVerified { get; set; }
    public string UserAgent { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int RiskScore { get; set; }
}

/// <summary>
/// Result of device registration — includes the created DeviceRegistration
/// and an optional FraudFlag if the device limit was exceeded.
/// </summary>
public record DeviceRegistrationResult(
    DeviceRegistration DeviceRegistration,
    FraudFlag? FraudFlag);
}
