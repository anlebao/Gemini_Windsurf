namespace VanAn.CoreHub.Services.Registrations
{
    /// <summary>
    /// GTM Drill Machine W2 (2026-09-08): Registration lifecycle management.
    /// Merchant submits registration via Register form → SysAdmin reviews → Contact/Onboard/Reject.
    /// </summary>
    public interface ITenantRegistrationService
    {
        /// <summary>
        /// Merchant submits a registration (from demo or direct form).
        /// Turnstile verification handled by controller before calling this method.
        /// Honeypot check handled by controller (silent reject before calling this method).
        /// </summary>
        Task<Guid> SubmitRegistrationAsync(SubmitRegistrationRequest req, bool turnstileVerified, CancellationToken ct = default);

        /// <summary>
        /// SysAdmin marks registration as Contacted (follow-up initiated).
        /// </summary>
        Task MarkContactedAsync(Guid registrationId, Guid sysAdminUserId, CancellationToken ct = default);

        /// <summary>
        /// SysAdmin marks registration as Onboarded — tenant created from this registration.
        /// Records the new tenant ID for traceability.
        /// </summary>
        Task MarkOnboardedAsync(Guid registrationId, Guid onboardedTenantId, Guid sysAdminUserId, CancellationToken ct = default);

        /// <summary>
        /// SysAdmin rejects the registration with reason.
        /// </summary>
        Task RejectRegistrationAsync(Guid registrationId, string reason, Guid sysAdminUserId, CancellationToken ct = default);

        /// <summary>
        /// Lists all Submitted registrations (SysAdmin queue).
        /// </summary>
        Task<IReadOnlyList<RegistrationDto>> ListPendingRegistrationsAsync(CancellationToken ct = default);

        /// <summary>
        /// Gets a single registration by ID.
        /// </summary>
        Task<RegistrationDto?> GetRegistrationAsync(Guid registrationId, CancellationToken ct = default);
    }
}
