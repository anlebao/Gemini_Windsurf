# MÃ NGUỒN ĐẠI DIỆN (SOURCE CODE EXTRACT)
## HỆ THỐNG PHẦN MỀM QUẢN LÝ VÀ HẠCH TOÁN THƯƠNG MẠI ShopERP
### (ShopERP System)

> **Tài liệu kèm theo Hồ sơ đăng ký quyền tác giả đối với chương trình máy tính**
> — Nộp Cục Bản quyền tác giả, Bộ Văn hóa, Thể thao và Du lịch —
>
> Các đoạn mã nguồn dưới đây được **trích nguyên văn** từ kho mã nguồn của hệ thống ShopERP
> (thư mục `1_Shared/Domain/` — tầng Domain, nơi chứa các ý tưởng sáng tạo cốt lõi của chương trình).
> Tài liệu này **không sửa đổi bất kỳ file mã nguồn gốc nào**; chú thích tác quyền được gắn
> ở đầu mỗi đoạn trích trong tài liệu này.

---

## HƯỚNG DẪN SỬ DỤNG

1. **Bản in mã nguồn nộp Cục Bản quyền** phải được tạo từ **kho mã nguồn gốc** (toàn bộ file mã code
   của dự án), theo quy định tại **Điều 43.9 Nghị định 17/2023/NĐ-CP**:
   - Bản in khổ A4 chứa **toàn bộ giao diện và mã code** của chương trình máy tính;
   - Nếu bản in mã code từ 100 trang trở lên (chắc chắn đúng với ShopERP — khoảng vài nghìn file),
     in **25 trang đầu + 25 trang giữa + 25 trang cuối** của phần mã code;
   - Kèm **đĩa CD** chứa toàn bộ mã nguồn (1 mặt đĩa dán giấy trắng ghi tên chương trình máy tính).
2. Tài liệu này chỉ tập hợp **các đoạn mã tiêu biểu** thể hiện trí tuệ sáng tạo của hệ thống,
   dùng làm tài liệu minh họa và phục vụ việc lựa chọn/đối chiếu khi trích 25 trang đầu/giữa/cuối.
3. Trước khi in ấn/nộp, bắt buộc **rà soát loại bỏ bí mật** (khóa API, chuỗi kết nối, token, thông tin
   nhân sự thật trong log) khỏi mã nguồn (xem file `03_Huong_Dan_Va_Checklist_Nop_Ho_So.md`, Bước 5).

---

# PHẦN 1 — LÕI KẾ TOÁN (CORE ACCOUNTING ENGINE)

## 1.1. `JournalEntry.cs` — Bút toán tổng hợp & kiểm soát cân đối Nợ-Có

**Đường dẫn gốc:** `1_Shared/Domain/JournalEntry.cs`

```csharp
// Copyright (c) 2026 ShopERP Ecosystem. All Rights Reserved.
// File: JournalEntry.cs — Immutable Journal Entry aggregate root
// Source of Truth: VanAn.Shared.Domain

using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Immutable Journal Entry aggregate root for Hybrid Architecture
    /// Source of Truth: VanAn.Shared.Domain
    /// </summary>
    public sealed partial class JournalEntry : BaseEntity, IMustHaveTenant
    {
        public new TenantId TenantId { get; } = null!;
        public JournalEntryId JournalEntryId { get; } = null!;
        public string JournalNo { get; } = null!;
        public DateTime EntryDate { get; }
        public AccountingPeriod Period { get; } = null!;
        public string Description { get; } = null!;
        public string? ReferenceType { get; }
        public Guid? ReferenceId { get; }
        public bool IsReversal { get; }
        public JournalEntryId? ReversedJournalId { get; }

        // SRS v1.2 §18.3 — discriminator cho idempotency unique index (reversal cần
        // purpose riêng). Xem AccountingPostingPurposes (Domain.cs).
        public string? PostingPurpose { get; }

        // SRS v1.2 W2/B (approved 2026-10-31 — Gate 5, additive nullable):
        // version của AccountMappingRule đã dùng để tạo JE này ("builtin" khi fallback
        // hardcode). Rule đổi không hồi tố JE cũ.
        public string? MappingVersion { get; }

        private readonly List<JournalEntryLine> _lines = [];
        public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();

        // EF Core constructor
#pragma warning disable CS8618
        private JournalEntry() { }
#pragma warning restore CS8618

        public static JournalEntry Create(
            TenantId tenantId,
            string accountNumber,
            decimal amount,
            DateTime entryDate,
            string description,
            AccountingPeriod period)
        {
            JournalEntry entry = new(tenantId, entryDate, description);
            entry.AddLine(accountNumber, amount, 0, description);
            return entry;
        }

        public JournalEntry(
            TenantId tenantId,
            DateTime entryDate,
            string description,
            string? referenceType = null,
            Guid? referenceId = null,
            bool isReversal = false,
            JournalEntryId? reversedJournalId = null,
            string? postingPurpose = null,
            string? mappingVersion = null)
            : base(tenantId)
        {
            TenantId = tenantId;
            JournalEntryId = new JournalEntryId(Guid.NewGuid());
            EntryDate = entryDate;
            Period = AccountingPeriod.FromDateTime(entryDate);
            Description = description ?? throw new ArgumentNullException(nameof(description));
            ReferenceType = referenceType;
            ReferenceId = referenceId;
            IsReversal = isReversal;
            ReversedJournalId = reversedJournalId;
            PostingPurpose = postingPurpose;
            MappingVersion = mappingVersion;
            JournalNo = GenerateJournalNo();
        }

        public void AddLine(string accountNumber, decimal debitAmount, decimal creditAmount, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
            {
                throw new ArgumentException("Account number is required", nameof(accountNumber));
            }

            if (!MyRegex().IsMatch(accountNumber))
            {
                throw new ArgumentException($"Invalid Vietnamese account number format: {accountNumber}", nameof(accountNumber));
            }

            if (debitAmount < 0 || creditAmount < 0)
            {
                throw new ArgumentException("Amounts cannot be negative");
            }

            if (debitAmount > 0 && creditAmount > 0)
            {
                throw new ArgumentException("Cannot have both debit and credit amounts greater than zero");
            }

            _lines.Add(new JournalEntryLine(this, _lines.Count + 1, accountNumber, debitAmount, creditAmount, description));
        }

        public JournalEntry CreateReversal(string reason)
        {
            if (IsReversal)
            {
                throw new InvalidOperationException("Cannot create reversal of a reversal entry");
            }

            JournalEntry reversalEntry = new(
                TenantId,
                DateTime.UtcNow,
                $"Reversal: {Description}",
                ReferenceType,
                ReferenceId,
                isReversal: true,
                reversedJournalId: JournalEntryId
            );

            // Create reversal lines (swap debit/credit)
            foreach (JournalEntryLine line in _lines)
            {
                reversalEntry.AddLine(line.AccountNumber, line.CreditAmount, line.DebitAmount,
                    $"Reversal: {line.Description}");
            }

            return reversalEntry;
        }

        /// <summary>
        /// SRS v1.2 NFR-001/§12.1 — enforce ΣNợ == ΣCó trước khi ghi sổ.
        /// Gọi tại posting boundary (repository). Skip cho opening-balance purpose
        /// (single-line seeds by design) hoặc purpose null (legacy paths).
        /// </summary>
        public void EnsureBalanced(decimal tolerance = 0.01m)
        {
            decimal totalDebit = _lines.Sum(l => l.DebitAmount);
            decimal totalCredit = _lines.Sum(l => l.CreditAmount);
            if (Math.Abs(totalDebit - totalCredit) > tolerance)
            {
                throw new ValidationException(
                    $"JournalEntry {JournalNo} mất cân đối: Nợ {totalDebit:N2} != Có {totalCredit:N2} (chênh {Math.Abs(totalDebit - totalCredit):N2})");
            }
        }

        private static string GenerateJournalNo()
        {
            return $"J{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper(System.Globalization.CultureInfo.CurrentCulture)}";
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"^\d{1,10}$")]
        private static partial System.Text.RegularExpressions.Regex MyRegex();
    }

    public sealed class JournalEntryLine
    {
        // VAS W1: Explicit Id for EF Core persistence (composite key: JournalEntryId + Id).
        // SQLite cannot auto-generate int values for composite keys, so Id is set sequentially
        // by JournalEntry.AddLine (1-based, per JournalEntry).
        public int Id { get; private set; }
        public Guid JournalEntryId { get; private set; }
        public string AccountNumber { get; private set; } = string.Empty;
        public decimal DebitAmount { get; private set; }
        public decimal CreditAmount { get; private set; }
        public string? Description { get; private set; }

        // EF Core constructor for materialization
        protected JournalEntryLine() { }

        public JournalEntryLine(JournalEntry journal, int id, string accountNumber, decimal debitAmount, decimal creditAmount, string? description)
        {
            if (id < 1)
            {
                throw new ArgumentException("Line Id must be >= 1", nameof(id));
            }
            if (debitAmount < 0 || creditAmount < 0)
            {
                throw new ArgumentException("Amounts cannot be negative");
            }

            if (debitAmount > 0 && creditAmount > 0)
            {
                throw new ArgumentException("Cannot have both debit and credit amounts greater than zero");
            }

            Id = id;
            JournalEntryId = journal.JournalEntryId.Value;
            AccountNumber = accountNumber;
            DebitAmount = debitAmount;
            CreditAmount = creditAmount;
            Description = description;
        }
    }

    public sealed record JournalEntryId(Guid Value)
    {
        public static JournalEntryId Empty => new(Guid.Empty);

        public static JournalEntryId New()
        {
            return new(Guid.NewGuid());
        }
    }
}
```

## 1.2. `AccountingStandardRules.cs` — Khóa chuẩn mực kế toán theo loại hình

**Đường dẫn gốc:** `1_Shared/Domain/AccountingStandardRules.cs`

```csharp
// Copyright (c) 2026 ShopERP Ecosystem. All Rights Reserved.
// File: AccountingStandardRules.cs — Regime-lock theo Tenant.Type

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Regime-lock (2026-10-30, user-approved): chuẩn kế toán được phép áp dụng theo Tenant.Type.
    /// Pure static — dùng chung bởi ShopERP (dropdown UI) + CoreHub (validate write-path
    /// SetAccountingStandardAsync). Default-lock theo loại hình + bầu chọn hợp pháp:
    ///   HKD → TT 152 (sổ riêng, KHÔNG BCTC) hoặc tự nguyện TT 58 (Điều 2.2 TT 58/2026)
    ///   DN siêu nhỏ → TT 58 mặc định, được chọn TT 133 (áp dụng chế độ cao hơn)
    ///   DN vừa/nhỏ → TT 133 · DN lớn → TT 99 · HTX → TT 71 (khóa cứng)
    ///   Type=null (legacy, chưa phân loại) → không khóa (đủ 4 — giữ hành vi cũ)
    /// </summary>
    public static class AccountingStandardRules
    {
        /// <summary>Chuẩn mặc định theo Tenant.Type (null = không suy ra được — hiển thị TT133 như cũ).</summary>
        public static AccountingStandard DefaultStandard(TenantType? tenantType) => tenantType switch
        {
            TenantType.Enterprise_Large => AccountingStandard.TT99_2025,
            TenantType.Enterprise_SuperSmall => AccountingStandard.TT58_2026,
            TenantType.HTX => AccountingStandard.TT71_2024,
            _ => AccountingStandard.TT133_2016
        };

        /// <summary>
        /// Danh sách chuẩn được phép chọn cho tenant. Trống = BCTC doanh nghiệp không áp dụng
        /// (HKD chưa bầu chọn TT 58 → dùng sổ TT 152).
        /// Override hợp lệ = phải nằm trong danh sách này.
        /// </summary>
        public static IReadOnlyList<AccountingStandard> AllowedStandards(
            TenantType? tenantType, AccountingStandard? accountingStandardOverride) => tenantType switch
        {
            TenantType.Enterprise_Large => new[] { AccountingStandard.TT99_2025 },
            TenantType.Enterprise_SME => new[] { AccountingStandard.TT133_2016 },
            TenantType.HTX => new[] { AccountingStandard.TT71_2024 },
            TenantType.Enterprise_SuperSmall => new[] { AccountingStandard.TT58_2026, AccountingStandard.TT133_2016 },
            TenantType.HKD => accountingStandardOverride == AccountingStandard.TT58_2026
                ? new[] { AccountingStandard.TT58_2026 }
                : Array.Empty<AccountingStandard>(),
            _ => new[]
            {
                AccountingStandard.TT133_2016, AccountingStandard.TT99_2025,
                AccountingStandard.TT58_2026, AccountingStandard.TT71_2024
            }
        };

        /// <summary>
        /// Chuẩn hiệu lực = override (nếu thuộc allowed) → default (nếu thuộc allowed) →
        /// phần tử đầu allowed → null khi allowed rỗng (BCTC không áp dụng).
        /// </summary>
        public static AccountingStandard? ResolveEffective(TenantType? tenantType, AccountingStandard? accountingStandardOverride)
        {
            var allowed = AllowedStandards(tenantType, accountingStandardOverride);
            if (allowed.Count == 0) return null;
            if (accountingStandardOverride is { } ovr && allowed.Contains(ovr)) return ovr;
            var def = DefaultStandard(tenantType);
            return allowed.Contains(def) ? def : allowed[0];
        }

        /// <summary>
        /// Chuẩn có thể BẦU CHỌN (override) cho loại hình — dùng cho admin modal
        /// "Chế độ kế toán". Khác AllowedStandards: HKD chưa elected vẫn thấy TT 58.
        /// Rỗng = loại hình khóa cứng, chỉ dùng mặc định.
        /// </summary>
        public static IReadOnlyList<AccountingStandard> ElectableStandards(TenantType? tenantType) => tenantType switch
        {
            TenantType.HKD => new[] { AccountingStandard.TT58_2026 },                 // Điều 2.2 TT 58/2026
            TenantType.Enterprise_SuperSmall => new[] { AccountingStandard.TT133_2016 }, // tự nguyện lên TT 133
            null => new[] { AccountingStandard.TT99_2025, AccountingStandard.TT58_2026, AccountingStandard.TT71_2024 },
            _ => Array.Empty<AccountingStandard>()
        };

        /// <summary>Chuẩn đang dùng khác default của loại hình → badge "áp dụng tự nguyện".</summary>
        public static bool IsVoluntary(TenantType? tenantType, AccountingStandard standard)
            => standard != DefaultStandard(tenantType);
    }
}
```

---

# PHẦN 2 — SỔ KẾ TOÁN HỘ KINH DOANH (HKD & REGIME ACCOUNTING)

## 2.1. `HKDTemplates.cs` — Mẫu S1a & S2a (Thông tư 152/2025/TT-BTC)

**Đường dẫn gốc:** `1_Shared/Domain/HKDTemplates.cs` (trích mẫu S1a_HKD và S2a_HKD)

```csharp
// Copyright (c) 2026 ShopERP Ecosystem. All Rights Reserved.
// File: HKDTemplates.cs — Sổ kế toán HKD theo Thông tư 152/2025/TT-BTC

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// S1a_HKD Template (Không chịu thuế GTGT, không nộp thuế TNCN)
    /// For HKD Group 1 businesses
    /// </summary>
    public record S1aHKDTemplate : HKDBookTemplate
    {
        public S1aHKDTemplate()
        {
            TemplateCode = "S1a_HKD";
            TemplateName = "Sổ kế toán cho hộ kinh doanh không chịu thuế GTGT";
            TargetGroup = HKDGroup.Group1;

            Fields =
            [
                new()
                {
                    FieldName = "TotalRevenue",
                    DisplayName = "Tổng doanh thu",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new()
                {
                    FieldName = "TotalExpense",
                    DisplayName = "Tổng chi phí",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""6"", ""Debit"")"
                },
                new()
                {
                    FieldName = "NetProfit",
                    DisplayName = "Lợi nhuận",
                    Type = FieldType.Decimal,
                    Formula = "TotalRevenue - TotalExpense"
                }
            ];
        }

        public override async Task<GenericHKDBook> CreateBookAsync(
            TenantId tenantId,
            AccountingPeriod period,
            List<JournalEntry> entries)
        {
            GenericHKDBook book = new()
            {
                TenantId = tenantId,
                Period = period,
                BookTypeCode = TemplateCode,
                Template = this,
                Entries = entries
            };

            await book.CalculateAsync();
            await book.ValidateAsync();

            return book;
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ KẾ TOÁN S1a_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal revenue))
            {
                report += $"Tổng doanh thu: {revenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalExpense", out decimal expense))
            {
                report += $"Tổng chi phí: {expense:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("NetProfit", out decimal profit))
            {
                report += $"Lợi nhuận: {profit:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
        }
    }

    /// <summary>
    /// S2a_HKD Template (Nộp thuế GTGT và TNCN theo tỷ lệ % trên doanh thu)
    /// For HKD Group 2 businesses.
    /// Wave 5 (TT 152/2025/TT-BTC): 4 industry groups × 3 fields (Revenue, VatAmount, PIT per group).
    /// Tax rates per Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025.
    /// </summary>
    public record S2aHKDTemplate : HKDBookTemplate
    {
        public S2aHKDTemplate()
        {
            TemplateCode = "S2a_HKD";
            TemplateName = "Sổ kế toán cho hộ kinh doanh nộp thuế GTGT và TNCN";
            TargetGroup = HKDGroup.Group2;

            // Wave 5: 4 industry groups × 3 fields. NULL IndustrySector → OtherBusiness bucket.
            Fields =
            [
                // ── Distribution (GTGT 1%, TNCN 0.5%) ──
                new() { FieldName = "Revenue_Distribution", DisplayName = "Doanh thu — Phân phối", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Distribution"")" },
                new() { FieldName = "VatAmount_Distribution", DisplayName = "Thuế GTGT — Phân phối", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.01" },
                new() { FieldName = "PIT_Distribution", DisplayName = "Thuế TNCN — Phân phối", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.005" },
                // ── ProductionTransport (GTGT 3%, TNCN 1.5%) ──
                new() { FieldName = "Revenue_ProductionTransport", DisplayName = "Doanh thu — Sản xuất, vận tải", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""ProductionTransport"")" },
                new() { FieldName = "VatAmount_ProductionTransport", DisplayName = "Thuế GTGT — Sản xuất, vận tải", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.03" },
                new() { FieldName = "PIT_ProductionTransport", DisplayName = "Thuế TNCN — Sản xuất, vận tải", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.015" },
                // ── Service (GTGT 5%, TNCN 2%) ──
                new() { FieldName = "Revenue_Service", DisplayName = "Doanh thu — Dịch vụ", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Service"")" },
                new() { FieldName = "VatAmount_Service", DisplayName = "Thuế GTGT — Dịch vụ", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.05" },
                new() { FieldName = "PIT_Service", DisplayName = "Thuế TNCN — Dịch vụ", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.02" },
                // ── OtherBusiness (GTGT 2%, TNCN 1%) — includes NULL IndustrySector entries ──
                new() { FieldName = "Revenue_OtherBusiness", DisplayName = "Doanh thu — Hoạt động khác", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""OtherBusiness"")" },
                new() { FieldName = "VatAmount_OtherBusiness", DisplayName = "Thuế GTGT — Hoạt động khác", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.02" },
                new() { FieldName = "PIT_OtherBusiness", DisplayName = "Thuế TNCN — Hoạt động khác", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.01" }
                // ── Totals ── (tiếp theo trong file gốc)
            ];
        }
    }
}
```

---

# PHẦN 3 — TẦNG TRÍ TUỆ TÀI CHÍNH (FINANCIAL INTELLIGENCE)

## 3.1. `FinancialIntelligenceRecords.cs` — Kết quả phân tích tài chính

**Đường dẫn gốc:** `1_Shared/Domain/FinancialIntelligenceRecords.cs` (trích các record chính)

```csharp
// Copyright (c) 2026 ShopERP Ecosystem. All Rights Reserved.
// File: FinancialIntelligenceRecords.cs — VA-FI-MVP2 (approved 2026-08-21)
// All records are immutable + NOT persisted (pure calculation, recompute on demand).
// Trust Level 1 (deterministic) per parent spec §27.

namespace VanAn.Shared.Domain
{
    /// <summary>Profit status classification for ProfitSummary widget.</summary>
    public enum ProfitStatus
    {
        Profitable = 0,
        AtBreakEven = 1,
        Loss = 2,
        InsufficientData = 3
    }

    /// <summary>Break-even status classification.</summary>
    public enum BreakEvenStatus
    {
        AboveBreakEven = 0,
        AtBreakEven = 1,
        BelowBreakEven = 2,
        InsufficientData = 3
    }

    /// <summary>
    /// Profit summary for dashboard — quick "tháng này có lời không?" (KU-01).
    /// Derived from extended IncomeStatement (Revenue, COGS, OpEx, NetProfit).
    /// </summary>
    public record ProfitSummary(
        TenantId TenantId,
        AccountingPeriod Period,
        DateTime CalculatedAt,
        decimal Revenue,
        decimal COGS,
        decimal GrossProfit,
        decimal GrossMarginPercent,
        decimal OperatingExpenses,
        decimal OperatingProfit,
        decimal NetProfit,
        decimal NetMarginPercent,
        ProfitStatus Status,
        string? WarningMessage
    );

    /// <summary>
    /// Break-even analysis result — single period (KU-02).
    /// Formula: BreakEvenRevenue = TotalFixedCost / ContributionMarginRatio.
    /// </summary>
    public record BreakEvenAnalysis(
        TenantId TenantId,
        AccountingPeriod Period,
        DateTime CalculatedAt,
        FinancialModelVersion ModelVersion,
        decimal TotalFixedCost,
        decimal TotalRevenue,
        decimal TotalVariableCost,
        decimal TotalContributionMargin,
        decimal ContributionMarginRatio,
        decimal BreakEvenRevenue,
        decimal BreakEvenUnits,
        decimal MarginOfSafetyRevenue,
        decimal MarginOfSafetyPercent,
        BreakEvenStatus Status,
        string? WarningMessage,
        IReadOnlyList<string> SourceAccountCodes
    );

    /// <summary>One product line in multi-product break-even analysis.</summary>
    public record ProductBreakEvenLine(
        Guid ProductId,
        string ProductName,
        decimal SellingPrice,
        decimal VariableCost,
        decimal ContributionMargin,
        decimal ContributionMarginRatio,
        decimal SalesMixPercent,
        int UnitsSoldInPeriod,
        decimal ProductBreakEvenUnits
    );

    /// <summary>
    /// Multi-product break-even — weighted average contribution margin.
    /// Σ ProductBreakEvenUnits ≈ total break-even units (tolerance 5% due to rounding).
    /// </summary>
    public record MultiProductBreakEven(
        TenantId TenantId,
        AccountingPeriod Period,
        DateTime CalculatedAt,
        FinancialModelVersion ModelVersion,
        decimal TotalFixedCost,
        decimal WeightedContributionMargin,
        decimal WeightedContributionMarginRatio,
        decimal BreakEvenRevenue,
        IReadOnlyList<ProductBreakEvenLine> ProductLines
    );
}
```

## 3.2. `BusinessProfile.cs` — Hồ sơ kinh doanh & phiên bản mô hình tài chính

**Đường dẫn gốc:** `1_Shared/Domain/BusinessProfile.cs` (trích phần lõi)

```csharp
// Copyright (c) 2026 ShopERP Ecosystem. All Rights Reserved.
// File: BusinessProfile.cs — Pricing model & FinancialModelVersion

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// VA-FI-MVP2 (2026-08-21): Pricing model — how the tenant prices its products/services.
    /// Used by BusinessProfile for financial intelligence calculations.
    /// </summary>
    public enum PricingModel
    {
        /// <summary>Fixed price per product/service (most HKD).</summary>
        FixedPrice = 0,
        /// <summary>Dynamic pricing (happy hour, peak hours, surge).</summary>
        DynamicPricing = 1,
        /// <summary>Mix of fixed + dynamic.</summary>
        Mixed = 2
    }

    /// <summary>
    /// VA-FI-MVP2 (BR-006 compliance): Every financial calculation must be versioned.
    /// Starts at 1.0; minor increments on each BusinessProfile update.
    /// Stored as string "Major.Minor" in EF (HasConversion string).
    /// </summary>
    public readonly record struct FinancialModelVersion(int Major, int Minor)
    {
        public static FinancialModelVersion Initial => new(1, 0);

        public FinancialModelVersion Increment() => new(Major, Minor + 1);

        public override string ToString() => $"{Major}.{Minor}";

        public static FinancialModelVersion Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Initial;
            var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
                return Initial;
            return new(major, minor);
        }
    }

    /// <summary>
    /// VA-FI-MVP2: Tenant business profile for Financial Intelligence Layer.
    /// Stores fixed costs + capacity + pricing model — declared by Owner (1-time + updates).
    /// NOT auto-derived from accounting (fixed costs = subjective estimate by Owner).
    ///
    /// Single-Identity Pattern compliant:
    /// - Id (PK from BaseEntity) is the only identity column.
    /// - BusinessProfileId VO is ignored in EF config.
    /// - Constructor sets Id = BusinessProfileId.Value.
    /// </summary>
    public class BusinessProfile : BaseEntity
    {
        public BusinessProfileId BusinessProfileId { get; private set; } = new(Guid.NewGuid());

        // === Fixed Costs (monthly, VND) ===
        public decimal MonthlyRent { get; private set; }
        public decimal MonthlyPayroll { get; private set; }
        public decimal MonthlyUtilities { get; private set; }
        public decimal MonthlyMarketing { get; private set; }
        public decimal MonthlyLogistics { get; private set; }
        public decimal MonthlyOtherOpEx { get; private set; }
        public decimal MonthlyDepreciation { get; private set; }  // CAPEX amortization

        // === Capacity ===
        public int DailyCapacityUnits { get; private set; }
        public int OperatingDaysPerMonth { get; private set; }

        // === Pricing model ===
        public PricingModel PricingModel { get; private set; }

        // === Metadata ===
        public string? Notes { get; private set; }
        public FinancialModelVersion Version { get; private set; }

        // Computed (not stored — calculated on read; EF Ignore in config)
        public decimal TotalMonthlyFixedCost =>
            MonthlyRent + MonthlyPayroll + MonthlyUtilities + MonthlyMarketing +
            MonthlyLogistics + MonthlyOtherOpEx + MonthlyDepreciation;
    }
}
```

---

# PHẦN 4 — BÁN HÀNG & GIỎ HÀNG ĐA THUÊ BAO (COMMERCE)

## 4.1. `CartItem.cs` — Dòng giỏ hàng đa thuê bao

**Đường dẫn gốc:** `1_Shared/Domain/CartItem.cs`

```csharp
// Copyright (c) 2026 ShopERP Ecosystem. All Rights Reserved.
// File: CartItem.cs — Shopping cart item entity (Phase 2.5.2: KhachLink PWA)

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Shopping cart item entity
    /// Phase 2.5.2: KhachLink PWA - Customer-Facing Offline-First Interface
    /// </summary>
    public record CartItem
    {
        public required Guid Id { get; init; }
        public required Guid ProductId { get; init; }
        public required string ProductName { get; init; } = string.Empty;
        public required string Description { get; init; } = string.Empty;
        public required int Quantity { get; init; }
        public required decimal UnitPrice { get; init; }
        public decimal VatRate { get; init; } = 0.10m;  // RC-7: snapshot from Product

        // Phase 5: Tenant that owns this product. Required for multi-tenant cart → multi-order checkout.
        // Defaults to Guid.Empty so legacy/uninitialized CartItem objects remain valid (no `required` modifier).
        // Validation (TenantId != Guid.Empty) happens at checkout.
        public Guid TenantId { get; init; } = Guid.Empty;

        // Tenant display name — resolved from PG Tenants table at checkout/tracking time.
        // Stored in cart so Cart/Checkout pages can show "Cửa hàng: {name}" instead of raw GUID.
        // Empty string for legacy items (resolved lazily at checkout response).
        public string TenantName { get; init; } = string.Empty;

        /// <summary>Free/Charity flag — when true, UnitPrice=0 is valid and bypasses Tier 0 price guard.
        /// Set from FeaturedProduct.ProductType (Free/Charity) when adding featured product to cart.</summary>
        public bool IsFree { get; init; } = false;

        /// <summary>C3 (2026-09-14): Charity flag — true when the product's ProductType is Charity
        /// (quyên góp từ thiện). Distinct from IsFree (Free OR Charity): drives the "Quyên góp từ thiện"
        /// checkout step when the tenant's Charity_Donation_Enabled toggle is ON.
        /// Legacy carts (localStorage) default to false — charity items behave as plain free items until re-added.</summary>
        public bool IsCharity { get; init; } = false;

        // Computed properties
        public decimal TotalPrice => Quantity * UnitPrice;
        // VAT-inclusive extraction: UnitPrice is gross, net = gross / (1 + rate)
        public decimal VatAmount => TotalPrice - (TotalPrice / (1 + VatRate));
        public decimal NetAmount => TotalPrice - VatAmount;
    }
}
```

## 4.2. `FeaturedProduct.cs` — Sản phẩm nổi bật (trích phần lõi)

**Đường dẫn gốc:** `1_Shared/Domain/FeaturedProduct.cs`

```csharp
// Copyright (c) 2026 ShopERP Ecosystem. All Rights Reserved.
// File: FeaturedProduct.cs — Phase 6 (Admin UI): Sysadmin-curated marketing product

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Product type for FeaturedProduct — controls checkout price validation behavior.
    /// Paid: standard product, UnitPrice > 0 enforced at checkout.
    /// Free: charity/giveaway product, UnitPrice = 0 allowed (bypasses Tier 0 price guard).
    /// Charity: donation-style product, UnitPrice = 0 allowed + flagged for accounting.
    /// </summary>
    public enum FeaturedProductType
    {
        /// <summary>Standard paid product. DisplayPrice > 0 enforced.</summary>
        Paid = 0,
        /// <summary>Free product (e.g., giveaway, sample). DisplayPrice = 0 allowed.</summary>
        Free = 1,
        /// <summary>Charity/donation product. DisplayPrice = 0 allowed, flagged for accounting.</summary>
        Charity = 2
    }

    /// <summary>
    /// Phase 6 (Admin UI): Value object for FeaturedProduct business key.
    /// Follows Single-Identity Pattern — ignored in EF config (Id = PK only).
    /// </summary>
    public record FeaturedProductId(Guid Value)
    {
        public static implicit operator Guid(FeaturedProductId id) => id.Value;
        public static implicit operator FeaturedProductId(Guid value) => new(value);
        public static FeaturedProductId FromGuid(Guid value) => new(value);
    }

    /// <summary>
    /// Phase 6 (Admin UI): Sysadmin-curated marketing product for Home.razor discovery.
    /// PG-only entity (NOT in ShopERP SQLite). Holds marketing display info (name, price, image)
    /// that may differ from the operational Product in ShopERP. ProductId + TenantId reference
    /// the product in ShopERP SQLite (validated at checkout via Phase 5 price validation).
    /// Follows Single-Identity Pattern: Id = PK, FeaturedProductId VO is ignored in EF config.
    /// </summary>
    public class FeaturedProduct : BaseEntity
    {
        public FeaturedProductId FeaturedProductId { get; protected set; } = new(Guid.NewGuid());

        /// <summary>Business reference to Product (in ShopERP SQLite, not PG FK).</summary>
        public Guid ProductId { get; protected set; }

        /// <summary>Marketing name (may differ from Product.Name).</summary>
        public string DisplayName { get; protected set; } = string.Empty;

        /// <summary>Marketing description.</summary>
        public string? DisplayDescription { get; protected set; }

        /// <summary>Marketing image URL.</summary>
        public string? ImageUrl { get; protected set; }

        /// <summary>Display price (may differ from actual — show "from" price).</summary>
        public decimal DisplayPrice { get; protected set; }

        /// <summary>VAT rate snapshot at time of featuring (e.g., 0.10 = 10%).
        /// Stored so KhachLink can add featured products to cart without scanning QR.
        /// May differ from Product.VatRate if product VAT changed after featuring.</summary>
        public decimal VatRate { get; protected set; } = 0.10m;

        public bool IsActive { get; protected set; } = true;

        /// <summary>Product type — controls checkout price validation.
        /// Paid (default): DisplayPrice > 0 enforced at Tier 0.
        /// Free/Charity: DisplayPrice = 0 allowed, bypasses Tier 0 price guard.</summary>
        public FeaturedProductType ProductType { get; protected set; } = FeaturedProductType.Paid;

        /// <summary>Display ordering (lower = first).</summary>
        public int SortOrder { get; protected set; }

        /// <summary>When added to featured list.</summary>
        public DateTime FeaturedAt { get; protected set; }

        protected FeaturedProduct() { }

        public FeaturedProduct(TenantId tenantId, Guid productId, string displayName, decimal displayPrice,
            string? displayDescription = null, string? imageUrl = null, int sortOrder = 0, decimal vatRate = 0.10m,
            FeaturedProductType productType = FeaturedProductType.Paid)
            : base(tenantId)
        {
            // (phần thân constructor — xem file gốc)
        }
    }
}
```

---

# PHẦN 5 — NỀN TẢNG ĐA THUÊ BAO (MULTI-TENANCY)

## 5.1. `Common.cs` — Hợp đồng đa thuê bao & nền tảng thực thể

**Đường dẫn gốc:** `1_Shared/Domain/Common.cs` (trích các hợp đồng nền tảng)

```csharp
// Copyright (c) 2026 ShopERP Ecosystem. All Rights Reserved.
// File: Common.cs — Domain Event, Multi-tenancy contract, BaseEntity

namespace VanAn.Shared.Domain.Common
{
    /// <summary>
    /// Domain Event marker interface — Wave 5 DDD Foundation
    /// </summary>
    public interface IDomainEvent
    {
        Guid EventId { get; }
        DateTime OccurredAt { get; }
    }

    /// <summary>
    /// AggregateRoot base class — collects domain events, Wave 5 DDD Foundation
    /// </summary>
    public abstract class AggregateRoot : BaseEntity
    {
        private readonly List<IDomainEvent> _domainEvents = [];

        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        protected AggregateRoot() { }

        protected AggregateRoot(TenantId tenantId) : base(tenantId) { }

        protected void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void ClearDomainEvents()
        {
            _domainEvents.Clear();
        }

        protected void UpdateAudit()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Platform-level roles for system administration (cross-tenant operations).
    /// Distinct from tenant-level UserRole (Owner, StoreKeeper, etc.).
    /// Wave 5: SystemAdmin for Tenant Management.
    /// </summary>
    public enum PlatformRole
    {
        None = 0,
        SystemAdmin = 1  // Platform-level admin, manages multi-tenant operations
    }

    /// <summary>
    /// Contract cho Multi-tenancy - Bắt buộc mọi Entity phải có TenantId
    /// </summary>
    public interface IMustHaveTenant
    {
        /// <summary>
        /// Tenant ID để cách ly dữ liệu giữa các quán
        /// </summary>
        TenantId TenantId { get; }
    }

    /// <summary>
    /// Audit interface - Clear contract
    /// </summary>
    public interface IAuditableEntity
    {
        DateTime CreatedAt { get; }
        DateTime UpdatedAt { get; }
        string? CreatedBy { get; }
        string? UpdatedBy { get; }
    }

    public abstract class BaseEntity : IMustHaveTenant, IAuditableEntity
    {
        public Guid Id { get; protected set; } = Guid.NewGuid();

        public TenantId TenantId { get; protected set; } = null!;

        public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;
        public string? CreatedBy { get; protected set; }
        public string? UpdatedBy { get; protected set; }
        public bool IsDeleted { get; protected set; }

        protected BaseEntity() { }

        protected BaseEntity(TenantId tenantId)
        {
            TenantId = tenantId;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        protected void UpdateAudit(string? updatedBy = null)
        {
            UpdatedAt = DateTime.UtcNow;
            if (updatedBy != null)
            {
                UpdatedBy = updatedBy;
            }
        }
    }
}
```

---

# PHẦN 6 — ĐỒNG BỘ DỮ LIỆU & OUTBOX ENGINE

## 6.1. `OutboxMessage.cs` — Hộp thư sự kiện bất khả thất bại

**Đường dẫn gốc:** `1_Shared/Domain/OutboxMessage.cs`

```csharp
// Copyright (c) 2026 ShopERP Ecosystem. All Rights Reserved.
// File: OutboxMessage.cs — Transactional Outbox for reliable event publishing

using System.Text.Json;
using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Outbox Message entity for reliable event publishing
    /// Implements atomic transaction pattern with orders
    /// </summary>
    public sealed class OutboxMessage : BaseEntity
    {
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public string EventType { get; private set; } = string.Empty;
        public string Payload { get; private set; } = string.Empty;
        public DateTime OccurredOn { get; private set; }
        public DateTime? ProcessedOn { get; private set; }
        public int RetryCount { get; private set; }
        public string? LastError { get; private set; }
        public DateTime? NextRetryAt { get; private set; }

        private OutboxMessage() { }

        /// <summary>
        /// Creates new outbox message with event data
        /// </summary>
        public static OutboxMessage Create(string eventType, object payload)
        {
            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                Payload = JsonSerializer.Serialize(payload, s_jsonOptions),
                OccurredOn = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Marks message as successfully processed
        /// </summary>
        public void MarkAsProcessed()
        {
            ProcessedOn = DateTime.UtcNow;
            NextRetryAt = null;
            LastError = null;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks message as failed with retry schedule
        /// </summary>
        public void MarkAsFailed(string error)
        {
            RetryCount++;
            LastError = error;

            // Exponential backoff: 1, 2, 4, 8, 16 minutes
            double delayMinutes = Math.Min(60, Math.Pow(2, RetryCount));
            NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event type constants for type safety
    /// </summary>
    public static class EventTypes
    {
        public const string OrderCompleted = "OrderCompleted";
        public const string AccountingEntryCreated = "AccountingEntryCreated";
        public const string HKDBooksGenerated = "HKDBooksGenerated";
        public const string LoyaltyPointsChanged = "LoyaltyPointsChanged";
        // Loyalty Points Integrity (Batch 1): PG→SQLite mirror sync event.
        // NatsSyncWorker normalizes to "loyalty.changed" + RoutingKey → subject
        // "vanan.cloud.loyalty.changed.{customerDeviceId}" — matches LoyaltySyncSubscriber wildcard.
        public const string LoyaltyChanged = "LoyaltyChanged";
        // (còn tiếp — xem file gốc)
    }
}
```

---

# PHỤ LỤC — DANH MỤC FILE MÃ NGUỒN CHÍNH (MODULE INVENTORY)

| Phân hệ | Thư mục gốc | Ví dụ file quan trọng |
|---|---|---|
| Domain (toàn hệ thống) | `1_Shared/Domain/` | `Domain.cs`, `Common.cs`, `JournalEntry.cs`, `AccountingStandardRules.cs`, `HKDTemplates.cs`, `GenericHKDBook.cs`, `FinancialIntelligenceRecords.cs`, `BusinessProfile.cs`, `CartItem.cs`, `FeaturedProduct.cs`, `OutboxMessage.cs`, `ShopInstance.cs` |
| Gateway (API + định tuyến) | `2_Gateway/` | Controllers API, YARP config, NATS Publisher/Subscriber, PostgreSQL migrations |
| CoreHub (nghiệp vụ) | `3_CoreHub/` | `Services/` (Kế toán, Đơn hàng, HKD, FI, Công nợ, Luồng đơn...), `Infrastructure/` (Outbox, DataSync, NATS) |
| ShopERP (UI quản trị) | `5_WebApps/ShopERP/` | Blazor Pages: POS, Đơn hàng, Kế toán, Sổ HKD, Báo cáo, Quản trị |
| KhachLink (PWA khách hàng) | `5_WebApps/KhachLink/` | Blazor WASM: Trang chủ, Giỏ hàng, Thanh toán, Theo dõi đơn |
| Directory (SSR) | `5_WebApps/Directory/` | Trang chủ thuê bao, PWA Directory |
| UI Platform | `UI.Platform/` | Thành phần giao diện dùng chung |
| Phí nền tảng | `8_Metering/` | RateCard, Wallet, Top-up, Deny gate |
| Kiểm thử | `6_Testing/`, `6_Tests/` | xUnit test, Playwright E2E |

> **Lưu ý quan trọng:** Bản in mã nguồn chính thức nộp Cục Bản quyền phải bao gồm **mã nguồn do tổ chức tự viết** (các thư mục nêu trên), **loại trừ** mã nguồn bên thứ ba/công cụ sinh tự động (`node_modules/`, `obj/`, `bin/`, `artifacts/`, khóa cấu hình chứa bí mật...). Chi tiết thực hiện xem file `03_Huong_Dan_Va_Checklist_Nop_Ho_So.md`.

---

*— Hết Tài liệu Mã nguồn Đại diện —*
*Các đoạn mã được trích từ kho mã nguồn ShopERP (repo `Gemini_Windsurf`) nhằm minh họa trí tuệ sáng tạo của chương trình. Không có file mã nguồn gốc nào bị sửa đổi khi biên soạn tài liệu này.*