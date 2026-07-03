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
        /// Wave 5: Get account sum filtered by industry sector (TT 152 S2a/S2b industry-group split).
        /// Entries with NULL IndustrySector are counted in the <see cref="IndustrySector.OtherBusiness"/> group
        /// (ensures TotalRevenue = SUM(all sector revenues) always holds).
        /// </summary>
        /// <param name="context">Data provider context with tenant and period</param>
        /// <param name="accountPattern">Account pattern (e.g., "5", "611")</param>
        /// <param name="side">Credit or Debit</param>
        /// <param name="industrySector">Industry sector filter (NULL → OtherBusiness bucket)</param>
        /// <returns>Sum of amounts for the specified industry sector</returns>
        decimal GetAccountSum(DataProviderContext context, string accountPattern, string side, IndustrySector? industrySector);

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
