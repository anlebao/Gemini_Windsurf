using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Tiered Auth Phase 2: Thrown when a customer attempts an operation that requires
    /// a higher IdentityLevel than they currently hold (e.g., redeem points requires Verified).
    /// Controllers should catch this and return HTTP 403 with an upgrade-required payload
    /// so the client can prompt the user to verify via OTP.
    /// </summary>
    public class IdentityLevelNotSufficientException : Exception
    {
        public Guid CustomerId { get; }
        public IdentityLevel CurrentLevel { get; }
        public IdentityLevel RequiredLevel { get; }

        public IdentityLevelNotSufficientException(Guid customerId, IdentityLevel currentLevel, IdentityLevel requiredLevel)
            : base($"Customer {customerId} has IdentityLevel={currentLevel} but operation requires >= {requiredLevel}. Please upgrade via OTP verification.")
        {
            CustomerId = customerId;
            CurrentLevel = currentLevel;
            RequiredLevel = requiredLevel;
        }
    }
}
