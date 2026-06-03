namespace VanAn.Shared.DTOs;

/// <summary>
/// DTO for Revenue Entry form submission
/// </summary>
public class RevenueEntryDto
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
}
