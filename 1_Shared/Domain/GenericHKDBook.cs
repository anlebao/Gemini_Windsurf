namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Generic HKD Book - Simple, no generic type complexity
    /// Production-ready dynamic book implementation
    /// </summary>
    public record GenericHKDBook : HKDBook
    {
        public HKDBookTemplate Template { get; init; } = null!;
        public Dictionary<string, decimal> NumericValues { get; init; } = [];
        public Dictionary<string, string> TextValues { get; init; } = [];

        public override async Task CalculateAsync()
        {
            await Template.CalculateAsync(this);
        }

        public override async Task ValidateAsync()
        {
            await Template.ValidateAsync(this);
        }

        public override async Task<string> GenerateReportAsync()
        {
            return await Template.GenerateReportAsync(this);
        }
    }

    /// <summary>
    /// Base HKD Book with string-based BookType
    /// </summary>
    public abstract record HKDBook
    {
        public TenantId TenantId { get; init; } = null!;
        public AccountingPeriod Period { get; init; } = null!;
        public string BookTypeCode { get; init; } = null!; // STRING, not enum!
        public List<JournalEntry> Entries { get; init; } = [];
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
        public string TemplateVersion { get; init; } = "1.0";

        public abstract Task CalculateAsync();
        public abstract Task ValidateAsync();
        public abstract Task<string> GenerateReportAsync();
    }
}
