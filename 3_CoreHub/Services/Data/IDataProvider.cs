using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Data
{
    /// <summary>
    /// Context-aware Data Provider Interface
    /// Critical for thread-safe concurrent operations
    /// </summary>
    public interface IDataProvider
    {
        /// <summary>
        /// Get account sum for specific pattern and side
        /// </summary>
        /// <param name="context">Data provider context with tenant and period</param>
        /// <param name="accountPattern">Account pattern (e.g., "5*", "611")</param>
        /// <param name="side">Credit or Debit</param>
        /// <returns>Sum of amounts</returns>
        decimal GetAccountSum(DataProviderContext context, string accountPattern, string side);
        
        /// <summary>
        /// Get account balance (Debit - Credit)
        /// </summary>
        /// <param name="context">Data provider context</param>
        /// <param name="accountPattern">Account pattern</param>
        /// <returns>Account balance</returns>
        decimal GetAccountBalance(DataProviderContext context, string accountPattern);
        
        /// <summary>
        /// Get pre-aggregated data for context
        /// </summary>
        /// <param name="context">Data provider context</param>
        /// <returns>Dictionary of pre-aggregated values</returns>
        Task<Dictionary<string, decimal>> GetPreAggregatedDataAsync(DataProviderContext context);
        
        /// <summary>
        /// Get period total for specific account pattern
        /// </summary>
        /// <param name="context">Data provider context</param>
        /// <param name="accountPattern">Account pattern</param>
        /// <returns>Period total</returns>
        decimal GetPeriodTotal(DataProviderContext context, string accountPattern);
    }
}
