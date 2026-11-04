using VanAn.Shared.Domain;

namespace VanAn.Shared.DTOs
{
    /// <summary>
    /// Data Transfer Object for Accounting Entry
    /// Used for API responses and service layer communication
    /// </summary>
    public class AccountingEntryDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        // required (2026-11-03): field này từng bị bỏ sót ở 4 list-mapping → dashboard revenue=0.
        // required buộc MỌI initializer phải set (kể cả chuỗi rỗng) — quên map = compile error.
        public required string AccountCode { get; set; }
        public AccountingEntryType EntryType { get; set; }
        public DateTime CreatedAt { get; set; }
        public AccountingBookType AccountingBookType { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public Guid? ReversalEntryId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? Vendor { get; set; }
        public string? Category { get; set; }
        public string? Reference { get; set; }

        // Wave 5 (approved 2026-07-03): Industry sector for TT 152 S2a/S2b industry-group split.
        public IndustrySector? IndustrySector { get; set; }

        // VALCN v2.0 Phase 1 — CorrelationId for traceability (Order.Id). Null = legacy.
        public Guid? CorrelationId { get; set; }

        // SRS v1.2 W2/C1 — số chứng từ tuần tự + hình thức thanh toán + link khoản nợ (nullable).
        public string? VoucherNumber { get; set; }
        public string? PaymentMethod { get; set; }
        public Guid? AllocatedToEntryId { get; set; }

        // SRS v1.2 W3/D — TK tiền thực ghi sổ (111/112x). Null → mặc định 111.
        public string? CashAccountCode { get; set; }

        /// <summary>
        /// Alias for TransactionDate — used in form submission
        /// </summary>
        public DateTime Date
        {
            get => TransactionDate;
            set => TransactionDate = value;
        }

        /// <summary>
        /// SINGLE mapper entity→DTO (2026-11-03): 10+ site từng copy-paste initializer — field mới
        /// quên map ở site này gây sai lệch giữa các mặt hiển thị (dashboard revenue=0 do AccountCode
        /// bị drop ở list-mapping). Mọi entity→DTO BẮT BUỘC qua đây: thêm field = sửa 1 chỗ.
        /// </summary>
        public static AccountingEntryDto FromEntity(AccountingEntry e) => new()
        {
            Id = e.Id,
            TenantId = e.TenantId.Value,
            Amount = e.Amount,
            Description = e.Description,
            AccountCode = e.AccountCode ?? string.Empty,
            EntryType = e.EntryType,
            CreatedAt = e.CreatedAt,
            AccountingBookType = e.AccountingBookType,
            PeriodYear = e.PeriodYear,
            PeriodMonth = e.PeriodMonth,
            ReversalEntryId = e.ReversalEntryId,
            TransactionDate = e.TransactionDate,
            Vendor = e.Vendor,
            Category = e.Category,
            Reference = e.Reference,
            IndustrySector = e.IndustrySector,
            CorrelationId = e.CorrelationId,
            VoucherNumber = e.VoucherNumber,
            PaymentMethod = e.PaymentMethod,
            AllocatedToEntryId = e.AllocatedToEntryId,
            CashAccountCode = e.CashAccountCode,
        };
    }
}
