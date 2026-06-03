using VanAn.Shared.Domain;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// HKD Tax Reporting Service Implementation - Phase 2.3.9
    /// Implements Vietnamese Accounting Standard (Thông tư 152/2025/TT-BTC)
    /// Tax reporting formats for 7 HKD book types
    /// </summary>
    public class HKDTaxReportingService(
        ILogger<HKDTaxReportingService> logger,
        IHKDTaxClassificationService taxClassificationService,
        IHKDComplianceService complianceService) : IHKDTaxReportingService
    {
        private readonly ILogger<HKDTaxReportingService> _logger = logger;
        private readonly IHKDTaxClassificationService _taxClassificationService = taxClassificationService;
        private readonly IHKDComplianceService _complianceService = complianceService;

        /// <summary>
        /// Generate tax report for HKD book type
        /// </summary>
        public async Task<HKDTaxReport> GenerateTaxReportAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Generating tax report for tenant {TenantId}, book type {BookType}, period {Period}",
                tenantId.Value, bookType, period.ToString("yyyy-MM"));

            // Generate report based on book type
            return bookType switch
            {
                AccountingBookType.S1a_HKD => await GenerateS1aReportAsync(tenantId, period, cancellationToken),
                AccountingBookType.S2a_HKD => await GenerateS2aReportAsync(tenantId, period, cancellationToken),
                AccountingBookType.S2b_HKD => await GenerateS2bReportAsync(tenantId, period, cancellationToken),
                AccountingBookType.S2c_HKD => await GenerateS2cReportAsync(tenantId, period, cancellationToken),
                AccountingBookType.S2d_HKD => await GenerateS2dReportAsync(tenantId, period, cancellationToken),
                AccountingBookType.S2e_HKD => await GenerateS2eReportAsync(tenantId, period, cancellationToken),
                AccountingBookType.S3a_HKD => await GenerateS3aReportAsync(tenantId, period, cancellationToken),
                AccountingBookType.RevenueBook => null!,
                AccountingBookType.ExpenseBook => null!,
                AccountingBookType.CashBankBook => null!,
                AccountingBookType.TaxDeclarationBook => null!,
                _ => throw new ArgumentException($"Unsupported HKD book type: {bookType}")
            };
        }

        /// <summary>
        /// Get available report formats for HKD book type
        /// </summary>
        public async Task<List<ReportFormat>> GetReportFormatsAsync(
            AccountingBookType bookType,
            CancellationToken cancellationToken = default)
        {
            List<ReportFormat> formats = [];
            HKDGroup group = GetHKDGroupForBookType(bookType);

            // Get FormatType based on book type
            string formatType = bookType switch
            {
                AccountingBookType.S1a_HKD => "TaxExemption",
                AccountingBookType.S2a_HKD => "VATAndPIT",
                AccountingBookType.S2b_HKD => "RevenueOnly",
                AccountingBookType.S2c_HKD => "RevenueExpense",
                AccountingBookType.S2d_HKD => "Inventory",
                AccountingBookType.S2e_HKD => "CashFlow",
                AccountingBookType.S3a_HKD => "SpecialTax",
                AccountingBookType.RevenueBook => null!,
                AccountingBookType.ExpenseBook => null!,
                AccountingBookType.CashBankBook => null!,
                AccountingBookType.TaxDeclarationBook => null!,
                _ => "Standard"
            };

            // Standard formats for all HKD books
            formats.AddRange(new[]
            {
                new ReportFormat
                {
                    FormatId = "PDF",
                    FormatName = "PDF Format",
                    FormatType = formatType,
                    Extension = ".pdf",
                    MimeType = "application/pdf",
                    IsStandard = true,
                    SupportedBookTypes = [bookType.ToString()],
                    FormatOptions = new Dictionary<string, string>
                    {
                        { "PageSize", "A4" },
                        { "Orientation", "Portrait" },
                        { "Font", "Arial" }
                    }
                },
                new ReportFormat
                {
                    FormatId = "EXCEL",
                    FormatName = "Excel Format",
                    FormatType = formatType,
                    Extension = ".xlsx",
                    MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    IsStandard = true,
                    SupportedBookTypes = [bookType.ToString()],
                    FormatOptions = new Dictionary<string, string>
                    {
                        { "Template", "Standard" },
                        { "IncludeFormulas", "true" }
                    }
                },
                new ReportFormat
                {
                    FormatId = "XML",
                    FormatName = "XML Format",
                    FormatType = formatType,
                    Extension = ".xml",
                    MimeType = "application/xml",
                    IsStandard = true,
                    SupportedBookTypes = [bookType.ToString()],
                    FormatOptions = new Dictionary<string, string>
                    {
                        { "Schema", "TT152-2025" },
                        { "Encoding", "UTF-8" }
                    }
                },
                new ReportFormat
                {
                    FormatId = "CSV",
                    FormatName = "CSV Format",
                    FormatType = formatType,
                    Extension = ".csv",
                    MimeType = "text/csv",
                    IsStandard = true,
                    SupportedBookTypes = [bookType.ToString()],
                    FormatOptions = new Dictionary<string, string>
                    {
                        { "Delimiter", "," },
                        { "Encoding", "UTF-8" }
                    }
                }
            });

            // Group-specific formats
            if (group == HKDGroup.Group2)
            {
                formats.Add(new ReportFormat
                {
                    FormatId = "JSON",
                    FormatName = "JSON Format",
                    FormatType = formatType,
                    Extension = ".json",
                    MimeType = "application/json",
                    IsStandard = false,
                    SupportedBookTypes = [bookType.ToString()],
                    FormatOptions = new Dictionary<string, string>
                    {
                        { "PrettyPrint", "true" },
                        { "Schema", "TT152-2025" }
                    }
                });
            }

            return await Task.FromResult(formats);
        }

        /// <summary>
        /// Export tax report to specified format
        /// </summary>
        public async Task<byte[]> ExportTaxReportAsync(
            HKDTaxReport report,
            ReportFormat format,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Exporting tax report to format {FormatId}", format.FormatId);

            return format.FormatId switch
            {
                "PDF" => await ExportToPdfAsync(report, cancellationToken),
                "EXCEL" => await ExportToExcelAsync(report, cancellationToken),
                "XML" => await ExportToXmlAsync(report, cancellationToken),
                "JSON" => await ExportToJsonAsync(report, cancellationToken),
                "CSV" => await ExportToCsvAsync(report, cancellationToken),
                _ => throw new ArgumentException($"Unsupported format: {format.FormatId}")
            };
        }

        /// <summary>
        /// Validate report format compliance
        /// </summary>
        public async Task<ReportFormatValidationResult> ValidateReportFormatAsync(
            HKDTaxReport report,
            ReportFormat format,
            CancellationToken cancellationToken = default)
        {
            List<FormatValidationError> errors = [];
            List<FormatValidationWarning> warnings = [];

            // Validate required fields
            foreach (string field in report.Metadata.RequiredFields)
            {
                if (!HasRequiredField(report, field))
                {
                    errors.Add(new FormatValidationError
                    {
                        FieldName = field,
                        ErrorType = "MissingRequiredField",
                        Description = $"Thiếu trường bắt buộc: {field}",
                        ExpectedValue = "Required",
                        ActualValue = "Missing"
                    });
                }
            }

            // Validate format-specific rules
            if (format.FormatId == "XML")
            {
                if (report.Metadata.TemplateVersion != "1.0")
                {
                    warnings.Add(new FormatValidationWarning
                    {
                        FieldName = "TemplateVersion",
                        WarningType = "VersionMismatch",
                        Description = "Phiên bản template không khớp",
                        Recommendation = "Cập nhật lên phiên bản 1.0"
                    });
                }
            }

            // Validate data integrity
            if (report.Data.TotalRevenue < 0)
            {
                errors.Add(new FormatValidationError
                {
                    FieldName = "TotalRevenue",
                    ErrorType = "InvalidValue",
                    Description = "Tổng doanh thu không được âm",
                    ExpectedValue = ">= 0",
                    ActualValue = report.Data.TotalRevenue.ToString()
                });
            }

            return new ReportFormatValidationResult
            {
                Format = format,
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings,
                ValidationDate = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Get report template for HKD book type
        /// </summary>
        public async Task<ReportTemplate> GetReportTemplateAsync(
            AccountingBookType bookType,
            ReportFormat format,
            CancellationToken cancellationToken = default)
        {
            HKDGroup group = GetHKDGroupForBookType(bookType);
            ReportTemplate template = await GenerateTemplateAsync(bookType, format, group, cancellationToken);

            return template;
        }

        /// <summary>
        /// Generate quarterly tax summary
        /// </summary>
        public async Task<HKDQuarterlyTaxSummary> GenerateQuarterlyTaxSummaryAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            int year,
            int quarter,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Generating quarterly tax summary for tenant {TenantId}, book type {BookType}, year {Year}, quarter {Quarter}",
                tenantId.Value, bookType, year, quarter);

            List<MonthlySummary> monthlySummaries = [];
            decimal quarterlyRevenue = 0m;
            decimal quarterlyExpense = 0m;
            decimal quarterlyVat = 0m;
            decimal quarterlyPersonalIncomeTax = 0m;
            decimal quarterlySpecialTax = 0m;

            // Generate monthly summaries for the quarter
            int startMonth = ((quarter - 1) * 3) + 1;
            for (int month = startMonth; month < startMonth + 3; month++)
            {
                // In real implementation, would query actual monthly data
                decimal monthlyRevenue = GenerateMockRevenue(bookType, month);
                decimal monthlyExpense = monthlyRevenue * 0.7m;
                decimal monthlyVat = bookType == AccountingBookType.S1a_HKD ? 0 : monthlyRevenue * 0.05m;
                decimal monthlyPersonalIncomeTax = bookType == AccountingBookType.S1a_HKD ? 0 : monthlyRevenue * 0.1m;
                decimal monthlySpecialTax = bookType == AccountingBookType.S3a_HKD ? monthlyRevenue * 0.05m : 0;

                monthlySummaries.Add(new MonthlySummary
                {
                    Month = month,
                    Revenue = monthlyRevenue,
                    Expense = monthlyExpense,
                    Vat = monthlyVat,
                    PersonalIncomeTax = monthlyPersonalIncomeTax,
                    SpecialTax = monthlySpecialTax
                });

                quarterlyRevenue += monthlyRevenue;
                quarterlyExpense += monthlyExpense;
                quarterlyVat += monthlyVat;
                quarterlyPersonalIncomeTax += monthlyPersonalIncomeTax;
                quarterlySpecialTax += monthlySpecialTax;
            }

            return new HKDQuarterlyTaxSummary
            {
                BookType = bookType,
                TenantId = tenantId,
                Year = year,
                Quarter = quarter,
                QuarterlyRevenue = quarterlyRevenue,
                QuarterlyExpense = quarterlyExpense,
                QuarterlyVat = quarterlyVat,
                QuarterlyPersonalIncomeTax = quarterlyPersonalIncomeTax,
                QuarterlySpecialTax = quarterlySpecialTax,
                MonthlySummaries = monthlySummaries,
                GeneratedDate = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Generate annual tax summary
        /// </summary>
        public async Task<HKDAnnualTaxSummary> GenerateAnnualTaxSummaryAsync(
            TenantId tenantId,
            AccountingBookType bookType,
            int year,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Generating annual tax summary for tenant {TenantId}, book type {BookType}, year {Year}",
                tenantId.Value, bookType, year);

            List<QuarterlySummary> quarterlySummaries = [];
            decimal annualRevenue = 0m;
            decimal annualExpense = 0m;
            decimal annualVat = 0m;
            decimal annualPersonalIncomeTax = 0m;
            decimal annualSpecialTax = 0m;

            // Generate quarterly summaries for the year
            for (int quarter = 1; quarter <= 4; quarter++)
            {
                HKDQuarterlyTaxSummary quarterlySummary = await GenerateQuarterlyTaxSummaryAsync(tenantId, bookType, year, quarter, cancellationToken);

                quarterlySummaries.Add(new QuarterlySummary
                {
                    Quarter = quarter,
                    Revenue = quarterlySummary.QuarterlyRevenue,
                    Expense = quarterlySummary.QuarterlyExpense,
                    Vat = quarterlySummary.QuarterlyVat,
                    PersonalIncomeTax = quarterlySummary.QuarterlyPersonalIncomeTax,
                    SpecialTax = quarterlySummary.QuarterlySpecialTax
                });

                annualRevenue += quarterlySummary.QuarterlyRevenue;
                annualExpense += quarterlySummary.QuarterlyExpense;
                annualVat += quarterlySummary.QuarterlyVat;
                annualPersonalIncomeTax += quarterlySummary.QuarterlyPersonalIncomeTax;
                annualSpecialTax += quarterlySummary.QuarterlySpecialTax;
            }

            // Generate year comparison
            decimal previousYearRevenue = annualRevenue * 0.9m; // Mock 10% growth
            decimal previousYearExpense = annualExpense * 0.9m; // Mock 10% growth
            decimal previousYearVat = annualVat * 0.9m; // Mock 10% growth
            decimal previousYearPersonalIncomeTax = annualPersonalIncomeTax * 0.9m; // Mock 10% growth
            decimal previousYearSpecialTax = annualSpecialTax * 0.9m; // Mock 10% growth

            TaxYearComparison yearComparison = new()
            {
                PreviousYearRevenue = previousYearRevenue,
                PreviousYearExpense = previousYearExpense,
                PreviousYearVat = previousYearVat,
                PreviousYearPersonalIncomeTax = previousYearPersonalIncomeTax,
                PreviousYearSpecialTax = previousYearSpecialTax,
                RevenueGrowthRate = previousYearRevenue > 0 ? (annualRevenue - previousYearRevenue) / previousYearRevenue * 100 : 0,
                ExpenseGrowthRate = previousYearExpense > 0 ? (annualExpense - previousYearExpense) / previousYearExpense * 100 : 0,
                TaxEfficiency = annualRevenue > 0 ? (annualVat + annualPersonalIncomeTax + annualSpecialTax) / annualRevenue * 100 : 0
            };

            return new HKDAnnualTaxSummary
            {
                BookType = bookType,
                TenantId = tenantId,
                Year = year,
                AnnualRevenue = annualRevenue,
                AnnualExpense = annualExpense,
                AnnualVat = annualVat,
                AnnualPersonalIncomeTax = annualPersonalIncomeTax,
                AnnualSpecialTax = annualSpecialTax,
                QuarterlySummaries = quarterlySummaries,
                YearComparison = yearComparison,
                GeneratedDate = DateTime.UtcNow
            };
        }

        #region Private Helper Methods

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
                AccountingBookType.RevenueBook => HKDGroup.Group1,
                AccountingBookType.ExpenseBook => HKDGroup.Group1,
                AccountingBookType.CashBankBook => HKDGroup.Group1,
                AccountingBookType.TaxDeclarationBook => HKDGroup.Group1,
                _ => HKDGroup.Group1
            };
        }

        /// <summary>
        /// Generate S1a-HKD tax report (Tax Exemption)
        /// </summary>
        private async Task<HKDTaxReport> GenerateS1aReportAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Generating S1a-HKD tax report for tenant {TenantId}, period {Period}",
                tenantId.Value, period.ToString("yyyy-MM"));

            ReportData reportData = await GenerateS1aReportDataAsync(tenantId, period, cancellationToken);
            ReportMetadata metadata = await GetReportMetadataAsync(AccountingBookType.S1a_HKD, cancellationToken);

            return new HKDTaxReport
            {
                BookType = AccountingBookType.S1a_HKD,
                TenantId = tenantId,
                Period = period,
                ReportType = "TaxExemption",
                ReportTitle = "Tax Exemption Report - Báo cáo thuế miễn thuế GTGT, không nộp TNCN - S1a_HKD",
                IsTaxExempt = true,
                VATAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                VATRate = 0m,
                TotalRevenue = reportData.TotalRevenue,
                TotalExpenses = reportData.TotalExpense,
                NetIncome = reportData.TotalRevenue - reportData.TotalExpense,
                CashInflows = reportData.TotalRevenue,
                CashOutflows = reportData.TotalExpense,
                NetCashFlow = reportData.TotalRevenue - reportData.TotalExpense,
                MaterialCosts = 0m,
                SpecialTaxAmount = 0m,
                SpecialTaxCategories = [],
                TaxAuthorityReferences = ["Thông tư 152/2025/TT-BTC", "Điều 5 - Miễn thuế GTGT"],
                RevenueCategories = ["Doanh thu bán hàng", "Doanh thu dịch vụ"],
                ProductCategories = ["Hàng hóa", "Dịch vụ"],
                RevenueBreakdown = GenerateRevenueBreakdown(reportData),
                ExpenseBreakdown = GenerateExpenseBreakdown(reportData),
                InventoryBreakdown = [],
                PaymentMethodBreakdown = GeneratePaymentMethodBreakdown(reportData),
                Data = reportData,
                Metadata = metadata,
                GeneratedDate = DateTime.UtcNow,
                GeneratedBy = "HKDTaxReportingService"
            };
        }

        /// <summary>
        /// Generate S1a-HKD report data (Tax Exemption)
        /// </summary>
        private static async Task<ReportData> GenerateS1aReportDataAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock data for S1a-HKD (Tax Exemption)
            List<TransactionRecord> transactions = [];
            List<TaxBreakdownRecord> taxBreakdowns = [];
            Dictionary<string, decimal> accountSummaries = [];

            // Generate mock revenue transactions
            decimal baseRevenue = 50000000m; // 50 million VND
            for (int i = 1; i <= period.Day; i++)
            {
                if (i % 7 == 0) // Weekly revenue
                {
                    decimal dailyRevenue = baseRevenue / 30;
                    transactions.Add(new TransactionRecord
                    {
                        TransactionDate = new DateTime(period.Year, period.Month, i),
                        Description = "Doanh thu bán hàng ngày",
                        Amount = dailyRevenue,
                        AccountNumber = "5111",
                        TransactionType = "Revenue",
                        Reference = $"S1A-{i:D3}"
                    });
                    accountSummaries["5111"] = accountSummaries.GetValueOrDefault("5111") + dailyRevenue;
                }
            }

            // Generate mock expense transactions
            decimal baseExpense = 30000000m; // 30 million VND
            for (int i = 1; i <= period.Day; i++)
            {
                if (i % 5 == 0) // Every 5 days
                {
                    decimal dailyExpense = baseExpense / 30;
                    transactions.Add(new TransactionRecord
                    {
                        TransactionDate = new DateTime(period.Year, period.Month, i),
                        Description = "Chi phí nguyên vật liệu",
                        Amount = dailyExpense,
                        AccountNumber = "6321",
                        TransactionType = "Expense",
                        Reference = $"S1A-EXP-{i:D3}"
                    });
                    accountSummaries["6321"] = accountSummaries.GetValueOrDefault("6321") + dailyExpense;
                }
            }

            decimal totalRevenue = accountSummaries.GetValueOrDefault("5111");
            decimal totalExpense = accountSummaries.GetValueOrDefault("6321");

            return new ReportData
            {
                TotalRevenue = totalRevenue,
                TotalExpense = totalExpense,
                VatAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                SpecialTaxAmount = 0m,
                NetAmount = totalRevenue - totalExpense,
                Transactions = transactions,
                TaxBreakdowns = taxBreakdowns,
                AccountSummaries = accountSummaries
            };
        }

        /// <summary>
        /// Generate revenue breakdown from report data
        /// </summary>
        private List<TransactionRecord> GenerateRevenueBreakdown(ReportData reportData)
        {
            return
            [
                .. reportData.Transactions
                                .Where(t => t.TransactionType == "Revenue")
                                .OrderBy(t => t.TransactionDate)
,
            ];
        }

        /// <summary>
        /// Generate expense breakdown from report data
        /// </summary>
        private List<TransactionRecord> GenerateExpenseBreakdown(ReportData reportData)
        {
            return
            [
                .. reportData.Transactions
                                .Where(t => t.TransactionType == "Expense")
                                .OrderBy(t => t.TransactionDate)
,
            ];
        }

        /// <summary>
        /// Generate payment method breakdown from report data
        /// </summary>
        private static List<TransactionRecord> GeneratePaymentMethodBreakdown(ReportData reportData)
        {
            return reportData.Transactions
                .Where(t => !string.IsNullOrEmpty(t.Reference))
                .GroupBy(t => t.Reference.Split('-').FirstOrDefault())
                .Select(g => new TransactionRecord
                {
                    TransactionDate = g.First().TransactionDate,
                    Description = $"Thanh toán {g.Key}",
                    Amount = g.Sum(t => t.Amount),
                    AccountNumber = "1111",
                    TransactionType = "Payment",
                    Reference = g.Key
                })
                .ToList();
        }

        /// <summary>
        /// Get required fields for book type
        /// </summary>
        private List<string> GetRequiredFieldsForBookType(AccountingBookType bookType)
        {
            return bookType switch
            {
                AccountingBookType.S1a_HKD => ["Revenue", "Expenses", "TaxExemption"],
                AccountingBookType.S2a_HKD => ["Revenue", "VAT", "PIT", "TaxRate"],
                AccountingBookType.S2b_HKD => ["Revenue", "ServiceRevenue", "GoodsRevenue"],
                AccountingBookType.S2c_HKD => ["Revenue", "Expenses", "NetIncome"],
                AccountingBookType.S2d_HKD => ["Inventory", "MaterialCosts", "Products"],
                AccountingBookType.S2e_HKD => ["CashFlow", "Inflows", "Outflows"],
                AccountingBookType.S3a_HKD => ["SpecialTax", "TaxCategories", "Industry"],
                AccountingBookType.RevenueBook => new List<string>(),
                AccountingBookType.ExpenseBook => new List<string>(),
                AccountingBookType.CashBankBook => new List<string>(),
                AccountingBookType.TaxDeclarationBook => new List<string>(),
                _ => new List<string>()
            };
        }

        /// <summary>
        /// Get validation rules for book type
        /// </summary>
        private List<ValidationRule> GetValidationRulesForBookType(AccountingBookType bookType)
        {
            return bookType switch
            {
                AccountingBookType.S1a_HKD =>
            [
                new() { RuleName = "TaxExemption", Expression = "VATAmount == 0", ErrorMessage = "VAT must be 0 for tax exemption", Severity = SeverityLevel.High },
                new() { RuleName = "NoPIT", Expression = "PersonalIncomeTaxAmount == 0", ErrorMessage = "PIT must be 0 for tax exemption", Severity = SeverityLevel.High }
            ],
                AccountingBookType.S2a_HKD =>
            [
                new() { RuleName = "VATRate", Expression = "VATRate == 0.05", ErrorMessage = "VAT rate must be 5%", Severity = SeverityLevel.High },
                new() { RuleName = "PITCalculation", Expression = "PersonalIncomeTaxAmount > 0", ErrorMessage = "PIT must be calculated", Severity = SeverityLevel.High }
            ],
                AccountingBookType.RevenueBook => new List<ValidationRule>(),
                AccountingBookType.ExpenseBook => new List<ValidationRule>(),
                AccountingBookType.CashBankBook => new List<ValidationRule>(),
                AccountingBookType.TaxDeclarationBook => new List<ValidationRule>(),
                AccountingBookType.S2b_HKD => new List<ValidationRule>(),
                AccountingBookType.S2c_HKD => new List<ValidationRule>(),
                AccountingBookType.S2d_HKD => new List<ValidationRule>(),
                AccountingBookType.S2e_HKD => new List<ValidationRule>(),
                AccountingBookType.S3a_HKD => new List<ValidationRule>(),
                _ => new List<ValidationRule>()
            };
        }

        /// <summary>
        /// Get formulas for book type
        /// </summary>
        private Dictionary<string, string> GetFormulasForBookType(AccountingBookType bookType)
        {
            return bookType switch
            {
                AccountingBookType.S1a_HKD => new Dictionary<string, string>
            {
                { "NetIncome", "TotalRevenue - TotalExpenses" },
                { "TaxLiability", "0" }
            },
                AccountingBookType.S2a_HKD => new Dictionary<string, string>
            {
                { "VAT", "TotalRevenue * 0.05" },
                { "PIT", "TotalRevenue * 0.01" },
                { "NetIncome", "TotalRevenue - TotalExpenses - VAT - PIT" }
            },
                AccountingBookType.RevenueBook => null!,
                AccountingBookType.ExpenseBook => null!,
                AccountingBookType.CashBankBook => null!,
                AccountingBookType.TaxDeclarationBook => null!,
                AccountingBookType.S2b_HKD => null!,
                AccountingBookType.S2c_HKD => null!,
                AccountingBookType.S2d_HKD => null!,
                AccountingBookType.S2e_HKD => null!,
                AccountingBookType.S3a_HKD => null!,
                _ => []
            };
        }

        private async Task<ReportMetadata> GetReportMetadataAsync(
            AccountingBookType bookType,
            CancellationToken cancellationToken)
        {
            List<string> requiredFields = GetRequiredFieldsForBookType(bookType);
            List<ValidationRule> validationRules = GetValidationRulesForBookType(bookType);
            Dictionary<string, string> formulas = GetFormulasForBookType(bookType);

            return await Task.FromResult(new ReportMetadata
            {
                ReportVersion = "1.0",
                TemplateVersion = "1.0",
                RequiredFields = requiredFields,
                ValidationRules = validationRules,
                Formulas = formulas
            });
        }

        private static string GetReportTypeForBookType(AccountingBookType bookType)
        {
            return bookType switch
            {
                AccountingBookType.S1a_HKD => "Báo cáo thuế GTGT (Miễn thuế)",
                AccountingBookType.S2a_HKD => "Báo cáo thuế GTGT và TNCN",
                AccountingBookType.S2b_HKD => "Báo cáo doanh thu bán hàng",
                AccountingBookType.S2c_HKD => "Báo cáo chi tiết doanh thu, chi phí",
                AccountingBookType.S2d_HKD => "Báo cáo chi tiết vật liệu, dụng cụ",
                AccountingBookType.S2e_HKD => "Báo cáo chi tiết tiền",
                AccountingBookType.S3a_HKD => "Báo cáo thuế đặc biệt",
                AccountingBookType.RevenueBook => null!,
                AccountingBookType.ExpenseBook => null!,
                AccountingBookType.CashBankBook => null!,
                AccountingBookType.TaxDeclarationBook => null!,
                _ => "Báo cáo thuế HKD"
            };
        }

        private static decimal GenerateMockRevenue(AccountingBookType bookType, int month)
        {
            // Generate realistic mock revenue based on book type and month
            decimal baseRevenue = bookType switch
            {
                AccountingBookType.S1a_HKD => 50000000m,  // 50 triệu
                AccountingBookType.S2a_HKD => 80000000m,  // 80 triệu
                AccountingBookType.S2b_HKD => 100000000m, // 100 triệu
                AccountingBookType.S2c_HKD => 90000000m,  // 90 triệu
                AccountingBookType.S2d_HKD => 70000000m,  // 70 triệu
                AccountingBookType.S2e_HKD => 60000000m,  // 60 triệu
                AccountingBookType.S3a_HKD => 120000000m, // 120 triệu
                AccountingBookType.RevenueBook => 0m,
                AccountingBookType.ExpenseBook => 0m,
                AccountingBookType.CashBankBook => 0m,
                AccountingBookType.TaxDeclarationBook => 0m,
                _ => 50000000m
            };

            // Add seasonal variation
            decimal seasonalFactor = month switch
            {
                1 or 2 or 11 or 12 => 1.2m, // Holiday season
                3 or 4 or 9 or 10 => 1.0m,  // Normal season
                5 or 6 or 7 or 8 => 0.8m,  // Low season
                _ => 1.0m
            };

            return baseRevenue * seasonalFactor;
        }

        private List<TransactionRecord> GenerateMockTransactions(AccountingBookType bookType, DateTime period)
        {
            List<TransactionRecord> transactions = [];
            Random random = new(period.Day);

            // Generate 5-10 mock transactions
            int transactionCount = random.Next(5, 11);
            for (int i = 0; i < transactionCount; i++)
            {
                int day = random.Next(1, DateTime.DaysInMonth(period.Year, period.Month) + 1);
                decimal amount = GenerateMockRevenue(bookType, period.Month) / transactionCount;

                transactions.Add(new TransactionRecord
                {
                    TransactionDate = new DateTime(period.Year, period.Month, day),
                    Description = $"Giao dịch {i + 1}",
                    Amount = amount,
                    AccountNumber = bookType == AccountingBookType.S1a_HKD ? "511" : "511",
                    TransactionType = "Doanh thu",
                    Reference = $"TXN-{period:yyyyMM}-{i + 1:D3}"
                });
            }

            return transactions;
        }

        private static List<TaxBreakdownRecord> GenerateMockTaxBreakdowns(AccountingBookType bookType, decimal revenue)
        {
            var breakdowns = new List<TaxBreakdownRecord>();
            if (bookType != AccountingBookType.S1a_HKD)
                breakdowns.Add(new TaxBreakdownRecord {TaxType = "VAT", TaxableAmount = revenue, TaxRate = 5m, TaxAmount = revenue * 0.05m, Description = "Thuế GTGT 5%" });
            if (bookType != AccountingBookType.S1a_HKD)
                breakdowns.Add(new TaxBreakdownRecord {TaxType = "TNCN", TaxableAmount = revenue, TaxRate = 10m, TaxAmount = revenue * 0.1m, Description = "Thuế TNCN 10%" });
            if (bookType == AccountingBookType.S3a_HKD)
                breakdowns.Add(new TaxBreakdownRecord {TaxType = "Thuế đặc biệt", TaxableAmount = revenue, TaxRate = 5m, TaxAmount = revenue * 0.05m, Description = "Thuế đặc biệt 5%" });

            return breakdowns;
        }

        private static Dictionary<string, decimal> GenerateMockAccountSummaries(AccountingBookType bookType)
        {
            Dictionary<string, decimal> summaries = new()
            {
                ["511"] = GenerateMockRevenue(bookType, DateTime.Now.Month)
            };
            summaries["632"] = summaries["511"] * 0.6m; // COGS
            summaries["641"] = summaries["511"] * 0.1m; // Operating expenses
            summaries["111"] = summaries["511"] * 0.2m; // Cash
            summaries["112"] = summaries["511"] * 0.3m; // Bank deposits

            return summaries;
        }

        /// <summary>
        /// Generate S2a-HKD tax report (VAT + PIT Percentage)
        /// </summary>
        private async Task<HKDTaxReport> GenerateS2aReportAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Generating S2a-HKD tax report for tenant {TenantId}, period {Period}",
                tenantId.Value, period.ToString("yyyy-MM"));

            ReportData reportData = await GenerateS2aReportDataAsync(tenantId, period, cancellationToken);
            ReportMetadata metadata = await GetReportMetadataAsync(AccountingBookType.S2a_HKD, cancellationToken);

            return new HKDTaxReport
            {
                BookType = AccountingBookType.S2a_HKD,
                TenantId = tenantId,
                Period = period,
                ReportType = "VATAndPIT",
                ReportTitle = "Báo cáo thuế GTGT 5% và TNCN theo % doanh thu - S2a-HKD",
                IsTaxExempt = false,
                VATAmount = reportData.VatAmount,
                PersonalIncomeTaxAmount = reportData.PersonalIncomeTaxAmount,
                VATRate = 0.05m,
                TotalRevenue = reportData.TotalRevenue,
                TotalExpenses = reportData.TotalExpense,
                NetIncome = reportData.NetAmount,
                CashInflows = reportData.TotalRevenue,
                CashOutflows = reportData.TotalExpense,
                NetCashFlow = reportData.NetAmount,
                MaterialCosts = 0m,
                SpecialTaxAmount = 0m,
                SpecialTaxCategories = [],
                TaxAuthorityReferences = ["Thông tư 152/2025/TT-BTC", "Điều 6 - Thuế GTGT 5%"],
                RevenueCategories = ["Doanh thu chịu thuế GTGT", "Doanh thu chịu thuế TNCN"],
                ProductCategories = ["Hàng hóa chịu thuế", "Dịch vụ chịu thuế"],
                RevenueBreakdown = GenerateRevenueBreakdown(reportData),
                ExpenseBreakdown = GenerateExpenseBreakdown(reportData),
                InventoryBreakdown = [],
                PaymentMethodBreakdown = GeneratePaymentMethodBreakdown(reportData),
                Data = reportData,
                Metadata = metadata,
                GeneratedDate = DateTime.UtcNow,
                GeneratedBy = "HKDTaxReportingService"
            };
        }

        /// <summary>
        /// Generate S2a-HKD report data (VAT + PIT Percentage)
        /// </summary>
        private static async Task<ReportData> GenerateS2aReportDataAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock data for S2a-HKD (VAT + PIT Percentage)
            List<TransactionRecord> transactions = [];
            List<TaxBreakdownRecord> taxBreakdowns = [];
            Dictionary<string, decimal> accountSummaries = [];

            // Generate mock revenue transactions
            decimal baseRevenue = 80000000m; // 80 million VND
            for (int i = 1; i <= period.Day; i++)
            {
                if (i % 5 == 0) // Every 5 days
                {
                    decimal dailyRevenue = baseRevenue / 30;
                    transactions.Add(new TransactionRecord
                    {
                        TransactionDate = new DateTime(period.Year, period.Month, i),
                        Description = "Doanh thu chịu thuế GTGT",
                        Amount = dailyRevenue,
                        AccountNumber = "5111",
                        TransactionType = "Revenue",
                        Reference = $"S2A-{i:D3}"
                    });
                    accountSummaries["5111"] = accountSummaries.GetValueOrDefault("5111") + dailyRevenue;
                }
            }

            // Generate mock expense transactions
            decimal baseExpense = 40000000m; // 40 million VND
            for (int i = 1; i <= period.Day; i++)
            {
                if (i % 4 == 0) // Every 4 days
                {
                    decimal dailyExpense = baseExpense / 30;
                    transactions.Add(new TransactionRecord
                    {
                        TransactionDate = new DateTime(period.Year, period.Month, i),
                        Description = "Chi phí chịu thuế",
                        Amount = dailyExpense,
                        AccountNumber = "6321",
                        TransactionType = "Expense",
                        Reference = $"S2A-EXP-{i:D3}"
                    });
                    accountSummaries["6321"] = accountSummaries.GetValueOrDefault("6321") + dailyExpense;
                }
            }

            decimal totalRevenue = accountSummaries.GetValueOrDefault("5111");
            decimal totalExpense = accountSummaries.GetValueOrDefault("6321");
            decimal vatAmount = totalRevenue * 0.05m; // 5% VAT
            decimal pitAmount = totalRevenue * 0.01m; // 1% PIT

            // Add tax breakdown records
            taxBreakdowns.Add(new TaxBreakdownRecord
            {
                TaxType = "VAT",
                TaxableAmount = totalRevenue,
                TaxRate = 0.05m,
                TaxAmount = vatAmount,
                Description = "Thuế GTGT 5%"
            });

            taxBreakdowns.Add(new TaxBreakdownRecord
            {
                TaxType = "PIT",
                TaxableAmount = totalRevenue,
                TaxRate = 0.01m,
                TaxAmount = pitAmount,
                Description = "Thuế TNCN 1% trên doanh thu"
            });

            return new ReportData
            {
                TotalRevenue = totalRevenue,
                TotalExpense = totalExpense,
                VatAmount = vatAmount,
                PersonalIncomeTaxAmount = pitAmount,
                SpecialTaxAmount = 0m,
                NetAmount = totalRevenue - totalExpense - vatAmount - pitAmount,
                Transactions = transactions,
                TaxBreakdowns = taxBreakdowns,
                AccountSummaries = accountSummaries
            };
        }

        /// <summary>
        /// Generate S2b-HKD tax report (Revenue Reports)
        /// </summary>
        private async Task<HKDTaxReport> GenerateS2bReportAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock implementation for S2b-HKD
            ReportData reportData = await GenerateS2bReportDataAsync(tenantId, period, cancellationToken);
            ReportMetadata metadata = await GetReportMetadataAsync(AccountingBookType.S2b_HKD, cancellationToken);

            return new HKDTaxReport
            {
                BookType = AccountingBookType.S2b_HKD,
                TenantId = tenantId,
                Period = period,
                ReportType = "RevenueStatement",
                ReportTitle = "Báo cáo doanh thu bán hàng hóa, dịch vụ - S2b-HKD",
                IsTaxExempt = false,
                VATAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                VATRate = 0m,
                TotalRevenue = reportData.TotalRevenue,
                TotalExpenses = reportData.TotalExpense,
                NetIncome = reportData.NetAmount,
                CashInflows = reportData.TotalRevenue,
                CashOutflows = reportData.TotalExpense,
                NetCashFlow = reportData.NetAmount,
                MaterialCosts = 0m,
                SpecialTaxAmount = 0m,
                SpecialTaxCategories = [],
                TaxAuthorityReferences = ["Thông tư 152/2025/TT-BTC"],
                RevenueCategories = ["Doanh thu hàng hóa", "Doanh thu dịch vụ"],
                ProductCategories = ["Sản phẩm", "Dịch vụ"],
                RevenueBreakdown = GenerateRevenueBreakdown(reportData),
                ExpenseBreakdown = GenerateExpenseBreakdown(reportData),
                InventoryBreakdown = [],
                PaymentMethodBreakdown = GeneratePaymentMethodBreakdown(reportData),
                Data = reportData,
                Metadata = metadata,
                GeneratedDate = DateTime.UtcNow,
                GeneratedBy = "HKDTaxReportingService"
            };
        }

        /// <summary>
        /// Generate S2b-HKD report data
        /// </summary>
        private async Task<ReportData> GenerateS2bReportDataAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock implementation
            List<TransactionRecord> transactions = [];
            Dictionary<string, decimal> accountSummaries = [];

            decimal baseRevenue = 100000000m; // 100 million VND
            for (int i = 1; i <= period.Day; i++)
            {
                if (i % 3 == 0)
                {
                    decimal dailyRevenue = baseRevenue / 30;
                    transactions.Add(new TransactionRecord
                    {
                        TransactionDate = new DateTime(period.Year, period.Month, i),
                        Description = "Doanh thu bán hàng",
                        Amount = dailyRevenue,
                        AccountNumber = "5111",
                        TransactionType = "Revenue",
                        Reference = $"S2B-{i:D3}"
                    });
                    accountSummaries["5111"] = accountSummaries.GetValueOrDefault("5111") + dailyRevenue;
                }
            }

            return new ReportData
            {
                TotalRevenue = accountSummaries.GetValueOrDefault("5111"),
                TotalExpense = 0m,
                VatAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                SpecialTaxAmount = 0m,
                NetAmount = accountSummaries.GetValueOrDefault("5111"),
                Transactions = transactions,
                TaxBreakdowns = [],
                AccountSummaries = accountSummaries
            };
        }

        /// <summary>
        /// Generate S2c-HKD tax report (Revenue/Expense Reports)
        /// </summary>
        private async Task<HKDTaxReport> GenerateS2cReportAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock implementation for S2c-HKD
            ReportData reportData = await GenerateS2cReportDataAsync(tenantId, period, cancellationToken);
            ReportMetadata metadata = await GetReportMetadataAsync(AccountingBookType.S2c_HKD, cancellationToken);

            return new HKDTaxReport
            {
                BookType = AccountingBookType.S2c_HKD,
                TenantId = tenantId,
                Period = period,
                ReportType = "IncomeStatement",
                ReportTitle = "Báo cáo chi tiết doanh thu, chi phí - S2c-HKD",
                IsTaxExempt = false,
                VATAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                VATRate = 0m,
                TotalRevenue = reportData.TotalRevenue,
                TotalExpenses = reportData.TotalExpense,
                NetIncome = reportData.NetAmount,
                CashInflows = reportData.TotalRevenue,
                CashOutflows = reportData.TotalExpense,
                NetCashFlow = reportData.NetAmount,
                MaterialCosts = 0m,
                SpecialTaxAmount = 0m,
                SpecialTaxCategories = [],
                TaxAuthorityReferences = ["Thông tư 152/2025/TT-BTC"],
                RevenueCategories = ["Doanh thu", "Chi phí"],
                ProductCategories = [],
                RevenueBreakdown = GenerateRevenueBreakdown(reportData),
                ExpenseBreakdown = GenerateExpenseBreakdown(reportData),
                InventoryBreakdown = [],
                PaymentMethodBreakdown = GeneratePaymentMethodBreakdown(reportData),
                Data = reportData,
                Metadata = metadata,
                GeneratedDate = DateTime.UtcNow,
                GeneratedBy = "HKDTaxReportingService"
            };
        }

        /// <summary>
        /// Generate S2c-HKD report data
        /// </summary>
        private async Task<ReportData> GenerateS2cReportDataAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock implementation
            return new ReportData
            {
                TotalRevenue = 120000000m,
                TotalExpense = 70000000m,
                VatAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                SpecialTaxAmount = 0m,
                NetAmount = 50000000m,
                Transactions = [],
                TaxBreakdowns = [],
                AccountSummaries = []
            };
        }

        /// <summary>
        /// Generate S2d-HKD tax report (Inventory Reports)
        /// </summary>
        private async Task<HKDTaxReport> GenerateS2dReportAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock implementation for S2d-HKD
            ReportData reportData = await GenerateS2dReportDataAsync(tenantId, period, cancellationToken);
            ReportMetadata metadata = await GetReportMetadataAsync(AccountingBookType.S2d_HKD, cancellationToken);

            return new HKDTaxReport
            {
                BookType = AccountingBookType.S2d_HKD,
                TenantId = tenantId,
                Period = period,
                ReportType = "InventoryStatement",
                ReportTitle = "Báo cáo chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa - S2d-HKD",
                IsTaxExempt = false,
                VATAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                VATRate = 0m,
                TotalRevenue = reportData.TotalRevenue,
                TotalExpenses = reportData.TotalExpense,
                NetIncome = reportData.NetAmount,
                CashInflows = reportData.TotalRevenue,
                CashOutflows = reportData.TotalExpense,
                NetCashFlow = reportData.NetAmount,
                MaterialCosts = 30000000m,
                SpecialTaxAmount = 0m,
                SpecialTaxCategories = [],
                TaxAuthorityReferences = ["Thông tư 152/2025/TT-BTC"],
                RevenueCategories = [],
                ProductCategories = ["Vật liệu", "Dụng cụ", "Sản phẩm", "Hàng hóa"],
                RevenueBreakdown = GenerateRevenueBreakdown(reportData),
                ExpenseBreakdown = GenerateExpenseBreakdown(reportData),
                InventoryBreakdown = [],
                PaymentMethodBreakdown = GeneratePaymentMethodBreakdown(reportData),
                Data = reportData,
                Metadata = metadata,
                GeneratedDate = DateTime.UtcNow,
                GeneratedBy = "HKDTaxReportingService"
            };
        }

        /// <summary>
        /// Generate S2d-HKD report data
        /// </summary>
        private async Task<ReportData> GenerateS2dReportDataAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock implementation
            return new ReportData
            {
                TotalRevenue = 0m,
                TotalExpense = 30000000m,
                VatAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                SpecialTaxAmount = 0m,
                NetAmount = -30000000m,
                Transactions = [],
                TaxBreakdowns = [],
                AccountSummaries = []
            };
        }

        /// <summary>
        /// Generate S2e-HKD tax report (Cash Flow Reports)
        /// </summary>
        private async Task<HKDTaxReport> GenerateS2eReportAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock implementation for S2e-HKD
            ReportData reportData = await GenerateS2eReportDataAsync(tenantId, period, cancellationToken);
            ReportMetadata metadata = await GetReportMetadataAsync(AccountingBookType.S2e_HKD, cancellationToken);

            return new HKDTaxReport
            {
                BookType = AccountingBookType.S2e_HKD,
                TenantId = tenantId,
                Period = period,
                ReportType = "CashFlowStatement",
                ReportTitle = "Báo cáo chi tiết tiền - S2e-HKD",
                IsTaxExempt = false,
                VATAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                VATRate = 0m,
                TotalRevenue = reportData.TotalRevenue,
                TotalExpenses = reportData.TotalExpense,
                NetIncome = reportData.NetAmount,
                CashInflows = 150000000m,
                CashOutflows = 80000000m,
                NetCashFlow = 70000000m,
                MaterialCosts = 0m,
                SpecialTaxAmount = 0m,
                SpecialTaxCategories = [],
                TaxAuthorityReferences = ["Thông tư 152/2025/TT-BTC"],
                RevenueCategories = [],
                ProductCategories = [],
                RevenueBreakdown = GenerateRevenueBreakdown(reportData),
                ExpenseBreakdown = GenerateExpenseBreakdown(reportData),
                InventoryBreakdown = [],
                PaymentMethodBreakdown = GeneratePaymentMethodBreakdown(reportData),
                Data = reportData,
                Metadata = metadata,
                GeneratedDate = DateTime.UtcNow,
                GeneratedBy = "HKDTaxReportingService"
            };
        }

        /// <summary>
        /// Generate S2e-HKD report data
        /// </summary>
        private async Task<ReportData> GenerateS2eReportDataAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock implementation
            return new ReportData
            {
                TotalRevenue = 150000000m,
                TotalExpense = 80000000m,
                VatAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                SpecialTaxAmount = 0m,
                NetAmount = 70000000m,
                Transactions = [],
                TaxBreakdowns = [],
                AccountSummaries = []
            };
        }

        /// <summary>
        /// Generate S3a-HKD tax report (Special Tax Reports)
        /// </summary>
        private async Task<HKDTaxReport> GenerateS3aReportAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock implementation for S3a-HKD
            ReportData reportData = await GenerateS3aReportDataAsync(tenantId, period, cancellationToken);
            ReportMetadata metadata = await GetReportMetadataAsync(AccountingBookType.S3a_HKD, cancellationToken);

            return new HKDTaxReport
            {
                BookType = AccountingBookType.S3a_HKD,
                TenantId = tenantId,
                Period = period,
                ReportType = "SpecialTaxDeclaration",
                ReportTitle = "Special Tax Report - Báo cáo thuế đặc biệt, thuế khác - S3a_HKD",
                IsTaxExempt = false,
                VATAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                VATRate = 0m,
                TotalRevenue = reportData.TotalRevenue,
                TotalExpenses = reportData.TotalExpense,
                NetIncome = reportData.NetAmount,
                CashInflows = reportData.TotalRevenue,
                CashOutflows = reportData.TotalExpense,
                NetCashFlow = reportData.NetAmount,
                MaterialCosts = 0m,
                SpecialTaxAmount = 5000000m,
                SpecialTaxCategories = ["Thuế môi trường", "Thuế tiêu thụ đặc biệt"],
                TaxAuthorityReferences = ["Thông tư 152/2025/TT-BTC", "Luật Thuế môi trường"],
                RevenueCategories = [],
                ProductCategories = [],
                RevenueBreakdown = GenerateRevenueBreakdown(reportData),
                ExpenseBreakdown = GenerateExpenseBreakdown(reportData),
                InventoryBreakdown = [],
                PaymentMethodBreakdown = GeneratePaymentMethodBreakdown(reportData),
                Data = reportData,
                Metadata = metadata,
                GeneratedDate = DateTime.UtcNow,
                GeneratedBy = "HKDTaxReportingService"
            };
        }

        /// <summary>
        /// Generate S3a-HKD report data
        /// </summary>
        private async Task<ReportData> GenerateS3aReportDataAsync(
            TenantId tenantId,
            DateTime period,
            CancellationToken cancellationToken = default)
        {
            // Mock implementation
            return new ReportData
            {
                TotalRevenue = 200000000m,
                TotalExpense = 100000000m,
                VatAmount = 0m,
                PersonalIncomeTaxAmount = 0m,
                SpecialTaxAmount = 5000000m,
                NetAmount = 95000000m,
                Transactions = [],
                TaxBreakdowns = [],
                AccountSummaries = []
            };
        }

        private async Task<ReportTemplate> GenerateTemplateAsync(
            AccountingBookType bookType,
            ReportFormat format,
            HKDGroup group,
            CancellationToken cancellationToken)
        {
            List<TaxReportField> fields = GetTemplateFieldsForBookType(bookType);
            List<TemplateFormula> formulas = GetTemplateFormulasForBookType(bookType);
            string templateContent = await GenerateTemplateContentAsync(bookType, format, cancellationToken);

            return new ReportTemplate
            {
                BookType = bookType,
                Format = format,
                TemplateName = $"{bookType}_Template_{format.FormatId}",
                TemplateContent = templateContent,
                Fields = fields,
                Formulas = formulas,
                DefaultValues = GetDefaultValuesForBookType(bookType)
            };
        }

        private List<TaxReportField> GetTemplateFieldsForBookType(AccountingBookType bookType)
        {
            return
            [
                new TaxReportField
                {
                    FieldName = "TenantName",
                    DisplayName = "Tên hộ kinh doanh",
                    DataType = "string",
                    IsRequired = true,
                    Format = "string",
                    ValidationRule = "not empty",
                    DefaultValue = ""
                },
                new TaxReportField
                {
                    FieldName = "TaxCode",
                    DisplayName = "Mã số thuế",
                    DataType = "string",
                    IsRequired = true,
                    Format = "string",
                    ValidationRule = "^[0-9-]{10,13}$",
                    DefaultValue = ""
                },
                new TaxReportField
                {
                    FieldName = "Period",
                    DisplayName = "Kỳ báo cáo",
                    DataType = "datetime",
                    IsRequired = true,
                    Format = "yyyy-MM",
                    ValidationRule = "not empty",
                    DefaultValue = DateTime.Now.ToString("yyyy-MM")
                },
                new TaxReportField
                {
                    FieldName = "TotalRevenue",
                    DisplayName = "Tổng doanh thu",
                    DataType = "decimal",
                    IsRequired = true,
                    Format = "#,##0.00",
                    ValidationRule = "> 0",
                    DefaultValue = 0
                }
            ];
        }

        private List<TemplateFormula> GetTemplateFormulasForBookType(AccountingBookType bookType)
        {
            return
            [
                new TemplateFormula
                {
                    FormulaName = "VATAmount",
                    Expression = "TotalRevenue * 0.05",
                    Description = "Thuế GTGT 5%",
                    Dependencies = ["TotalRevenue"]
                },
                new TemplateFormula
                {
                    FormulaName = "PersonalIncomeTax",
                    Expression = "TotalRevenue * 0.1",
                    Description = "Thuế TNCN 10%",
                    Dependencies = ["TotalRevenue"]
                }
            ];
        }

        private static Dictionary<string, object> GetDefaultValuesForBookType(AccountingBookType bookType)
        {
            return new Dictionary<string, object>
            {
                { "Currency", "VND" },
                { "Language", "vi-VN" },
                { "ReportVersion", "1.0" },
                { "ComplianceStandard", "TT152-2025" }
            };
        }

        private static async Task<string> GenerateTemplateContentAsync(
            AccountingBookType bookType,
            ReportFormat format,
            CancellationToken cancellationToken)
        {
            // In real implementation, would load actual template files
            // For MVP, generate basic template content
            string template = $"Template for {bookType} in {format.FormatId} format";
            return await Task.FromResult(template);
        }

        private static bool HasRequiredField(HKDTaxReport report, string fieldName)
        {
            return fieldName switch
            {
                "TotalRevenue" => report.Data.TotalRevenue >= 0,
                "TotalExpense" => report.Data.TotalExpense >= 0,
                "Period" => report.Period != default,
                "TenantId" => report.TenantId.Value != Guid.Empty,
                _ => false
            };
        }

        private static async Task<byte[]> ExportToPdfAsync(HKDTaxReport report, CancellationToken cancellationToken)
        {
            // In real implementation, would use PDF library like iTextSharp or PdfSharp
            // For MVP, return mock PDF content
            string content = $"PDF Report for {report.BookType} - {report.Period:yyyy-MM}";
            return await Task.FromResult(Encoding.UTF8.GetBytes(content));
        }

        private static async Task<byte[]> ExportToExcelAsync(HKDTaxReport report, CancellationToken cancellationToken)
        {
            // In real implementation, would use Excel library like EPPlus or ClosedXML
            // For MVP, return mock Excel content
            string content = $"Excel Report for {report.BookType} - {report.Period:yyyy-MM}";
            return await Task.FromResult(Encoding.UTF8.GetBytes(content));
        }

        private static async Task<byte[]> ExportToXmlAsync(HKDTaxReport report, CancellationToken cancellationToken)
        {
            string xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<TaxReport>
    <BookType>{report.BookType}</BookType>
    <TenantId>{report.TenantId}</TenantId>
    <Period>{report.Period:yyyy-MM-dd}</Period>
    <TotalRevenue>{report.Data.TotalRevenue}</TotalRevenue>
    <TotalExpense>{report.Data.TotalExpense}</TotalExpense>
    <VatAmount>{report.Data.VatAmount}</VatAmount>
    <PersonalIncomeTaxAmount>{report.Data.PersonalIncomeTaxAmount}</PersonalIncomeTaxAmount>
    <SpecialTaxAmount>{report.Data.SpecialTaxAmount}</SpecialTaxAmount>
</TaxReport>";

            return await Task.FromResult(Encoding.UTF8.GetBytes(xml));
        }

        private static async Task<byte[]> ExportToJsonAsync(HKDTaxReport report, CancellationToken cancellationToken)
        {
            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            return await Task.FromResult(Encoding.UTF8.GetBytes(json));
        }

        private static async Task<byte[]> ExportToCsvAsync(HKDTaxReport report, CancellationToken cancellationToken)
        {
            string csv = $"BookType,TenantId,Period,TotalRevenue,TotalExpense,VatAmount,PersonalIncomeTaxAmount,SpecialTaxAmount\n";
            csv += $"{report.BookType},{report.TenantId},{report.Period:yyyy-MM-dd},{report.Data.TotalRevenue},{report.Data.TotalExpense},{report.Data.VatAmount},{report.Data.PersonalIncomeTaxAmount},{report.Data.SpecialTaxAmount}";
            return await Task.FromResult(Encoding.UTF8.GetBytes(csv));
        }
    }
    #endregion
}
