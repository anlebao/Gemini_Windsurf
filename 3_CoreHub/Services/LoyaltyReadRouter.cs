using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Loyalty Consistency Fix Phase 2 (BUG #4, #7, #8): shared helper for mode-aware point balance reads.
/// In Alliance mode + customer has DeviceId → query PG AllianceWallet via IAllianceWalletService (HTTP proxy).
/// Otherwise → return SQLite balance (caller-provided).
///
/// Graceful fallback: on any error (Gateway unreachable, parsing, etc.), returns SQLite balance — never
/// blocks UI on PG outage. Customer sees potentially stale local mirror instead of error page.
/// </summary>
public class LoyaltyReadRouter(
    ILoyaltyModeResolver? modeResolver,
    IAllianceWalletService? walletService,
    ILogger<LoyaltyReadRouter> logger)
{
    private readonly ILoyaltyModeResolver? _modeResolver = modeResolver;
    private readonly IAllianceWalletService? _walletService = walletService;
    private readonly ILogger<LoyaltyReadRouter> _logger = logger;

    /// <summary>
    /// Returns effective point balance for read paths (UI display).
    /// </summary>
    /// <param name="tenantId">Customer's tenant</param>
    /// <param name="deviceGuid">Customer's DeviceId (nullable — required for Alliance wallet lookup)</param>
    /// <param name="sqliteBalance">Fallback balance from SQLite LoyaltyRewards</param>
    /// <returns>PG wallet balance in Alliance mode, SQLite balance otherwise</returns>
    public async Task<int> GetEffectiveBalanceAsync(Guid tenantId, Guid? deviceGuid, int sqliteBalance)
    {
        if (_modeResolver is null || _walletService is null)
            return sqliteBalance;

        if (deviceGuid is null || deviceGuid.Value == Guid.Empty)
            return sqliteBalance;

        try
        {
            LoyaltyMode mode = await _modeResolver.GetEffectiveModeAsync(tenantId);
            if (mode != LoyaltyMode.Alliance)
                return sqliteBalance;

            if (!await _modeResolver.IsAllianceMemberAsync(tenantId))
                return sqliteBalance;

            var wallet = await _walletService.GetWalletByDeviceIdAsync(deviceGuid.Value);
            return wallet?.TotalPointBalance ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoyaltyReadRouter: wallet query failed for tenant {TenantId}, device {DeviceId} — returning SQLite balance", tenantId, deviceGuid);
            return sqliteBalance; // Graceful fallback
        }
    }
}
