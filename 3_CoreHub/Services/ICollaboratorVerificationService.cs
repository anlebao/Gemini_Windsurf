using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S6-T5: Collaborator SMS OTP verification + deposit wallet service.
/// Toggle-gated (SystemAdmin ON/OFF). When ON, Salesman/Shipper must verify phone via SMS OTP.
/// SMS fee deducted from deposit wallet. Deposit exhausted → cannot send OTP.
/// Customer redeem points NEVER requires SMS OTP (always free).
/// </summary>
public interface ICollaboratorVerificationService
{
    /// <summary>Get current settings (toggle + fee + min deposit).</summary>
    Task<CollaboratorVerificationSettingsDto> GetSettingsAsync();

    /// <summary>Update settings (SystemAdmin only).</summary>
    Task SetSettingsAsync(bool enabled, decimal feePerVerification, decimal minDeposit, Guid updatedBy);

    /// <summary>
    /// Init SMS OTP verification for a collaborator.
    /// Checks toggle ON + deposit balance ≥ fee → generates 6-digit OTP → sends SMS → deducts fee.
    /// Throws if toggle OFF, balance insufficient, or SMS send fails.
    /// </summary>
    Task<InitVerificationResultDto> InitVerificationAsync(Guid customerId, string phoneNumber);

    /// <summary>
    /// Verify OTP code. On success, marks CommunityRole.IsPhoneVerified = true.
    /// Throws if OTP invalid/expired or no active CommunityRole found.
    /// </summary>
    Task VerifyOtpAsync(Guid customerId, string otpCode);

    /// <summary>
    /// Deposit money into collaborator's wallet (WalletTransactionType.Deposit).
    /// </summary>
    Task DepositAsync(Guid customerId, decimal amount);

    /// <summary>Check if SMS verification is required (toggle ON + collaborator not yet verified).</summary>
    Task<bool> IsVerificationRequiredAsync(Guid customerId);
}

// DTOs
public class CollaboratorVerificationSettingsDto
{
    public bool Enabled { get; set; }
    public decimal FeePerVerification { get; set; }
    public decimal MinDeposit { get; set; }
}

public class InitVerificationResultDto
{
    public string Message { get; set; } = string.Empty;
    public decimal FeeDeducted { get; set; }
    public decimal BalanceAfter { get; set; }
}
