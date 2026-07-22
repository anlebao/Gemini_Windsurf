using VanAn.Shared.Domain;

namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// Data Transfer Object for HKD Book â€” API response shape for Wave 7 endpoint.
    /// Maps from <see cref="GenericHKDBook"/> domain record without exposing Domain entities directly.
    /// </summary>
    public class HKDBookDto
    {
        public Guid TenantId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string BookTypeCode { get; set; } = string.Empty;
        public string TemplateVersion { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public Dictionary<string, decimal> NumericValues { get; set; } = [];
        public Dictionary<string, string> TextValues { get; set; } = [];
        public List<HKDBookEntryDto> Entries { get; set; } = [];

        /// <summary>
        /// Map from <see cref="GenericHKDBook"/> domain record to DTO.
        /// </summary>
        public static HKDBookDto FromDomain(GenericHKDBook book)
        {
            return new HKDBookDto
            {
                TenantId = book.TenantId.Value,
                Year = book.Period.Year,
                Month = book.Period.Month,
                BookTypeCode = book.BookTypeCode,
                TemplateVersion = book.TemplateVersion,
                GeneratedAt = book.GeneratedAt,
                NumericValues = new Dictionary<string, decimal>(book.NumericValues),
                TextValues = new Dictionary<string, string>(book.TextValues),
                Entries = book.Entries.Select(HKDBookEntryDto.FromDomain).ToList()
            };
        }
    }

    /// <summary>
    /// Simplified JournalEntry projection â€” exposes only API-safe fields.
    /// </summary>
    public class HKDBookEntryDto
    {
        public Guid Id { get; set; }
        public string JournalNo { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<HKDBookEntryLineDto> Lines { get; set; } = [];

        public static HKDBookEntryDto FromDomain(JournalEntry entry)
        {
            return new HKDBookEntryDto
            {
                Id = entry.Id,
                JournalNo = entry.JournalNo,
                EntryDate = entry.EntryDate,
                Description = entry.Description,
                Lines = entry.Lines.Select(HKDBookEntryLineDto.FromDomain).ToList()
            };
        }
    }

    /// <summary>
    /// Simplified JournalEntryLine projection.
    /// </summary>
    public class HKDBookEntryLineDto
    {
        public string AccountNumber { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string Description { get; set; } = string.Empty;

        public static HKDBookEntryLineDto FromDomain(JournalEntryLine line)
        {
            return new HKDBookEntryLineDto
            {
                AccountNumber = line.AccountNumber,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                Description = line.Description ?? string.Empty
            };
        }
    }

    /// <summary>
    /// DTO for the list of available HKD book templates (GET /api/hkd-books).
    /// </summary>
    public class HKDBookTemplateDto
    {
        public string TemplateCode { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public string TemplateVersion { get; set; } = string.Empty;
        public string TargetGroup { get; set; } = string.Empty;

        public static HKDBookTemplateDto FromDomain(HKDBookTemplate template)
        {
            return new HKDBookTemplateDto
            {
                TemplateCode = template.TemplateCode,
                TemplateName = template.TemplateName,
                TemplateVersion = template.TemplateVersion,
                TargetGroup = template.TargetGroup.ToString()
            };
        }
    }
}
