using VanAn.Shared.Domain;
using CoreAccountingEntry = VanAn.Shared.Domain.AccountingEntry;
using Microsoft.Extensions.Logging;

namespace VanAn.CoreHub.Services;

/// <summary>
/// HKD Tax Classification Service Implementation - Phase 2.3.9
/// Implements Vietnamese Accounting Standard (Thông tư 152/2025/TT-BTC)
/// Tax classification for 7 HKD book types
/// </summary>
public class HKDTaxClassificationService : IHKDTaxClassificationService
{
    private readonly ILogger<HKDTaxClassificationService> _logger;
    
    public HKDTaxClassificationService(ILogger<HKDTaxClassificationService> logger)
    {
        _logger = logger;
    }
    
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
        
        var group = GetHKDGroupForBookType(bookType);
        var classification = await GetTaxClassificationAsync(group, bookType);
        
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
        var group = GetHKDGroupForBookType(bookType);
        var rates = new List<VatRate>();
        
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
        }
        
        return await Task.FromResult(rates);
    }
    
    /// <summary>
    /// Calculate tax obligations for HKD book type
    /// </summary>
    public async Task<HKDTaxCalculation> CalculateTaxAsync(
        TenantId tenantId,
        AccountingBookType bookType,
        decimal revenueAmount,
        decimal expenseAmount = 0,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating tax for tenant {TenantId}, book type {BookType}, revenue {Revenue}", 
            tenantId.Value, bookType, revenueAmount);
        
        var group = GetHKDGroupForBookType(bookType);
        var classification = await GetTaxClassificationAsync(group, bookType);
        
        var vatAmount = 0m;
        var personalIncomeTaxAmount = 0m;
        var specialTaxAmount = 0m;
        var taxBreakdowns = new List<TaxBreakdown>();
        
        // VAT calculation
        if (classification.RequiresVatDeclaration)
        {
            vatAmount = revenueAmount * (classification.TaxRate / 100);
            taxBreakdowns.Add(new TaxBreakdown
            {
                TaxType = "VAT",
                TaxableAmount = revenueAmount,
                TaxRate = classification.TaxRate,
                TaxAmount = vatAmount,
                Description = "Thuế GTGT theo Thông tư 152/2025/TT-BTC"
            });
        }
        
        // Personal Income Tax calculation
        if (classification.RequiresPersonalIncomeTax)
        {
            personalIncomeTaxAmount = revenueAmount * 0.1m; // 10% TNCN
            taxBreakdowns.Add(new TaxBreakdown
            {
                TaxType = "TNCN",
                TaxableAmount = revenueAmount,
                TaxRate = 10m,
                TaxAmount = personalIncomeTaxAmount,
                Description = "Thuế TNCN theo Thông tư 152/2025/TT-BTC"
            });
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
        
        var totalTaxAmount = vatAmount + personalIncomeTaxAmount + specialTaxAmount;
        var netAmount = revenueAmount - totalTaxAmount;
        
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
        
        var issues = new List<ComplianceIssue>();
        var warnings = new List<ComplianceWarning>();
        
        var group = GetHKDGroupForBookType(bookType);
        var classification = await GetTaxClassificationAsync(group, bookType);
        
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
        var group = GetHKDGroupForBookType(bookType);
        var requirements = new List<ReportingRequirement>();
        var requiredDocuments = new List<string>();
        var reportFormats = new List<string>();
        
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
                requiredDocuments.AddRange(new[] { "Sổ S1a-HKD", "Hóa đơn đầu ra", "Hóa đơn đầu vào" });
                reportFormats.AddRange(new[] { "Excel", "PDF", "XML" });
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
                    Deadline = new DateTime(DateTime.Now.Year, ((DateTime.Now.Month - 1) / 3 + 1) * 3, 30),
                    IsMandatory = true
                });
                requiredDocuments.AddRange(new[] { 
                    "Sổ S2a-HKD", "Sổ S2b-HKD", "Sổ S2c-HKD", "Sổ S2d-HKD", "Sổ S2e-HKD",
                    "Hóa đơn đầu ra", "Hóa đơn đầu vào", "Phiếu thu", "Phiếu chi"
                });
                reportFormats.AddRange(new[] { "Excel", "PDF", "XML", "JSON" });
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
                requiredDocuments.AddRange(new[] { "Sổ S3a-HKD", "Hóa đơn", "Giấy phép kinh doanh" });
                reportFormats.AddRange(new[] { "Excel", "PDF" });
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
    
    private HKDGroup GetHKDGroupForBookType(AccountingBookType bookType)
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
                ApplicableTaxes = new List<string> { "Không" }
            },
            HKDGroup.Group2 => new TaxClassificationData
            {
                VatRate = VatRate.Five,
                RequiresVatDeclaration = true,
                RequiresPersonalIncomeTax = true,
                RequiresSpecialTax = false,
                TaxRate = 5m,
                TaxClassification = "Nộp thuế GTGT và TNCN theo tỷ lệ %",
                ApplicableTaxes = new List<string> { "GTGT", "TNCN" }
            },
            HKDGroup.Group3 => new TaxClassificationData
            {
                VatRate = VatRate.Zero,
                RequiresVatDeclaration = false,
                RequiresPersonalIncomeTax = false,
                RequiresSpecialTax = true,
                TaxRate = 0m,
                TaxClassification = "Chịu các loại thuế khác",
                ApplicableTaxes = new List<string> { "Thuế đặc biệt", "Thuế tiêu thụ đặc biệt" }
            },
            _ => throw new ArgumentException($"Unknown HKD group: {group}")
        });
    }
    
    #endregion
}

/// <summary>
/// Internal tax classification data
/// </summary>
internal record TaxClassificationData
{
    public VatRate VatRate { get; init; }
    public bool RequiresVatDeclaration { get; init; }
    public bool RequiresPersonalIncomeTax { get; init; }
    public bool RequiresSpecialTax { get; init; }
    public decimal TaxRate { get; init; }
    public string TaxClassification { get; init; }
    public List<string> ApplicableTaxes { get; init; } = new();
}
