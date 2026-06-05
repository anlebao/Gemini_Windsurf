# PHASE 2.3.10: Compliance Validation - Vietnamese Standards Compliance

**Ngày tạo:** 2026-05-02  
**Phiên bản:** 1.0  
**Trạng thái:** URGENT - NEW STANDARDS EFFECTIVE 01/01/2026  
**File Reference:** `C:\VibeCoding\Gemini_Windsurf\docs\plan_MVP\Accounting\MVP plan Account M.md` (Phase 2.3.10)

---

## 🚨 CRITICAL UPDATE: THÔNG TƯ 99/2025/TT-BTC

### **Timeline & Impact**
- **Effective Date:** 01/01/2026 (áp dụng cho năm tài chính bắt đầu từ hoặc sau ngày 01/01/2026)
- **Time Remaining:** 6 tháng để implement
- **Replaces:** Thông tư 200/2014/TT-BTC, Thông tư 75/2015/TT-BTC, Thông tư 53/2016/TT-BTC
- **MVP Impact:** CẦN HOÀN THÀNH TRƯỚC MVP RELEASE ĐỂ ĐẢM BẢO COMPLIANCE

---

## 📊 REVERSE IMPACT ANALYSIS

### **CURRENT CODEBASE IMPACT ASSESSMENT**

#### **🔍 Files Requiring Updates (HIGH PRIORITY)**

##### **1. ProductionFormulaEngine.cs**
```csharp
// c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Formula\ProductionFormulaEngine.cs
// IMPACT: Contains hardcoded account numbers that will be deprecated

CURRENT ISSUES:
- Line 731: var accountNumber = ae.EntryType == AccountingEntryType.Revenue ? "511" : "611";
  ⚠️ TK 611 sẽ bị BỎ theo TT99 - cần chuyển sang TK 632
- Multiple SUM_ACCOUNT formulas using deprecated accounts
- Need to add support for new accounts: 82111, 82112
```

##### **2. HKDBookService.cs**
```csharp
// c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\HKDBookService.cs
// IMPACT: Account number validation and processing logic

CURRENT ISSUES:
- Line 731: Hardcoded "611" account for expenses
- Account validation logic needs update for new account ranges
- Balance calculations need to account for removed accounts
```

##### **3. Template System Files**
```csharp
// c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Template\
// IMPACT: All HKD book templates need account number updates

FILES AFFECTED:
- BaseHKDBookTemplate.cs
- HKDBookGenerationService.cs
- TemplateFactory.cs
- All specific template implementations (S1a, S2a, S3a, etc.)
```

##### **4. Validation Services**
```csharp
// c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\
// IMPACT: Account number validation rules

FILES AFFECTED:
- TemplateValidator.cs (IsValidAccountNumber method)
- JournalService.cs (account validation)
- JournalEntryService.cs (account validation)
- EnhancedJournalFactory.cs (account processing)
```

#### **🔍 Files Requiring Updates (MEDIUM PRIORITY)**

##### **5. Data Provider Services**
```csharp
// c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Data\
// IMPACT: Account pattern matching and aggregation

FILES AFFECTED:
- IDataProvider.cs (account pattern handling)
- SmartPreAggregationService.cs (account pattern matching)
```

##### **6. Business Rule Registry**
```csharp
// c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\
// IMPACT: Compliance rules need update

FILES AFFECTED:
- BusinessRuleRegistry.cs (new compliance rules)
- All business rule implementations
```

---

## 🎯 DETAILED CODING PLAN

### **PHASE 1: EMERGENCY ACCOUNT NUMBER MIGRATION (Week 1-2)**

#### **DAY 1-2: Update ProductionFormulaEngine**
```csharp
// FILE: c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Formula\ProductionFormulaEngine.cs

// TASK 1: Replace deprecated account 611 with 632
// BEFORE:
var accountNumber = ae.EntryType == AccountingEntryType.Revenue ? "511" : "611";

// AFTER:
var accountNumber = ae.EntryType == AccountingEntryType.Revenue ? "511" : "632";

// TASK 2: Add support for new accounts 82111, 82112
// NEW METHOD:
private decimal EvaluateNewTaxAccount(string formula, FormulaContext context)
{
    // Handle TK 82111: Chi phí thuế TNDN hiện hành
    if (formula.Contains("82111"))
    {
        return CalculateCurrentIncomeTaxExpense(context);
    }
    
    // Handle TK 82112: Chi phí thuế TNDN bổ sung (thuế tối thiểu toàn cầu)
    if (formula.Contains("82112"))
    {
        return CalculateAdditionalIncomeTaxExpense(context);
    }
    
    return 0;
}

// TASK 3: Update account validation for new ranges
private bool IsValidAccountNumberTT99(string accountNumber)
{
    // TT99 allows more flexible account structures
    // Support new account ranges: 82111, 82112
    return accountNumber.Length >= 3 && 
           accountNumber.Length <= 10 && 
           (accountNumber.All(char.IsDigit) || accountNumber.StartsWith("8211"));
}
```

#### **DAY 3-4: Update HKDBookService**
```csharp
// FILE: c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\HKDBookService.cs

// TASK 1: Replace deprecated account references
// BEFORE:
var accountNumber = ae.EntryType == AccountingEntryType.Revenue ? "511" : "611";

// AFTER:
var accountNumber = ae.EntryType == AccountingEntryType.Revenue ? "511" : "632";

// TASK 2: Update account mapping logic
private string MapAccountNumberTT99(string oldAccountNumber)
{
    return oldAccountNumber switch
    {
        "1562" => "632",  // Chi phí thu mua → Giá vốn hàng bán
        "611" => "632",   // Mua hàng → Giá vốn hàng bán
        _ => oldAccountNumber
    };
}

// TASK 3: Add new account processing
private decimal ProcessNewTaxAccounts(string accountNumber, decimal amount)
{
    return accountNumber switch
    {
        "82111" => ProcessCurrentIncomeTaxExpense(amount),
        "82112" => ProcessAdditionalIncomeTaxExpense(amount),
        _ => amount
    };
}
```

#### **DAY 5-7: Update Validation Services**
```csharp
// FILE: c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Template\TemplateValidator.cs

// TASK 1: Update IsValidAccountNumber for TT99
private bool IsValidAccountNumber(string accountNumber)
{
    // TT99: Support new account structures
    if (accountNumber.StartsWith("8211")) // New tax accounts
        return accountNumber.Length == 5 && int.TryParse(accountNumber, out _);
    
    // Existing validation for other accounts
    return accountNumber.Length >= 3 && 
           accountNumber.Length <= 10 && 
           int.TryParse(accountNumber, out _);
}

// FILE: c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Journal\JournalService.cs

// TASK 2: Update account validation
public Task<JournalEntry> AddLineAsync(JournalEntry entry, string accountNumber, decimal debitAmount, decimal creditAmount, string? description = null)
{
    // TT99: Enhanced validation
    if (string.IsNullOrEmpty(accountNumber) || accountNumber.Length < 3)
        throw new ValidationException("Account number must be at least 3 digits");
    
    // New validation for tax accounts
    if (accountNumber.StartsWith("8211") && accountNumber.Length != 5)
        throw new ValidationException("Tax account numbers must be 5 digits (82111 or 82112)");
    
    if (!IsValidAccountNumberTT99(accountNumber))
        throw new ValidationException("Invalid account number format");
    
    // Rest of existing logic...
}
```

### **PHASE 2: TEMPLATE SYSTEM UPDATE (Week 3-4)**

#### **DAY 8-10: Update BaseHKDBookTemplate**
```csharp
// FILE: c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Template\BaseHKDBookTemplate.cs

// TASK 1: Add TT99 account mapping
protected virtual string MapAccountNumberTT99(string accountNumber)
{
    // TT99 Account Mapping Table
    var accountMap = new Dictionary<string, string>
    {
        {"1562", "632"},  // Chi phí thu mua → Giá vốn hàng bán
        {"611", "632"},   // Mua hàng → Giá vốn hàng bán
        {"8211", "82111"}, // Tax accounts - default to current
        {"8212", "82112"}  // Tax accounts - additional
    };
    
    return accountMap.TryGetValue(accountNumber, out var mapped) ? mapped : accountNumber;
}

// TASK 2: Update template validation for TT99
protected ValidationResult ValidateTemplateTT99(HKDBookTemplate template, TemplateParameters parameters)
{
    var errors = new List<string>();
    
    // Validate account numbers against TT99
    foreach (var line in template.Lines)
    {
        if (!IsValidAccountNumberTT99(line.AccountNumber))
        {
            errors.Add($"Invalid TT99 account number: {line.AccountNumber}");
        }
    }
    
    // Validate formulas don't use deprecated accounts
    foreach (var line in template.Lines)
    {
        if (line.AmountFormula?.Contains("1562") == true || 
            line.AmountFormula?.Contains("611") == true)
        {
            errors.Add($"Formula uses deprecated account: {line.AmountFormula}");
        }
    }
    
    return errors.Any() ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success;
}
```

#### **DAY 11-14: Update Specific Templates**
```csharp
// FILE: c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Template\HKDBookGenerationService.cs

// TASK 1: Update template generation for TT99
public async Task<HKDBook> GenerateBookAsync(TenantId tenantId, AccountingPeriod period, string templateCode)
{
    var template = await _templateFactory.GetTemplateAsync(templateCode, tenantId);
    
    // TT99: Validate template compliance
    var validationResult = ValidateTemplateTT99(template, new TemplateParameters(tenantId, period));
    if (!validationResult.IsValid)
    {
        throw new InvalidOperationException($"Template not TT99 compliant: {validationResult.ErrorMessage}");
    }
    
    // Map account numbers to TT99
    var mappedTemplate = await MapTemplateAccountsTT99(template);
    
    // Rest of existing generation logic...
}

// TASK 2: Add TT99 compliance checking
private async Task<HKDBookTemplate> MapTemplateAccountsTT99(HKDBookTemplate originalTemplate)
{
    var mappedTemplate = originalTemplate.Clone();
    
    foreach (var line in mappedTemplate.Lines)
    {
        line.AccountNumber = MapAccountNumberTT99(line.AccountNumber);
        
        // Update formulas to use new account numbers
        if (line.AmountFormula != null)
        {
            line.AmountFormula = ReplaceAccountNumbersInFormula(line.AmountFormula);
        }
    }
    
    return mappedTemplate;
}

private string ReplaceAccountNumbersInFormula(string formula)
{
    return formula
        .Replace("1562", "632")
        .Replace("611", "632")
        .Replace("\"1562\"", "\"632\"")
        .Replace("\"611\"", "\"632\"");
}
```

### **PHASE 3: BUSINESS RULES & COMPLIANCE (Week 5-6)**

#### **DAY 15-18: Update BusinessRuleRegistry**
```csharp
// FILE: c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\BusinessRuleRegistry.cs

// TASK 1: Add TT99 compliance rules
public class TT99ComplianceRules : IBusinessRuleRegistry
{
    public ValidationResult ValidateAccountingEntry(AccountingEntry entry)
    {
        var errors = new List<string>();
        
        // Rule 1: Check for deprecated accounts
        foreach (var line in entry.Lines)
        {
            if (line.AccountNumber == "1562" || line.AccountNumber == "611")
            {
                errors.Add($"Deprecated account number used: {line.AccountNumber}. Use 632 instead.");
            }
        }
        
        // Rule 2: Validate new tax accounts usage
        var taxAccounts = entry.Lines.Where(l => l.AccountNumber.StartsWith("8211")).ToList();
        if (taxAccounts.Any())
        {
            foreach (var taxAccount in taxAccounts)
            {
                if (taxAccount.AccountNumber != "82111" && taxAccount.AccountNumber != "82112")
                {
                    errors.Add($"Invalid tax account number: {taxAccount.AccountNumber}");
                }
            }
        }
        
        // Rule 3: Check tax calculation compliance
        if (taxAccounts.Any())
        {
            ValidateTaxCalculationCompliance(entry, errors);
        }
        
        return errors.Any() ? ValidationResult.Failure(errors.ToArray()) : ValidationResult.Success;
    }
    
    private void ValidateTaxCalculationCompliance(AccountingEntry entry, List<string> errors)
    {
        // TT99: Tax calculation rules
        var currentTaxExpense = entry.Lines.FirstOrDefault(l => l.AccountNumber == "82111");
        var additionalTaxExpense = entry.Lines.FirstOrDefault(l => l.AccountNumber == "82112");
        
        if (currentTaxExpense != null && additionalTaxExpense != null)
        {
            // Validate minimum tax calculation (15% global minimum tax)
            var totalTax = currentTaxExpense.DebitAmount + additionalTaxExpense.DebitAmount;
            var revenue = entry.Lines.Where(l => l.AccountNumber.StartsWith("5")).Sum(l => l.CreditAmount);
            
            if (totalTax < revenue * 0.15m)
            {
                errors.Add("Tax expense below global minimum tax rate (15%)");
            }
        }
    }
}
```

#### **DAY 19-21: Update Data Provider Services**
```csharp
// FILE: c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Data\SmartPreAggregationService.cs

// TASK 1: Update account pattern matching for TT99
public async Task<decimal> GetAccountSum(DataProviderContext context, string accountPattern, string side)
{
    // TT99: Handle new account patterns
    if (accountPattern.StartsWith("8211"))
    {
        // Handle tax account patterns
        return await GetTaxAccountSum(context, accountPattern, side);
    }
    
    // Handle deprecated account patterns
    if (accountPattern == "1562" || accountPattern == "611")
    {
        // Map to new account 632
        return await GetAccountSum(context, "632", side);
    }
    
    // Existing logic for other patterns...
}

private async Task<decimal> GetTaxAccountSum(DataProviderContext context, string accountPattern, string side)
{
    var query = _context.AccountingEntries
        .Where(e => e.TenantId.Equals(context.TenantId) &&
                   e.Period.Year == context.Period.Year &&
                   e.Period.Month == context.Period.Month &&
                   e.Lines.Any(l => l.AccountNumber.StartsWith(accountPattern)));
    
    return await query
        .SelectMany(e => e.Lines)
        .Where(l => l.AccountNumber.StartsWith(accountPattern))
        .SumAsync(l => side.Equals("Credit", StringComparison.OrdinalIgnoreCase) ? l.CreditAmount : l.DebitAmount);
}
```

### **PHASE 4: TESTING & VALIDATION (Week 7-8)**

#### **DAY 22-25: Create TT99 Compliance Tests**
```csharp
// FILE: c:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\Compliance\TT99ComplianceTests.cs

public class TT99ComplianceTests : IntegrationTestBase
{
    [Fact]
    public async Task AccountingEntry_Should_Reject_Deprecated_Accounts()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var entry = TestEntityBuilder.CreateAccountingEntry(new TenantId(tenantId));
        
        // Add line with deprecated account 1562
        entry.AddLine("1562", 1000, 0, "Deprecated purchase cost");
        
        var businessRules = ServiceProvider.GetRequiredService<IBusinessRuleRegistry>();
        
        // Act
        var result = businessRules.ValidateAccountingEntry(entry);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Deprecated account number used: 1562", result.ErrorMessage);
    }
    
    [Fact]
    public async Task FormulaEngine_Should_Support_New_Tax_Accounts()
    {
        // Arrange
        var formulaEngine = ServiceProvider.GetRequiredService<IFormulaEngine>();
        var context = new FormulaContext(new TenantId(Guid.NewGuid()), AccountingPeriod.Create(2026, 1))
            .WithVariable("Revenue", 1000000);
        
        // Act
        var result = await formulaEngine.EvaluateAsync("82111", context);
        
        // Assert
        Assert.True(result > 0); // Should calculate current tax expense
    }
    
    [Fact]
    public async Task TemplateGeneration_Should_Map_Deprecated_Accounts()
    {
        // Arrange
        var templateService = ServiceProvider.GetRequiredService<HKDBookGenerationService>();
        var tenantId = new TenantId(Guid.NewGuid());
        var period = AccountingPeriod.Create(2026, 1);
        
        // Act
        var book = await templateService.GenerateBookAsync(tenantId, period, "S1a-HKD");
        
        // Assert
        Assert.NotNull(book);
        // Verify no deprecated accounts in generated book
        Assert.DoesNotContain(book.ToString(), "1562");
        Assert.DoesNotContain(book.ToString(), "611");
    }
}
```

#### **DAY 26-28: Performance & Integration Testing**
```csharp
// FILE: c:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Performance.Tests\TT99PerformanceTests.cs

public class TT99PerformanceTests : PerformanceTestBase
{
    [Fact]
    public async Task AccountMigration_Should_Not_Impact_Performance()
    {
        // Arrange
        var formulaEngine = ServiceProvider.GetRequiredService<IFormulaEngine>();
        var context = new FormulaContext(new TenantId(Guid.NewGuid()), AccountingPeriod.Create(2026, 1));
        
        // Act
        var stopwatch = Stopwatch.StartNew();
        
        // Test with new account numbers
        var results = new List<decimal>();
        for (int i = 0; i < 1000; i++)
        {
            results.Add(await formulaEngine.EvaluateAsync("SUM_ACCOUNT(\"8211\", \"Debit\")", context));
        }
        
        stopwatch.Stop();
        
        // Assert
        Assert.True(stopwatch.ElapsedMilliseconds < 5000, "Account migration should not impact performance");
        Assert.Equal(1000, results.Count);
    }
}
```

---

## 📋 IMPLEMENTATION CHECKLIST

### **HIGH PRIORITY (Must complete before MVP)**
- [ ] Update ProductionFormulaEngine with TT99 account mapping
- [ ] Replace deprecated account 611 with 632 in HKDBookService
- [ ] Update all validation services for new account ranges
- [ ] Add support for new tax accounts 82111, 82112
- [ ] Update BaseHKDBookTemplate with TT99 compliance
- [ ] Create TT99ComplianceRules in BusinessRuleRegistry
- [ ] Update DataProvider services for new account patterns
- [ ] Create comprehensive TT99 compliance tests

### **MEDIUM PRIORITY (Can complete after MVP)**
- [ ] Implement template account mapping system
- [ ] Add TT99 validation to all HKD book templates
- [ ] Update formula engine for complex tax calculations
- [ ] Create performance benchmarks for TT99 compliance
- [ ] Add audit trail for account number migrations

### **LOW PRIORITY (Enhancement)**
- [ ] Add UI for TT99 compliance checking
- [ ] Create migration tools for existing data
- [ ] Implement automatic account number suggestions
- [ ] Add TT99 compliance reporting dashboard

---

## 🔄 FILE PATH REFERENCES

### **Core Implementation Files:**
- **Formula Engine:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Formula\ProductionFormulaEngine.cs`
- **HKD Book Service:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\HKDBookService.cs`
- **Template Base:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Template\BaseHKDBookTemplate.cs`
- **Template Factory:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Template\TemplateFactory.cs`
- **Business Rules:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\BusinessRuleRegistry.cs`

### **Validation Services:**
- **Template Validator:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Template\TemplateValidator.cs`
- **Journal Service:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Journal\JournalService.cs`
- **Journal Entry Service:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Journal\JournalEntryService.cs`

### **Data Services:**
- **Data Provider:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\Data\IDataProvider.cs`
- **Smart PreAggregation:** `c:\VibeCoding\Gemini_Windsurf\3_CoreHub\Services\PreAggregation\SmartPreAggregationService.cs`

### **Test Files:**
- **Compliance Tests:** `c:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Core.Tests\Compliance\TT99ComplianceTests.cs`
- **Performance Tests:** `c:\VibeCoding\Gemini_Windsurf\6_Tests\VanAn.Performance.Tests\TT99PerformanceTests.cs`

---

## ⚠️ RISK MITIGATION

### **Timeline Risks:**
- **Risk:** 6 months until TT99 effective date
- **Mitigation:** Complete high-priority items in first 4 weeks
- **Contingency:** Phase rollout with backward compatibility

### **Technical Risks:**
- **Risk:** Account number dependencies throughout codebase
- **Mitigation:** Comprehensive search and replace with validation
- **Contingency:** Create mapping layer for gradual migration

### **Business Risks:**
- **Risk:** Non-compliance penalties after 01/01/2026
- **Mitigation:** Early compliance validation and testing
- **Contingency:** Manual override capabilities for critical periods

---

## 📊 SUCCESS METRICS

### **Compliance Metrics:**
- ✅ **100%** of deprecated accounts replaced
- ✅ **100%** of new accounts supported
- ✅ **0 errors** in TT99 compliance validation
- ✅ **<5%** performance impact from account migration

### **Quality Metrics:**
- ✅ **90%+** test coverage for TT99 changes
- ✅ **0 breaking changes** to public APIs
- ✅ **Complete** audit trail for account migrations
- ✅ **Full** backward compatibility during transition

---

## 🚀 NEXT STEPS

### **Week 1: Emergency Migration**
1. **Day 1-2:** Update ProductionFormulaEngine
2. **Day 3-4:** Update HKDBookService  
3. **Day 5-7:** Update validation services

### **Week 2: Template System**
1. **Day 8-10:** Update BaseHKDBookTemplate
2. **Day 11-14:** Update specific templates

### **Week 3: Business Rules**
1. **Day 15-18:** Update BusinessRuleRegistry
2. **Day 19-21:** Update DataProvider services

### **Week 4: Testing & Validation**
1. **Day 22-25:** Create TT99 compliance tests
2. **Day 26-28:** Performance and integration testing

---

## 📚 CONCLUSION

**This coding plan provides a comprehensive roadmap for TT99 compliance implementation:**

### **Key Benefits:**
- ✅ **Timeline Compliance:** Ready before 01/01/2026 deadline
- ✅ **Zero Disruption:** Backward compatibility during transition
- ✅ **Complete Coverage:** All affected components addressed
- ✅ **Quality Assurance:** Comprehensive testing and validation

### **Implementation Strategy:**
- **Phase 1:** Emergency account number migration (Week 1-2)
- **Phase 2:** Template system updates (Week 3-4)  
- **Phase 3:** Business rules and compliance (Week 5-6)
- **Phase 4:** Testing and validation (Week 7-8)

### **Success Criteria:**
- **Functional:** 100% TT99 compliance
- **Performance:** <5% impact on system performance
- **Quality:** 90%+ test coverage for changes
- **Timeline:** Complete before MVP release

**This plan ensures Van An ecosystem will be fully compliant with Thông tư 99/2025/TT-BTC while maintaining system stability and performance.**

---

**Document Status:** ✅ Complete  
**Implementation Ready:** 🚀 Immediate  
**Next Review:** 2026-05-09  
**File Reference:** `C:\VibeCoding\Gemini_Windsurf\docs\plan_MVP\Accounting\MVP plan Account M.md` (Phase 2.3.10)
