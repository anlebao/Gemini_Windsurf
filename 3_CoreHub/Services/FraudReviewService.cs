using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S6 (Sprint 6 v1.2): Fraud review service implementation.
/// Admin review queue, confirm/dismiss with side effects, 3-strike ban, fraud stats, salesman self-view.
/// </summary>
public class FraudReviewService(
    IVanAnDbContext dbContext,
    IWalletService walletService,
    ILogger<FraudReviewService> logger) : IFraudReviewService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IWalletService _walletService = walletService;
    private readonly ILogger<FraudReviewService> _logger = logger;
    private const int StrikeBanThreshold = 3;

    public async Task<PagedResult<FraudFlagDto>> GetFlagsAsync(string status, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var statusEnum = ParseStatus(status);

        var query = _dbContext.FraudFlags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => f.Status == statusEnum);

        var total = await query.CountAsync();

        var flags = await query
            .OrderByDescending(f => f.RiskScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Load customer names
        var customerIds = flags.Where(f => f.CustomerId.HasValue).Select(f => f.CustomerId!.Value).Distinct().ToList();
        var customers = await _dbContext.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName })
            .ToDictionaryAsync(c => c.Id, c => c.FullName);

        var items = flags.Select(f => new FraudFlagDto
        {
            Id = f.Id,
            CustomerId = f.CustomerId,
            CustomerName = f.CustomerId.HasValue && customers.TryGetValue(f.CustomerId.Value, out var name) ? name : "Unknown",
            EntityType = f.EntityType.ToString(),
            EntityId = f.EntityId,
            RiskScore = f.RiskScore,
            RiskFactors = f.RiskFactors,
            Status = f.Status.ToString(),
            CreatedAt = f.CreatedAt
        }).ToList();

        return new PagedResult<FraudFlagDto> { Total = total, Items = items };
    }

    public async Task<FraudFlagDetailDto?> GetDetailAsync(Guid id)
    {
        var flag = await _dbContext.FraudFlags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id);

        if (flag == null) return null;

        var dto = new FraudFlagDetailDto
        {
            Id = flag.Id,
            CustomerId = flag.CustomerId,
            EntityType = flag.EntityType.ToString(),
            EntityId = flag.EntityId,
            RiskScore = flag.RiskScore,
            RiskFactors = flag.RiskFactors,
            Status = flag.Status.ToString(),
            CreatedAt = flag.CreatedAt,
            Description = flag.Description,
            FlagType = flag.FlagType.ToString(),
            ReviewedBy = flag.ReviewedBy,
            ReviewedAt = flag.ReviewedAt,
            ReviewNote = flag.ReviewNote
        };

        // Load customer name
        if (flag.CustomerId.HasValue)
        {
            var customer = await _dbContext.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => c.Id == flag.CustomerId.Value)
                .Select(c => new { c.FullName })
                .FirstOrDefaultAsync();
            dto.CustomerName = customer?.FullName ?? "Unknown";
        }

        // Load related entity based on EntityType
        if (flag.EntityType == FraudEntityType.SalesReferral)
        {
            var referral = await _dbContext.SalesReferrals
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.Id == flag.EntityId)
                .Select(r => new FraudSalesReferralDto
                {
                    Id = r.Id,
                    OrderId = r.OrderId,
                    CommissionAmount = r.CommissionAmount,
                    CommissionStatus = r.CommissionStatus.ToString()
                })
                .FirstOrDefaultAsync();
            dto.SalesReferral = referral;
        }
        else if (flag.EntityType == FraudEntityType.AppInstallAttribution)
        {
            var attr = await _dbContext.AppInstallAttributions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(a => a.Id == flag.EntityId)
                .Select(a => new FraudAppInstallDto
                {
                    Id = a.Id,
                    BonusAmount = a.BonusAmount,
                    AttributionStatus = a.AttributionStatus.ToString()
                })
                .FirstOrDefaultAsync();
            dto.AppInstallAttribution = attr;
        }
        else if (flag.EntityType == FraudEntityType.DeviceRegistration)
        {
            var device = await _dbContext.DeviceRegistrations
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(d => d.Id == flag.EntityId)
                .Select(d => new FraudDeviceDto
                {
                    Id = d.Id,
                    FingerprintHash = d.FingerprintHash,
                    Platform = d.Platform,
                    IpAddress = d.IpAddress,
                    FirstSeenAt = d.FirstSeenAt,
                    LastSeenAt = d.LastSeenAt,
                    IsVerified = d.IsVerified,
                    RiskScore = d.RiskScore
                })
                .FirstOrDefaultAsync();
            dto.Device = device;
        }

        return dto;
    }

    public async Task<ConfirmResultDto> ConfirmAsync(Guid fraudFlagId, Guid confirmedBy)
    {
        var flag = await _dbContext.FraudFlags
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == fraudFlagId);

        if (flag == null)
            throw new InvalidOperationException($"FraudFlag {fraudFlagId} not found.");

        if (flag.Status != FraudFlagStatus.Pending)
            throw new InvalidOperationException($"FraudFlag {fraudFlagId} is already {flag.Status}.");

        var sideEffects = new List<string>();

        // 1. Confirm the flag
        flag.Confirm(confirmedBy, "Fraud confirmed by admin");

        // 2. Update related entity status to Rejected
        if (flag.EntityType == FraudEntityType.SalesReferral)
        {
            var referral = await _dbContext.SalesReferrals
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == flag.EntityId);

            if (referral != null)
            {
                // 3. Wallet reversal if commission already paid (check BEFORE MarkRejected changes status)
                if (referral.CommissionStatus == CommissionStatus.Paid)
                {
                    var commissionTx = await _dbContext.WalletTransactions
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(w => w.RelatedOrderId == referral.OrderId
                            && w.Type == WalletTransactionType.Commission
                            && w.OwnerId == referral.SalesmanId)
                        .FirstOrDefaultAsync();

                    if (commissionTx != null)
                    {
                        await _walletService.ReverseTransactionAsync(referral.SalesmanId, commissionTx.Id);
                        sideEffects.Add($"WalletReversal:{commissionTx.Amount}");
                    }
                }

                // Now reject the referral
                referral.MarkRejected("Fraud confirmed");
                sideEffects.Add($"SalesReferral.{referral.Id}.CommissionStatus=Rejected");
            }
        }
        else if (flag.EntityType == FraudEntityType.AppInstallAttribution)
        {
            var attribution = await _dbContext.AppInstallAttributions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == flag.EntityId);

            if (attribution != null)
            {
                // Wallet reversal if bonus already paid (check BEFORE MarkRejected changes status)
                if (attribution.AttributionStatus == AttributionStatus.Paid && attribution.WalletTransactionId.HasValue)
                {
                    await _walletService.ReverseTransactionAsync(attribution.SalesmanId, attribution.WalletTransactionId.Value);
                    sideEffects.Add("WalletReversal:AppInstallBonus");
                }

                attribution.MarkRejected("Fraud confirmed");
                sideEffects.Add($"AppInstallAttribution.{attribution.Id}.AttributionStatus=Rejected");
            }
        }

        // 4. Check 3-strike ban
        var customerBanned = false;
        if (flag.CustomerId.HasValue)
        {
            var confirmedCount = await _dbContext.FraudFlags
                .IgnoreQueryFilters()
                .AsNoTracking()
                .CountAsync(f => f.CustomerId == flag.CustomerId
                    && f.Status == FraudFlagStatus.Confirmed);

            // +1 for current flag (not yet saved)
            if (confirmedCount + 1 >= StrikeBanThreshold)
            {
                var customer = await _dbContext.Customers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.Id == flag.CustomerId.Value);

                if (customer != null && customer.IsActive)
                {
                    customer.UpdateCustomerDetails(
                        customer.FullName,
                        customer.PhoneNumber,
                        customer.Email,
                        customer.CustomerTier,
                        customer.DeviceId,
                        isActive: false);
                    customerBanned = true;
                    sideEffects.Add($"Customer.{customer.Id}.Banned (3-strike rule)");
                }
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("ConfirmAsync: FraudFlag {Id} confirmed by {Reviewer}. SideEffects: {Count}. Banned: {Banned}",
            fraudFlagId, confirmedBy, sideEffects.Count, customerBanned);

        return new ConfirmResultDto
        {
            Status = "Confirmed",
            SideEffects = sideEffects,
            CustomerBanned = customerBanned
        };
    }

    public async Task<DismissResultDto> DismissAsync(Guid fraudFlagId, Guid dismissedBy)
    {
        var flag = await _dbContext.FraudFlags
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == fraudFlagId);

        if (flag == null)
            throw new InvalidOperationException($"FraudFlag {fraudFlagId} not found.");

        if (flag.Status != FraudFlagStatus.Pending)
            throw new InvalidOperationException($"FraudFlag {fraudFlagId} is already {flag.Status}.");

        var sideEffects = new List<string>();

        // 1. Dismiss the flag
        flag.Dismiss(dismissedBy, "False positive — dismissed by admin");

        // 2. Whitelist related device if applicable
        if (flag.EntityType == FraudEntityType.DeviceRegistration)
        {
            var device = await _dbContext.DeviceRegistrations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Id == flag.EntityId);

            if (device != null && !device.IsVerified)
            {
                device.Verify();
                sideEffects.Add($"DeviceRegistration.{device.Id}.IsVerified=true");
            }
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("DismissAsync: FraudFlag {Id} dismissed by {Reviewer}. SideEffects: {Count}",
            fraudFlagId, dismissedBy, sideEffects.Count);

        return new DismissResultDto
        {
            Status = "Dismissed",
            SideEffects = sideEffects
        };
    }

    public async Task<FraudStatsDto> GetStatsAsync()
    {
        var flags = await _dbContext.FraudFlags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .ToListAsync();

        var stats = new FraudStatsDto
        {
            Pending = flags.Count(f => f.Status == FraudFlagStatus.Pending),
            Confirmed = flags.Count(f => f.Status == FraudFlagStatus.Confirmed),
            Dismissed = flags.Count(f => f.Status == FraudFlagStatus.Dismissed),
            Reviewed = flags.Count(f => f.Status == FraudFlagStatus.Reviewed)
        };

        // Loss prevented: sum commission amounts for confirmed SalesReferral flags
        var confirmedReferralIds = flags
            .Where(f => f.Status == FraudFlagStatus.Confirmed && f.EntityType == FraudEntityType.SalesReferral)
            .Select(f => f.EntityId)
            .ToList();

        if (confirmedReferralIds.Count > 0)
        {
            stats.TotalLossPrevented = await _dbContext.SalesReferrals
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => confirmedReferralIds.Contains(r.Id))
                .SumAsync(r => r.CommissionAmount);
        }

        // Top 5 flagged customers
        var topFlagged = flags
            .Where(f => f.CustomerId.HasValue)
            .GroupBy(f => f.CustomerId!.Value)
            .Select(g => new { CustomerId = g.Key, FlagCount = g.Count() })
            .OrderByDescending(x => x.FlagCount)
            .Take(5)
            .ToList();

        if (topFlagged.Count > 0)
        {
            var topCustomerIds = topFlagged.Select(t => t.CustomerId).ToList();
            var customerNames = await _dbContext.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(c => topCustomerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.FullName })
                .ToDictionaryAsync(c => c.Id, c => c.FullName);

            stats.TopFlaggedCustomers = topFlagged.Select(t => new TopFlaggedCustomerDto
            {
                CustomerId = t.CustomerId,
                CustomerName = customerNames.TryGetValue(t.CustomerId, out var name) ? name : "Unknown",
                FlagCount = t.FlagCount
            }).ToList();
        }

        return stats;
    }

    public async Task<List<FraudFlagDto>> GetMyFlagsAsync(Guid customerId)
    {
        var flags = await _dbContext.FraudFlags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => f.CustomerId == customerId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return flags.Select(f => new FraudFlagDto
        {
            Id = f.Id,
            CustomerId = f.CustomerId,
            EntityType = f.EntityType.ToString(),
            EntityId = f.EntityId,
            RiskScore = f.RiskScore,
            RiskFactors = f.RiskFactors,
            Status = f.Status.ToString(),
            CreatedAt = f.CreatedAt
        }).ToList();
    }

    private static FraudFlagStatus ParseStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "pending" => FraudFlagStatus.Pending,
            "confirmed" => FraudFlagStatus.Confirmed,
            "dismissed" => FraudFlagStatus.Dismissed,
            "reviewed" => FraudFlagStatus.Reviewed,
            _ => FraudFlagStatus.Pending
        };
    }
}
