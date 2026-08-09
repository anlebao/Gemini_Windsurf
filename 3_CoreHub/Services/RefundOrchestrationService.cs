using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Repositories;
using VanAn.CoreHub.Services;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services;

/// <summary>
/// VALCN v2.0 Phase 4 — Refund Orchestration implementation (UC-06, INV-002).
/// Coordinates 4-step reversal on order cancel/refund. Feature-flagged via ValcnV2_RefundReversal.
///
/// DRIFT from task card (resolved during ANALYZE):
///   - No IIdempotentOperationRepository/DbSet → natural idempotency: check if reversal entries already exist.
///   - No ILoyaltyIssuanceRecordRepository → direct IVanAnDbContext.LoyaltyIssuanceRecords (matches OrderWorkflowService pattern).
///   - No IWalletTransactionRepository → direct IVanAnDbContext.WalletTransactions (matches FraudReviewService pattern).
///   - No ILoyaltyRewardsService.DeductPointsForOrderAsync → use SubtractPointsAsync(customerId, points, reason).
///   - No payment integration (VNPay/Momo) → Option B: accrual liability entry (accountCode "331").
/// </summary>
public class RefundOrchestrationService : IRefundOrchestrationService
{
    private readonly ILoyaltyRewardsService _loyaltyService;
    private readonly ILoyaltyBudgetService _budgetService;
    private readonly IAccountingEntryRepository _accountingEntryRepo;
    private readonly IWalletService _walletService;
    private readonly IVanAnDbContext _dbContext;
    private readonly ILogger<RefundOrchestrationService> _logger;

    // Account code for "Phải trả khách hàng" (customer refund payable) — TT 152/2025/TT-BTC
    private const string RefundPayableAccountCode = "331";

    public RefundOrchestrationService(
        ILoyaltyRewardsService loyaltyService,
        ILoyaltyBudgetService budgetService,
        IAccountingEntryRepository accountingEntryRepo,
        IWalletService walletService,
        IVanAnDbContext dbContext,
        ILogger<RefundOrchestrationService> logger)
    {
        _loyaltyService = loyaltyService;
        _budgetService = budgetService;
        _accountingEntryRepo = accountingEntryRepo;
        _walletService = walletService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task OrchestrateReversalAsync(Guid orderId, TenantId tenantId, string reason, CancellationToken ct = default)
    {
        _logger.LogInformation("VALCN v2.0 Phase 4: Starting refund reversal for order {OrderId} (tenant {TenantId})", orderId, tenantId.Value);

        // ==================== NATURAL IDEMPOTENCY (fix I6 — no IdempotentOperation table needed) ====================
        // If reversal entries already exist for this CorrelationId, the reversal was already processed.
        var existingEntries = await _accountingEntryRepo.GetByCorrelationIdAsync(orderId, tenantId, ct);
        var alreadyReversed = existingEntries.Any(e => e.ReversalEntryId != null);
        if (alreadyReversed)
        {
            _logger.LogInformation("Refund reversal for order {OrderId} already processed (reversal entries exist) — skipping", orderId);
            return;
        }

        // ==================== STEP 2a: Payment refund / accrual liability ====================
        // CRITICAL: Nếu không có 2a, Cash ≠ Accounting → BCTC sai (vi phạm TT 152/2025/TT-BTC)
        // No payment provider integration (VNPay/Momo) exists → Option B: accrual liability entry.
        // TODO: Replace with actual payment refund API when payment provider is integrated (v3.0).
        await ProcessAccrualLiabilityAsync(orderId, tenantId, existingEntries, reason, ct);

        // ==================== STEP 2b: Accounting reversal ====================
        // Query AccountingEntry by CorrelationId (Phase 1 field) → CreateReversal for each non-reversed entry.
        // Re-query to include the accrual entry from 2a (it has the same CorrelationId).
        var allEntries = await _accountingEntryRepo.GetByCorrelationIdAsync(orderId, tenantId, ct);
        foreach (var entry in allEntries.Where(e => e.ReversalEntryId == null))
        {
            var reversalEntry = AccountingEntry.CreateReversal(entry, reason);  // preserves CorrelationId (Phase 1 fix M3)
            await _accountingEntryRepo.AddAsync(reversalEntry, ct);
            _logger.LogDebug("Step 2b: Created reversal entry for original {OriginalId} (CorrelationId={CorrelationId})", entry.Id, orderId);
        }
        await _dbContext.SaveChangesAsync(ct);

        // ==================== STEP 2c: Loyalty reversal ====================
        // Query LoyaltyIssuanceRecord by OrderId (Phase 1 entity) → SubtractPoints + MarkReversed + budget decrement.
        var issuanceRecords = await _dbContext.LoyaltyIssuanceRecords
            .Where(r => r.OrderId == orderId && r.TenantId == tenantId && !r.IsReversed)
            .ToListAsync(ct);

        var totalReversedPoints = 0;
        foreach (var record in issuanceRecords)
        {
            await _loyaltyService.SubtractPointsAsync(record.CustomerId, record.PointsIssued, $"Reversal: {reason}");
            record.MarkReversed();
            totalReversedPoints += record.PointsIssued;
        }
        await _dbContext.SaveChangesAsync(ct);

        if (totalReversedPoints > 0)
        {
            await _budgetService.DecrementIssuanceAsync(tenantId.Value, totalReversedPoints, ct);
            _logger.LogInformation("Step 2c: Reversed {Points} loyalty points for order {OrderId}", totalReversedPoints, orderId);
        }

        // ==================== STEP 2d: Referral commission reversal ====================
        // Query WalletTransaction by RelatedOrderId + Type == Commission (pattern from FraudReviewService.cs:191-197).
        var commissionTxns = await _dbContext.WalletTransactions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(w => w.RelatedOrderId == orderId && w.Type == WalletTransactionType.Commission)
            .ToListAsync(ct);

        foreach (var txn in commissionTxns)
        {
            await _walletService.ReverseTransactionAsync(txn.OwnerId, txn.Id);
            _logger.LogInformation("Step 2d: Reversed referral commission {Amount} for order {OrderId}", txn.Amount, orderId);
        }
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("VALCN v2.0 Phase 4: Full refund reversal completed for order {OrderId} (payment + accounting + loyalty + referral)", orderId);
    }

    /// <summary>
    /// Step 2a — Option B: Create accrual liability entry (no payment provider integration).
    /// Ensures Cash = Accounting per TT 152/2025/TT-BTC.
    /// AccountCode "331" = "Phải trả khách hàng" (customer refund payable).
    /// TODO: Replace with actual payment refund API when payment provider is integrated.
    /// </summary>
    private async Task ProcessAccrualLiabilityAsync(
        Guid orderId, TenantId tenantId, IEnumerable<AccountingEntry> existingEntries, string reason, CancellationToken ct)
    {
        // Only create accrual if there are original revenue entries to reverse (no point creating liability for a zero-revenue order)
        var revenueEntries = existingEntries.Where(e => e.EntryType == AccountingEntryType.Revenue).ToList();
        if (revenueEntries.Count == 0)
        {
            _logger.LogDebug("Step 2a: No revenue entries for order {OrderId} — skipping accrual liability", orderId);
            return;
        }

        var totalRevenue = revenueEntries.Sum(e => e.Amount);
        if (totalRevenue <= 0)
        {
            _logger.LogDebug("Step 2a: Total revenue for order {OrderId} is {Amount} — skipping accrual liability", orderId, totalRevenue);
            return;
        }

        var period = AccountingPeriod.FromDateTime(DateTime.UtcNow);
        var liabilityEntry = AccountingEntry.CreateExpense(
            tenantId,
            period,
            new Money(totalRevenue),
            $"Refund payable — Order {orderId} — {reason}",
            accountCode: RefundPayableAccountCode,
            reference: orderId.ToString(),
            correlationId: orderId);

        await _accountingEntryRepo.AddAsync(liabilityEntry, ct);
        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Step 2a: Created accrual liability entry {Amount} for order {OrderId} (accountCode {AccountCode})",
            totalRevenue, orderId, RefundPayableAccountCode);
    }
}
