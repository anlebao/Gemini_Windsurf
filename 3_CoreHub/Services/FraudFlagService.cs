using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// CC-S4 (Sprint 4 v1.2): Fraud flag service implementation.
/// Create/query/confirm fraud flags. Admin review queue in Sprint 6.
/// </summary>
public class FraudFlagService(
    IVanAnDbContext dbContext,
    ILogger<FraudFlagService> logger) : IFraudFlagService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly ILogger<FraudFlagService> _logger = logger;

    public async Task<FraudFlag> CreateFlagAsync(
        Guid tenantId,
        FraudEntityType entityType,
        Guid entityId,
        Guid? customerId,
        FraudFlagType flagType,
        int riskScore,
        string riskFactors,
        string description)
    {
        var flag = new FraudFlag(
            new TenantId(tenantId),
            entityType,
            entityId,
            customerId,
            flagType,
            riskScore,
            riskFactors,
            description);

        _dbContext.FraudFlags.Add(flag);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("CreateFlagAsync: FraudFlag {Id} created for {EntityType} {EntityId}, riskScore={Score}",
            flag.Id, entityType, entityId, riskScore);

        return flag;
    }

    public async Task<List<FraudFlag>> GetPendingFlagsAsync()
    {
        return await _dbContext.FraudFlags
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => f.Status == FraudFlagStatus.Pending)
            .OrderByDescending(f => f.RiskScore)
            .ToListAsync();
    }

    public async Task ConfirmFlagAsync(Guid flagId, Guid reviewedBy, string note)
    {
        var flag = await _dbContext.FraudFlags
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == flagId);

        if (flag == null)
            throw new InvalidOperationException($"FraudFlag {flagId} not found");

        flag.Confirm(reviewedBy, note);

        // Update related entity status to Rejected
        if (flag.EntityType == FraudEntityType.SalesReferral)
        {
            var referral = await _dbContext.SalesReferrals
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == flag.EntityId);
            referral?.MarkRejected("Fraud confirmed");
        }
        else if (flag.EntityType == FraudEntityType.AppInstallAttribution)
        {
            var attribution = await _dbContext.AppInstallAttributions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == flag.EntityId);
            attribution?.MarkRejected("Fraud confirmed");
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("ConfirmFlagAsync: FraudFlag {Id} confirmed by {Reviewer}", flagId, reviewedBy);
    }

    public async Task DismissFlagAsync(Guid flagId, Guid reviewedBy, string note)
    {
        var flag = await _dbContext.FraudFlags
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == flagId);

        if (flag == null)
            throw new InvalidOperationException($"FraudFlag {flagId} not found");

        flag.Dismiss(reviewedBy, note);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("DismissFlagAsync: FraudFlag {Id} dismissed by {Reviewer}", flagId, reviewedBy);
    }
}
