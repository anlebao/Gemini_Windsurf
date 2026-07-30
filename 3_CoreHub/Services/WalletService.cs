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

            // HR-SCALE-3: atomic BalanceAfter — transaction ensures no concurrent writes
            await using var tx = await _dbContext.BeginTransactionAsync();

            try
            {
                var isPostgres = _dbContext.ProviderName.Contains("PostgreSQL") ||
                                 _dbContext.ProviderName.Contains("Npgsql");

                decimal balanceBefore;
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
                await tx.CommitAsync();

                _logger.LogInformation("WalletTransaction created: Id={Id} BalanceAfter={BalanceAfter}",
                    walletTx.Id, walletTx.BalanceAfter);

                return walletTx;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create WalletTransaction for Owner={OwnerId}", ownerId);
                await tx.RollbackAsync();
                throw;
            }
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
        /// </summary>
        public async Task<WalletTransaction> ConfirmCodAsync(Guid shipperId, Guid orderId, decimal amount)
        {
            // 1. Load order (cross-tenant — delivery spans tenants)
            var order = await _dbContext.Orders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                throw new InvalidOperationException($"Order {orderId} not found.");

            // 2. Idempotency: COD already collected
            if (order.CodCollectedAt != null)
                throw new InvalidOperationException($"COD already confirmed for order {orderId}.");

            // 3. Verify caller is the shipper of this order's DeliveryTask
            var deliveryTask = await _dbContext.DeliveryTasks
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.OrderId == orderId && d.ShipperId == shipperId);

            if (deliveryTask == null)
                throw new UnauthorizedAccessException($"Caller is not the shipper of order {orderId}.");

            // 4. Verify amount matches Order.CodAmount (if set)
            if (order.CodAmount.HasValue && order.CodAmount.Value != amount)
                throw new InvalidOperationException($"Amount {amount} does not match Order.CodAmount {order.CodAmount.Value}.");

            // 5. Branch by CommerceMode (Sprint 7)
            if (order.CommerceMode == CommerceMode.Reseller)
            {
                return await ConfirmCodResellerAsync(shipperId, order, amount);
            }

            // === Marketplace path (existing Sprint 5 logic) ===

            // 6. Create CODCollection tx for shipper (+amount)
            var shipperTx = await CreateTransactionAsync(
                shipperId,
                WalletTransactionType.CODCollection,
                amount,
                $"COD collection for order {orderId}",
                orderId);

            // 7. Create Settlement tx for shop (-amount) — shop wallet owner = TenantId
            var shopOwnerId = order.TenantId.Value;
            await CreateTransactionAsync(
                shopOwnerId,
                WalletTransactionType.Settlement,
                -amount,
                $"COD settlement for order {orderId} (shipper collected)",
                orderId,
                shipperTx.Id);

            // 8. Mark order COD collected
            order.MarkCodCollected(amount);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("COD confirmed: Order={OrderId} Shipper={ShipperId} Amount={Amount}",
                orderId, shipperId, amount);

            return shipperTx;
        }

        /// <summary>
        /// Sprint 7 — Reseller COD confirmation. 6 tx (5-split + CODCollection).
        /// COD collected = CostPrice + DeliveryFee + Commission + PlatformFee + CommunityFund.
        /// Vạn An là trung gian — phân phối toàn bộ dòng tiền.
        /// </summary>
        private async Task<WalletTransaction> ConfirmCodResellerAsync(Guid shipperId, Order order, decimal codAmount)
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
            // Commission: only if salesman referred this order. Loaded from SalesReferral if exists.
            decimal commission = 0m;
            Guid? salesmanId = order.SalesmanId;
            if (salesmanId.HasValue)
            {
                // Commission = margin × CommissionRate (OnMargin base for Reseller)
                // CommissionRate from ProductReferralConfig — loaded by SalesmanService.CreateCommissionAsync
                // Here we just reserve the amount; actual commission tx created by SalesmanService
                // For financial balance check, we compute expected commission
                var config = await _dbContext.ProductReferralConfigs
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ProductId == order.ReferralProductId && c.IsActive);
                if (config != null)
                {
                    commission = margin * config.CommissionRate;
                }
            }

            // Financial balance invariant: costPrice + deliveryFee + commission + platformFee + communityFund = codAmount
            // Note: codAmount = SellPrice + DeliveryFee (customer pays both)
            // SellPrice = CostPrice + Margin → codAmount = CostPrice + Margin + DeliveryFee
            // margin = platformFee + communityFund + commission + vanAnNetProfit
            // So: costPrice + deliveryFee + commission + platformFee + communityFund = costPrice + deliveryFee + margin = codAmount ✓
            // (assuming commission + platformFee + communityFund ≤ margin; remainder = VanAn net profit kept in PlatformWallet)

            // 1. CODCollection (+codAmount, shipper) — shipper thu hộ
            var shipperTx = await CreateTransactionAsync(
                shipperId,
                WalletTransactionType.CODCollection,
                codAmount,
                $"COD collection for order {orderId} (Reseller)",
                orderId);

            // 2. Settlement (+costPrice, tenant) — Vạn An trả tenant giá vốn
            await CreateTransactionAsync(
                tenantId,
                WalletTransactionType.Settlement,
                costPrice,
                $"Cost price settlement for order {orderId} (Reseller — Vạn An mua từ tenant)",
                orderId,
                shipperTx.Id);

            // 3. DeliveryFee (+deliveryFee, shipper) — Vạn An trả shipper phí giao
            if (deliveryFee > 0)
            {
                await CreateTransactionAsync(
                    shipperId,
                    WalletTransactionType.DeliveryFee,
                    deliveryFee,
                    $"Delivery fee for order {orderId} (Reseller)",
                    orderId,
                    shipperTx.Id);
            }

            // 4. Commission (+commission, salesman) — Vạn An trả salesman (if referral)
            // Note: Commission tx created here for Reseller mode (in Marketplace, SalesmanService creates it separately)
            if (commission > 0 && salesmanId.HasValue)
            {
                await CreateTransactionAsync(
                    salesmanId.Value,
                    WalletTransactionType.Commission,
                    commission,
                    $"Commission for order {orderId} (Reseller — OnMargin)",
                    orderId,
                    shipperTx.Id);
            }

            // 5. PlatformFee (+platformFee, PlatformWallet) — Vạn An giữ margin share
            if (platformFee > 0)
            {
                await CreateTransactionAsync(
                    SystemWalletIds.PlatformWallet,
                    WalletTransactionType.PlatformFee,
                    platformFee,
                    $"Platform fee for order {orderId} (Reseller — {platformFeeRate:P1} of margin)",
                    orderId,
                    shipperTx.Id);
            }

            // 6. CommunityFund (+communityFund, CommunityFundWallet) — quỹ cộng đồng
            if (communityFund > 0)
            {
                await CreateTransactionAsync(
                    SystemWalletIds.CommunityFund,
                    WalletTransactionType.CommunityFund,
                    communityFund,
                    $"Community fund for order {orderId} (Reseller — {communityFundRate:P1} of margin)",
                    orderId,
                    shipperTx.Id);
            }

            // 7. Mark order COD collected
            order.MarkCodCollected(codAmount);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "COD confirmed (Reseller): Order={OrderId} Shipper={ShipperId} COD={CodAmount} CostPrice={CostPrice} DeliveryFee={DeliveryFee} Commission={Commission} PlatformFee={PlatformFee} CommunityFund={CommunityFund}",
                orderId, shipperId, codAmount, costPrice, deliveryFee, commission, platformFee, communityFund);

            return shipperTx;
        }

        /// <summary>
        /// Sprint 5: Shipper confirms advance payment to shop (paid cash before pickup).
        /// Creates AdvancePayment tx for shipper (-amount). Pending shop confirmation via ConfirmAdvanceReceivedAsync.
        /// </summary>
        public async Task<WalletTransaction> ConfirmAdvanceAsync(Guid shipperId, Guid orderId, decimal amount)
        {
            // 1. Load order (cross-tenant)
            var order = await _dbContext.Orders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                throw new InvalidOperationException($"Order {orderId} not found.");

            // 2. Verify caller is the shipper
            var deliveryTask = await _dbContext.DeliveryTasks
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.OrderId == orderId && d.ShipperId == shipperId);

            if (deliveryTask == null)
                throw new UnauthorizedAccessException($"Caller is not the shipper of order {orderId}.");

            // 3. Branch by CommerceMode (Sprint 7)
            if (order.CommerceMode == CommerceMode.Reseller)
            {
                // Reseller: Vạn An ứng tiền cho tenant (not shipper).
                // Shipper still calls this endpoint (auth verification), but the actual advance
                // is from PlatformWallet → tenant. Shipper just triggers the flow.
                var tenantId = order.TenantId.Value;

                // AdvancePayment (-amount, PlatformWallet) — Vạn An ứng
                var advanceTx = await CreateTransactionAsync(
                    SystemWalletIds.PlatformWallet,
                    WalletTransactionType.AdvancePayment,
                    -amount,
                    $"Advance payment to tenant for order {orderId} (Reseller — Vạn An ứng)",
                    orderId);

                // Settlement (+amount, tenant) — tenant nhận
                await CreateTransactionAsync(
                    tenantId,
                    WalletTransactionType.Settlement,
                    amount,
                    $"Advance received from Vạn An for order {orderId} (Reseller)",
                    orderId,
                    advanceTx.Id);

                _logger.LogInformation("Advance confirmed (Reseller): Order={OrderId} Amount={Amount} (Vạn An → tenant)",
                    orderId, amount);

                return advanceTx;
            }

            // === Marketplace path (existing Sprint 5 logic) ===

            // 4. Create AdvancePayment tx for shipper (-amount — shipper paid cash, wallet goes negative)
            var marketplaceAdvanceTx = await CreateTransactionAsync(
                shipperId,
                WalletTransactionType.AdvancePayment,
                -amount,
                $"Advance payment to shop for order {orderId}",
                orderId);

            _logger.LogInformation("Advance confirmed: Order={OrderId} Shipper={ShipperId} Amount={Amount}",
                orderId, shipperId, amount);

            return marketplaceAdvanceTx;
        }

        /// <summary>
        /// Sprint 5: Shop confirms they received advance payment from shipper.
        /// Creates Settlement tx for shop (+amount), linked to original AdvancePayment via RelatedTransactionId.
        /// </summary>
        public async Task<WalletTransaction> ConfirmAdvanceReceivedAsync(Guid shopOwnerId, Guid advanceTransactionId)
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

            // 3. Create Settlement tx for shop (+amount)
            var settlementTx = await CreateTransactionAsync(
                shopOwnerId,
                WalletTransactionType.Settlement,
                -advanceTx.Amount, // AdvancePayment was -amount, so -(-amount) = +amount
                $"Advance received from shipper for order {advanceTx.RelatedOrderId}",
                advanceTx.RelatedOrderId,
                advanceTransactionId);

            _logger.LogInformation("Advance received confirmed: AdvanceTx={AdvanceTxId} Shop={ShopOwnerId} Amount={Amount}",
                advanceTransactionId, shopOwnerId, -advanceTx.Amount);

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
        /// Creates 5-split: ExternalPayment + Settlement + DeliveryFee + Commission? + PlatformFee + CommunityFund.
        /// </summary>
        public async Task<WalletTransaction> ConfirmExternalPaymentAsync(Guid orderId, decimal amount, string paymentRef)
        {
            if (string.IsNullOrWhiteSpace(paymentRef))
                throw new ArgumentException("PaymentRef cannot be empty", nameof(paymentRef));

            var order = await _dbContext.Orders
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                throw new InvalidOperationException($"Order {orderId} not found.");

            if (order.CommerceMode != CommerceMode.Reseller)
                throw new InvalidOperationException($"Order {orderId} is not Reseller mode — external payment not applicable.");

            if (order.CodCollectedAt != null)
                throw new InvalidOperationException($"Order {orderId} already paid (COD collected).");

            // Verify amount = SellPrice + DeliveryFee
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

            // Commission (if salesman referral)
            decimal commission = 0m;
            Guid? salesmanId = order.SalesmanId;
            if (salesmanId.HasValue)
            {
                var config = await _dbContext.ProductReferralConfigs
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ProductId == order.ReferralProductId && c.IsActive);
                if (config != null)
                {
                    commission = margin * config.CommissionRate;
                }
            }

            // 1. ExternalPayment (+amount, PlatformWallet) — customer pays Vạn An
            var externalTx = await CreateTransactionAsync(
                SystemWalletIds.PlatformWallet,
                WalletTransactionType.ExternalPayment,
                amount,
                $"External payment for order {orderId} (Ref: {paymentRef})",
                orderId);

            // 2. Settlement (+costPrice, tenant) — Vạn An trả tenant giá vốn
            await CreateTransactionAsync(
                tenantId,
                WalletTransactionType.Settlement,
                costPrice,
                $"Cost price settlement for order {orderId} (Reseller — external payment)",
                orderId,
                externalTx.Id);

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
                    await CreateTransactionAsync(
                        deliveryTask.ShipperId,
                        WalletTransactionType.DeliveryFee,
                        deliveryFee,
                        $"Delivery fee for order {orderId} (Reseller — external payment)",
                        orderId,
                        externalTx.Id);
                }
            }

            // 4. Commission (+commission, salesman)
            if (commission > 0 && salesmanId.HasValue)
            {
                await CreateTransactionAsync(
                    salesmanId.Value,
                    WalletTransactionType.Commission,
                    commission,
                    $"Commission for order {orderId} (Reseller — external payment, OnMargin)",
                    orderId,
                    externalTx.Id);
            }

            // 5. PlatformFee (+platformFee, PlatformWallet)
            if (platformFee > 0)
            {
                await CreateTransactionAsync(
                    SystemWalletIds.PlatformWallet,
                    WalletTransactionType.PlatformFee,
                    platformFee,
                    $"Platform fee for order {orderId} (Reseller — external payment, {platformFeeRate:P1} of margin)",
                    orderId,
                    externalTx.Id);
            }

            // 6. CommunityFund (+communityFund, CommunityFundWallet)
            if (communityFund > 0)
            {
                await CreateTransactionAsync(
                    SystemWalletIds.CommunityFund,
                    WalletTransactionType.CommunityFund,
                    communityFund,
                    $"Community fund for order {orderId} (Reseller — external payment, {communityFundRate:P1} of margin)",
                    orderId,
                    externalTx.Id);
            }

            // Mark order as paid (use CodCollectedAt as payment confirmation marker)
            order.MarkCodCollected(amount);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "External payment confirmed (Reseller): Order={OrderId} Amount={Amount} Ref={PaymentRef} CostPrice={CostPrice} DeliveryFee={DeliveryFee} Commission={Commission} PlatformFee={PlatformFee} CommunityFund={CommunityFund}",
                orderId, amount, paymentRef, costPrice, deliveryFee, commission, platformFee, communityFund);

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
