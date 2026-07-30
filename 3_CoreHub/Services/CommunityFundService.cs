using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.CommunityFundAggregate;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Sprint 7 Q3 — Community fund service implementation.
/// Balance = sum of CommunityFund txs − sum of CommunityFundSpend txs on CommunityFundWallet.
/// Spend creates CommunityFundSpend wallet tx + CommunityFundSpendRecord audit entity.
/// </summary>
public class CommunityFundService(
    IVanAnDbContext dbContext,
    IWalletService walletService,
    ILogger<CommunityFundService> logger) : ICommunityFundService
{
    private readonly IVanAnDbContext _dbContext = dbContext;
    private readonly IWalletService _walletService = walletService;
    private readonly ILogger<CommunityFundService> _logger = logger;

    public async Task<CommunityFundBalanceDto> GetBalanceAsync()
    {
        var txs = await _dbContext.WalletTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.OwnerId == SystemWalletIds.CommunityFund)
            .ToListAsync();

        var totalCollected = txs.Where(t => t.Type == WalletTransactionType.CommunityFund).Sum(t => t.Amount);
        var totalSpent = txs.Where(t => t.Type == WalletTransactionType.CommunityFundSpend).Sum(t => Math.Abs(t.Amount));

        return new CommunityFundBalanceDto
        {
            Balance = totalCollected - totalSpent,
            TotalCollected = totalCollected,
            TotalSpent = totalSpent
        };
    }

    public async Task<CommunityFundSpendResultDto> SpendAsync(decimal amount, string reason, string recipient, Guid approvedBy)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive", nameof(amount));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason cannot be empty", nameof(reason));
        if (reason.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(reason), "Reason max 500 chars");
        if (string.IsNullOrWhiteSpace(recipient))
            throw new ArgumentException("Recipient cannot be empty", nameof(recipient));
        if (recipient.Length > 200)
            throw new ArgumentOutOfRangeException(nameof(recipient), "Recipient max 200 chars");
        if (approvedBy == Guid.Empty)
            throw new ArgumentException("ApprovedBy cannot be empty", nameof(approvedBy));

        var balance = await GetBalanceAsync();
        if (amount > balance.Balance)
            throw new InvalidOperationException($"Số dư quỹ không đủ. Current balance: {balance.Balance:N0}, requested: {amount:N0}");

        var tenantId = new TenantId(Guid.Empty); // system-wide

        // Create wallet transaction via WalletService (atomic, balance-checked)
        var walletTx = await _walletService.SpendCommunityFundAsync(amount, reason, approvedBy);

        // Create audit record
        var spendRecord = new CommunityFundSpendRecord(
            tenantId,
            amount,
            reason,
            recipient,
            approvedBy,
            walletTx.Id);
        _dbContext.CommunityFundSpendRecords.Add(spendRecord);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Community fund spend {Amount} by {ApprovedBy} for {Reason} → recipient {Recipient}",
            amount, approvedBy, reason, recipient);

        return new CommunityFundSpendResultDto
        {
            TransactionId = walletTx.Id,
            SpendRecordId = spendRecord.Id,
            BalanceAfter = balance.Balance - amount
        };
    }

    public async Task<PagedResult<CommunityFundSpendRecordDto>> GetHistoryAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _dbContext.CommunityFundSpendRecords
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(r => r.SpentAt);

        var total = await query.CountAsync();

        var records = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new CommunityFundSpendRecordDto
            {
                Id = r.Id,
                Amount = r.Amount,
                Reason = r.Reason,
                Recipient = r.Recipient,
                ApprovedBy = r.ApprovedBy,
                SpentAt = r.SpentAt,
                WalletTransactionId = r.WalletTransactionId
            })
            .ToListAsync();

        return new PagedResult<CommunityFundSpendRecordDto>
        {
            Total = total,
            Items = records
        };
    }
}
