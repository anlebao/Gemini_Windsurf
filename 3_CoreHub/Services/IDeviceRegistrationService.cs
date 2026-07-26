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
    }

    /// <summary>
    /// Result of device registration — includes the created DeviceRegistration
    /// and an optional FraudFlag if the device limit was exceeded.
    /// </summary>
    public record DeviceRegistrationResult(
        DeviceRegistration DeviceRegistration,
        FraudFlag? FraudFlag);
}
