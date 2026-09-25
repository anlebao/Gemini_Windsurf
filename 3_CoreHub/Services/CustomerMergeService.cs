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
    ILogger<CustomerMergeService> logger,
    VanAnDbContext? pgContext = null) : ICustomerMergeService
{
    private readonly ICustomerRepository _customerRepository = customerRepository;
    private readonly ILoyaltyRewardsService _loyaltyRewardsService = loyaltyRewardsService;
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<CustomerMergeService> _logger = logger;
    // 2026-09-25: community data (roles/referrals/wallet) is PG-only — in ShopERP scope
    // _dbContext is SQLite and those entities are Ignored, so PG migration goes through
    // this separate context (injected there; null in unit tests).
    private readonly VanAnDbContext? _pgContext = pgContext;

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

        // 2. Link DeviceId to login customer (so future guest checkouts on same device
        //    can find the login customer via DeviceId). Runs even when no stubs exist —
        //    stamping a real account is safe because the Guest filter below excludes it.
        loginCustomer.UpdateCustomerDetails(
            loginCustomer.FullName,
            loginCustomer.PhoneNumber,
            loginCustomer.Email,
            loginCustomer.CustomerTier,
            deviceId,
            loginCustomer.IsActive);
        await _customerRepository.UpdateAsync(loginCustomer);

        // 3. Find all ACTIVE customers with matching DeviceId (guest stubs from same device)
        //    Exclude the login customer itself. Use IgnoreQueryFilters to also find
        //    stubs that may have been soft-deleted by a previous merge (idempotency check).
        //    CRITICAL: only IdentityLevel.Guest records are mergeable stubs — real login
        //    accounts (Social/Verified) stamped with the same DeviceId by step 2 must never
        //    be soft-deleted or have their points drained by another login on a shared device.
        var stubs = await _dbContext.Customers
            .IgnoreQueryFilters()
            .Where(c => c.DeviceId == deviceId
                && c.Id != loginCustomerId
                && !c.IsDeleted
                && c.IdentityLevel == IdentityLevel.Guest)
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

            // 2026-09-25: migrate PG-side community data BEFORE soft-deleting the stub —
            // otherwise salesman/shipper role, commission referrals, wallet ledger and
            // withdrawal requests stay attached to a deleted customer and disappear from UI
            // (prod incident: CTV wallet showed 0 because data was orphaned on a stub).
            await MigrateCommunityDataAsync(stub.Id, loginCustomerId);

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

        return new CustomerMergeResult(stubsMerged, totalPointsTransferred);
    }

    /// <summary>
    /// Re-point PG-only community records from a merged stub to the login customer.
    /// Runs on the PG context (community tables are Ignored in ShopERP SQLite).
    /// Best-effort: failures are logged but do not abort the merge — the stub is still
    /// soft-deleted so identity fragmentation stops; an orphaned row is recoverable by SQL.
    /// </summary>
    private async Task MigrateCommunityDataAsync(Guid stubId, Guid loginCustomerId)
    {
        var pg = _pgContext ?? (_dbContext as VanAnDbContext);
        if (pg == null)
        {
            _logger.LogDebug("MigrateCommunityData: no PG context available — skipping (stub {StubId})", stubId);
            return;
        }

        try
        {
            // IgnoreQueryFilters: community rows may belong to a tenant that differs from the
            // ambient scope (guest stubs resolved cross-tenant, e.g. prod incident a5b6 stub
            // merged under a 0001-scoped request). Only the OWNER FK is re-pointed — TenantId,
            // amounts and statuses are preserved, so no cross-tenant data leak occurs.
            int roles = await pg.CommunityRoles
                .IgnoreQueryFilters()
                .Where(r => r.CustomerId == stubId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.CustomerId, loginCustomerId));
            int referrals = await pg.SalesReferrals
                .IgnoreQueryFilters()
                .Where(r => r.SalesmanId == stubId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.SalesmanId, loginCustomerId));
            int walletTx = await pg.WalletTransactions
                .IgnoreQueryFilters()
                .Where(w => w.OwnerId == stubId)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.OwnerId, loginCustomerId));
            int withdrawals = await pg.WithdrawalRequests
                .IgnoreQueryFilters()
                .Where(w => w.OwnerId == stubId)
                .ExecuteUpdateAsync(s => s.SetProperty(w => w.OwnerId, loginCustomerId));

            if (roles + referrals + walletTx + withdrawals > 0)
            {
                _logger.LogInformation(
                    "MigrateCommunityData: stub {StubId} → {LoginId}: roles={Roles} referrals={Referrals} walletTx={WalletTx} withdrawals={Withdrawals}",
                    stubId, loginCustomerId, roles, referrals, walletTx, withdrawals);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MigrateCommunityData: failed for stub {StubId} → {LoginId} — merge continues, fix orphaned rows via SQL", stubId, loginCustomerId);
        }
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
