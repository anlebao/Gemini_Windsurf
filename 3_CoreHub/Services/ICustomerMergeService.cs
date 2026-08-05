using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// TD-CUSTSYNC-001 / Issue #106: Customer identity merge service.
/// Merges DeviceId-based guest stubs into login-based customer accounts
/// to fix loyalty point fragmentation.
/// </summary>
public interface ICustomerMergeService
{
    /// <summary>
    /// Merge all DeviceId-based guest stubs into the login customer.
    /// Called after customer logs in (Google OAuth or Phone OTP).
    ///
    /// Steps:
    /// 1. Find all customers with matching DeviceId (guest stubs created from same device)
    /// 2. Sum their LoyaltyRewards.PointBalance into login customer's balance
    /// 3. Concatenate their LoyaltyRewards.History into login customer's history
    /// 4. Soft-delete the stubs (IsDeleted=true) to prevent future fragmentation
    ///
    /// Idempotent: if stubs already merged (IsDeleted=true), skip them.
    /// </summary>
    /// <param name="loginCustomerId">The login-based customer Id (merge target)</param>
    /// <param name="deviceId">Device fingerprint from the browser (merge key)</param>
    /// <returns>Number of stubs merged + total points transferred</returns>
    Task<CustomerMergeResult> MergeDeviceStubsIntoLoginAsync(Guid loginCustomerId, Guid deviceId);
}

/// <summary>Result of a customer merge operation.</summary>
public record CustomerMergeResult(int StubsMerged, int PointsTransferred);
