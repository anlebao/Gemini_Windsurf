namespace VanAn.Shared.DTOs;

/// <summary>
/// Period comparison result for month-over-month analysis
/// </summary>
public class PeriodComparison
{
    public decimal RevenueDeltaPercent { get; set; }
    public string RevenueLabel { get; set; } = string.Empty;
    public decimal ExpenseDeltaPercent { get; set; }
    public string ExpenseLabel { get; set; } = string.Empty;
}
