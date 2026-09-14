using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace VanAn.CoreHub.Infrastructure
{
    /// <summary>
    /// Phase 1 Scaling fix (C1, 2026-09-14): Gateway PG runs NpgsqlRetryingExecutionStrategy
    /// (EnableRetryOnFailure — Gateway Program.cs, commit 8f1144f3 deployed 2026-08-22).
    /// EF Core rejects user-initiated transactions unless the whole transactional unit
    /// (BeginTransaction → SaveChanges → Commit) runs inside
    /// CreateExecutionStrategy().ExecuteAsync. This broke ALL Gateway checkouts from
    /// 2026-08-22 (last successful order 2026-08-21 14:41; every checkout returned
    /// "NpgsqlRetryingExecutionStrategy does not support user-initiated transactions").
    /// </summary>
    public static class VanAnDbContextExecutionExtensions
    {
        /// <summary>
        /// Run an atomic transactional unit (BeginTransaction → SaveChanges → Commit) inside the
        /// EF execution strategy so user-initiated transactions are allowed — and retried as
        /// one unit — when EnableRetryOnFailure is configured (Gateway PG).
        /// Non-DbContext IVanAnDbContext implementations (unit-test mocks) run the operation directly.
        /// </summary>
        public static Task ExecuteAtomicAsync(this IVanAnDbContext context, Func<Task> operation)
        {
            if (context is DbContext efContext)
            {
                IExecutionStrategy strategy = efContext.Database.CreateExecutionStrategy();
                return strategy.ExecuteAsync(operation);
            }

            return operation();
        }
    }
}
