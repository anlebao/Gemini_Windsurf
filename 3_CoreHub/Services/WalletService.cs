using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// WalletService — v1.4 base (Sprint 0) + Sprint 5 extensions.
    /// HR-SCALE-3: atomic BalanceAfter via SELECT FOR UPDATE pattern on PG (LINQ fallback on SQLite for tests).
    /// Sprint 5: ConfirmCodAsync/ConfirmAdvanceAsync/ConfirmAdvanceReceivedAsync/ReverseTransactionAsync/GetWalletAsync/GetPendingAdvancesAsync.
    /// Sprint 4 CoolingPeriodJob uses CreateTransactionAsync to pay commissions after 24h cooling.
    /// </summary>
    public class WalletService : IWalletService
    {
        private readonly IVanAnDbContext _dbContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<WalletService> _logger;

        public WalletService(
            IVanAnDbContext dbContext,
            ITenantProvider tenantProvider,
            ILogger<WalletService> logger)
        {
            _dbContext = dbContext;
            _tenantProvider = tenantProvider;
            _logger = logger;
        }

        /// <summary>
        /// v1.4: Base atomic method — creates WalletTransaction with correct BalanceAfter.
        /// HR-SCALE-3: SELECT FOR UPDATE on last transaction row for this owner prevents race conditions.
        /// On PostgreSQL: uses FOR UPDATE clause. On SQLite (tests): uses LINQ within transaction (database-level lock).
        /// </summary>
        public async Task<WalletTransaction> CreateTransactionAsync(
            Guid ownerId,
            WalletTransactionType type,
            decimal amount,
            string description,
            Guid? relatedOrderId = null,
            Guid? relatedTransactionId = null)
        {
            var tenantId = new TenantId(_tenantProvider.TenantId);

            _logger.LogInformation("Creating WalletTransaction: Owner={OwnerId} Type={Type} Amount={Amount}",
                ownerId, type, amount);

            // HR-SCALE-3: atomic BalanceAfter — transaction ensures no concurrent writes.
            // C1 fix (2026-09-14): run inside the EF execution strategy — Gateway PG
            // (NpgsqlRetryingExecutionStrategy, Phase 1 Scaling) rejects user-initiated
            // transactions outside CreateExecutionStrategy.
            WalletTransaction walletTx = null!;
            await _dbContext.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _dbContext.BeginTransactionAsync();

                try
                {
                    walletTx = await CreateWalletTxCoreAsync(
                        ownerId, type, amount, description, relatedOrderId, relatedTransactionId,
                        runningBalances: null);
                    await tx.CommitAsync();

                    _logger.LogInformation("WalletTransaction created: Id={Id} BalanceAfter={BalanceAfter}",
                        walletTx.Id, walletTx.BalanceAfter);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create WalletTransaction for Owner={OwnerId}", ownerId);
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return walletTx;
        }

        /// <summary>
        /// Settlement Batch-1 refactor: shared ledger-creation core. The caller MUST hold an
        /// ambient transaction (ExecuteAtomicAsync + BeginTransactionAsync). runningBalances
        /// carries per-owner running balances inside a multi-entry batch (COD → Settlement →
        /// fees) so the BalanceAfter chain stays correct within one commit — previously each
        /// entry committed separately, allowing partial wallet state on mid-flow failure.
        /// </summary>
        private async Task<WalletTransaction> CreateWalletTxCoreAsync(
            Guid ownerId,
            WalletTransactionType type,
            decimal amount,
            string description,
            Guid? relatedOrderId,
            Guid? relatedTransactionId,
            Dictionary<Guid, decimal>? runningBalances)
        {
            var tenantId = new TenantId(_tenantProvider.TenantId);

            decimal balanceBefore;
            if (runningBalances != null && runningBalances.TryGetValue(ownerId, out decimal cachedBalance))
            {
                balanceBefore = cachedBalance;
            }
            else
            {
                var isPostgres = _dbContext.ProviderName.Contains("PostgreSQL") ||
                                 _dbContext.ProviderName.Contains("Npgsql");
                if (isPostgres)
                {
                    // PG: SELECT FOR UPDATE locks the row for concurrent-safety
                    var lastTx = await _dbContext.WalletTransactions
                        .FromSqlRaw(
                            "SELECT * FROM \"WalletTransactions\" WHERE \"OwnerId\" = {0} ORDER BY \"CreatedAt\" DESC LIMIT 1 FOR UPDATE",
                            ownerId)
                        .FirstOrDefaultAsync();
                    balanceBefore = lastTx?.BalanceAfter ?? 0m;
                }
                else
                {
                    // SQLite (tests): LINQ within transaction — database-level lock provides atomicity
                    var lastTx = await _dbContext.WalletTransactions
                        .Where(w => w.OwnerId == ownerId)
                        .OrderByDescending(w => w.CreatedAt)
                        .FirstOrDefaultAsync();
                    balanceBefore = lastTx?.BalanceAfter ?? 0m;
                }
            }

            var walletTx = new WalletTransaction(
                tenantId,
                ownerId,
                type,
                amount,
                balanceBefore,
                description,
                relatedOrderId,
                relatedTransactionId);

            _dbContext.WalletTransactions.Add(walletTx);
            await _dbContext.SaveChangesAsync();
            if (runningBalances != null) runningBalances[ownerId] = walletTx.BalanceAfter;
            return walletTx;
        }

        /// <summary>
        /// Get current balance for an owner (last transaction's BalanceAfter, or 0 if no transactions).
        /// </summary>
        public async Task<decimal> GetBalanceAsync(Guid ownerId)
        {
            var lastTx = await _dbContext.WalletTransactions
                .Where(w => w.OwnerId == ownerId)
                .OrderByDescending(w => w.CreatedAt)
                .FirstOrDefaultAsync();

            return lastTx?.BalanceAfter ?? 0m;
        }

        /// <summary>
        /// Sprint 5: Get wallet summary — balance + transaction history sorted by CreatedAt desc.
        /// Cross-tenant query (IgnoreQueryFilters) — wallet is global per owner.
        /// </summary>
        public async Task<WalletSummaryDto> GetWalletAsync(Guid ownerId)
        {
            var transactions = await _dbContext.WalletTransactions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => w.OwnerId == ownerId)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();

            var balance = transactions.FirstOrDefault()?.BalanceAfter ?? 0m;

            return new WalletSummaryDto
            {
                Balance = balance,
                Transactions = transactions.Select(t => new WalletTransactionDto
                {
                    Id = t.Id,
                    Type = t.Type.ToString(),
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    Description = t.Description,
                    RelatedOrderId = t.RelatedOrderId,
                    RelatedTransactionId = t.RelatedTransactionId,
                    CreatedAt = t.CreatedAt
                }).ToList()
            };
        }

        /// <summary>
        /// Sprint 5: Shipper confirms COD collection for an order.
        /// Creates CODCollection tx for shipper (+amount) + Settlement tx for shop (-amount).
        /// Sets Order.CodCollectedAt. Idempotency: throws if CodCollectedAt already set.
        /// Settlement Batch-1 (TC-01): amount is verified against the authoritative order amount
        /// (Marketplace: CodAmount snapshot ?? TotalAmount; Reseller: SellPrice + DeliveryFee),
        /// the delivery task must be OutForDelivery/Delivered, the order must not be cancelled
        /// or already paid, and the entire flow commits in ONE transaction — a mid-flow failure
        /// can no longer leave wallet entries without the COD marker (or vice versa).
        /// </summary>
        public async Task<WalletTransaction> ConfirmCodAsync(Guid shipperId, Guid orderId, decimal amount)
        {
            WalletTransaction shipperTx = null!;
            await _dbContext.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _dbContext.BeginTransactionAsync();
                try
                {
                    // 1. Load order (tracked — MarkCodCollected mutates it). Cross-tenant query
                    // because delivery spans tenants.
                    var order = await _dbContext.Orders
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(o => o.Id == orderId);

                    if (order == null)
                        throw new InvalidOperationException($"Order {orderId} not found.");

                    // 2. Idempotency + state guards (checked INSIDE the transaction so a racing
                    // request cannot both pass validation and double-write wallet entries).
                    if (order.CodCollectedAt != null)
                        throw new InvalidOperationException($"COD already confirmed for order {orderId}.");
                    if (order.Status == OrderStatusId.Cancelled)
                        throw new InvalidOperationException($"Order {orderId} is cancelled — cannot confirm COD.");
                    if (order.PaymentStatus == "Paid")
                        throw new InvalidOperationException($"Order {orderId} already paid via {order.PaymentMethod} — no COD to collect.");

                    // 3. Verify caller is the shipper of this order's DeliveryTask, and the
                    // delivery is actually in progress/finished — COD cannot be collected for
                    // an order that was never out for delivery.
                    var deliveryTask = await _dbContext.DeliveryTasks
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d => d.OrderId == orderId && d.ShipperId == shipperId);

                    if (deliveryTask == null)
                        throw new UnauthorizedAccessException($"Caller is not the shipper of order {orderId}.");
                    if (deliveryTask.Status != DeliveryTaskStatus.OutForDelivery && deliveryTask.Status != DeliveryTaskStatus.Delivered)
                        throw new InvalidOperationException(
                            $"Order {orderId} delivery status is {deliveryTask.Status} — COD can only be confirmed when OutForDelivery or Delivered.");

                    // 4. TC-01: server-side authoritative amount — never trust the client value.
                    // Marketplace: CodAmount snapshot (if the order created one) else TotalAmount.
                    // Reseller: SellPrice + DeliveryFee (what the customer owes at the door).
                    decimal expectedAmount = order.CommerceMode == CommerceMode.Reseller
                        ? (order.SellPrice ?? 0m) + (order.DeliveryFee ?? 0m)
                        : (order.CodAmount ?? order.TotalAmount);
                    if (amount != expectedAmount)
                        throw new InvalidOperationException(
                            $"Amount {amount} does not match expected COD amount {expectedAmount} for order {orderId}.");

                    var balances = new Dictionary<Guid, decimal>();

                    // 5. Branch by CommerceMode (Sprint 7)
                    if (order.CommerceMode == CommerceMode.Reseller)
                    {
                        shipperTx = await CreateResellerCodSplitCoreAsync(shipperId, order, amount, balances);
                    }
                    else
                    {
                        // === Marketplace path ===
                        // 6. CODCollection (+amount, shipper)
                        shipperTx = await CreateWalletTxCoreAsync(
                            shipperId,
                            WalletTransactionType.CODCollection,
                            amount,
                            $"COD collection for order {orderId}",
                            orderId,
                            null,
                            balances);

                        // 7. Settlement (-amount, shop) — shop wallet owner = TenantId
                        await CreateWalletTxCoreAsync(
                            order.TenantId.Value,
                            WalletTransactionType.Settlement,
                            -amount,
                            $"COD settlement for order {orderId} (shipper collected)",
                            orderId,
                            shipperTx.Id,
                            balances);
                    }

                    // 8. Mark order COD collected — same commit as the wallet entries.
                    order.MarkCodCollected(amount);
                    await _dbContext.SaveChangesAsync();
                    await tx.CommitAsync();

                    _logger.LogInformation("COD confirmed: Order={OrderId} Shipper={ShipperId} Amount={Amount} Mode={Mode}",
                        orderId, shipperId, amount, order.CommerceMode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to confirm COD: Order={OrderId} Shipper={ShipperId} Amount={Amount}",
                        orderId, shipperId, amount);
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return shipperTx;
        }

        /// <summary>
        /// Sprint 7 — Reseller COD split. Runs inside ConfirmCodAsync's ambient transaction.
        /// Legs: CODCollection + Settlement(costPrice) + DeliveryFee + PlatformFee + CommunityFund.
        /// Settlement Batch-1 (TC-03): the Commission leg was REMOVED — the referral commission is
        /// paid exactly once by CoolingPeriodJob against the SalesReferral created at order
        /// completion (with fraud scoring + 24h cooling). The unpaid commission implicitly stays
        /// in PlatformWallet until payout — the split remains balanced.
        /// </summary>
        private async Task<WalletTransaction> CreateResellerCodSplitCoreAsync(
            Guid shipperId, Order order, decimal codAmount, Dictionary<Guid, decimal> balances)
        {
            var orderId = order.Id;
            var tenantId = order.TenantId.Value;
            var margin = order.PlatformMargin ?? 0m;
            var costPrice = order.CostPrice ?? 0m;
            var deliveryFee = order.DeliveryFee ?? 0m;
            var platformFeeRate = order.PlatformFeeRate ?? 0m;
            var communityFundRate = order.CommunityFundRate ?? 0m;

            var platformFee = margin * platformFeeRate;
            var communityFund = margin * communityFundRate;

            // Financial balance invariant: codAmount = SellPrice + DeliveryFee = CostPrice + Margin + DeliveryFee
            // margin = platformFee + communityFund + commission(reserved, unpaid here) + vanAnNetProfit
            // (assuming commission + platformFee + communityFund ≤ margin; remainder = VanAn net profit kept in PlatformWallet)

            // 1. CODCollection (+codAmount, shipper) — shipper thu hộ
            var shipperTx = await CreateWalletTxCoreAsync(
                shipperId,
                WalletTransactionType.CODCollection,
                codAmount,
                $"COD collection for order {orderId} (Reseller)",
                orderId,
                null,
                balances);

            // 2. Settlement (+costPrice, tenant) — Vạn An trả tenant giá vốn
            await CreateWalletTxCoreAsync(
                tenantId,
                WalletTransactionType.Settlement,
                costPrice,
                $"Cost price settlement for order {orderId} (Reseller — Vạn An mua từ tenant)",
                orderId,
                shipperTx.Id,
                balances);

            // 3. DeliveryFee (+deliveryFee, shipper) — Vạn An trả shipper phí giao
            if (deliveryFee > 0)
            {
                await CreateWalletTxCoreAsync(
                    shipperId,
                    WalletTransactionType.DeliveryFee,
                    deliveryFee,
                    $"Delivery fee for order {orderId} (Reseller)",
                    orderId,
                    shipperTx.Id,
                    balances);
            }

            // 4. PlatformFee (+platformFee, PlatformWallet) — Vạn An giữ margin share
            if (platformFee > 0)
            {
                await CreateWalletTxCoreAsync(
                    SystemWalletIds.PlatformWallet,
                    WalletTransactionType.PlatformFee,
                    platformFee,
                    $"Platform fee for order {orderId} (Reseller — {platformFeeRate:P1} of margin)",
                    orderId,
                    shipperTx.Id,
                    balances);
            }

            // 5. CommunityFund (+communityFund, CommunityFundWallet) — quỹ cộng đồng
            if (communityFund > 0)
            {
                await CreateWalletTxCoreAsync(
                    SystemWalletIds.CommunityFund,
                    WalletTransactionType.CommunityFund,
                    communityFund,
                    $"Community fund for order {orderId} (Reseller — {communityFundRate:P1} of margin)",
                    orderId,
                    shipperTx.Id,
                    balances);
            }

            _logger.LogInformation(
                "COD confirmed (Reseller): Order={OrderId} Shipper={ShipperId} COD={CodAmount} CostPrice={CostPrice} DeliveryFee={DeliveryFee} PlatformFee={PlatformFee} CommunityFund={CommunityFund}",
                orderId, shipperId, codAmount, costPrice, deliveryFee, platformFee, communityFund);

            return shipperTx;
        }

        /// <summary>
        /// Sprint 5: Shipper confirms advance payment to shop (paid cash before pickup).
        /// Creates AdvancePayment tx for shipper (-amount). Pending shop confirmation via ConfirmAdvanceReceivedAsync.
        /// Settlement Batch-1 (TC-02): the whole flow is atomic + idempotent — a second call for the
        /// same order throws instead of minting a duplicate advance (previously N calls created N
        /// AdvancePayment txs; on Reseller each call also credited real money to the tenant wallet).
        /// </summary>
        public async Task<WalletTransaction> ConfirmAdvanceAsync(Guid shipperId, Guid orderId, decimal amount)
        {
            WalletTransaction advanceTx = null!;
            await _dbContext.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _dbContext.BeginTransactionAsync();
                try
                {
                    // 1. Load order (cross-tenant)
                    var order = await _dbContext.Orders
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == orderId);

                    if (order == null)
                        throw new InvalidOperationException($"Order {orderId} not found.");
                    if (order.Status == OrderStatusId.Cancelled)
                        throw new InvalidOperationException($"Order {orderId} is cancelled — cannot confirm advance.");

                    // 2. Verify caller is the shipper
                    var deliveryTask = await _dbContext.DeliveryTasks
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d => d.OrderId == orderId && d.ShipperId == shipperId);

                    if (deliveryTask == null)
                        throw new UnauthorizedAccessException($"Caller is not the shipper of order {orderId}.");

                    // 3. TC-02 idempotency: at most ONE advance per order. Checked inside the
                    // transaction so concurrent calls serialize instead of both passing.
                    var existingAdvance = await _dbContext.WalletTransactions
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .AnyAsync(w => w.RelatedOrderId == orderId && w.Type == WalletTransactionType.AdvancePayment);
                    if (existingAdvance)
                        throw new InvalidOperationException($"Advance payment for order {orderId} already exists — idempotency guard.");

                    var balances = new Dictionary<Guid, decimal>();

                    // 4. Branch by CommerceMode (Sprint 7)
                    if (order.CommerceMode == CommerceMode.Reseller)
                    {
                        // Reseller: Vạn An ứng tiền cho tenant (not shipper).
                        // Shipper still calls this endpoint (auth verification), but the actual advance
                        // is from PlatformWallet → tenant. Shipper just triggers the flow.
                        var tenantId = order.TenantId.Value;

                        // AdvancePayment (-amount, PlatformWallet) — Vạn An ứng
                        advanceTx = await CreateWalletTxCoreAsync(
                            SystemWalletIds.PlatformWallet,
                            WalletTransactionType.AdvancePayment,
                            -amount,
                            $"Advance payment to tenant for order {orderId} (Reseller — Vạn An ứng)",
                            orderId,
                            null,
                            balances);

                        // Settlement (+amount, tenant) — tenant nhận
                        await CreateWalletTxCoreAsync(
                            tenantId,
                            WalletTransactionType.Settlement,
                            amount,
                            $"Advance received from Vạn An for order {orderId} (Reseller)",
                            orderId,
                            advanceTx.Id,
                            balances);

                        _logger.LogInformation("Advance confirmed (Reseller): Order={OrderId} Amount={Amount} (Vạn An → tenant)",
                            orderId, amount);
                    }
                    else
                    {
                        // === Marketplace path ===
                        // AdvancePayment (-amount, shipper) — shipper paid cash, wallet goes negative
                        advanceTx = await CreateWalletTxCoreAsync(
                            shipperId,
                            WalletTransactionType.AdvancePayment,
                            -amount,
                            $"Advance payment to shop for order {orderId}",
                            orderId,
                            null,
                            balances);

                        _logger.LogInformation("Advance confirmed: Order={OrderId} Shipper={ShipperId} Amount={Amount}",
                            orderId, shipperId, amount);
                    }

                    await tx.CommitAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to confirm advance: Order={OrderId} Shipper={ShipperId} Amount={Amount}",
                        orderId, shipperId, amount);
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return advanceTx;
        }

        /// <summary>
        /// Sprint 5: Shop confirms they received advance payment from shipper.
        /// Creates Settlement tx for shop (+amount), linked to original AdvancePayment via RelatedTransactionId.
        /// Settlement Batch-1 (TC-02): verifies the advance's order actually belongs to the
        /// confirming tenant before crediting — wallet queries bypass tenant filters, so without
        /// this check any customer could confirm another shop's advance and credit their own
        /// tenant wallet while marking the real shop's advance as settled.
        /// </summary>
        public async Task<WalletTransaction> ConfirmAdvanceReceivedAsync(Guid shopOwnerId, Guid advanceTransactionId)
        {
            WalletTransaction settlementTx = null!;
            await _dbContext.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _dbContext.BeginTransactionAsync();
                try
                {
                    // 1. Load original AdvancePayment tx
                    var advanceTx = await _dbContext.WalletTransactions
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(w => w.Id == advanceTransactionId && w.Type == WalletTransactionType.AdvancePayment);

                    if (advanceTx == null)
                        throw new InvalidOperationException($"AdvancePayment transaction {advanceTransactionId} not found.");

                    // 2. Idempotency: check if settlement already exists for this advance
                    var existingSettlement = await _dbContext.WalletTransactions
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .AnyAsync(w => w.RelatedTransactionId == advanceTransactionId && w.Type == WalletTransactionType.Settlement);

                    if (existingSettlement)
                        throw new InvalidOperationException($"Advance {advanceTransactionId} already confirmed.");

                    // 3. TC-02 cross-tenant guard: the advance must belong to an order of the
                    // confirming tenant. Without this, wallet tx IDs are forgeable capability
                    // tokens that credit arbitrary tenant wallets.
                    if (advanceTx.RelatedOrderId == null)
                        throw new InvalidOperationException($"Advance {advanceTransactionId} has no linked order — tenant ownership cannot be verified.");

                    var orderTenantId = await _dbContext.Orders
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(o => o.Id == advanceTx.RelatedOrderId.Value)
                        .Select(o => o.TenantId)
                        .FirstOrDefaultAsync();

                    if (orderTenantId == null)
                        throw new InvalidOperationException($"Order {advanceTx.RelatedOrderId} for advance {advanceTransactionId} not found.");
                    if (orderTenantId.Value != shopOwnerId)
                        throw new UnauthorizedAccessException(
                            $"Shop {shopOwnerId} does not own the order for advance {advanceTransactionId} — cross-tenant confirm rejected.");

                    // 4. Create Settlement tx for shop (+amount)
                    settlementTx = await CreateWalletTxCoreAsync(
                        shopOwnerId,
                        WalletTransactionType.Settlement,
                        -advanceTx.Amount, // AdvancePayment was -amount, so -(-amount) = +amount
                        $"Advance received from shipper for order {advanceTx.RelatedOrderId}",
                        advanceTx.RelatedOrderId,
                        advanceTransactionId,
                        runningBalances: null);

                    await tx.CommitAsync();

                    _logger.LogInformation("Advance received confirmed: AdvanceTx={AdvanceTxId} Shop={ShopOwnerId} Amount={Amount}",
                        advanceTransactionId, shopOwnerId, -advanceTx.Amount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to confirm advance received: AdvanceTx={AdvanceTxId} Shop={ShopOwnerId}",
                        advanceTransactionId, shopOwnerId);
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return settlementTx;
        }

        /// <summary>
        /// Sprint 5: List pending advance payments for a shop owner.
        /// Returns AdvancePayment txs for orders in this tenant that have no matching Settlement.
        /// </summary>
        public async Task<List<PendingAdvanceDto>> GetPendingAdvancesAsync(Guid shopOwnerId)
        {
            // Get all AdvancePayment txs for orders in this tenant (shopOwnerId = TenantId)
            var advanceTxs = await _dbContext.WalletTransactions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => w.Type == WalletTransactionType.AdvancePayment && w.RelatedOrderId != null)
                .ToListAsync();

            // Get all Settlement txs that link to AdvancePayments
            var settledTxIds = await _dbContext.WalletTransactions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => w.Type == WalletTransactionType.Settlement && w.RelatedTransactionId != null)
                .Select(w => w.RelatedTransactionId!.Value)
                .ToListAsync();

            var settledSet = settledTxIds.ToHashSet();

            // Filter: orders belonging to this shop (tenant), not yet settled
            // Use TenantId value object comparison (Pattern #8: construct value object before comparison)
            var orderIds = advanceTxs.Where(a => a.RelatedOrderId.HasValue).Select(a => a.RelatedOrderId!.Value).Distinct().ToList();
            if (orderIds.Count == 0)
                return new List<PendingAdvanceDto>();

            var shopTenantId = new TenantId(shopOwnerId);
            var shopOrders = await _dbContext.Orders
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(o => orderIds.Contains(o.Id) && o.TenantId == shopTenantId)
                .Select(o => o.Id)
                .ToListAsync();

            var shopOrderSet = shopOrders.ToHashSet();

            return advanceTxs
                .Where(a => a.RelatedOrderId.HasValue && shopOrderSet.Contains(a.RelatedOrderId.Value) && !settledSet.Contains(a.Id))
                .Select(a => new PendingAdvanceDto
                {
                    TransactionId = a.Id,
                    ShipperId = a.OwnerId,
                    OrderId = a.RelatedOrderId!.Value,
                    Amount = -a.Amount, // AdvancePayment was -amount, display positive
                    CreatedAt = a.CreatedAt
                })
                .ToList();
        }

        /// <summary>
        /// Sprint 5: Reverse a wallet transaction by creating a Reversal entry.
        /// Original is NOT modified (immutable). Reversal Amount = -original.Amount.
        /// </summary>
        public async Task<WalletTransaction> ReverseTransactionAsync(Guid ownerId, Guid originalTransactionId)
        {
            var original = await _dbContext.WalletTransactions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == originalTransactionId && w.OwnerId == ownerId);

            if (original == null)
                throw new InvalidOperationException($"Transaction {originalTransactionId} not found for owner {ownerId}.");

            var reversalTx = await CreateTransactionAsync(
                ownerId,
                WalletTransactionType.Reversal,
                -original.Amount, // Negate original: if original was +50k, reversal is -50k
                $"Reversal of transaction {originalTransactionId}",
                original.RelatedOrderId,
                originalTransactionId);

            _logger.LogInformation("Transaction reversed: Original={OriginalId} Reversal={ReversalId}",
                originalTransactionId, reversalTx.Id);

            return reversalTx;
        }

        /// <summary>
        /// Sprint 7 Q5: Confirm external payment (non-COD Reseller — VietQR/card).
        /// Reseller only — rejects Marketplace orders.
        /// Legs: ExternalPayment + Settlement(costPrice) + DeliveryFee + PlatformFee + CommunityFund.
        /// Settlement Batch-1: whole split is atomic (one commit), and the Commission leg was
        /// REMOVED (TC-03) — referral commission is paid exactly once by CoolingPeriodJob via the
        /// SalesReferral created at order completion, after fraud scoring + cooling.
        /// </summary>
        public async Task<WalletTransaction> ConfirmExternalPaymentAsync(Guid orderId, decimal amount, string paymentRef)
        {
            if (string.IsNullOrWhiteSpace(paymentRef))
                throw new ArgumentException("PaymentRef cannot be empty", nameof(paymentRef));

            WalletTransaction externalTx = null!;
            await _dbContext.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _dbContext.BeginTransactionAsync();
                try
                {
                    var order = await _dbContext.Orders
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(o => o.Id == orderId);

                    if (order == null)
                        throw new InvalidOperationException($"Order {orderId} not found.");

                    if (order.CommerceMode != CommerceMode.Reseller)
                        throw new InvalidOperationException($"Order {orderId} is not Reseller mode — external payment not applicable.");

                    if (order.CodCollectedAt != null)
                        throw new InvalidOperationException($"Order {orderId} already paid (COD collected).");

                    if (order.Status == OrderStatusId.Cancelled)
                        throw new InvalidOperationException($"Order {orderId} is cancelled — cannot confirm external payment.");

                    // Verify amount = SellPrice + DeliveryFee (server-side authoritative)
                    var expectedAmount = (order.SellPrice ?? 0m) + (order.DeliveryFee ?? 0m);
                    if (amount != expectedAmount)
                        throw new InvalidOperationException($"Amount {amount} does not match expected SellPrice+DeliveryFee {expectedAmount}.");

                    var tenantId = order.TenantId.Value;
                    var margin = order.PlatformMargin ?? 0m;
                    var costPrice = order.CostPrice ?? 0m;
                    var deliveryFee = order.DeliveryFee ?? 0m;
                    var platformFeeRate = order.PlatformFeeRate ?? 0m;
                    var communityFundRate = order.CommunityFundRate ?? 0m;

                    var platformFee = margin * platformFeeRate;
                    var communityFund = margin * communityFundRate;

                    var balances = new Dictionary<Guid, decimal>();

                    // 1. ExternalPayment (+amount, PlatformWallet) — customer pays Vạn An
                    externalTx = await CreateWalletTxCoreAsync(
                        SystemWalletIds.PlatformWallet,
                        WalletTransactionType.ExternalPayment,
                        amount,
                        $"External payment for order {orderId} (Ref: {paymentRef})",
                        orderId,
                        null,
                        balances);

                    // 2. Settlement (+costPrice, tenant) — Vạn An trả tenant giá vốn
                    await CreateWalletTxCoreAsync(
                        tenantId,
                        WalletTransactionType.Settlement,
                        costPrice,
                        $"Cost price settlement for order {orderId} (Reseller — external payment)",
                        orderId,
                        externalTx.Id,
                        balances);

                    // 3. DeliveryFee (+deliveryFee, shipper) — Vạn An trả shipper
                    if (deliveryFee > 0)
                    {
                        // ShipperId from DeliveryTask
                        var deliveryTask = await _dbContext.DeliveryTasks
                            .IgnoreQueryFilters()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(d => d.OrderId == orderId);
                        if (deliveryTask != null)
                        {
                            await CreateWalletTxCoreAsync(
                                deliveryTask.ShipperId,
                                WalletTransactionType.DeliveryFee,
                                deliveryFee,
                                $"Delivery fee for order {orderId} (Reseller — external payment)",
                                orderId,
                                externalTx.Id,
                                balances);
                        }
                    }

                    // 4. PlatformFee (+platformFee, PlatformWallet)
                    if (platformFee > 0)
                    {
                        await CreateWalletTxCoreAsync(
                            SystemWalletIds.PlatformWallet,
                            WalletTransactionType.PlatformFee,
                            platformFee,
                            $"Platform fee for order {orderId} (Reseller — external payment, {platformFeeRate:P1} of margin)",
                            orderId,
                            externalTx.Id,
                            balances);
                    }

                    // 5. CommunityFund (+communityFund, CommunityFundWallet)
                    if (communityFund > 0)
                    {
                        await CreateWalletTxCoreAsync(
                            SystemWalletIds.CommunityFund,
                            WalletTransactionType.CommunityFund,
                            communityFund,
                            $"Community fund for order {orderId} (Reseller — external payment, {communityFundRate:P1} of margin)",
                            orderId,
                            externalTx.Id,
                            balances);
                    }

                    // Mark order as paid (use CodCollectedAt as payment confirmation marker)
                    order.MarkCodCollected(amount);
                    await _dbContext.SaveChangesAsync();
                    await tx.CommitAsync();

                    _logger.LogInformation(
                        "External payment confirmed (Reseller): Order={OrderId} Amount={Amount} Ref={PaymentRef} CostPrice={CostPrice} DeliveryFee={DeliveryFee} PlatformFee={PlatformFee} CommunityFund={CommunityFund}",
                        orderId, amount, paymentRef, costPrice, deliveryFee, platformFee, communityFund);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to confirm external payment: Order={OrderId} Amount={Amount} Ref={PaymentRef}",
                        orderId, amount, paymentRef);
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return externalTx;
        }

        /// <summary>
        /// Sprint 7 Q3: Spend from community fund (SysAdmin disbursement).
        /// Creates CommunityFundSpend tx (-amount on CommunityFundWallet).
        /// Audit record created by CommunityFundService.SpendAsync (separate concern).
        /// </summary>
        public async Task<WalletTransaction> SpendCommunityFundAsync(decimal amount, string reason, Guid approvedBy)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason cannot be empty", nameof(reason));

            var balance = await GetBalanceAsync(SystemWalletIds.CommunityFund);
            if (amount > balance)
                throw new InvalidOperationException($"Số dư quỹ cộng đồng không đủ. Current balance: {balance:N0}, requested: {amount:N0}");

            var spendTx = await CreateTransactionAsync(
                SystemWalletIds.CommunityFund,
                WalletTransactionType.CommunityFundSpend,
                -amount, // negative = money out
                $"Community fund spend: {reason} (approved by {approvedBy})",
                null);

            _logger.LogInformation("Community fund spent: Amount={Amount} Reason={Reason} ApprovedBy={ApprovedBy}",
                amount, reason, approvedBy);

            return spendTx;
        }
    }
}
