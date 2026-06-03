using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Formula;
using VanAn.CoreHub.Services.Data;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Template
{
    /// <summary>
    /// Template Factory - Creates template instances with proper dependency injection
    /// </summary>
    public class TemplateFactory
    {
        private readonly IFormulaEngine _formulaEngine;
        private readonly IDataProvider _dataProvider;
        private readonly ILoggerFactory _loggerFactory;
        
        public TemplateFactory(
            IFormulaEngine formulaEngine,
            IDataProvider dataProvider,
            ILoggerFactory loggerFactory)
        {
            _formulaEngine = formulaEngine;
            _dataProvider = dataProvider;
            _loggerFactory = loggerFactory;
        }
        
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
                HKDGroup.Group1 => new List<HKDBookTemplate> { CreateS1aTemplate() },
                HKDGroup.Group2 => new List<HKDBookTemplate> { 
                    CreateS2aTemplate(), 
                    CreateS2bTemplate(), 
                    CreateS2cTemplate(), 
                    CreateS2dTemplate(), 
                    CreateS2eTemplate() 
                },
                HKDGroup.Group3 => new List<HKDBookTemplate> { CreateS3aTemplate() },
                _ => new List<HKDBookTemplate>()
            };
        }
        
        private HKDBookTemplate CreateS1aTemplate()
        {
            var logger = _loggerFactory.CreateLogger<S1aHKDTemplateImpl>();
            return new S1aHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }
        
        private HKDBookTemplate CreateS2aTemplate()
        {
            var logger = _loggerFactory.CreateLogger<S2aHKDTemplateImpl>();
            return new S2aHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }
        
        private HKDBookTemplate CreateS2bTemplate()
        {
            var logger = _loggerFactory.CreateLogger<S2bHKDTemplateImpl>();
            return new S2bHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }
        
        private HKDBookTemplate CreateS2cTemplate()
        {
            var logger = _loggerFactory.CreateLogger<S2cHKDTemplateImpl>();
            return new S2cHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }
        
        private HKDBookTemplate CreateS2dTemplate()
        {
            var logger = _loggerFactory.CreateLogger<S2dHKDTemplateImpl>();
            return new S2dHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }
        
        private HKDBookTemplate CreateS2eTemplate()
        {
            var logger = _loggerFactory.CreateLogger<S2eHKDTemplateImpl>();
            return new S2eHKDTemplateImpl(_formulaEngine, _dataProvider, logger);
        }
        
        private HKDBookTemplate CreateS3aTemplate()
        {
            var logger = _loggerFactory.CreateLogger<S3aHKDTemplateImpl>();
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
            TemplateName = "Sá» ké toÃ¡n cho há» kinh doanh khÃ´ng chá»u thuÃ© GTGT";
            TargetGroup = HKDGroup.Group1;
            
            Fields = new List<TemplateField>
            {
                new() { 
                    FieldName = "TotalRevenue", 
                    DisplayName = "Tá»ng doanh thu", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new() { 
                    FieldName = "TotalExpense", 
                    DisplayName = "Tá»ng chi phÃ­", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""6"", ""Debit"")"
                },
                new() { 
                    FieldName = "NetProfit", 
                    DisplayName = "Lá»i nhuáºn", 
                    Type = FieldType.Decimal, 
                    Formula = "TotalRevenue - TotalExpense"
                }
            };
        }
        
        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            var report = $"Sá» Káº¿ TOÃN S1a-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Há» kinh doanh: {book.TenantId.Value}\n";
            
            if (book.NumericValues.TryGetValue("TotalRevenue", out var revenue))
                report += $"Tá»ng doanh thu: {revenue:N0} VNÄ\n";
            
            if (book.NumericValues.TryGetValue("TotalExpense", out var expense))
                report += $"Tá»ng chi phÃ­: {expense:N0} VNÄ\n";
            
            if (book.NumericValues.TryGetValue("NetProfit", out var profit))
                report += $"Lá»i nhuáºn: {profit:N0} VNÄ\n";
            
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
            TemplateName = "Sá» ké toÃ¡n cho há» kinh doanh ná»p thuÃ© GTGT vÃ  TNCN";
            TargetGroup = HKDGroup.Group2;
            
            Fields = new List<TemplateField>
            {
                new() { 
                    FieldName = "TotalRevenue", 
                    DisplayName = "Tá»ng doanh thu", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new() { 
                    FieldName = "VatAmount", 
                    DisplayName = "Tiá»n thuÃ© GTGT", 
                    Type = FieldType.Decimal, 
                    Formula = "TotalRevenue * 0.05"
                },
                new() { 
                    FieldName = "PersonalIncomeTax", 
                    DisplayName = "ThuÃ© TNCN", 
                    Type = FieldType.Decimal, 
                    Formula = "VatAmount * 0.1"
                },
                new() { 
                    FieldName = "NetRevenue", 
                    DisplayName = "Doanh thu sau thuÃ©", 
                    Type = FieldType.Decimal, 
                    Formula = "TotalRevenue - VatAmount - PersonalIncomeTax"
                }
            };
        }
        
        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            var report = $"Sá» Káº¿ TOÃN S2a-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Há» kinh doanh: {book.TenantId.Value}\n";
            
            if (book.NumericValues.TryGetValue("TotalRevenue", out var revenue))
                report += $"Tá»ng doanh thu: {revenue:N0} VNÄ\n";
            
            if (book.NumericValues.TryGetValue("VatAmount", out var vat))
                report += $"ThuÃ© GTGT: {vat:N0} VNÄ\n";
            
            if (book.NumericValues.TryGetValue("PersonalIncomeTax", out var pit))
                report += $"ThuÃ© TNCN: {pit:N0} VNÄ\n";
            
            if (book.NumericValues.TryGetValue("NetRevenue", out var net))
                report += $"Doanh thu sau thuÃ©: {net:N0} VNÄ\n";
            
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
            
            Fields = new List<TemplateField>
            {
                new() { 
                    FieldName = "Revenue", 
                    DisplayName = "Doanh thu bán hàng hóa", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""511"", ""Credit"")"
                },
                new() { 
                    FieldName = "ServiceRevenue", 
                    DisplayName = "Doanh thu dịch vụ", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""521"", ""Credit"")"
                },
                new() { 
                    FieldName = "TotalRevenue", 
                    DisplayName = "Tổng doanh thu", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"Revenue + ServiceRevenue"
                }
            };
        }
        
        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            var report = $"SỐ DOANH THU BÁN HÀNG HÓA, DỊCH VỤ S2b-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";
            
            if (book.NumericValues.TryGetValue("Revenue", out var revenue))
                report += $"Doanh thu bán hàng hóa: {revenue:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("ServiceRevenue", out var serviceRevenue))
                report += $"Doanh thu dịch vụ: {serviceRevenue:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("TotalRevenue", out var totalRevenue))
                report += $"Tổng doanh thu: {totalRevenue:N0} VNĐ\n";
            
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
            
            Fields = new List<TemplateField>
            {
                new() { 
                    FieldName = "Revenue", 
                    DisplayName = "Doanh thu", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new() { 
                    FieldName = "COGS", 
                    DisplayName = "Giá vốn hàng bán", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""632"", ""Debit"")"
                },
                new() { 
                    FieldName = "OperatingExpenses", 
                    DisplayName = "Chi phí hoạt động", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""641"", ""Debit"")"
                },
                new() { 
                    FieldName = "GrossProfit", 
                    DisplayName = "Lợi nhuận gộp", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"Revenue - COGS"
                },
                new() { 
                    FieldName = "NetProfit", 
                    DisplayName = "Lợi nhuận ròng", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"GrossProfit - OperatingExpenses"
                }
            };
        }
        
        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            var report = $"SỐ CHI TIẾT DOANH THU, CHI PHÍ S2c-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";
            
            if (book.NumericValues.TryGetValue("Revenue", out var revenue))
                report += $"Doanh thu: {revenue:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("COGS", out var cogs))
                report += $"Giá vốn hàng bán: {cogs:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("OperatingExpenses", out var expenses))
                report += $"Chi phí hoạt động: {expenses:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("GrossProfit", out var grossProfit))
                report += $"Lợi nhuận gộp: {grossProfit:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("NetProfit", out var netProfit))
                report += $"Lợi nhuận ròng: {netProfit:N0} VNĐ\n";
            
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
            
            Fields = new List<TemplateField>
            {
                new() { 
                    FieldName = "Materials", 
                    DisplayName = "Vật liệu", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""152"", ""Debit"")"
                },
                new() { 
                    FieldName = "Tools", 
                    DisplayName = "Dụng cụ", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""153"", ""Debit"")"
                },
                new() { 
                    FieldName = "Products", 
                    DisplayName = "Sản phẩm", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""155"", ""Debit"")"
                },
                new() { 
                    FieldName = "Goods", 
                    DisplayName = "Hàng hóa", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""156"", ""Debit"")"
                },
                new() { 
                    FieldName = "TotalInventory", 
                    DisplayName = "Tổng tồn kho", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"Materials + Tools + Products + Goods"
                }
            };
        }
        
        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            var report = $"SỐ CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA S2d-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";
            
            if (book.NumericValues.TryGetValue("Materials", out var materials))
                report += $"Vật liệu: {materials:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("Tools", out var tools))
                report += $"Dụng cụ: {tools:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("Products", out var products))
                report += $"Sản phẩm: {products:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("Goods", out var goods))
                report += $"Hàng hóa: {goods:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("TotalInventory", out var totalInventory))
                report += $"Tổng tồn kho: {totalInventory:N0} VNĐ\n";
            
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
            
            Fields = new List<TemplateField>
            {
                new() { 
                    FieldName = "Cash", 
                    DisplayName = "Tiền mặt", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""111"", ""Debit"")"
                },
                new() { 
                    FieldName = "BankDeposits", 
                    DisplayName = "Tiền gửi ngân hàng", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""112"", ""Debit"")"
                },
                new() { 
                    FieldName = "Receivables", 
                    DisplayName = "Phải thu khách hàng", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""131"", ""Debit"")"
                },
                new() { 
                    FieldName = "TotalCash", 
                    DisplayName = "Tổng tiền", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"Cash + BankDeposits"
                },
                new() { 
                    FieldName = "TotalAssets", 
                    DisplayName = "Tổng tài sản", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"Cash + BankDeposits + Receivables"
                }
            };
        }
        
        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            var report = $"SỐ CHI TIẾT TIỀN S2e-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Hộ kinh doanh: {book.TenantId.Value}\n";
            
            if (book.NumericValues.TryGetValue("Cash", out var cash))
                report += $"Tiền mặt: {cash:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("BankDeposits", out var bankDeposits))
                report += $"Tiền gửi ngân hàng: {bankDeposits:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("Receivables", out var receivables))
                report += $"Phải thu khách hàng: {receivables:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("TotalCash", out var totalCash))
                report += $"Tổng tiền: {totalCash:N0} VNĐ\n";
            
            if (book.NumericValues.TryGetValue("TotalAssets", out var totalAssets))
                report += $"Tổng tài sản: {totalAssets:N0} VNĐ\n";
            
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
            TemplateName = "Sá» cho há» kinh doanh cÃ³ hoáº¡t Äá»ng thuá»c diá»n chá»u cÃ¡c loáº¡i thuÃ­ khÃ¡c";
            TargetGroup = HKDGroup.Group3;
            
            Fields = new List<TemplateField>
            {
                new() { 
                    FieldName = "Revenue", 
                    DisplayName = "Doanh thu", 
                    Type = FieldType.Decimal, 
                    IsRequired = true,
                    Formula = @"SUM_ACCOUNT(""5"", ""Credit"")"
                },
                new() { 
                    FieldName = "SpecialTax", 
                    DisplayName = "ThuÃ© Äáº·c biá»t", 
                    Type = FieldType.Decimal, 
                    Formula = "Revenue * 0.1"
                },
                new() { 
                    FieldName = "OtherTax", 
                    DisplayName = "ThuÃ© khÃ¡c", 
                    Type = FieldType.Decimal, 
                    Formula = "Revenue * 0.05"
                },
                new() { 
                    FieldName = "NetRevenue", 
                    DisplayName = "Doanh thu sau thuÃ©", 
                    Type = FieldType.Decimal, 
                    Formula = "Revenue - SpecialTax - OtherTax"
                }
            };
        }
        
        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            var report = $"Sá» THUáº KHÃC S3a-HKD - {book.Period.Year}/{book.Period.Month:D2}\n";
            report += $"Há» kinh doanh: {book.TenantId.Value}\n";
            
            if (book.NumericValues.TryGetValue("Revenue", out var revenue))
                report += $"Doanh thu: {revenue:N0} VNÄ\n";
            
            if (book.NumericValues.TryGetValue("SpecialTax", out var special))
                report += $"ThuÃ© Äáº·c biá»t: {special:N0} VNÄ\n";
            
            if (book.NumericValues.TryGetValue("OtherTax", out var other))
                report += $"ThuÃ© khÃ¡c: {other:N0} VNÄ\n";
            
            if (book.NumericValues.TryGetValue("NetRevenue", out var net))
                report += $"Doanh thu sau thuÃ©: {net:N0} VNÄ\n";
            
            return await Task.FromResult(report);
        }
    }
}
