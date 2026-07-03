using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// HKD Tax Classification Service Implementation - Phase 2.3.9
    /// Implements Vietnamese Accounting Standard (Thông tư 152/2025/TT-BTC)
    /// Tax classification for 7 HKD book types
    /// </summary>
    public class HKDTaxClassificationService(ILogger<HKDTaxClassificationService> logger) : IHKDTaxClassificationService
    {
        private readonly ILogger<HKDTaxClassificationService> _logger = logger;
        private static readonly string[] collection = ["Sổ S1a-HKD", "Hóa đơn đầu ra", "Hóa đơn đầu vào"];
        private static readonly string[] collection0 = ["Excel", "PDF", "XML"];
        private static readonly string[] collection1 = [
                        "Sổ S2a-HKD",
            "Sổ S2b-HKD",
            "Sổ S2c-HKD",
            "Sổ S2d-HKD",
            "Sổ S2e-HKD",
            "Hóa đơn đầu ra",
            "Hóa đơn đầu vào",
            "Phiếu thu",
            "Phiếu chi"
                    ];
        private static readonly string[] collection2 = ["Excel", "PDF", "XML", "JSON"];
        private static readonly string[] collection3 = new[] { "Sổ S3a-HKD", "Hóa đơn", "Giấy phép kinh doanh" };
        private static readonly string[] collection4 = new[] { "Excel", "PDF" };

        /// <summary>
        /// Classify tax obligations for HKD based on book type and transaction
        /// </summary>
        public async Task<HKDTaxClassification> ClassifyTaxAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            CoreAccountingEntry entry,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Classifying tax for tenant {TenantId}, book type {BookType}",
                tenantId.Value, bookType);

            HKDGroup group = GetHKDGroupForBookType(bookType);
            TaxClassificationData classification = await GetTaxClassificationAsync(group, bookType);

            return new HKDTaxClassification
            {
                BookType = bookType,
                Group = group,
                VatRate = classification.VatRate,
                RequiresVatDeclaration = classification.RequiresVatDeclaration,
                RequiresPersonalIncomeTax = classification.RequiresPersonalIncomeTax,
                RequiresSpecialTax = classification.RequiresSpecialTax,
                TaxRate = classification.TaxRate,
                TaxClassification = classification.TaxClassification,
                ApplicableTaxes = classification.ApplicableTaxes
            };
        }

        /// <summary>
        /// Get applicable tax rates for HKD book type
        /// </summary>
        public async Task<List<VatRate>> GetApplicableVatRatesAsync(
            AccountingBookType bookType,
            CancellationToken cancellationToken = default)
        {
            HKDGroup group = GetHKDGroupForBookType(bookType);
            List<VatRate> rates = [];

            switch (group)
            {
                case HKDGroup.Group1: // S1a - Không chịu thuế GTGT
                    rates.Add(VatRate.Exempt);
                    break;

                case HKDGroup.Group2: // S2a-S2e - Nộp thuế GTGT theo tỷ lệ %
                    rates.Add(VatRate.Five);
                    rates.Add(VatRate.Ten);
                    break;

                case HKDGroup.Group3: // S3a - Thuế khác
                    rates.Add(VatRate.Zero);
                    rates.Add(VatRate.Five);
                    rates.Add(VatRate.Ten);
                    break;
                default:
                    break;
            }

            return await Task.FromResult(rates);
        }

        /// <summary>
        /// Calculate tax obligations for HKD book type.
        /// Wave 5c (2026-07-03): 2026 regulatory compliance — replaces hardcoded 10% TNCN + flat VAT
        /// with HKDRevenueClassification.CalculateTNCN / CalculateGTGT per Luật GTGT/TNCN sửa đổi 2025 +
        /// ND 117/2025 + NQ 198/2025/QH15.
        ///   Nhóm 1 (≤1B):       GTGT = 0, TNCN = 0 (exemption)
        ///   Nhóm 2 (>1B-≤3B):   GTGT = Revenue × industryVatRate, TNCN = (Revenue - 1B) × industryPitRate
        ///   Nhóm 3 (>3B-≤50B):  TNCN = (Revenue - Expense) × 17%
        ///   Nhóm 4 (>50B):      TNCN = (Revenue - Expense) × 20%
        /// </summary>
        public async Task<HKDTaxCalculation> CalculateTaxAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            decimal revenueAmount,
            decimal expenseAmount = 0,
            IndustrySector? industrySector = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Calculating 2026 tax for tenant {TenantId}, book type {BookType}, revenue {Revenue}, expense {Expense}, sector {Sector}",
                tenantId.Value, bookType, revenueAmount, expenseAmount, industrySector?.ToString() ?? "NULL");

            HKDGroup group = GetHKDGroupForBookType(bookType);
            TaxClassificationData classification = await GetTaxClassificationAsync(group, bookType);

            // Wave 5c: Determine 2026 revenue group (Nhóm 1-4) from revenue amount
            HKDRevenueGroup revenueGroup = HKDRevenueClassification.CalculateGroup(revenueAmount);

            // Wave 5c: Resolve industry rates (default to OtherBusiness if sector not provided)
            IndustrySector effectiveSector = industrySector ?? IndustrySector.OtherBusiness;
            if (industrySector is null)
            {
                _logger.LogWarning("CalculateTaxAsync: industrySector not provided, defaulting to OtherBusiness (GTGT 2%, TNCN 1%)");
            }
            decimal industryVatRate = GetVatRate(effectiveSector);
            decimal industryPitRate = GetPitRate(effectiveSector);

            decimal vatAmount = 0m;
            decimal personalIncomeTaxAmount = 0m;
            decimal specialTaxAmount = 0m;
            List<TaxBreakdown> taxBreakdowns = [];

            // Wave 5c: VAT calculation per 2026 law (Nhóm 1 = 0 exemption, Nhóm 2/3/4 = Revenue × industryVatRate)
            if (classification.RequiresVatDeclaration)
            {
                vatAmount = HKDRevenueClassification.CalculateGTGT(revenueGroup, revenueAmount, industryVatRate);
                taxBreakdowns.Add(new TaxBreakdown
                {
                    TaxType = "VAT",
                    TaxableAmount = revenueAmount,
                    TaxRate = industryVatRate * 100m,
                    TaxAmount = vatAmount,
                    Description = revenueGroup == HKDRevenueGroup.Group1
                        ? "Thuế GTGT 2026: Miễn thuế (Nhóm 1, doanh thu ≤ 1 tỷ)"
                        : $"Thuế GTGT 2026: Doanh thu × {industryVatRate:P1} (Nhóm {revenueGroup}, ND 117/2025)"
                });
            }

            // Wave 5c: TNCN calculation per 2026 law (replaces hardcoded 10%)
            if (classification.RequiresPersonalIncomeTax)
            {
                personalIncomeTaxAmount = HKDRevenueClassification.CalculateTNCN(revenueGroup, revenueAmount, expenseAmount, industryPitRate);
                string pitDescription = revenueGroup switch
                {
                    HKDRevenueGroup.Group1 => "Thuế TNCN 2026: Không chịu thuế (Nhóm 1, doanh thu ≤ 1 tỷ)",
                    HKDRevenueGroup.Group2 => $"Thuế TNCN 2026: (Doanh thu - 1 tỷ) × {industryPitRate:P1} (Nhóm 2, ND 117/2025)",
                    HKDRevenueGroup.Group3 => $"Thuế TNCN 2026: (Doanh thu - chi phí) × 17% (Nhóm 3, lợi nhuận)",
                    HKDRevenueGroup.Group4 => $"Thuế TNCN 2026: (Doanh thu - chi phí) × 20% (Nhóm 4, lợi nhuận)",
                    _ => "Thuế TNCN 2026"
                };
                taxBreakdowns.Add(new TaxBreakdown
                {
                    TaxType = "TNCN",
                    TaxableAmount = revenueAmount,
                    TaxRate = revenueGroup == HKDRevenueGroup.Group1 ? 0m :
                              revenueGroup == HKDRevenueGroup.Group2 ? industryPitRate * 100m :
                              revenueGroup == HKDRevenueGroup.Group3 ? 17m : 20m,
                    TaxAmount = personalIncomeTaxAmount,
                    Description = pitDescription
                });

                // Warn if Nhóm 3/4 and no expense data (PIT overstated)
                if ((revenueGroup == HKDRevenueGroup.Group3 || revenueGroup == HKDRevenueGroup.Group4) && expenseAmount == 0)
                {
                    _logger.LogWarning(
                        "CalculateTaxAsync 2026: Nhóm {Group} (revenue {Revenue:N0}) but expenseAmount = 0. " +
                        "TNCN = (Revenue - 0) × {Rate}% = {Pit:N0} (OVERSTATED — no chi phí provided).",
                        revenueGroup, revenueAmount, revenueGroup == HKDRevenueGroup.Group3 ? 17 : 20, personalIncomeTaxAmount);
                }
            }

            // Special Tax calculation
            if (classification.RequiresSpecialTax)
            {
                specialTaxAmount = revenueAmount * 0.05m; // 5% thuế đặc biệt
                taxBreakdowns.Add(new TaxBreakdown
                {
                    TaxType = "Thuế đặc biệt",
                    TaxableAmount = revenueAmount,
                    TaxRate = 5m,
                    TaxAmount = specialTaxAmount,
                    Description = "Thuế đặc biệt theo Thông tư 152/2025/TT-BTC"
                });
            }

            decimal totalTaxAmount = vatAmount + personalIncomeTaxAmount + specialTaxAmount;
            decimal netAmount = revenueAmount - totalTaxAmount;

            return new HKDTaxCalculation
            {
                BookType = bookType,
                RevenueAmount = revenueAmount,
                ExpenseAmount = expenseAmount,
                VatAmount = vatAmount,
                PersonalIncomeTaxAmount = personalIncomeTaxAmount,
                SpecialTaxAmount = specialTaxAmount,
                TotalTaxAmount = totalTaxAmount,
                NetAmount = netAmount,
                TaxBreakdowns = taxBreakdowns
            };
        }

        /// <summary>
        /// Validate tax compliance for HKD book type
        /// </summary>
        public async Task<HKDTaxComplianceResult> ValidateTaxComplianceAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            CoreAccountingEntry entry,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Validating tax compliance for tenant {TenantId}, book type {BookType}",
                tenantId.Value, bookType);

            List<ComplianceIssue> issues = [];
            List<ComplianceWarning> warnings = [];

            HKDGroup group = GetHKDGroupForBookType(bookType);
            TaxClassificationData classification = await GetTaxClassificationAsync(group, bookType);

            // Validate VAT compliance
            if (classification.RequiresVatDeclaration && entry.Amount <= 0)
            {
                issues.Add(new ComplianceIssue
                {
                    IssueType = "VAT_COMPLIANCE",
                    Description = "Số tiền không hợp lệ cho tính thuế GTGT",
                    Recommendation = "Kiểm tra lại số liệu doanh thu",
                    Severity = SeverityLevel.High
                });
            }

            // Validate Personal Income Tax compliance
            if (classification.RequiresPersonalIncomeTax && entry.Amount > 100000000) // > 100 triệu
            {
                warnings.Add(new ComplianceWarning
                {
                    WarningType = "TNCN_THRESHOLD",
                    Description = "Doanh thu vượt ngưỡng 100 triệu, cần kiểm tra TNCN",
                    Recommendation = "Xem xét đăng ký thuế TNCN theo phương pháp kê khai"
                });
            }

            // Validate Special Tax compliance
            if (classification.RequiresSpecialTax)
            {
                warnings.Add(new ComplianceWarning
                {
                    WarningType = "SPECIAL_TAX",
                    Description = "Hộ kinh doanh thuộc diện chịu thuế đặc biệt",
                    Recommendation = "Kiểm tra danh mục hàng hóa, dịch vụ chịu thuế đặc biệt"
                });
            }

            return new HKDTaxComplianceResult
            {
                BookType = bookType,
                IsCompliant = issues.Count == 0,
                Issues = issues,
                Warnings = warnings,
                ComplianceStatus = issues.Count == 0 ? "Đạt yêu cầu" : "Không đạt yêu cầu",
                ValidationDate = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Get tax reporting requirements for HKD book type
        /// </summary>
        public async Task<HKDTaxReportingRequirements> GetReportingRequirementsAsync(
            AccountingBookType bookType,
            CancellationToken cancellationToken = default)
        {
            HKDGroup group = GetHKDGroupForBookType(bookType);
            List<ReportingRequirement> requirements = [];
            List<string> requiredDocuments = [];
            List<string> reportFormats = [];

            switch (group)
            {
                case HKDGroup.Group1: // S1a
                    requirements.Add(new ReportingRequirement
                    {
                        RequirementType = "Báo cáo thuế GTGT",
                        Description = "Báo cáo GTGT hàng tháng (miễn thuế)",
                        Format = "Mẫu 01/GTGT",
                        Deadline = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 20),
                        IsMandatory = true
                    });
                    requiredDocuments.AddRange(collection);
                    reportFormats.AddRange(collection0);
                    break;

                case HKDGroup.Group2: // S2a-S2e
                    requirements.Add(new ReportingRequirement
                    {
                        RequirementType = "Báo cáo thuế GTGT",
                        Description = "Báo cáo GTGT hàng tháng",
                        Format = "Mẫu 01/GTGT",
                        Deadline = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 20),
                        IsMandatory = true
                    });
                    requirements.Add(new ReportingRequirement
                    {
                        RequirementType = "Báo cáo thuế TNCN",
                        Description = "Báo cáo TNCN hàng quý",
                        Format = "Mẫu 05/TNCN",
                        Deadline = new DateTime(DateTime.Now.Year, (((DateTime.Now.Month - 1) / 3) + 1) * 3, 30),
                        IsMandatory = true
                    });
                    requiredDocuments.AddRange(collection1);
                    reportFormats.AddRange(collection2);
                    break;

                case HKDGroup.Group3: // S3a
                    requirements.Add(new ReportingRequirement
                    {
                        RequirementType = "Báo cáo thuế đặc biệt",
                        Description = "Báo cáo thuế đặc biệt hàng tháng",
                        Format = "Mẫu 02/ThuếĐặcBiệt",
                        Deadline = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 25),
                        IsMandatory = true
                    });
                    requiredDocuments.AddRange(collection3);
                    reportFormats.AddRange(collection4);
                    break;
                default:
                    break;
            }

            return new HKDTaxReportingRequirements
            {
                BookType = bookType,
                Requirements = requirements,
                RequiredDocuments = requiredDocuments,
                ReportFormats = reportFormats,
                ReportingDeadline = DateTime.Now.AddMonths(1),
                ReportingFrequency = group == HKDGroup.Group2 ? "Hàng quý" : "Hàng tháng"
            };
        }

        #region Private Helper Methods

        /// <summary>
        /// Wave 5: 4-group VAT rate table per Luật Thuế GTGT sửa đổi 2025 + ND 117/2025.
        /// Key: IndustrySector, Value: VAT rate as fraction (e.g., 0.01m = 1%).
        /// </summary>
        private static readonly Dictionary<IndustrySector, decimal> IndustryVatRates = new()
        {
            [IndustrySector.Distribution] = 0.01m,         // 1%
            [IndustrySector.ProductionTransport] = 0.03m,  // 3%
            [IndustrySector.Service] = 0.05m,              // 5%
            [IndustrySector.OtherBusiness] = 0.02m         // 2%
        };

        /// <summary>
        /// Wave 5: 4-group PIT rate table (HKD Nhóm 2) per Luật Thuế TNCN sửa đổi 2025 + ND 117/2025.
        /// Key: IndustrySector, Value: PIT rate as fraction (e.g., 0.005m = 0.5%).
        /// </summary>
        private static readonly Dictionary<IndustrySector, decimal> IndustryPitRates = new()
        {
            [IndustrySector.Distribution] = 0.005m,         // 0.5%
            [IndustrySector.ProductionTransport] = 0.015m,  // 1.5%
            [IndustrySector.Service] = 0.02m,               // 2%
            [IndustrySector.OtherBusiness] = 0.01m           // 1%
        };

        /// <summary>
        /// Wave 5: Get VAT rate (fraction) for an industry sector.
        /// Per Luật Thuế GTGT sửa đổi 2025 + ND 117/2025.
        /// </summary>
        public decimal GetVatRate(IndustrySector sector)
        {
            if (!IndustryVatRates.TryGetValue(sector, out decimal rate))
            {
                _logger.LogWarning("No VAT rate found for IndustrySector {Sector}, defaulting to OtherBusiness (2%)", sector);
                return IndustryVatRates[IndustrySector.OtherBusiness];
            }
            return rate;
        }

        /// <summary>
        /// Wave 5: Get PIT rate (fraction) for an industry sector (HKD Nhóm 2).
        /// Per Luật Thuế TNCN sửa đổi 2025 + ND 117/2025.
        /// </summary>
        public decimal GetPitRate(IndustrySector sector)
        {
            if (!IndustryPitRates.TryGetValue(sector, out decimal rate))
            {
                _logger.LogWarning("No PIT rate found for IndustrySector {Sector}, defaulting to OtherBusiness (1%)", sector);
                return IndustryPitRates[IndustrySector.OtherBusiness];
            }
            return rate;
        }

        private static HKDGroup GetHKDGroupForBookType(AccountingBookType bookType)
        {
            return bookType switch
            {
                AccountingBookType.S1a_HKD => HKDGroup.Group1,
                AccountingBookType.S2a_HKD => HKDGroup.Group2,
                AccountingBookType.S2b_HKD => HKDGroup.Group2,
                AccountingBookType.S2c_HKD => HKDGroup.Group2,
                AccountingBookType.S2d_HKD => HKDGroup.Group2,
                AccountingBookType.S2e_HKD => HKDGroup.Group2,
                AccountingBookType.S3a_HKD => HKDGroup.Group3,
                AccountingBookType.RevenueBook => throw new NotImplementedException(),
                AccountingBookType.ExpenseBook => throw new NotImplementedException(),
                AccountingBookType.CashBankBook => throw new NotImplementedException(),
                AccountingBookType.TaxDeclarationBook => throw new NotImplementedException(),
                _ => throw new ArgumentException($"Unknown HKD book type: {bookType}")
            };
        }

        private async Task<TaxClassificationData> GetTaxClassificationAsync(HKDGroup group, AccountingBookType bookType)
        {
            return await Task.FromResult(group switch
            {
                HKDGroup.Group1 => new TaxClassificationData
                {
                    VatRate = VatRate.Exempt,
                    RequiresVatDeclaration = false,
                    RequiresPersonalIncomeTax = false,
                    RequiresSpecialTax = false,
                    TaxRate = 0m,
                    TaxClassification = "Miễn thuế GTGT, không nộp TNCN",
                    ApplicableTaxes = ["Không"]
                },
                HKDGroup.Group2 => new TaxClassificationData
                {
                    VatRate = VatRate.Five,
                    RequiresVatDeclaration = true,
                    RequiresPersonalIncomeTax = true,
                    RequiresSpecialTax = false,
                    TaxRate = 5m,
                    TaxClassification = "Nộp thuế GTGT và TNCN theo tỷ lệ %",
                    ApplicableTaxes = ["GTGT", "TNCN"]
                },
                HKDGroup.Group3 => new TaxClassificationData
                {
                    VatRate = VatRate.Zero,
                    RequiresVatDeclaration = false,
                    RequiresPersonalIncomeTax = false,
                    RequiresSpecialTax = true,
                    TaxRate = 0m,
                    TaxClassification = "Chịu các loại thuế khác",
                    ApplicableTaxes = ["Thuế đặc biệt", "Thuế tiêu thụ đặc biệt"]
                },
                _ => throw new ArgumentException($"Unknown HKD group: {group}")
            });
        }

        #endregion
    }

    /// <summary>
    /// Internal tax classification data
    /// </summary>
    internal sealed record TaxClassificationData
    {
        public VatRate VatRate { get; init; }
        public bool RequiresVatDeclaration { get; init; }
        public bool RequiresPersonalIncomeTax { get; init; }
        public bool RequiresSpecialTax { get; init; }
        public decimal TaxRate { get; init; }
        public string TaxClassification { get; init; }
        public List<string> ApplicableTaxes { get; init; } = [];
    }
}
