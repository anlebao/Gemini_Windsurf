namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// Balance summary for accounting dashboard metrics
    /// </summary>
    public class BalanceSummary
    {
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit { get; set; }
    }
}
