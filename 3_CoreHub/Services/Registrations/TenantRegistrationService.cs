using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain.Aggregates.TenantAggregate;

namespace VanAn.CoreHub.Services.Registrations
{
    /// <summary>
    /// GTM Drill Machine W2 (2026-09-08): Registration lifecycle management.
    /// Merchant submits registration → SysAdmin reviews → Contact/Onboard/Reject.
    /// Precedent: TenantClaimService (Crawl-to-Onboard Pipeline).
    /// </summary>
    public class TenantRegistrationService(
        IVanAnDbContext dbContext,
        ILogger<TenantRegistrationService> logger) : ITenantRegistrationService
    {
        public async Task<Guid> SubmitRegistrationAsync(
            SubmitRegistrationRequest req,
            bool turnstileVerified,
            CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(req.ShopName);
            ArgumentException.ThrowIfNullOrWhiteSpace(req.ContactName);
            ArgumentException.ThrowIfNullOrWhiteSpace(req.ContactPhone);
            ArgumentException.ThrowIfNullOrWhiteSpace(req.Source);

            var registration = TenantRegistration.Create(
                req.ShopName,
                req.ContactName,
                req.ContactPhone,
                req.Source,
                turnstileVerified,
                req.Industry,
                req.LogoUrl,
                req.ContactEmail);

            dbContext.TenantRegistrations.Add(registration);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Registration submitted for shop '{ShopName}' by {ContactName} — registration {RegistrationId}",
                req.ShopName, req.ContactName, registration.Id);

            return registration.Id;
        }

        public async Task MarkContactedAsync(Guid registrationId, Guid sysAdminUserId, CancellationToken ct = default)
        {
            var registration = await dbContext.TenantRegistrations
                .FirstOrDefaultAsync(r => r.Id == registrationId, ct)
                ?? throw new KeyNotFoundException($"Registration {registrationId} not found.");

            registration.MarkContacted(sysAdminUserId);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Registration {RegistrationId} marked as contacted by SysAdmin {SysAdminUserId}",
                registrationId, sysAdminUserId);
        }

        public async Task MarkOnboardedAsync(Guid registrationId, Guid onboardedTenantId, Guid sysAdminUserId, CancellationToken ct = default)
        {
            var registration = await dbContext.TenantRegistrations
                .FirstOrDefaultAsync(r => r.Id == registrationId, ct)
                ?? throw new KeyNotFoundException($"Registration {registrationId} not found.");

            registration.MarkOnboarded(sysAdminUserId, onboardedTenantId);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Registration {RegistrationId} marked as onboarded — tenant {TenantId} created by SysAdmin {SysAdminUserId}",
                registrationId, onboardedTenantId, sysAdminUserId);
        }

        public async Task RejectRegistrationAsync(Guid registrationId, string reason, Guid sysAdminUserId, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            var registration = await dbContext.TenantRegistrations
                .FirstOrDefaultAsync(r => r.Id == registrationId, ct)
                ?? throw new KeyNotFoundException($"Registration {registrationId} not found.");

            registration.Reject(sysAdminUserId, reason);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Registration {RegistrationId} rejected by SysAdmin {SysAdminUserId} — reason: {Reason}",
                registrationId, sysAdminUserId, reason);
        }

        public async Task<IReadOnlyList<RegistrationDto>> ListPendingRegistrationsAsync(CancellationToken ct = default)
        {
            var registrations = await dbContext.TenantRegistrations
                .Where(r => r.Status == TenantRegistration.RegistrationStatus.Submitted
                         || r.Status == TenantRegistration.RegistrationStatus.Contacted)
                .OrderBy(r => r.SubmittedAt)
                .ToListAsync(ct);

            return registrations.Select(MapToDto).ToList();
        }

        public async Task<RegistrationDto?> GetRegistrationAsync(Guid registrationId, CancellationToken ct = default)
        {
            var registration = await dbContext.TenantRegistrations
                .FirstOrDefaultAsync(r => r.Id == registrationId, ct);
            return registration is null ? null : MapToDto(registration);
        }

        private static RegistrationDto MapToDto(TenantRegistration r) => new(
            Id: r.Id,
            ShopName: r.ShopName,
            Industry: r.Industry,
            LogoUrl: r.LogoUrl,
            ContactName: r.ContactName,
            ContactPhone: r.ContactPhone,
            ContactEmail: r.ContactEmail,
            Source: r.Source,
            TurnstileVerified: r.TurnstileVerified,
            Status: r.Status.ToString(),
            SubmittedAt: r.SubmittedAt,
            ReviewedByUserId: r.ReviewedByUserId,
            ReviewedAt: r.ReviewedAt,
            RejectionReason: r.RejectionReason,
            OnboardedTenantId: r.OnboardedTenantId);
    }
}
