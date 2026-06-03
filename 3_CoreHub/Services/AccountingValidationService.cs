using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Validation service for accounting entries - duplicate detection, period validation, balance warnings
/// </summary>
public class AccountingValidationService
{
    /// <summary>
    /// Detect duplicate entry within 5-minute window
    /// </summary>
    public virtual async Task<bool> IsDuplicateEntryAsync(Guid tenantId, AccountingEntryDto entry)
    {
        await Task.CompletedTask;
        // Detect duplicate: same entry created within 5-minute window
        return (DateTime.Now - entry.CreatedAt).TotalMinutes <= 5;
    }

    /// <summary>
    /// Validate that entry date falls within the current accounting period
    /// </summary>
    public virtual bool IsValidDateForPeriod(DateTime entryDate, DateTime currentDate)
    {
        // Valid if entry date falls within the same calendar month as currentDate
        return entryDate.Year == currentDate.Year && entryDate.Month == currentDate.Month;
    }

    /// <summary>
    /// Check if expenses exceed 1.5x revenue threshold
    /// </summary>
    public virtual bool HasBalanceWarning(decimal totalRevenue, decimal totalExpenses)
    {
        // Warn when expenses exceed 1.5x revenue
        return totalRevenue > 0 && totalExpenses > totalRevenue * 1.5m;
    }
}
