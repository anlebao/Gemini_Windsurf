using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Common;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Aggregates.WalletAggregate;
using VanAn.Shared.Domain.Audit;
using VanAn.Shared.Domain.Common;
using VanAn.Shared.Services;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// WalletService — v1.4 base (Sprint 0) + Sprint 5 extensions.
    /// HR-SCALE-3: atomic BalanceAfter via SELECT FOR UPDATE pattern on PG (LINQ fallback on SQLite for tests).
    /// Sprint 5: ConfirmCodAsync/ConfirmAdvanceAsync/ConfirmAdvanceReceivedAsync/ReverseTransactionAsync/GetWalletAsync/GetPendingAdvancesAsync.
    /// Sprint 4 CoolingPeriodJob uses CreateTransactionAsync to pay commissions after 24h cooling.
    /// Settlement Batch-2 (TC-06): COD/external payment also marks the order Paid, emits
    /// OrderPaymentConfirmed (Outbox → NATS → ShopERP SQLite replica), and generates
    /// accounting entries on the local bookset (Gateway PG).
    /// </summary>
    public class WalletService : IWalletService
    {
        private readonly IVanAnDbContext _dbContext;
        private readonly ITenantProvider _tenantProvider;
        private readonly ILogger<WalletService> _logger;
        private readonly IOrderService? _orderService;
        private readonly IOutboxRepository? _outboxRepository;
        private readonly IShopFeatureSettingsService? _shopFeatureSettingsService;
        private readonly IAuditTrailService? _auditTrailService;

        public WalletService(
            IVanAnDbContext dbContext,
            ITenantProvider tenantProvider,
            ILogger<WalletService> logger,
            IOrderService? orderService = null,
            IOutboxRepository? outboxRepository = null,
            IShopFeatureSettingsService? shopFeatureSettingsService = null,
            IAuditTrailService? auditTrailService = null)
        {
            _dbContext = dbContext;
            _tenantProvider = tenantProvider;
            _logger = logger;
            _orderService = orderService;
            _outboxRepository = outboxRepository;
            _shopFeatureSettingsService = shopFeatureSettingsService;
            _auditTrailService = auditTrailService;
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
            Guid? relatedTransactionId = null,
            TenantId? tenantIdOverride = null)
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
                        runningBalances: null, tenantIdOverride: tenantIdOverride);
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
            Dictionary<Guid, decimal>? runningBalances,
            TenantId? tenantIdOverride = null)
        {
            // Settlement Batch-4 RV follow-up: order-linked legs must carry the ORDER's
            // tenant — the request-scoped provider is empty on Gateway community/admin
            // endpoints, which left every wallet tx at TenantId=Guid.Empty and made the
            // admin settlements tenant filter dead. Non-order txs keep the provider value.
            var tenantId = tenantIdOverride ?? new TenantId(_tenantProvider.TenantId);

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
                    // TC-10 S8: serialize per-owner wallet writes. FOR UPDATE below can
                    // only lock a row that EXISTS — two concurrent FIRST transactions for
                    // a new owner would both read balanceBefore=0 and corrupt the balance
                    // chain. The transaction-scoped advisory lock closes that gap; it is
                    // released automatically on commit/rollback.
                    if (_dbContext is DbContext efContext)
                    {
                        // CTE-wrapped: DO blocks cannot take bind parameters.
                        await efContext.Database.ExecuteSqlRawAsync(
                            "WITH l AS (SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))) SELECT 1",
                            ownerId.ToString());
                    }

                    // PG: SELECT FOR UPDATE locks the row for concurrent-safety.
                    // IgnoreQueryFilters is mandatory here: the raw SQL runs INSIDE the
                    // composed tenant filter, so LIMIT 1 picks the owner's globally-latest
                    // tx before the filter — a tx written under another tenant context
                    // would be filtered out and silently return balanceBefore=0.
                    var lastTx = await _dbContext.WalletTransactions
                        .FromSqlRaw(
                            "SELECT * FROM \"WalletTransactions\" WHERE \"OwnerId\" = {0} ORDER BY \"CreatedAt\" DESC LIMIT 1 FOR UPDATE",
                            ownerId)
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync();
                    balanceBefore = lastTx?.BalanceAfter ?? 0m;
                }
                else
                {
                    // SQLite (tests): LINQ within transaction — database-level lock provides atomicity
                    var lastTx = await _dbContext.WalletTransactions
                        .IgnoreQueryFilters()
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

            // TC-10 S7: append-only audit trail for every wallet movement — Reversal
            // entries also flow through here. Best-effort: an audit failure must never
            // roll back a financial transaction.
            await TryAuditWalletTxAsync(walletTx);
            return walletTx;
        }

        /// <summary>
        /// TC-10 S7: audit a wallet transaction creation. The audit row is persisted via
        /// the async AuditLogQueue (non-accounting types), so it never blocks or
        /// participates in the caller's ambient transaction.
        /// </summary>
        private async Task TryAuditWalletTxAsync(WalletTransaction walletTx)
        {
            if (_auditTrailService == null)
                return;

            try
            {
                var newValues = JsonSerializer.Serialize(new
                {
                    walletTx.OwnerId,
                    Type = walletTx.Type.ToString(),
                    walletTx.Amount,
                    walletTx.BalanceAfter,
                    walletTx.RelatedOrderId,
                    walletTx.RelatedTransactionId,
                    walletTx.Description
                });
                var correlationId = walletTx.RelatedOrderId?.ToString()
                    ?? walletTx.RelatedTransactionId?.ToString();

                await _auditTrailService.LogCreateAsync(
                    AuditableEntityType.WalletTransaction,
                    walletTx.Id,
                    newValues,
                    correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Audit logging failed for WalletTransaction {Id} — wallet entry already persisted",
                    walletTx.Id);
            }
        }

        /// <summary>
        /// Get current balance for an owner (last transaction's BalanceAfter, or 0 if no transactions).
        /// </summary>
        public async Task<decimal> GetBalanceAsync(Guid ownerId)
        {
            var lastTx = await _dbContext.WalletTransactions
                .IgnoreQueryFilters()
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

            // TC-08/TC-09: số dư khả dụng = balance − COD đang giữ hộ − pending withdrawals
            var codHeld = await ComputeHeldCodAsync(ownerId);
            var pendingWithdrawalAmounts = await _dbContext.WithdrawalRequests
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.OwnerId == ownerId &&
                    (r.Status == WithdrawalStatus.Pending || r.Status == WithdrawalStatus.Approved))
                .Select(r => r.Amount)
                .ToListAsync();
            var pendingWithdrawals = pendingWithdrawalAmounts.Sum(); // SQLite can't Sum decimal server-side

            return new WalletSummaryDto
            {
                Balance = balance,
                CodHeld = codHeld,
                AvailableBalance = balance - codHeld - pendingWithdrawals,
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
        /// Settlement Batch-2 (TC-06): collecting COD is the cash-basis payment event —
        /// the order is marked Paid (method "COD"), an OrderPaymentConfirmed outbox event is
        /// committed atomically (→ ShopERP SQLite replica + local accounting entries), and
        /// accounting entries are generated on this bookset (Gateway PG) after commit.
        /// </summary>
        public async Task<WalletTransaction> ConfirmCodAsync(Guid shipperId, Guid orderId, decimal amount)
        {
            WalletTransaction shipperTx = null!;
            Order? confirmedOrder = null;
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
                            balances,
                            order.TenantId);

                        // 7. Settlement (-amount, shop) — shop wallet owner = TenantId
                        await CreateWalletTxCoreAsync(
                            order.TenantId.Value,
                            WalletTransactionType.Settlement,
                            -amount,
                            $"COD settlement for order {orderId} (shipper collected)",
                            orderId,
                            shipperTx.Id,
                            balances,
                            order.TenantId);
                    }

                    // 8. Mark order COD collected — same commit as the wallet entries.
                    //    TC-06: also marks the order Paid (method COD, ref = CODCollection tx).
                    order.MarkCodCollected(amount, PaymentMethodConstants.Cod, shipperTx.Id.ToString());

                    // 9. TC-06: enqueue OrderPaymentConfirmed atomically — NatsSyncWorker publishes
                    //    vanan.cloud.order.payment.confirmed.{shopInstanceId} → ShopERP
                    //    PaymentConfirmedSubscriber marks the SQLite replica Paid + generates
                    //    the tenant's local accounting entries (idempotent by order reference).
                    await EnqueueOrderPaymentConfirmedAsync(order, shipperTx.Id.ToString());

                    await _dbContext.SaveChangesAsync();
                    await tx.CommitAsync();
                    confirmedOrder = order;

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

            // TC-06: accounting entries on this bookset (Gateway PG) — best-effort AFTER
            // commit; payment state is already durable (same policy as ConfirmPaymentAsync).
            if (confirmedOrder != null)
                await TryGenerateOrderAccountingAsync(confirmedOrder.Id, confirmedOrder.TenantId.Value);

            return shipperTx;
        }

        /// <summary>
        /// Settlement Batch-2 (TC-06): enqueue OrderPaymentConfirmed for the ShopERP replica.
        /// Mirrors OrderService.MarkPaidAsync's event payload — routed by the tenant's
        /// ShopInstanceId (PG-only column) so only the owning ShopERP instance consumes it.
        /// Runs inside the caller's ambient transaction (outbox row commits with the
        /// wallet entries + order mark). Routing-key lookup is best-effort — the column
        /// does not exist on SQLite test contexts.
        /// </summary>
        private async Task EnqueueOrderPaymentConfirmedAsync(Order order, string transactionId)
        {
            if (_outboxRepository == null)
            {
                _logger.LogWarning(
                    "OutboxRepository not available — OrderPaymentConfirmed for order {OrderId} not enqueued; ShopERP replica will not receive payment status",
                    order.Id);
                return;
            }

            string? routingKey = null;
            try
            {
                var shopInstanceId = await _dbContext.Tenants
                    .IgnoreQueryFilters()
                    .Where(t => t.Id == order.TenantId && t.ShopInstanceId.HasValue)
                    .Select(t => t.ShopInstanceId!.Value)
                    .FirstOrDefaultAsync();
                if (shopInstanceId != Guid.Empty)
                    routingKey = shopInstanceId.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ShopInstanceId lookup failed for tenant {TenantId} — OrderPaymentConfirmed will be published without routing key",
                    order.TenantId.Value);
            }

            var payload = new
            {
                EventId = Guid.NewGuid(),
                OrderId = order.Id,
                TenantId = order.TenantId.Value,
                TransactionId = transactionId,
                PaymentMethod = order.PaymentMethod ?? PaymentMethodConstants.Cod,
                PaidAt = DateTime.UtcNow
            };

            var outboxEvent = new OutboxEvent(
                order.TenantId,
                new ElectronicInvoiceId(Guid.Empty), // non-invoice event — R14 domain limitation
                "OrderPaymentConfirmed",
                JsonSerializer.Serialize(payload),
                routingKey,
                correlationId: order.Id);
            // EnqueueAsync only tracks the entity — the caller's SaveChangesAsync commits it
            // atomically with the order update.
            await _outboxRepository.EnqueueAsync(outboxEvent);
        }

        /// <summary>
        /// Settlement Batch-2 (TC-06): generate accounting entries on this process's bookset
        /// (Gateway PG) after COD/external payment commits. Best-effort — payment state is
        /// already durable; failures are logged for reconciliation (same policy as
        /// OrderService.ConfirmPaymentAsync). Honours the tenant's Accounting_Sync_Enabled
        /// toggle; GenerateAccountingEntriesAsync is idempotent via the order-reference
        /// check so a later POS/webhook confirm cannot double-book.
        /// </summary>
        private async Task TryGenerateOrderAccountingAsync(Guid orderId, Guid tenantId)
        {
            if (_orderService == null)
                return;

            try
            {
                if (_shopFeatureSettingsService != null)
                {
                    bool accountingEnabled = await _shopFeatureSettingsService.IsEnabledAsync(
                        tenantId, nameof(ShopFeatureSettingsDto.Accounting_Sync_Enabled));
                    if (!accountingEnabled)
                    {
                        _logger.LogInformation(
                            "Accounting sync disabled for tenant {TenantId} — skipping entry generation for order {OrderId}",
                            tenantId, orderId);
                        return;
                    }
                }

                // Reload with Items+Product for COGS (mirrors ConfirmPaymentAsync's
                // GetByIdWithIncludesAsync — Customer intentionally NOT included).
                var orderWithItems = await _dbContext.Orders
                    .IgnoreQueryFilters()
                    .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                if (orderWithItems == null)
                {
                    _logger.LogWarning("TryGenerateOrderAccounting: order {OrderId} not found — skipping entries", orderId);
                    return;
                }

                await _orderService.GenerateAccountingEntriesAsync(orderWithItems, new TenantId(tenantId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Accounting entry generation failed for order {OrderId} — payment already recorded; manual reconciliation may be needed",
                    orderId);
            }
        }

        /// <summary>
        /// Sprint 7 — Reseller COD split. Runs inside ConfirmCodAsync's ambient transaction.
        /// Legs: CODCollection + Settlement(costPrice) + DeliveryFee + PlatformFee + CommunityFund.
        /// Settlement Batch-1 (TC-03): the Commission leg was REMOVED — the referral commission is
        /// paid exactly once by CoolingPeriodJob against the SalesReferral created at order
        /// completion (with fraud scoring + 24h cooling). The unpaid commission implicitly stays
        /// in PlatformWallet until payout — the split remains balanced.
        /// </summary>
        /// <summary>
        /// Settlement Batch-4 (TC-10 S3): margin split with VND rounding + rate invariant.
        /// Amounts are whole VND — halves round away from zero (same convention as
        /// InvoicePolicyService). Rates must be non-negative and their sum must not
        /// exceed 1: commission (paid separately by CoolingPeriodJob) + platformFee +
        /// communityFund must fit inside the margin; the remainder stays implicitly in
        /// PlatformWallet as Vạn An net profit (decision: no dedicated ledger tx).
        /// </summary>
        private static (decimal platformFee, decimal communityFund) ComputeMarginSplit(
            Guid orderId, decimal margin, decimal platformFeeRate, decimal communityFundRate)
        {
            if (platformFeeRate < 0m || communityFundRate < 0m)
                throw new InvalidOperationException(
                    $"Invalid fee config for order {orderId} — margin rates cannot be negative.");
            if (platformFeeRate + communityFundRate > 1m)
                throw new InvalidOperationException(
                    $"Invalid fee config for order {orderId} — platformFeeRate ({platformFeeRate:P1}) + communityFundRate ({communityFundRate:P1}) exceeds 100% of margin.");

            var platformFee = Math.Round(margin * platformFeeRate, 0, MidpointRounding.AwayFromZero);
            var communityFund = Math.Round(margin * communityFundRate, 0, MidpointRounding.AwayFromZero);
            return (platformFee, communityFund);
        }

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

            var (platformFee, communityFund) = ComputeMarginSplit(orderId, margin, platformFeeRate, communityFundRate);

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
                balances,
                order.TenantId);

            // 2. Settlement (+costPrice, tenant) — Vạn An trả tenant giá vốn
            await CreateWalletTxCoreAsync(
                tenantId,
                WalletTransactionType.Settlement,
                costPrice,
                $"Cost price settlement for order {orderId} (Reseller — Vạn An mua từ tenant)",
                orderId,
                shipperTx.Id,
                balances,
                order.TenantId);

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
                    balances,
                    order.TenantId);
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
                    balances,
                    order.TenantId);
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
                    balances,
                    order.TenantId);
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
                            balances,
                            order.TenantId);

                        // Settlement (+amount, tenant) — tenant nhận
                        await CreateWalletTxCoreAsync(
                            tenantId,
                            WalletTransactionType.Settlement,
                            amount,
                            $"Advance received from Vạn An for order {orderId} (Reseller)",
                            orderId,
                            advanceTx.Id,
                            balances,
                            order.TenantId);

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
                            balances,
                            order.TenantId);

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
                        runningBalances: null,
                        tenantIdOverride: orderTenantId);

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
            // TC-10 S6: push tenant/order filtering to the database — the previous
            // version materialized EVERY AdvancePayment + Settlement row (all tenants)
            // and joined in memory. Correlated EXISTS keep the whole filter server-side.
            // Pattern #8: construct the TenantId value object before comparing.
            var shopTenantId = new TenantId(shopOwnerId);
            return await _dbContext.WalletTransactions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => w.Type == WalletTransactionType.AdvancePayment
                    && w.RelatedOrderId != null
                    && _dbContext.Orders.IgnoreQueryFilters()
                        .Any(o => o.Id == w.RelatedOrderId!.Value && o.TenantId == shopTenantId)
                    && !_dbContext.WalletTransactions.IgnoreQueryFilters()
                        .Any(s => s.Type == WalletTransactionType.Settlement
                            && s.RelatedTransactionId == w.Id))
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new PendingAdvanceDto
                {
                    TransactionId = w.Id,
                    ShipperId = w.OwnerId,
                    OrderId = w.RelatedOrderId!.Value,
                    Amount = -w.Amount, // AdvancePayment was -amount, display positive
                    CreatedAt = w.CreatedAt
                })
                .ToListAsync();
        }

        /// <summary>
        /// Settlement Batch-3 (TC-08, Q1): Shipper nộp tiền COD đã thu hộ — per-order remit (Q1a).
        /// Marketplace: Remittance(-codAmount, shipper) + Settlement(+codAmount, shop wallet = TenantId).
        /// Reseller: Remittance(-codAmount, shipper) + Settlement(+codAmount, PlatformWallet) (Q1b).
        /// Closes the ledger loop: shipper nets to fee-only, beneficiary nets to zero (physical cash
        /// handed over — no platform credit). Amount is derived server-side from the order; the
        /// whole pair commits atomically and at most one Remittance can exist per order.
        /// </summary>
        public async Task<WalletTransaction> RemitCodAsync(Guid shipperId, Guid orderId)
        {
            WalletTransaction remitTx = null!;
            await _dbContext.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _dbContext.BeginTransactionAsync();
                try
                {
                    // 1. Load order (cross-tenant — delivery spans tenants)
                    var order = await _dbContext.Orders
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == orderId);

                    if (order == null)
                        throw new InvalidOperationException($"Order {orderId} not found.");
                    if (order.Status == OrderStatusId.Cancelled)
                        throw new InvalidOperationException($"Order {orderId} is cancelled — cannot remit COD.");
                    if (order.CodCollectedAt == null)
                        throw new InvalidOperationException($"COD not yet collected for order {orderId} — nothing to remit.");

                    // 2. Verify caller is the shipper of this order's DeliveryTask
                    var deliveryTask = await _dbContext.DeliveryTasks
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(d => d.OrderId == orderId && d.ShipperId == shipperId);

                    if (deliveryTask == null)
                        throw new UnauthorizedAccessException($"Caller is not the shipper of order {orderId}.");

                    // 3. Idempotency: at most ONE remittance per order (checked inside the tx
                    // so concurrent calls serialize instead of both passing).
                    var existingRemit = await _dbContext.WalletTransactions
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .AnyAsync(w => w.RelatedOrderId == orderId && w.Type == WalletTransactionType.Remittance);
                    if (existingRemit)
                        throw new InvalidOperationException($"COD remittance for order {orderId} already exists — idempotency guard.");

                    // 4. Server-side authoritative amount — same source as ConfirmCodAsync.
                    decimal codAmount = order.CommerceMode == CommerceMode.Reseller
                        ? (order.SellPrice ?? 0m) + (order.DeliveryFee ?? 0m)
                        : (order.CodAmount ?? order.TotalAmount);

                    var balances = new Dictionary<Guid, decimal>();

                    // 5. Remittance (-codAmount, shipper) — shipper hands over the collected cash
                    remitTx = await CreateWalletTxCoreAsync(
                        shipperId,
                        WalletTransactionType.Remittance,
                        -codAmount,
                        $"COD remittance for order {orderId}",
                        orderId,
                        null,
                        balances,
                        order.TenantId);

                    // 6. Settlement (+codAmount, beneficiary) — Marketplace: shop wallet
                    //    (order.TenantId); Reseller: PlatformWallet (Q1b — Vạn An nhận COD hộ).
                    var beneficiary = order.CommerceMode == CommerceMode.Reseller
                        ? SystemWalletIds.PlatformWallet
                        : order.TenantId.Value;
                    await CreateWalletTxCoreAsync(
                        beneficiary,
                        WalletTransactionType.Settlement,
                        codAmount,
                        $"COD remittance received for order {orderId} ({order.CommerceMode})",
                        orderId,
                        remitTx.Id,
                        balances,
                        order.TenantId);

                    await tx.CommitAsync();

                    _logger.LogInformation(
                        "COD remitted: Order={OrderId} Shipper={ShipperId} Amount={Amount} Beneficiary={Beneficiary} Mode={Mode}",
                        orderId, shipperId, codAmount, beneficiary, order.CommerceMode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remit COD: Order={OrderId} Shipper={ShipperId}",
                        orderId, shipperId);
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return remitTx;
        }

        /// <summary>
        /// TC-08: COD the shipper collected but has not yet remitted (and not reversed).
        /// </summary>
        public async Task<List<PendingRemittanceDto>> GetPendingRemittancesAsync(Guid shipperId)
        {
            var unremitted = await GetUnremittedCodTxsAsync(shipperId);
            return unremitted
                .Select(t => new PendingRemittanceDto
                {
                    OrderId = t.RelatedOrderId!.Value,
                    Amount = t.Amount,
                    CollectedAt = t.CreatedAt
                })
                .ToList();
        }

        /// <summary>
        /// TC-08/TC-09: sum of COD amounts the owner collected but has not remitted/reversed —
        /// "tiền đang giữ hộ" that must not be withdrawable. 0 for non-shippers.
        /// </summary>
        private async Task<decimal> ComputeHeldCodAsync(Guid ownerId)
        {
            var unremitted = await GetUnremittedCodTxsAsync(ownerId);
            return unremitted.Sum(t => t.Amount);
        }

        /// <summary>
        /// CODCollection txs for the owner whose orders have no Remittance leg yet and which were
        /// not reversed. Wallet is append-only — reversal is detected via Reversal.RelatedTransactionId.
        /// </summary>
        private async Task<List<WalletTransaction>> GetUnremittedCodTxsAsync(Guid ownerId)
        {
            var codTxs = await _dbContext.WalletTransactions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => w.OwnerId == ownerId && w.Type == WalletTransactionType.CODCollection && w.RelatedOrderId != null)
                .ToListAsync();
            if (codTxs.Count == 0)
                return new List<WalletTransaction>();

            var remittedOrderIds = (await _dbContext.WalletTransactions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => w.Type == WalletTransactionType.Remittance && w.RelatedOrderId != null)
                .Select(w => w.RelatedOrderId!.Value)
                .ToListAsync()).ToHashSet();

            var reversedTxIds = (await _dbContext.WalletTransactions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(w => w.Type == WalletTransactionType.Reversal && w.RelatedTransactionId != null)
                .Select(w => w.RelatedTransactionId!.Value)
                .ToListAsync()).ToHashSet();

            return codTxs
                .Where(t => !remittedOrderIds.Contains(t.RelatedOrderId!.Value) && !reversedTxIds.Contains(t.Id))
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
                originalTransactionId,
                tenantIdOverride: original.TenantId);

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
            Order? confirmedOrder = null;
            await _dbContext.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _dbContext.BeginTransactionAsync();
                try
                {
                    // TC-10 S9: lock the order row on PG. Without FOR UPDATE, two
                    // concurrent confirms both read the pre-commit state under
                    // READ COMMITTED, both pass the Paid checks, and double-write the
                    // wallet split. The loser blocks here and re-reads post-commit.
                    var isPostgres = _dbContext.ProviderName.Contains("PostgreSQL")
                        || _dbContext.ProviderName.Contains("Npgsql");
                    Order? order;
                    if (isPostgres)
                    {
                        order = await _dbContext.Orders
                            .FromSqlRaw("SELECT * FROM \"Orders\" WHERE \"Id\" = {0} FOR UPDATE", orderId)
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync();
                    }
                    else
                    {
                        order = await _dbContext.Orders
                            .IgnoreQueryFilters()
                            .FirstOrDefaultAsync(o => o.Id == orderId);
                    }

                    if (order == null)
                        throw new InvalidOperationException($"Order {orderId} not found.");

                    if (order.CommerceMode != CommerceMode.Reseller)
                        throw new InvalidOperationException($"Order {orderId} is not Reseller mode — external payment not applicable.");

                    if (order.CodCollectedAt != null)
                        throw new InvalidOperationException($"Order {orderId} already paid (COD collected).");

                    // TC-06: Paid can now also come from COD/external marking — reject a
                    // second payment confirmation regardless of which path paid first.
                    if (order.PaymentStatus == "Paid")
                        throw new InvalidOperationException($"Order {orderId} already paid via {order.PaymentMethod}.");

                    if (order.Status == OrderStatusId.Cancelled)
                        throw new InvalidOperationException($"Order {orderId} is cancelled — cannot confirm external payment.");

                    // TC-10 S9: a payment reference must be unique across orders — a
                    // reused ref means a webhook replay or a double-pay attempt.
                    var refAlreadyUsed = await _dbContext.Orders
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .AnyAsync(o => o.VietQR_TransactionId == paymentRef && o.Id != orderId);
                    if (refAlreadyUsed)
                        throw new InvalidOperationException(
                            $"Payment reference '{paymentRef}' was already used for another order — replay rejected.");

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

                    var (platformFee, communityFund) = ComputeMarginSplit(orderId, margin, platformFeeRate, communityFundRate);

                    var balances = new Dictionary<Guid, decimal>();

                    // 1. ExternalPayment (+amount, PlatformWallet) — customer pays Vạn An
                    externalTx = await CreateWalletTxCoreAsync(
                        SystemWalletIds.PlatformWallet,
                        WalletTransactionType.ExternalPayment,
                        amount,
                        $"External payment for order {orderId} (Ref: {paymentRef})",
                        orderId,
                        null,
                        balances,
                        order.TenantId);

                    // 2. Settlement (+costPrice, tenant) — Vạn An trả tenant giá vốn
                    await CreateWalletTxCoreAsync(
                        tenantId,
                        WalletTransactionType.Settlement,
                        costPrice,
                        $"Cost price settlement for order {orderId} (Reseller — external payment)",
                        orderId,
                        externalTx.Id,
                        balances,
                        order.TenantId);

                    // 3. DeliveryFee (+deliveryFee, shipper) — Vạn An trả shipper
                    if (deliveryFee > 0)
                    {
                        // TC-10 S9: pick the DELIVERED/active task — a plain
                        // FirstOrDefault could return a cancelled or failed delivery
                        // attempt and credit the fee to the wrong shipper.
                        var deliveryTask = await _dbContext.DeliveryTasks
                            .IgnoreQueryFilters()
                            .AsNoTracking()
                            .Where(d => d.OrderId == orderId
                                && d.Status != DeliveryTaskStatus.Cancelled
                                && d.Status != DeliveryTaskStatus.Failed)
                            .OrderByDescending(d => d.Status == DeliveryTaskStatus.Delivered)
                            .ThenByDescending(d => d.CreatedAt)
                            .FirstOrDefaultAsync();
                        if (deliveryTask != null)
                        {
                            await CreateWalletTxCoreAsync(
                                deliveryTask.ShipperId,
                                WalletTransactionType.DeliveryFee,
                                deliveryFee,
                                $"Delivery fee for order {orderId} (Reseller — external payment)",
                                orderId,
                                externalTx.Id,
                                balances,
                                order.TenantId);
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
                            balances,
                            order.TenantId);
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
                            balances,
                            order.TenantId);
                    }

                    // Mark order as paid — TC-06: sets PaymentStatus=Paid (method EXTERNAL,
                    // ref = caller's paymentRef) + emits OrderPaymentConfirmed atomically.
                    order.MarkCodCollected(amount, PaymentMethodConstants.External, paymentRef);
                    await EnqueueOrderPaymentConfirmedAsync(order, paymentRef);
                    await _dbContext.SaveChangesAsync();
                    await tx.CommitAsync();
                    confirmedOrder = order;

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

            // TC-06: accounting entries on this bookset (Gateway PG) — best-effort after commit.
            if (confirmedOrder != null)
                await TryGenerateOrderAccountingAsync(confirmedOrder.Id, confirmedOrder.TenantId.Value);

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

        // ============================================================
        // Settlement Batch-3 (TC-09, Q4): WithdrawalRequest lifecycle —
        // Pending → Approved/Rejected (SystemAdmin) → Paid (manual bank ref).
        // The Withdrawal wallet tx is created exactly once at pay time.
        // ============================================================

        /// <summary>TC-09: minimum withdrawal amount per docs (03-salesman §7.2, 04-shipper §10.3).</summary>
        public const decimal MinWithdrawalAmount = 500_000m;

        /// <summary>
        /// TC-09: Owner requests a payout. Validates min amount + available balance
        /// (ledger balance − COD đang giữ hộ) and blocks while another request is
        /// Pending/Approved. The wallet tx is NOT created here — only at MarkPaid.
        /// </summary>
        public async Task<WithdrawalRequest> RequestWithdrawalAsync(Guid ownerId, decimal amount)
        {
            if (amount < MinWithdrawalAmount)
                throw new ArgumentException($"Số tiền rút tối thiểu là {MinWithdrawalAmount:N0}đ.", nameof(amount));

            WithdrawalRequest request = null!;
            await _dbContext.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _dbContext.BeginTransactionAsync();
                try
                {
                    // Chống double-spend: one open request per owner at a time (checked inside
                    // the tx so concurrent requests serialize).
                    var hasOpen = await _dbContext.WithdrawalRequests
                        .IgnoreQueryFilters()
                        .AnyAsync(r => r.OwnerId == ownerId &&
                            (r.Status == WithdrawalStatus.Pending || r.Status == WithdrawalStatus.Approved));
                    if (hasOpen)
                        throw new InvalidOperationException(
                            "A withdrawal request is already pending or approved — wait for it to be processed or cancel it first.");

                    // Available balance = ledger balance − COD đang giữ hộ (TC-08)
                    var lastTx = await _dbContext.WalletTransactions
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(w => w.OwnerId == ownerId)
                        .OrderByDescending(w => w.CreatedAt)
                        .Select(w => w.BalanceAfter)
                        .FirstOrDefaultAsync();
                    var codHeld = await ComputeHeldCodAsync(ownerId);
                    var available = lastTx - codHeld;
                    if (amount > available)
                        throw new InvalidOperationException(
                            $"Insufficient available balance: {available:N0}đ (balance {lastTx:N0}đ − COD held {codHeld:N0}đ), requested {amount:N0}đ.");

                    request = new WithdrawalRequest(new TenantId(_tenantProvider.TenantId), ownerId, amount);
                    _dbContext.WithdrawalRequests.Add(request);
                    await _dbContext.SaveChangesAsync();
                    await tx.CommitAsync();

                    _logger.LogInformation("Withdrawal requested: Id={RequestId} Owner={OwnerId} Amount={Amount}",
                        request.Id, ownerId, amount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create withdrawal request: Owner={OwnerId} Amount={Amount}",
                        ownerId, amount);
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return request;
        }

        /// <summary>TC-09: owner's own withdrawal request history (newest first).</summary>
        public async Task<List<WithdrawalRequestDto>> GetWithdrawalsAsync(Guid ownerId)
        {
            return await _dbContext.WithdrawalRequests
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(r => r.OwnerId == ownerId)
                .OrderByDescending(r => r.RequestedAt)
                .Select(r => new WithdrawalRequestDto
                {
                    Id = r.Id,
                    OwnerId = r.OwnerId,
                    Amount = r.Amount,
                    Status = r.Status.ToString(),
                    BankReference = r.BankReference,
                    RejectReason = r.RejectReason,
                    RequestedAt = r.RequestedAt,
                    ProcessedAt = r.ProcessedAt,
                    WalletTransactionId = r.WalletTransactionId
                })
                .ToListAsync();
        }

        /// <summary>TC-09: owner cancels their own Pending request.</summary>
        public async Task CancelWithdrawalAsync(Guid ownerId, Guid requestId)
        {
            var request = await _dbContext.WithdrawalRequests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
                throw new InvalidOperationException($"Withdrawal request {requestId} not found.");
            if (request.OwnerId != ownerId)
                throw new UnauthorizedAccessException($"Withdrawal request {requestId} does not belong to this owner.");

            request.Cancel();
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Withdrawal cancelled: Id={RequestId} Owner={OwnerId}", requestId, ownerId);
        }

        /// <summary>TC-09 admin: paginated request list across all owners.</summary>
        public async Task<WithdrawalRequestListResult> GetWithdrawalRequestsAsync(WithdrawalStatus? status, int page, int pageSize)
        {
            var query = _dbContext.WithdrawalRequests
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(r => r.RequestedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new WithdrawalRequestDto
                {
                    Id = r.Id,
                    OwnerId = r.OwnerId,
                    Amount = r.Amount,
                    Status = r.Status.ToString(),
                    BankReference = r.BankReference,
                    RejectReason = r.RejectReason,
                    RequestedAt = r.RequestedAt,
                    ProcessedAt = r.ProcessedAt,
                    WalletTransactionId = r.WalletTransactionId
                })
                .ToListAsync();

            return new WithdrawalRequestListResult { Total = total, Page = page, PageSize = pageSize, Items = items };
        }

        /// <summary>TC-09 admin: Pending → Approved.</summary>
        public async Task ApproveWithdrawalAsync(Guid requestId, Guid adminId)
        {
            var request = await _dbContext.WithdrawalRequests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
                throw new InvalidOperationException($"Withdrawal request {requestId} not found.");

            request.Approve(adminId);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Withdrawal approved: Id={RequestId} Admin={AdminId}", requestId, adminId);
        }

        /// <summary>TC-09 admin: Pending/Approved → Rejected. No wallet tx is created.</summary>
        public async Task RejectWithdrawalAsync(Guid requestId, Guid adminId, string reason)
        {
            var request = await _dbContext.WithdrawalRequests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
                throw new InvalidOperationException($"Withdrawal request {requestId} not found.");

            request.Reject(adminId, reason);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Withdrawal rejected: Id={RequestId} Admin={AdminId} Reason={Reason}",
                requestId, adminId, reason);
        }

        /// <summary>
        /// TC-09 admin: Approved → Paid after the manual bank transfer (Q4b — admin nhập tay
        /// bank ref). Creates WalletTransaction(Withdrawal, -amount) + marks the request Paid
        /// in ONE transaction — a second pay attempt hits the Approved-status guard (409),
        /// so the ledger can never be debited twice for one request.
        /// </summary>
        public async Task<WithdrawalRequest> MarkWithdrawalPaidAsync(Guid requestId, Guid adminId, string bankReference)
        {
            WithdrawalRequest result = null!;
            await _dbContext.ExecuteAtomicAsync(async () =>
            {
                await using var tx = await _dbContext.BeginTransactionAsync();
                try
                {
                    var request = await _dbContext.WithdrawalRequests
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(r => r.Id == requestId);

                    if (request == null)
                        throw new InvalidOperationException($"Withdrawal request {requestId} not found.");
                    if (request.Status != WithdrawalStatus.Approved)
                        throw new InvalidOperationException(
                            $"Withdrawal request {requestId} is {request.Status} — only Approved requests can be paid.");

                    // Balance check at pay time — the balance may have dropped since approval.
                    var lastTx = await _dbContext.WalletTransactions
                        .IgnoreQueryFilters()
                        .AsNoTracking()
                        .Where(w => w.OwnerId == request.OwnerId)
                        .OrderByDescending(w => w.CreatedAt)
                        .Select(w => w.BalanceAfter)
                        .FirstOrDefaultAsync();
                    if (lastTx < request.Amount)
                        throw new InvalidOperationException(
                            $"Insufficient balance for payout: {lastTx:N0}đ, requested {request.Amount:N0}đ.");

                    var walletTx = await CreateWalletTxCoreAsync(
                        request.OwnerId,
                        WalletTransactionType.Withdrawal,
                        -request.Amount,
                        $"Withdrawal payout for request {request.Id} (bank ref: {bankReference})",
                        null,
                        null,
                        runningBalances: null);

                    request.MarkPaid(adminId, bankReference, walletTx.Id);
                    await _dbContext.SaveChangesAsync();
                    await tx.CommitAsync();
                    result = request;

                    _logger.LogInformation(
                        "Withdrawal paid: Id={RequestId} Owner={OwnerId} Amount={Amount} BankRef={BankRef} WalletTx={WalletTxId} Admin={AdminId}",
                        requestId, request.OwnerId, request.Amount, bankReference, walletTx.Id, adminId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to pay withdrawal: Id={RequestId} Admin={AdminId}",
                        requestId, adminId);
                    await tx.RollbackAsync();
                    throw;
                }
            });

            return result;
        }
    }
}
