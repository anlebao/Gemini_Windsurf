# TASK CARD — Phase 4: Refund Orchestration + Full Reversal (UC-06, INV-002)

> **Status:** 📋 PENDING (requires Phase 1 + Phase 2 + Phase 3 complete)
> **Priority:** P0 — HIGHEST RISK (production gap — INV-002 currently violated, BCTC sai nếu thiếu 2a)
> **Branch:** `feature/valcn-v2-phase4-refund-reversal`
> **Estimated sessions:** 3-4 (tăng từ 2-3 — add 2a payment refund + 2d referral reversal)
> **Mode:** IMPLEMENT
> **Domain modification:** NO (logic only — LoyaltyIssuanceRecord + CorrelationId from Phase 1, budget decrement from Phase 3, WalletService.ReverseTransactionAsync existing)

## Objective
`RefundOrchestrationService` coordinate **đầy đủ UC-06 (4 steps)** khi order cancel/refund:
- **Step 2a — Payment refund / accrual liability:** Hoàn tiền customer HOẶC ghi nhận liability entry (đảm bảo Cash = Accounting)
- **Step 2b — Accounting reversal:** `AccountingEntry.CreateReversal` (preserve CorrelationId)
- **Step 2c — Loyalty reversal:** `LoyaltyIssuanceRecord` query + `DeductPoints` + budget decrement
- **Step 2d — Referral commission reversal:** `WalletService.ReverseTransactionAsync` (đã có sẵn)

**Why:** Verification phát hiện INV-002 đang bị vi phạm. UC spec UC-06 require 4 steps. **Bỏ 2a → BCTC sai** (Cash ≠ Accrual, vi phạm TT 152/2025/TT-BTC). Bỏ 2d → wallet balance partner sai + CAC metric sai.

**Dependency (fix C1):** Phase 1 đã set `CorrelationId` trên AccountingEntry + tạo `LoyaltyIssuanceRecord` → Phase 4 query được cả 2 bằng OrderId.

## Prerequisites
- [ ] Phase 1 complete — `LoyaltyIssuanceRecord` entity + `AccountingEntry.CorrelationId` + `CreateReversal` preserves CorrelationId + `IFeatureFlagService` registered
- [ ] Phase 2 complete — `Order.PlatformFeeAmount` (for reversal amount calc)
- [ ] Phase 3 complete — `LoyaltyBudgetService.DecrementIssuanceAsync` method exists
- [ ] `dotnet build VanAn.sln` Release — 0 errors (baseline)

## Files to Modify/Create

| File | Status | Purpose |
|------|--------|---------|
| `3_CoreHub/Services/IRefundOrchestrationService.cs` | NEW | Interface |
| `3_CoreHub/Services/RefundOrchestrationService.cs` | NEW | Coordinate 4 steps reversal |
| `3_CoreHub/Services/OrderWorkflowService.cs` | MODIFY | Hook refund orchestration on cancel — **wrapped in feature flag** |
| `3_CoreHub/Services/WalletService.cs` | (existing — đã có ReverseTransactionAsync) | No change — chỉ call |
| DI registration | MODIFY | Register RefundOrchestrationService |
| Tests | NEW | Refund reversal tests (4 steps, feature ON + OFF) |

## Detailed Changes

### Change 1: IRefundOrchestrationService interface
```csharp
public interface IRefundOrchestrationService
{
    /// <summary>
    /// Orchestrate FULL reversal (UC-06 — 4 steps) when an order is cancelled or refunded.
    /// 2a. Payment refund (hoặc accrual liability entry) — đảm bảo Cash = Accounting
    /// 2b. Accounting reversal (AccountingEntry.CreateReversal, preserve CorrelationId)
    /// 2c. Loyalty reversal (LoyaltyIssuanceRecord query → DeductPoints → budget decrement)
    /// 2d. Referral commission reversal (WalletService.ReverseTransactionAsync)
    /// Idempotent via IdempotentOperation entity (fix I6).
    /// Full refund/cancel only — no partial refund (fix M2).
    /// </summary>
    Task OrchestrateReversalAsync(Guid orderId, TenantId tenantId, string reason, CancellationToken ct = default);
}
```

### Change 2: RefundOrchestrationService — 4 steps
```csharp
public class RefundOrchestrationService : IRefundOrchestrationService
{
    private readonly ILoyaltyRewardsService _loyaltyService;
    private readonly ILoyaltyIssuanceRecordRepository _issuanceRepo;  // Phase 1 entity
    private readonly ILoyaltyBudgetService _budgetService;  // Phase 3
    private readonly IAccountingService _accountingService;
    private readonly IAccountingEntryRepository _accountingEntryRepo;
    private readonly IWalletService _walletService;  // existing — for 2d referral reversal
    private readonly IWalletTransactionRepository _walletTxRepo;  // query commission by OrderId
    private readonly IIdempotentOperationRepository _idempotencyRepo;  // fix I6
    private readonly IOrderRepository _orderRepo;
    private readonly ILogger<RefundOrchestrationService> _logger;

    public async Task OrchestrateReversalAsync(Guid orderId, TenantId tenantId, string reason, CancellationToken ct)
    {
        // FIX I6: Idempotency via existing IdempotentOperation entity
        var operationId = $"refund-reversal-{orderId}";
        var existing = await _idempotencyRepo.GetByOperationIdAsync(operationId, ct);
        if (existing != null)
        {
            _logger.LogInformation("Refund reversal for order {OrderId} already processed", orderId);
            return;
        }

        var order = await _orderRepo.GetByIdAsync(orderId, tenantId, ct);
        if (order == null) return;

        // ==================== STEP 2a: Payment refund / accrual liability ====================
        // CRITICAL: Nếu không có 2a, Cash ≠ Accounting → BCTC sai (vi phạm TT 152/2025)
        // INVESTIGATE: Check if payment provider (VNPay/Momo) integration exists
        //   - If YES: call payment refund API → actual cash out
        //   - If NO (MVP COD-only hoặc chưa integrate): create accrual liability entry
        await ProcessPaymentRefundOrAccrualAsync(order, reason, ct);
        // Implementation options (decide in INVESTIGATE):
        //   Option A (payment integrated): await _paymentService.RefundAsync(order.PaymentTransactionId, order.TotalAmount, ct);
        //   Option B (accrual — no payment integration):
        //     var liabilityEntry = AccountingEntry.CreateExpense(
        //         tenantId, period, new Money(order.TotalAmount), $"Refund payable — Order #{orderId}",
        //         accountCode: "331",  // Phải trả khách hàng
        //         correlationId: orderId);
        //     await _accountingService.CreateEntryAsync(liabilityEntry, ct);

        // ==================== STEP 2b: Accounting reversal ====================
        // Query AccountingEntry by CorrelationId (Phase 1 field) → CreateReversal
        var originalEntries = await _accountingEntryRepo.GetByCorrelationIdAsync(orderId, tenantId, ct);
        foreach (var entry in originalEntries.Where(e => e.ReversalEntryId == null))
        {
            var reversalEntry = AccountingEntry.CreateReversal(entry, reason);  // preserves CorrelationId (Phase 1 fix M3)
            await _accountingService.CreateEntryAsync(reversalEntry, ct);
        }

        // ==================== STEP 2c: Loyalty reversal ====================
        // Query LoyaltyIssuanceRecord by OrderId (Phase 1 entity) → DeductPoints + budget decrement
        var issuanceRecords = await _issuanceRepo.GetByOrderIdAsync(orderId, tenantId, ct);
        var totalReversedPoints = 0;
        foreach (var record in issuanceRecords.Where(r => !r.IsReversed))
        {
            await _loyaltyService.DeductPointsForOrderAsync(record.CustomerId, tenantId, record.PointsIssued, reason, ct);
            record.MarkReversed();
            totalReversedPoints += record.PointsIssued;
        }
        await _issuanceRepo.UpdateRangeAsync(issuanceRecords, ct);
        if (totalReversedPoints > 0)
        {
            await _budgetService.DecrementIssuanceAsync(tenantId, totalReversedPoints, ct);
        }

        // ==================== STEP 2d: Referral commission reversal ====================
        // Query WalletTransaction by RelatedOrderId (existing field — Domain.cs:3914)
        // Filter: Type == Commission (referral commission only, not other wallet txs)
        // Use WalletService.ReverseTransactionAsync (existing — WalletService.cs:503)
        var commissionTxns = await _walletTxRepo.GetByRelatedOrderIdAsync(orderId, WalletTransactionType.Commission, ct);
        foreach (var txn in commissionTxns.Where(t => t.Type == WalletTransactionType.Commission))
        {
            await _walletService.ReverseTransactionAsync(txn.OwnerId, txn.Id);
            _logger.LogInformation("Reversed referral commission {Amount} for order {OrderId}", txn.Amount, orderId);
        }

        // ==================== STEP: Mark idempotent ====================
        await _idempotencyRepo.AddAsync(new IdempotentOperation(operationId, "RefundReversal", "Success", DateTime.UtcNow), ct);

        _logger.LogInformation("Full refund reversal completed for order {OrderId}: payment + accounting + loyalty + referral", orderId);
    }

    private async Task ProcessPaymentRefundOrAccrualAsync(Order order, string reason, CancellationToken ct)
    {
        // INVESTIGATE in Phase 0/Phase 4: decide Option A (payment API) or Option B (accrual)
        // MVP-safe default: Option B (accrual liability) — ensures BCTC correctness without payment integration
        // TODO: replace with actual payment refund when payment provider integrated
    }
}
```

### Change 3: OrderWorkflowService — hook on cancel (feature-flagged)
**Default OFF = existing behavior (cancel silent, no reversal).**

```csharp
// In TransitionStatusAsync or HandleOrderCancelledAsync
if (newStatus.Value == "cancelled")
{
    // VALCN v2.0 Phase 4 — feature-flagged, default OFF
    // When OFF: existing behavior (cancel silent, no reversal — INV-002 gap remains)
    // When ON: 4-step reversal (UC-06)
    if (await _featureFlagService.IsEnabledAsync("ValcnV2_RefundReversal", ct))
    {
        await _refundOrchestrationService.OrchestrateReversalAsync(
            order.Id, order.TenantId, $"Order cancelled: {reason}", ct);
    }
    // When OFF: no reversal (existing behavior — historical cancelled orders not retroactively reversed)
}
```

**Note:** `OrderStatusId` is a record with string Value (<ref_snippet file="C:\VibeCoding\Gemini_Windsurf\1_Shared\Domain.cs" lines="422-432" />). Status values: "pending", "confirmed", "preparing", "ready", "delivering", "completed", "cancelled". **No "refunded" status** — refund flows through "cancelled".

### Change 4: DI Registration
```csharp
services.AddScoped<IRefundOrchestrationService, RefundOrchestrationService>();
```

## Verification
- [ ] `dotnet build VanAn.sln` Release — 0 errors
- [ ] `guard-check.ps1` — PASS
- [ ] Test: Feature OFF (default) → Order cancel → existing behavior (no reversal, silent cancel)
- [ ] Test: Feature ON → Order cancel → **Step 2a**: payment refund OR accrual liability entry created (Cash = Accounting)
- [ ] Test: Feature ON → Order cancel → **Step 2b**: accounting reversal entry created (CorrelationId preserved, revenue = 0)
- [ ] Test: Feature ON → Order cancel → **Step 2c**: loyalty points reversed (DeductPoints + LoyaltyIssuanceRecord.IsReversed = true + budget decremented)
- [ ] Test: Feature ON → Order cancel → **Step 2d**: referral commission reversed (WalletTransaction reversal created, partner wallet balance giảm)
- [ ] Test: Feature ON → Idempotency — call OrchestrateReversalAsync twice → only processed once
- [ ] Test: Feature ON → Order with no rewards → no error, accounting + payment + referral still processed
- [ ] Test: Feature ON → Order with no referral commission → no error, other steps still processed
- [ ] Test: Feature ON → **Cash = Accounting after reversal** (critical — TT 152/2025 compliance)
- [ ] Test: Feature ON → INV-002 enforced: Refunded → Reward reversed
- [ ] Test: Feature ON → **No partial refund** (fix M2) — only full cancel triggers reversal
- [ ] Test: Toggle OFF→ON runtime → new cancellations get reversal (no restart, no retroactive)
- [ ] Existing tests pass (feature OFF = same as before)

## Rollback
`git revert <commit>` OR toggle OFF via `/admin/valcn-features` — feature OFF = existing behavior (silent cancel). **Note:** Toggle OFF không retroactive — orders đã cancelled khi ON sẽ giữ reversal entries (không undo).

## Risk Mitigation
- **Step 2a payment integration risk:** Nếu chưa có payment provider → dùng Option B (accrual liability entry, accountCode "331"). Đảm bảo BCTC đúng mà không cần payment integration. Upgrade sang Option A khi payment provider available.
- **Partial failure:** Mỗi step độc lập. Nếu 2b fail nhưng 2c success → retry 2b only (idempotency per step if needed).
- **Race condition:** IdempotentOperation prevents double processing.
- **Existing redemption flow:** DO NOT refactor `RedemptionService` — chỉ add order-level reversal.

---

## ANALYZE UPDATE (to be filled during INVESTIGATE step)

### INVESTIGATE checklist
- [ ] Read `OrderWorkflowService.TransitionStatusAsync` full method (line 54) — find cancel hook point
- [ ] Verify `OrderStatusId` values (<ref_snippet file="C:\VibeCoding\Gemini_Windsurf\1_Shared\Domain.cs" lines="422-432" />) — confirm no "refunded", only "cancelled"
- [ ] Read `OmnichannelOrderService.CancelOrderAsync` (line 430-496) — existing cancel logic, `CalculateRefundAmount`
- [ ] **CRITICAL — Payment integration:** Grep for `IPaymentService`, `PaymentProvider`, `VNPay`, `Momo`, `VietQR` — confirm whether payment refund API exists
- [ ] If no payment integration: confirm Option B (accrual liability entry) approach — find accountCode "331" or equivalent in chart of accounts
- [ ] Read `WalletService.ReverseTransactionAsync` (line 503-525) — confirm signature + usage pattern
- [ ] Find `IWalletTransactionRepository` — `GetByRelatedOrderIdAsync` method or add
- [ ] Read `FraudReviewService.cs:189-222` — pattern reference for referral commission reversal
- [ ] Find `IIdempotentOperationRepository` — confirm `GetByOperationIdAsync` method (fix I6)
- [ ] Find `IAccountingEntryRepository` — confirm `GetByCorrelationIdAsync` method or add
- [ ] Find `ILoyaltyIssuanceRecordRepository` (Phase 1) — confirm `GetByOrderIdAsync` method
- [ ] Read `AccountingEntry.CreateReversal` — confirm preserves CorrelationId (Phase 1 fix M3)
- [ ] Find `ILoyaltyRewardsService.DeductPointsForOrderAsync` or equivalent — may need to add
- [ ] Check if refund event already exists in OutboxEvent types
- [ ] Find existing order cancel tests — pattern for new tests

### Verified Accurate
- (fill after investigation)

### DRIFT
- (fill if investigation finds drift)
