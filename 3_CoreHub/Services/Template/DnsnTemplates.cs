using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Formula;
using VanAn.CoreHub.Services.Data;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services.Template
{
    /// <summary>
    /// TT 58/2026/TT-BTC — sổ kế toán DN siêu nhỏ (DNSN).
    /// Verified từ spec: docs/AI/tasks/tt133_tt58_compliance/spec/tt58-2026-full-text.txt
    ///   Điều 5: S1-DNSN (GTGT % + TNDN %)        → DnsnS1TemplateImpl
    ///   Điều 6: S2a+S2b+S2c+S2d-DNSN            → DnsnS2a/S2b/S2c/S2dTemplateImpl
    ///   Điều 7: S3a+S3b-DNSN                   → DnsnS3a/S3bTemplateImpl
    ///   Điều 8: S2b+S2c+S2d+S3b-DNSN           → tái dùng impl tương ứng
    ///   Điều 9 (tùy chọn): S4a-DNSN công nợ     → DnsnS4aTemplateImpl (S4b/S4c/S4d defer)
    /// Thuế GTGT % + TNDN % trên DT theo nhóm ngành — tái dùng tỷ lệ ND 117/2025
    /// (Distribution 1%/0.5% · ProductionTransport 3%/1.5% · Service 5%/2% · OtherBusiness 2%/1%).
    /// NULL IndustrySector → bucket OtherBusiness (giống TT152).
    /// </summary>

    /// <summary>S1-DNSN — sổ doanh thu (Điều 5: GTGT % + TNDN % trên DT).</summary>
    public record DnsnS1TemplateImpl : BaseHKDBookTemplate
    {
        public DnsnS1TemplateImpl(
            IFormulaEngine formulaEngine, IDataProvider dataProvider,
            ILogger<DnsnS1TemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S1_DNSN";
            TemplateName = "Sổ S1-DNSN — Sổ doanh thu (TT 58/2026 Điều 5)";

            Fields =
            [
                new() { FieldName = "Revenue_Distribution", DisplayName = "Doanh thu — Phân phối", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Distribution"")" },
                new() { FieldName = "Vat_Distribution", DisplayName = "Thuế GTGT — Phân phối", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.01" },
                new() { FieldName = "Tndn_Distribution", DisplayName = "Thuế TNDN — Phân phối", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.005" },
                new() { FieldName = "Revenue_ProductionTransport", DisplayName = "Doanh thu — Sản xuất, vận tải", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""ProductionTransport"")" },
                new() { FieldName = "Vat_ProductionTransport", DisplayName = "Thuế GTGT — Sản xuất, vận tải", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.03" },
                new() { FieldName = "Tndn_ProductionTransport", DisplayName = "Thuế TNDN — Sản xuất, vận tải", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.015" },
                new() { FieldName = "Revenue_Service", DisplayName = "Doanh thu — Dịch vụ", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Service"")" },
                new() { FieldName = "Vat_Service", DisplayName = "Thuế GTGT — Dịch vụ", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.05" },
                new() { FieldName = "Tndn_Service", DisplayName = "Thuế TNDN — Dịch vụ", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.02" },
                new() { FieldName = "Revenue_OtherBusiness", DisplayName = "Doanh thu — Hoạt động khác", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""OtherBusiness"")" },
                new() { FieldName = "Vat_OtherBusiness", DisplayName = "Thuế GTGT — Hoạt động khác", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.02" },
                new() { FieldName = "Tndn_OtherBusiness", DisplayName = "Thuế TNDN — Hoạt động khác", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.01" },
                new() { FieldName = "TotalRevenue", DisplayName = "Tổng doanh thu", Type = FieldType.Decimal, IsRequired = true, Formula = "Revenue_Distribution + Revenue_ProductionTransport + Revenue_Service + Revenue_OtherBusiness" },
                new() { FieldName = "TotalVat", DisplayName = "Tổng thuế GTGT phải nộp", Type = FieldType.Decimal, Formula = "Vat_Distribution + Vat_ProductionTransport + Vat_Service + Vat_OtherBusiness" },
                new() { FieldName = "TotalTndn", DisplayName = "Tổng thuế TNDN phải nộp", Type = FieldType.Decimal, Formula = "Tndn_Distribution + Tndn_ProductionTransport + Tndn_Service + Tndn_OtherBusiness" }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            string report = $"SỔ S1-DNSN (TT 58/2026 Điều 5) - {book.Period.Year}/{book.Period.Month:D2}\n";
            foreach (var sector in new[] { "Distribution", "ProductionTransport", "Service", "OtherBusiness" })
            {
                if (book.NumericValues.TryGetValue($"Revenue_{sector}", out decimal r))
                    report += $"{sector}: DT {r:N0} · GTGT {book.NumericValues.GetValueOrDefault($"Vat_{sector}"):N0} · TNDN {book.NumericValues.GetValueOrDefault($"Tndn_{sector}"):N0}\n";
            }
            report += $"TỔNG: DT {book.NumericValues.GetValueOrDefault("TotalRevenue"):N0} · GTGT {book.NumericValues.GetValueOrDefault("TotalVat"):N0} · TNDN {book.NumericValues.GetValueOrDefault("TotalTndn"):N0}\n";
            return await Task.FromResult(report);
        }
    }

    /// <summary>S2a-DNSN — sổ doanh thu (Điều 6: GTGT % + TNDN trên thu nhập — chỉ dòng GTGT).</summary>
    public record DnsnS2aTemplateImpl : BaseHKDBookTemplate
    {
        public DnsnS2aTemplateImpl(
            IFormulaEngine formulaEngine, IDataProvider dataProvider,
            ILogger<DnsnS2aTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S2a_DNSN";
            TemplateName = "Sổ S2a-DNSN — Sổ doanh thu (TT 58/2026 Điều 6)";

            Fields =
            [
                new() { FieldName = "Revenue_Distribution", DisplayName = "Doanh thu — Phân phối", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Distribution"")" },
                new() { FieldName = "Vat_Distribution", DisplayName = "Thuế GTGT — Phân phối", Type = FieldType.Decimal, Formula = "Revenue_Distribution * 0.01" },
                new() { FieldName = "Revenue_ProductionTransport", DisplayName = "Doanh thu — Sản xuất, vận tải", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""ProductionTransport"")" },
                new() { FieldName = "Vat_ProductionTransport", DisplayName = "Thuế GTGT — Sản xuất, vận tải", Type = FieldType.Decimal, Formula = "Revenue_ProductionTransport * 0.03" },
                new() { FieldName = "Revenue_Service", DisplayName = "Doanh thu — Dịch vụ", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Service"")" },
                new() { FieldName = "Vat_Service", DisplayName = "Thuế GTGT — Dịch vụ", Type = FieldType.Decimal, Formula = "Revenue_Service * 0.05" },
                new() { FieldName = "Revenue_OtherBusiness", DisplayName = "Doanh thu — Hoạt động khác", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""OtherBusiness"")" },
                new() { FieldName = "Vat_OtherBusiness", DisplayName = "Thuế GTGT — Hoạt động khác", Type = FieldType.Decimal, Formula = "Revenue_OtherBusiness * 0.02" },
                new() { FieldName = "TotalRevenue", DisplayName = "Tổng doanh thu", Type = FieldType.Decimal, IsRequired = true, Formula = "Revenue_Distribution + Revenue_ProductionTransport + Revenue_Service + Revenue_OtherBusiness" },
                new() { FieldName = "TotalVat", DisplayName = "Tổng thuế GTGT phải nộp", Type = FieldType.Decimal, Formula = "Vat_Distribution + Vat_ProductionTransport + Vat_Service + Vat_OtherBusiness" }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            return await Task.FromResult(
                $"SỔ S2a-DNSN (Điều 6) - {book.Period.Year}/{book.Period.Month:D2} · " +
                $"Tổng DT {book.NumericValues.GetValueOrDefault("TotalRevenue"):N0} · GTGT {book.NumericValues.GetValueOrDefault("TotalVat"):N0}\n");
        }
    }

    /// <summary>S2b-DNSN — sổ chi tiết doanh thu, chi phí (Điều 6 + Điều 8).</summary>
    public record DnsnS2bTemplateImpl : BaseHKDBookTemplate
    {
        public DnsnS2bTemplateImpl(
            IFormulaEngine formulaEngine, IDataProvider dataProvider,
            ILogger<DnsnS2bTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S2b_DNSN";
            TemplateName = "Sổ S2b-DNSN — Sổ chi tiết doanh thu, chi phí (TT 58/2026)";

            Fields =
            [
                new() { FieldName = "TotalRevenue", DisplayName = "Doanh thu, thu nhập", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT(""5"", ""Credit"") + SUM_ACCOUNT(""7"", ""Credit"")" },
                new() { FieldName = "CostOfGoodsSold", DisplayName = "Giá vốn hàng bán", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""632"", ""Debit"")" },
                new() { FieldName = "FinancialCost", DisplayName = "Chi phí tài chính (lãi vay)", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""635"", ""Debit"")" },
                new() { FieldName = "ManagementCost", DisplayName = "Chi phí quản lý kinh doanh", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""642"", ""Debit"")" },
                new() { FieldName = "OtherCost", DisplayName = "Chi phí khác", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""811"", ""Debit"")" },
                new() { FieldName = "TotalExpense", DisplayName = "Tổng chi phí", Type = FieldType.Decimal, IsRequired = true, Formula = "CostOfGoodsSold + FinancialCost + ManagementCost + OtherCost" },
                new() { FieldName = "NetProfit", DisplayName = "Lợi nhuận trong kỳ", Type = FieldType.Decimal, Formula = "TotalRevenue - TotalExpense" }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            return await Task.FromResult(
                $"SỔ S2b-DNSN - {book.Period.Year}/{book.Period.Month:D2} · " +
                $"DT {book.NumericValues.GetValueOrDefault("TotalRevenue"):N0} · " +
                $"CP {book.NumericValues.GetValueOrDefault("TotalExpense"):N0} · " +
                $"LN {book.NumericValues.GetValueOrDefault("NetProfit"):N0}\n");
        }
    }

    /// <summary>S2c-DNSN — sổ chi tiết VL, DC, SP, HH (Điều 6 + Điều 8). Giá trị (không ĐVT nếu thiếu).</summary>
    public record DnsnS2cTemplateImpl : BaseHKDBookTemplate
    {
        public DnsnS2cTemplateImpl(
            IFormulaEngine formulaEngine, IDataProvider dataProvider,
            ILogger<DnsnS2cTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S2c_DNSN";
            TemplateName = "Sổ S2c-DNSN — Sổ chi tiết VL, DC, SP, HH (TT 58/2026)";

            Fields =
            [
                new() { FieldName = "Inventory_In", DisplayName = "Giá trị nhập trong kỳ", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT(""15"", ""Debit"")" },
                new() { FieldName = "Inventory_Out", DisplayName = "Giá trị xuất trong kỳ", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT(""15"", ""Credit"")" },
                new() { FieldName = "Inventory_Closing", DisplayName = "Giá trị tồn cuối kỳ", Type = FieldType.Decimal, Formula = @"BALANCE_ACCOUNT(""15"", ""Debit"")" }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            return await Task.FromResult(
                $"SỔ S2c-DNSN - {book.Period.Year}/{book.Period.Month:D2} · " +
                $"Nhập {book.NumericValues.GetValueOrDefault("Inventory_In"):N0} · " +
                $"Xuất {book.NumericValues.GetValueOrDefault("Inventory_Out"):N0} · " +
                $"Tồn cuối {book.NumericValues.GetValueOrDefault("Inventory_Closing"):N0}\n");
        }
    }

    /// <summary>S2d-DNSN — sổ chi tiết tiền (Điều 6 + Điều 8). Tách tiền mặt vs TGNH.</summary>
    public record DnsnS2dTemplateImpl : BaseHKDBookTemplate
    {
        public DnsnS2dTemplateImpl(
            IFormulaEngine formulaEngine, IDataProvider dataProvider,
            ILogger<DnsnS2dTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S2d_DNSN";
            TemplateName = "Sổ S2d-DNSN — Sổ chi tiết tiền (TT 58/2026)";

            Fields =
            [
                new() { FieldName = "Cash_In", DisplayName = "Thu tiền mặt", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""111"", ""Debit"")" },
                new() { FieldName = "Cash_Out", DisplayName = "Chi tiền mặt", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""111"", ""Credit"")" },
                new() { FieldName = "Cash_Closing", DisplayName = "Tồn tiền mặt cuối kỳ", Type = FieldType.Decimal, Formula = @"BALANCE_ACCOUNT(""111"", ""Debit"")" },
                new() { FieldName = "Bank_In", DisplayName = "Thu tiền gửi không kỳ hạn", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""112"", ""Debit"")" },
                new() { FieldName = "Bank_Out", DisplayName = "Chi tiền gửi không kỳ hạn", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""112"", ""Credit"")" },
                new() { FieldName = "Bank_Closing", DisplayName = "Tồn TGNH cuối kỳ", Type = FieldType.Decimal, Formula = @"BALANCE_ACCOUNT(""112"", ""Debit"")" },
                new() { FieldName = "Total_In", DisplayName = "Tổng thu", Type = FieldType.Decimal, Formula = "Cash_In + Bank_In" },
                new() { FieldName = "Total_Out", DisplayName = "Tổng chi", Type = FieldType.Decimal, Formula = "Cash_Out + Bank_Out" },
                new() { FieldName = "Total_Closing", DisplayName = "Tổng tồn cuối kỳ", Type = FieldType.Decimal, Formula = "Cash_Closing + Bank_Closing" }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            return await Task.FromResult(
                $"SỔ S2d-DNSN - {book.Period.Year}/{book.Period.Month:D2} · " +
                $"Thu {book.NumericValues.GetValueOrDefault("Total_In"):N0} · " +
                $"Chi {book.NumericValues.GetValueOrDefault("Total_Out"):N0} · " +
                $"Tồn {book.NumericValues.GetValueOrDefault("Total_Closing"):N0}\n");
        }
    }

    /// <summary>S3a-DNSN — sổ doanh thu (Điều 7: GTGT khấu trừ — không dòng thuế).</summary>
    public record DnsnS3aTemplateImpl : BaseHKDBookTemplate
    {
        public DnsnS3aTemplateImpl(
            IFormulaEngine formulaEngine, IDataProvider dataProvider,
            ILogger<DnsnS3aTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S3a_DNSN";
            TemplateName = "Sổ S3a-DNSN — Sổ doanh thu (TT 58/2026 Điều 7)";

            Fields =
            [
                new() { FieldName = "Revenue_Distribution", DisplayName = "Doanh thu — Phân phối", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Distribution"")" },
                new() { FieldName = "Revenue_ProductionTransport", DisplayName = "Doanh thu — Sản xuất, vận tải", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""ProductionTransport"")" },
                new() { FieldName = "Revenue_Service", DisplayName = "Doanh thu — Dịch vụ", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""Service"")" },
                new() { FieldName = "Revenue_OtherBusiness", DisplayName = "Doanh thu — Hoạt động khác", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT_BY_INDUSTRY(""5"", ""Credit"", ""OtherBusiness"")" },
                new() { FieldName = "TotalRevenue", DisplayName = "Tổng doanh thu", Type = FieldType.Decimal, IsRequired = true, Formula = "Revenue_Distribution + Revenue_ProductionTransport + Revenue_Service + Revenue_OtherBusiness" }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            return await Task.FromResult(
                $"SỔ S3a-DNSN (Điều 7) - {book.Period.Year}/{book.Period.Month:D2} · " +
                $"Tổng DT {book.NumericValues.GetValueOrDefault("TotalRevenue"):N0}\n");
        }
    }

    /// <summary>S3b-DNSN — sổ theo dõi nghĩa vụ thuế GTGT (Điều 7 + Điều 8 — khấu trừ).</summary>
    public record DnsnS3bTemplateImpl : BaseHKDBookTemplate
    {
        public DnsnS3bTemplateImpl(
            IFormulaEngine formulaEngine, IDataProvider dataProvider,
            ILogger<DnsnS3bTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S3b_DNSN";
            TemplateName = "Sổ S3b-DNSN — Sổ theo dõi nghĩa vụ thuế GTGT (TT 58/2026)";

            Fields =
            [
                new() { FieldName = "VatInput", DisplayName = "Thuế GTGT đầu vào được khấu trừ", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT(""133"", ""Debit"")" },
                new() { FieldName = "VatOutput", DisplayName = "Thuế GTGT đầu ra", Type = FieldType.Decimal, IsRequired = true, Formula = @"SUM_ACCOUNT(""3331"", ""Credit"")" },
                new() { FieldName = "VatPayable", DisplayName = "Thuế GTGT phải nộp (đầu ra − đầu vào)", Type = FieldType.Decimal, Formula = "VatOutput - VatInput" }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            return await Task.FromResult(
                $"SỔ S3b-DNSN - {book.Period.Year}/{book.Period.Month:D2} · " +
                $"Đầu vào {book.NumericValues.GetValueOrDefault("VatInput"):N0} · " +
                $"Đầu ra {book.NumericValues.GetValueOrDefault("VatOutput"):N0} · " +
                $"Phải nộp {book.NumericValues.GetValueOrDefault("VatPayable"):N0}\n");
        }
    }

    /// <summary>S4a-DNSN — sổ chi tiết thanh toán công nợ (Điều 9 — tùy chọn, D2 đã chốt CÓ).</summary>
    public record DnsnS4aTemplateImpl : BaseHKDBookTemplate
    {
        public DnsnS4aTemplateImpl(
            IFormulaEngine formulaEngine, IDataProvider dataProvider,
            ILogger<DnsnS4aTemplateImpl> logger) : base(formulaEngine, dataProvider, logger)
        {
            TemplateCode = "S4a_DNSN";
            TemplateName = "Sổ S4a-DNSN — Sổ chi tiết thanh toán với người bán/người mua (TT 58/2026 Điều 9)";

            Fields =
            [
                new() { FieldName = "Receivable_In", DisplayName = "Phải thu phát sinh tăng", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""131"", ""Debit"")" },
                new() { FieldName = "Receivable_Out", DisplayName = "Phải thu phát sinh giảm", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""131"", ""Credit"")" },
                new() { FieldName = "Receivable_Closing", DisplayName = "Phải thu cuối kỳ", Type = FieldType.Decimal, Formula = @"BALANCE_ACCOUNT(""131"", ""Debit"")" },
                new() { FieldName = "OtherReceivable_Closing", DisplayName = "Phải thu khác cuối kỳ", Type = FieldType.Decimal, Formula = @"BALANCE_ACCOUNT(""138"", ""Debit"") + BALANCE_ACCOUNT(""141"", ""Debit"")" },
                new() { FieldName = "Payable_In", DisplayName = "Phải trả phát sinh tăng", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""331"", ""Credit"")" },
                new() { FieldName = "Payable_Out", DisplayName = "Phải trả phát sinh giảm", Type = FieldType.Decimal, Formula = @"SUM_ACCOUNT(""331"", ""Debit"")" },
                new() { FieldName = "Payable_Closing", DisplayName = "Phải trả cuối kỳ", Type = FieldType.Decimal, Formula = @"BALANCE_ACCOUNT(""331"", ""Credit"")" }
            ];
        }

        public override async Task<string> GenerateReportAsync(GenericHKDBook book)
        {
            return await Task.FromResult(
                $"SỔ S4a-DNSN - {book.Period.Year}/{book.Period.Month:D2} · " +
                $"Phải thu CK {book.NumericValues.GetValueOrDefault("Receivable_Closing"):N0} · " +
                $"Phải trả CK {book.NumericValues.GetValueOrDefault("Payable_Closing"):N0}\n");
        }
    }
}
