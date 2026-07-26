using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// WalletService base — v1.4 (moved from Sprint 5 to Sprint 0).
    /// HR-SCALE-3: atomic BalanceAfter via SELECT FOR UPDATE pattern on PG.
    /// Sprint 5 will extend this with ConfirmCodAsync/ConfirmAdvanceAsync/ReverseTransactionAsync/SettleAsync.
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
        /// Community entities are PG-only (v1.3) — uses PG-specific FOR UPDATE clause.
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
                // Lock last transaction row for this owner (PG SELECT FOR UPDATE)
                // SQLite fallback: transaction isolation handles correctness for PoC scale
                var lastTx = await _dbContext.WalletTransactions
                    .FromSqlRaw(
                        "SELECT * FROM \"WalletTransactions\" WHERE \"OwnerId\" = {0} ORDER BY \"CreatedAt\" DESC LIMIT 1 FOR UPDATE",
                        ownerId)
                    .FirstOrDefaultAsync();

                var balanceBefore = lastTx?.BalanceAfter ?? 0m;

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
    }
}
