namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// DTO for Expense Entry form submission
    /// </summary>
    public class ExpenseEntryDto
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Vendor { get; set; }
        public string? Category { get; set; }
        public string? Reference { get; set; }
    }
}
