using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Formula;
using VanAn.CoreHub.Services.Data;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Template
{
    /// <summary>
    /// Template Factory - Creates template instances with proper dependency injection
    /// </summary>
    public class TemplateFactory(
        IFormulaEngine formulaEngine,
        IDataProvider dataProvider,
        ILoggerFactory loggerFactory)
    {
        private readonly IFormulaEngine _formulaEngine = formulaEngine;
        private readonly IDataProvider _dataProvider = dataProvider;
        private readonly ILoggerFactory _loggerFactory = loggerFactory;

        /// <summary>
        /// Create template instance with dependencies
        /// </summary>
        public HKDBookTemplate CreateTemplate(HKDGroup group, string templateCode)
        {
            return group switch
            {
                HKDGroup.Group1 => CreateS1aTemplate(),
                HKDGroup.Group2 => CreateGroup2Template(templateCode),
                HKDGroup.Group3 => CreateS3aTemplate(),
                _ => throw new ArgumentException($"Unsupported HKD group: {group}")
            };
        }

        /// <summary>
        /// Get all templates for a group
        /// </summary>
        public List<HKDBookTemplate> GetTemplatesForGroup(HKDGroup group)
        {
            return group switch
            {
                HKDGroup.Group1 => [CreateS1aTemplate()],
                HKDGroup.Group2 => [
                    CreateS2aTemplate(),
                    CreateS2bTemplate(),
                    CreateS2cTemplate(),
                    CreateS2dTemplate(),
                    CreateS2eTemplate()
                ],
                HKDGroup.Group3 => [CreateS3aTemplate()],
                _ => []
            };
        }

        private HKDBookTemplate CreateS1aTemplate()
        {
            ILogger<S1aHKDTemplateImpl> logger = _loggerFactory.CreateLogger<S1aHKDTemplateImpl>();
            return new S1aHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }

        private S2aHKDTemplateImpl CreateS2aTemplate()
        {
            ILogger<S2aHKDTemplateImpl> logger = _loggerFactory.CreateLogger<S2aHKDTemplateImpl>();
            return new S2aHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }

        private S2bHKDTemplateImpl CreateS2bTemplate()
        {
            ILogger<S2bHKDTemplateImpl> logger = _loggerFactory.CreateLogger<S2bHKDTemplateImpl>();
            return new S2bHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }

        private S2cHKDTemplateImpl CreateS2cTemplate()
        {
            ILogger<S2cHKDTemplateImpl> logger = _loggerFactory.CreateLogger<S2cHKDTemplateImpl>();
            return new S2cHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }

        private S2dHKDTemplateImpl CreateS2dTemplate()
        {
            ILogger<S2dHKDTemplateImpl> logger = _loggerFactory.CreateLogger<S2dHKDTemplateImpl>();
            return new S2dHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }

        private S2eHKDTemplateImpl CreateS2eTemplate()
        {
            ILogger<S2eHKDTemplateImpl> logger = _loggerFactory.CreateLogger<S2eHKDTemplateImpl>();
            return new S2eHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }

        private S3aHKDTemplateImpl CreateS3aTemplate()
        {
            ILogger<S3aHKDTemplateImpl> logger = _loggerFactory.CreateLogger<S3aHKDTemplateImpl>();
            return new S3aHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }

        private HKDBookTemplate CreateGroup2Template(string templateCode)
        {
            return templateCode switch
            {
                "S2a_HKD" => CreateS2aTemplate(),
                "S2b_HKD" => CreateS2bTemplate(),
                "S2c_HKD" => CreateS2cTemplate(),
                "S2d_HKD" => CreateS2dTemplate(),
                "S2e_HKD" => CreateS2eTemplate(),
                _ => throw new ArgumentException($"Unknown template code: {templateCode}")
            };
        }
    }

    /// <summary>
    /// Implementation classes for templates with proper dependency injection
    /// </summary>

    public record S1aHKDTemplateImpl : BaseHKDBookTemplate
    {
        public S1aHKDTemplateImpl(
            IFormulaEngine formulaEngine,
            IDataProvider dataProvider,
            ILogger<S1aHKDTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
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

    public record S2aHKDTemplateImpl : BaseHKDBookTemplate
    {
        public S2aHKDTemplateImpl(
            IFormulaEngine formulaEngine,
            IDataProvider dataProvider,
            ILogger<S2aHKDTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S2a_HKD";
            TemplateName = "Sổ kế toán cho hộ kinh doanh nộp thuế GTGT và TNCN";
            TargetGroup = HKDGroup.Group2;

            // Wave 5 (TT 152/2025/TT-BTC): 4 industry groups × 3 fields (Revenue, VatAmount, PIT per group).
            // Tax rates per Luật Thuế GTGT/TNCN sửa đổi 2025 + ND 117/2025:
            //   Distribution:        GTGT 1% (0.01),  TNCN 0.5% (0.005)
            //   ProductionTransport: GTGT 3% (0.03),  TNCN 1.5% (0.015)
            //   Service:             GTGT 5% (0.05),  TNCN 2%   (0.02)
            //   OtherBusiness:       GTGT 2% (0.02),  TNCN 1%   (0.01)
            // NULL IndustrySector entries are counted in the OtherBusiness bucket (ensures TotalRevenue = SUM(all sectors)).
            //
            // Wave 5c (2026-07-03): 2026 Regulatory Compliance Fix.
            //   Per-sector PIT fields below are the "TNCN theo doanh thu" display (Revenue × sectorRate)
            //   for Nhóm 2 informational purposes. The OFFICIAL TotalPIT is computed in CalculateAsync
            //   override using HKDRevenueClassification.CalculateTNCN per 2026 law:
            //     Nhóm 1 (≤1B):       GTGT = 0, TNCN = 0 (exemption)
            //     Nhóm 2 (>1B-≤3B):   TNCN = (TotalRevenue - 1B) × blendedIndustryRate
            //     Nhóm 3 (>3B-≤50B):  TNCN = (TotalRevenue - TotalExpense) × 17%
            //     Nhóm 4 (>50B):      TNCN = (TotalRevenue - TotalExpense) × 20%
            //   2026: thuế khoán BÃI BỎ (NQ 198/2025/QH15), lệ phí môn bài BÃI BỎ (Điều 10 NQ 198/2025/QH15).
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
                new() { FieldName = "PIT_OtherBusiness", DisplayName = "Thuế TNCN — Hoạt động khác", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.01" },
                // ── Totals ──
                new() { FieldName = "TotalRevenue", DisplayName = "Tổng doanh thu", Type = FieldType.Decimal, IsRequired = true, Formula = "Revenue_Distribution + Revenue_ProductionTransport + Revenue_Service + Revenue_OtherBusiness" },
                // Wave 5c: TotalExpense for Nhóm 3/4 profit-based TNCN formula (chi phí = account class 6 Debit)
                new() { FieldName = "TotalExpense", DisplayName = "Tổng chi phí", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""6*"", ""Debit"")" },
                new() { FieldName = "TotalVat", DisplayName = "Tổng thuế GTGT", Type = FieldType.Decimal, Formula = "VatAmount_Distribution + VatAmount_ProductionTransport + VatAmount_Service + VatAmount_OtherBusiness" },
                // Wave 5c: TotalPIT formula below is the flat per-sector sum (Nhóm 2 display).
                //   The OFFICIAL 2026 TotalPIT is overridden in CalculateAsync per HKDRevenueClassification.CalculateTNCN.
                new() { FieldName = "TotalPIT", DisplayName = "Tổng thuế TNCN", Type = FieldType.Decimal, Formula = "PIT_Distribution + PIT_ProductionTransport + PIT_Service + PIT_OtherBusiness" },
                new() { FieldName = "NetRevenue", DisplayName = "Doanh thu sau thuế", Type = FieldType.Decimal, Formula = "TotalRevenue - TotalVat - TotalPIT" }
            ];
        }

        /// <summary>
        /// Wave 5c (2026-07-03): Override CalculateAsync to compute official 2026 Nhóm-aware TotalPIT.
        /// Base CalculateAsync evaluates per-sector fields (Revenue, VatAmount, PIT) + flat TotalPIT.
        /// This override then replaces TotalPIT (and TotalVat for Nhóm 1) with the correct 2026 formula
        /// per HKDRevenueClassification.CalculateTNCN / CalculateGTGT.
        /// </summary>
        public override async Task CalculateAsync(GenericHKDBook book)
        {
            // 1. Base calculation: evaluates all field formulas (per-sector Revenue/VatAmount/PIT, TotalRevenue, TotalExpense, TotalVat, TotalPIT, NetRevenue)
            await base.CalculateAsync(book);

            // 2. Read computed totals
            if (!book.NumericValues.TryGetValue("TotalRevenue", out decimal totalRevenue))
            {
                Logger.LogWarning("S2a CalculateAsync override: TotalRevenue missing, skipping 2026 TNCN recalculation");
                return;
            }

            book.NumericValues.TryGetValue("TotalExpense", out decimal totalExpense);
            book.NumericValues.TryGetValue("TotalVat", out decimal baseTotalVat);

            // 3. Determine 2026 revenue group (Nhóm 1-4) from total revenue
            HKDRevenueGroup group = HKDRevenueClassification.CalculateGroup(totalRevenue);

            // 4. Compute blended PIT rate for Nhóm 2 (weighted by per-sector revenue share)
            //    Per-sector rates per ND 117/2025: Distribution 0.5%, ProductionTransport 1.5%, Service 2%, OtherBusiness 1%
            decimal blendedPitRate = ComputeBlendedPitRate(book, totalRevenue);

            // 5. Compute official 2026 TotalPIT via Domain static method
            decimal officialTotalPit = HKDRevenueClassification.CalculateTNCN(group, totalRevenue, totalExpense, blendedPitRate);
            book.NumericValues["TotalPIT"] = officialTotalPit;

            // 6. Nhóm 1 (≤1B): GTGT exemption — zero out TotalVat
            if (group == HKDRevenueGroup.Group1)
            {
                book.NumericValues["TotalVat"] = 0m;
            }

            // 7. Warn if Nhóm 3/4 and no expense data recorded (PIT would be overstated)
            if ((group == HKDRevenueGroup.Group3 || group == HKDRevenueGroup.Group4) && totalExpense == 0)
            {
                Logger.LogWarning(
                    "S2a 2026 TNCN: Nhóm {Group} (revenue {Revenue:N0}₫) but TotalExpense = 0. " +
                    "TNCN = (Revenue - 0) × {Rate}% = {Pit:N0}₫ (OVERSTATED — no chi phí recorded). " +
                    "Record expense entries via RecordExpenseAsync for correct profit-based TNCN.",
                    group, totalRevenue, group == HKDRevenueGroup.Group3 ? 17 : 20, officialTotalPit);
            }

            // 8. Recalculate NetRevenue with official 2026 totals
            decimal officialTotalVat = book.NumericValues.TryGetValue("TotalVat", out decimal vat) ? vat : 0m;
            book.NumericValues["NetRevenue"] = totalRevenue - officialTotalVat - officialTotalPit;

            Logger.LogInformation(
                "S2a 2026 TNCN override: Nhóm {Group}, TotalRevenue={Revenue:N0}₫, TotalExpense={Expense:N0}₫, " +
                "TotalVat={Vat:N0}₫, TotalPIT={Pit:N0}₫ (blendedPitRate={Rate:P4})",
                group, totalRevenue, totalExpense, officialTotalVat, officialTotalPit, blendedPitRate);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Compute blended PIT rate (weighted average of per-sector rates by revenue share).
        /// Used for Nhóm 2 TNCN formula: (TotalRevenue - 1B) × blendedPitRate.
        /// Per-sector rates per ND 117/2025: Distribution 0.5%, ProductionTransport 1.5%, Service 2%, OtherBusiness 1%.
        /// </summary>
        private static decimal ComputeBlendedPitRate(GenericHKDBook book, decimal totalRevenue)
        {
            if (totalRevenue == 0) return 0m;

            decimal weightedSum = 0m;
            if (book.NumericValues.TryGetValue("Revenue_Distribution", out decimal revDist))
                weightedSum += revDist * 0.005m;
            if (book.NumericValues.TryGetValue("Revenue_ProductionTransport", out decimal revProd))
                weightedSum += revProd * 0.015m;
            if (book.NumericValues.TryGetValue("Revenue_Service", out decimal revSvc))
                weightedSum += revSvc * 0.02m;
            if (book.NumericValues.TryGetValue("Revenue_OtherBusiness", out decimal revOther))
                weightedSum += revOther * 0.01m;

            return weightedSum / totalRevenue;
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ KẾ TOÁN S2a_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            // Wave 5c: Display 2026 revenue group (Nhóm) for transparency
            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal totalRevenueForGroup))
            {
                HKDRevenueGroup group = HKDRevenueClassification.CalculateGroup(totalRevenueForGroup);
                report += $"Nhóm doanh thu 2026: {group} (per Luật GTGT/TNCN sửa đổi 2025 + ND 117/2025 + NQ 198/2025/QH15)\n";
            }

            report += "--- Phân phối (GTGT 1%, TNCN 0.5%) ---\n";
            report += ReportSectorLine(book, "Distribution");
            report += "--- Sản xuất, vận tải (GTGT 3%, TNCN 1.5%) ---\n";
            report += ReportSectorLine(book, "ProductionTransport");
            report += "--- Dịch vụ (GTGT 5%, TNCN 2%) ---\n";
            report += ReportSectorLine(book, "Service");
            report += "--- Hoạt động khác (GTGT 2%, TNCN 1%) ---\n";
            report += ReportSectorLine(book, "OtherBusiness");

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal totalRevenue))
            {
                report += $"Tổng doanh thu: {totalRevenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalExpense", out decimal totalExpense))
            {
                report += $"Tổng chi phí: {totalExpense:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalVat", out decimal totalVat))
            {
                report += $"Tổng thuế GTGT: {totalVat:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalPIT", out decimal totalPit))
            {
                report += $"Tổng thuế TNCN (2026): {totalPit:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("NetRevenue", out decimal net))
            {
                report += $"Doanh thu sau thuế: {net:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
        }

        private static string ReportSectorLine(GenericHKDBook book, string sector)
        {
            string line = "";
            if (book.NumericValues.TryGetValue($"Revenue_{sector}", out decimal revenue))
            {
                line += $"  Doanh thu: {revenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue($"VatAmount_{sector}", out decimal vat))
            {
                line += $"  Thuế GTGT: {vat:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue($"PIT_{sector}", out decimal pit))
            {
                line += $"  Thuế TNCN: {pit:N0} VNĐ\n";
            }

            return line;
        }
    }

    public record S2bHKDTemplateImpl : BaseHKDBookTemplate
    {
        public S2bHKDTemplateImpl(
            IFormulaEngine formulaEngine,
            IDataProvider dataProvider,
            ILogger<S2bHKDTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S2b_HKD";
            TemplateName = "Sổ doanh thu bán hàng hóa, dịch vụ";
            TargetGroup = HKDGroup.Group2;

            // Wave 5 (TT 152/2025/TT-BTC): 4 industry groups × 2 fields (Revenue, VatAmount per group).
            // Split by industry sector (NOT goods-vs-service — that was a TT 200 hallucination).
            // VAT rates per Luật Thuế GTGT sửa đổi 2025 + ND 117/2025:
            //   Distribution: 1%, ProductionTransport: 3%, Service: 5%, OtherBusiness: 2%
            // NULL IndustrySector entries are counted in the OtherBusiness bucket.
            Fields =
            [
                // ── Distribution (GTGT 1%) ──
                new() { FieldName = "Revenue_Distribution", DisplayName = "Doanh thu — Phân phối", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Distribution"")" },
                new() { FieldName = "VatAmount_Distribution", DisplayName = "Thuế GTGT — Phân phối", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.01" },
                // ── ProductionTransport (GTGT 3%) ──
                new() { FieldName = "Revenue_ProductionTransport", DisplayName = "Doanh thu — Sản xuất, vận tải", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""ProductionTransport"")" },
                new() { FieldName = "VatAmount_ProductionTransport", DisplayName = "Thuế GTGT — Sản xuất, vận tải", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.03" },
                // ── Service (GTGT 5%) ──
                new() { FieldName = "Revenue_Service", DisplayName = "Doanh thu — Dịch vụ", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Service"")" },
                new() { FieldName = "VatAmount_Service", DisplayName = "Thuế GTGT — Dịch vụ", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.05" },
                // ── OtherBusiness (GTGT 2%) — includes NULL IndustrySector entries ──
                new() { FieldName = "Revenue_OtherBusiness", DisplayName = "Doanh thu — Hoạt động khác", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""OtherBusiness"")" },
                new() { FieldName = "VatAmount_OtherBusiness", DisplayName = "Thuế GTGT — Hoạt động khác", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.02" },
                // ── Totals ──
                new() { FieldName = "TotalRevenue", DisplayName = "Tổng doanh thu", Type = FieldType.Decimal, IsRequired = true, Formula = "Revenue_Distribution + Revenue_ProductionTransport + Revenue_Service + Revenue_OtherBusiness" },
                new() { FieldName = "TotalVat", DisplayName = "Tổng thuế GTGT", Type = FieldType.Decimal, Formula = "VatAmount_Distribution + VatAmount_ProductionTransport + VatAmount_Service + VatAmount_OtherBusiness" }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ S2b-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";
            report += ReportS2bSectorLine(book, "Distribution", "Phân phối");
            report += ReportS2bSectorLine(book, "ProductionTransport", "Sản xuất, vận tải");
            report += ReportS2bSectorLine(book, "Service", "Dịch vụ");
            report += ReportS2bSectorLine(book, "OtherBusiness", "Hoạt động khác");

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal totalRevenue))
            {
                report += $"Tổng doanh thu: {totalRevenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalVat", out decimal totalVat))
            {
                report += $"Tổng thuế GTGT: {totalVat:N0} VNĐ\n";
            }

            return report;
        }

        private static string ReportS2bSectorLine(GenericHKDBook book, string sector, string label)
        {
            string line = "";
            if (book.NumericValues.TryGetValue($"Revenue_{sector}", out decimal revenue))
            {
                line += $"Doanh thu — {label}: {revenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue($"VatAmount_{sector}", out decimal vat))
            {
                line += $"Thuế GTGT — {label}: {vat:N0} VNĐ\n";
            }

            return line;
        }
    }

    public record S2cHKDTemplateImpl : BaseHKDBookTemplate
    {
        public S2cHKDTemplateImpl(
            IFormulaEngine formulaEngine,
            IDataProvider dataProvider,
            ILogger<S2cHKDTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S2c_HKD";
            TemplateName = "Số chi tiết doanh thu, chi phí";
            TargetGroup = HKDGroup.Group2;

            Fields =
            [
                new()
                {
                    FieldName = "Revenue",
                    DisplayName = "Doanh thu",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new()
                {
                    FieldName = "COGS",
                    DisplayName = "Giá vốn hàng bán",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""632"", ""Debit"")"
                },
                new()
                {
                    FieldName = "OperatingExpenses",
                    DisplayName = "Chi phí hoạt động",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""641"", ""Debit"")"
                },
                new()
                {
                    FieldName = "GrossProfit",
                    DisplayName = "Lợi nhuận gộp",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"Revenue - COGS"
                },
                new()
                {
                    FieldName = "NetProfit",
                    DisplayName = "Lợi nhuận ròng",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"GrossProfit - OperatingExpenses"
                }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỐ CHI TIẾT DOANH THU, CHI PHÍ S2c-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Revenue", out decimal revenue))
            {
                report += $"Doanh thu: {revenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("COGS", out decimal cogs))
            {
                report += $"Giá vốn hàng bán: {cogs:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("OperatingExpenses", out decimal expenses))
            {
                report += $"Chi phí hoạt động: {expenses:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("GrossProfit", out decimal grossProfit))
            {
                report += $"Lợi nhuận gộp: {grossProfit:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("NetProfit", out decimal netProfit))
            {
                report += $"Lợi nhuận ròng: {netProfit:N0} VNĐ\n";
            }

            return report;
        }
    }

    public record S2dHKDTemplateImpl : BaseHKDBookTemplate
    {
        public S2dHKDTemplateImpl(
            IFormulaEngine formulaEngine,
            IDataProvider dataProvider,
            ILogger<S2dHKDTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S2d_HKD";
            TemplateName = "Số chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa";
            TargetGroup = HKDGroup.Group2;

            Fields =
            [
                new()
                {
                    FieldName = "Materials",
                    DisplayName = "Vật liệu",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""152"", ""Debit"")"
                },
                new()
                {
                    FieldName = "Tools",
                    DisplayName = "Dụng cụ",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""153"", ""Debit"")"
                },
                new()
                {
                    FieldName = "Products",
                    DisplayName = "Sản phẩm",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""155"", ""Debit"")"
                },
                new()
                {
                    FieldName = "Goods",
                    DisplayName = "Hàng hóa",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""156"", ""Debit"")"
                },
                new()
                {
                    FieldName = "TotalInventory",
                    DisplayName = "Tổng tồn kho",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"Materials + Tools + Products + Goods"
                }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỐ CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA S2d-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Materials", out decimal materials))
            {
                report += $"Vật liệu: {materials:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("Tools", out decimal tools))
            {
                report += $"Dụng cụ: {tools:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("Products", out decimal products))
            {
                report += $"Sản phẩm: {products:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("Goods", out decimal goods))
            {
                report += $"Hàng hóa: {goods:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalInventory", out decimal totalInventory))
            {
                report += $"Tổng tồn kho: {totalInventory:N0} VNĐ\n";
            }

            return report;
        }
    }

    public record S2eHKDTemplateImpl : BaseHKDBookTemplate
    {
        public S2eHKDTemplateImpl(
            IFormulaEngine formulaEngine,
            IDataProvider dataProvider,
            ILogger<S2eHKDTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S2e_HKD";
            TemplateName = "Số chi tiết tiền";
            TargetGroup = HKDGroup.Group2;

            Fields =
            [
                new()
                {
                    FieldName = "Cash",
                    DisplayName = "Tiền mặt",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""111"", ""Debit"")"
                },
                new()
                {
                    FieldName = "BankDeposits",
                    DisplayName = "Tiền gửi ngân hàng",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""112"", ""Debit"")"
                },
                new()
                {
                    FieldName = "Receivables",
                    DisplayName = "Phải thu khách hàng",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""131"", ""Debit"")"
                },
                new()
                {
                    FieldName = "TotalCash",
                    DisplayName = "Tổng tiền",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"Cash + BankDeposits"
                },
                new()
                {
                    FieldName = "TotalAssets",
                    DisplayName = "Tổng tài sản",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"Cash + BankDeposits + Receivables"
                }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỐ CHI TIẾT TIỀN S2e-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Cash", out decimal cash))
            {
                report += $"Tiền mặt: {cash:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("BankDeposits", out decimal bankDeposits))
            {
                report += $"Tiền gửi ngân hàng: {bankDeposits:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("Receivables", out decimal receivables))
            {
                report += $"Phải thu khách hàng: {receivables:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalCash", out decimal totalCash))
            {
                report += $"Tổng tiền: {totalCash:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalAssets", out decimal totalAssets))
            {
                report += $"Tổng tài sản: {totalAssets:N0} VNĐ\n";
            }

            return report;
        }
    }

    public record S3aHKDTemplateImpl : BaseHKDBookTemplate
    {
        public S3aHKDTemplateImpl(
            IFormulaEngine formulaEngine,
            IDataProvider dataProvider,
            ILogger<S3aHKDTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S3a_HKD";
            TemplateName = "Sổ cho hộ kinh doanh có hoạt động thuộc diện chịu các loại thuế khác";
            TargetGroup = HKDGroup.Group3;

            Fields =
            [
                new()
                {
                    FieldName = "Revenue",
                    DisplayName = "Doanh thu",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new()
                {
                    FieldName = "SpecialTax",
                    DisplayName = "Thuế đặc biệt",
                    Type = FieldType.Decimal,
                    Formula = "Revenue * 0.1"
                },
                new()
                {
                    FieldName = "OtherTax",
                    DisplayName = "Thuế khác",
                    Type = FieldType.Decimal,
                    Formula = "Revenue * 0.05"
                },
                new()
                {
                    FieldName = "NetRevenue",
                    DisplayName = "Doanh thu sau thuế",
                    Type = FieldType.Decimal,
                    Formula = "Revenue - SpecialTax - OtherTax"
                }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ THUẾ KHÁC S3a_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Revenue", out decimal revenue))
            {
                report += $"Doanh thu: {revenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("SpecialTax", out decimal special))
            {
                report += $"Thuế đặc biệt: {special:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("OtherTax", out decimal other))
            {
                report += $"Thuế khác: {other:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("NetRevenue", out decimal net))
            {
                report += $"Doanh thu sau thuế: {net:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
        }
    }
}
