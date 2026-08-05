using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VanAn.CoreHub.Domain.Repositories;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// TD-CUSTSYNC-001 / Issue #106: Customer identity merge service.
/// Merges DeviceId-based guest stubs into login-based customer accounts.
/// </summary>
public class CustomerMergeService(
    ICustomerRepository customerRepository,
    ILoyaltyRewardsService loyaltyRewardsService,
    IVanAnDbContext dbContext,
    ILogger<CustomerMergeService> logger) : ICustomerMergeService
{
    private readonly ICustomerRepository _customerRepository = customerRepository;
    private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<CustomerMergeService> _logger = logger;

    public async Task<CustomerMergeResult> MergeDeviceStubsIntoLoginAsync(Guid loginCustomerId, Guid deviceId)
    {
        if (deviceId == Guid.Empty)
        {
            _logger.LogDebug("MergeDeviceStubs: deviceId is empty — skipping merge for customer {CustomerId}", loginCustomerId);
            return new CustomerMergeResult(0, 0);
        }

        // 1. Get the login customer (merge target)
        Customer? loginCustomer = await _customerRepository.GetByIdAsync(loginCustomerId);
        if (loginCustomer == null)
        {
            _logger.LogWarning("MergeDeviceStubs: Login customer {CustomerId} not found", loginCustomerId);
            return new CustomerMergeResult(0, 0);
        }

        // 2. Find all ACTIVE customers with matching DeviceId (guest stubs from same device)
        //    Exclude the login customer itself. Use IgnoreQueryFilters to also find
        //    stubs that may have been soft-deleted by a previous merge (idempotency check).
        var stubs = await _dbContext.Customers
            .IgnoreQueryFilters()
            .Where(c => c.DeviceId == deviceId
                && c.Id != loginCustomerId
                && !c.IsDeleted)
            .ToListAsync();

        if (stubs.Count == 0)
        {
            _logger.LogDebug("MergeDeviceStubs: No stubs found for deviceId {DeviceId} — already merged or no guest orders", deviceId);
            return new CustomerMergeResult(0, 0);
        }

        _logger.LogInformation("MergeDeviceStubs: Found {Count} stub(s) for deviceId {DeviceId}, merging into customer {CustomerId}",
            stubs.Count, deviceId, loginCustomerId);

        // 3. Get or create login customer's LoyaltyRewards
        var loginRewards = await _loyaltyRewardsService.GetOrCreateCustomerRewardsAsync(
            loginCustomerId, loginCustomer.TenantId);

        // 4. Merge each stub's loyalty points + history into login customer
        int totalPointsTransferred = 0;
        int stubsMerged = 0;

        // Parse login customer's existing history
        var combinedHistory = ParseHistory(loginRewards.History);

        foreach (var stub in stubs)
        {
            var stubRewards = await _loyaltyRewardsService.GetCustomerRewardsAsync(stub.Id);
            if (stubRewards != null && stubRewards.PointBalance > 0)
            {
                totalPointsTransferred += stubRewards.PointBalance;

                // Merge history entries
                var stubHistory = ParseHistory(stubRewards.History);
                combinedHistory.AddRange(stubHistory);

                _logger.LogInformation("MergeDeviceStubs: Transferring {Points} points from stub {StubId} to login {LoginId}",
                    stubRewards.PointBalance, stub.Id, loginCustomerId);
            }

            // Soft-delete the stub to prevent future fragmentation
            stub.SoftDelete();
            stubsMerged++;
        }

        // 5. Apply merged points to login customer's rewards
        if (totalPointsTransferred > 0)
        {
            loginRewards.AddPoints(totalPointsTransferred, $"Merge from {stubsMerged} guest stub(s) — TD-CUSTSYNC-001");

            // Update history with merged entries
            var updatedHistoryJson = JsonSerializer.Serialize(combinedHistory);
            loginRewards.UpdateHistory(updatedHistoryJson);

            // Save changes (LoyaltyRewards + Customer soft-deletes)
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("MergeDeviceStubs: SUCCESS — merged {StubsMerged} stub(s), transferred {Points} points to customer {CustomerId}",
                stubsMerged, totalPointsTransferred, loginCustomerId);
        }
        else
        {
            // Even if no points to transfer, soft-delete stubs to prevent future confusion
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("MergeDeviceStubs: Merged {StubsMerged} stub(s) with 0 points (soft-deleted for cleanup)", stubsMerged);
        }

        // 6. Link DeviceId to login customer (so future guest checkouts on same device
        //    can find the login customer via DeviceId)
        loginCustomer.UpdateCustomerDetails(
            loginCustomer.FullName,
            loginCustomer.PhoneNumber,
            loginCustomer.Email,
            loginCustomer.CustomerTier,
            deviceId,
            loginCustomer.IsActive);
        await _customerRepository.UpdateAsync(loginCustomer);

        return new CustomerMergeResult(stubsMerged, totalPointsTransferred);
    }

    private static List<LoyaltyHistoryEntry> ParseHistory(string historyJson)
    {
        if (string.IsNullOrEmpty(historyJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<LoyaltyHistoryEntry>>(historyJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
