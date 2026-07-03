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
                    FieldName = "VatAmount",
                    DisplayName = "Tiền thuế GTGT",
                    Type = FieldType.Decimal,
                    Formula = "TotalRevenue * 0.05"
                },
                new()
                {
                    FieldName = "PersonalIncomeTax",
                    DisplayName = "Thuế TNCN",
                    Type = FieldType.Decimal,
                    Formula = "VatAmount * 0.1"
                },
                new()
                {
                    FieldName = "NetRevenue",
                    DisplayName = "Doanh thu sau thuế",
                    Type = FieldType.Decimal,
                    Formula = "TotalRevenue - VatAmount - PersonalIncomeTax"
                }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ KẾ TOÁN S2a_HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal revenue))
            {
                report += $"Tổng doanh thu: {revenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("VatAmount", out decimal vat))
            {
                report += $"Thuế GTGT: {vat:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("PersonalIncomeTax", out decimal pit))
            {
                report += $"Thuế TNCN: {pit:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("NetRevenue", out decimal net))
            {
                report += $"Doanh thu sau thuế: {net:N0} VNĐ\n";
            }

            return await Task.FromResult(report);
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
            TemplateName = "Số doanh thu bán hàng hóa, dịch vụ";
            TargetGroup = HKDGroup.Group2;

            Fields =
            [
                new()
                {
                    FieldName = "Revenue",
                    DisplayName = "Doanh thu bán hàng hóa",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""511"", ""Credit"")"
                },
                new()
                {
                    FieldName = "ServiceRevenue",
                    DisplayName = "Doanh thu dịch vụ",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""521"", ""Credit"")"
                },
                new()
                {
                    FieldName = "TotalRevenue",
                    DisplayName = "Tổng doanh thu",
                    Type = FieldType.Decimal,
                    IsRequired = true,
                    Formula = @"Revenue + ServiceRevenue"
                }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỐ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ S2b-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";

            if (book.NumericValues.TryGetValue("Revenue", out decimal revenue))
            {
                report += $"Doanh thu bán hàng hóa: {revenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("ServiceRevenue", out decimal serviceRevenue))
            {
                report += $"Doanh thu dịch vụ: {serviceRevenue:N0} VNĐ\n";
            }

            if (book.NumericValues.TryGetValue("TotalRevenue", out decimal totalRevenue))
            {
                report += $"Tổng doanh thu: {totalRevenue:N0} VNĐ\n";
            }

            return report;
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
