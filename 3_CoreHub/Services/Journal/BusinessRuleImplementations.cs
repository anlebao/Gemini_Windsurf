using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services.Journal
{
    /// <summary>
    /// Vietnamese Accounting Rules - TT152/2025/TT-BTC Compliance
    /// Implements Vietnamese accounting standards and regulations
    /// </summary>

    public class VietnameseVATRule : IBusinessRule
    {
        private readonly ILogger<VietnameseVATRule> _logger;

        public VietnameseVATRule(ILogger<VietnameseVATRule> logger)
        {
            _logger = logger;
        }

        public string Name => "VietnameseVAT";

        public Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            try
            {
                // Apply VAT for all transactions except exempt ones
                var transactionType = context.GetParameter<string>("TransactionType", "");
                var isExempt = context.GetParameter<bool>("IsVATExempt", false);
                var shouldApply = !isExempt && context.Amount > 0;
                
                return Task.FromResult(shouldApply);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VietnameseVATRule.ShouldApplyAsync");
                return Task.FromResult(false);
            }
        }

        public Task ApplyAsync(TemplateContext context)
        {
            try
            {
                // Vietnamese VAT rates: 0%, 5%, 10%
                var vatRate = context.GetParameter<decimal>("VATRate", 0.10m); // Default 10%
                var taxableAmount = context.NetAmount > 0 ? context.NetAmount : context.Amount;
                
                // Validate VAT rate
                if (vatRate != 0m && vatRate != 0.05m && vatRate != 0.10m)
                {
                    _logger.LogWarning("Invalid VAT rate: {VATRate}, using default 10%", vatRate);
                    vatRate = 0.10m;
                }
                
                context.VatAmount = taxableAmount * vatRate;
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VietnameseVATRule.ApplyAsync");
                throw;
            }
        }
    }

    public class CorporateIncomeTaxRule : IBusinessRule
    {
        private readonly ILogger<CorporateIncomeTaxRule> _logger;

        public CorporateIncomeTaxRule(ILogger<CorporateIncomeTaxRule> logger)
        {
            _logger = logger;
        }

        public string Name => "CorporateIncomeTax";

        public Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            try
            {
                // Apply CIT for revenue transactions
                var transactionType = context.GetParameter<string>("TransactionType", "");
                var shouldApply = transactionType == "Revenue" || transactionType == "Sale";
                return Task.FromResult(shouldApply);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CorporateIncomeTaxRule.ShouldApplyAsync");
                return Task.FromResult(false);
            }
        }

        public Task ApplyAsync(TemplateContext context)
        {
            try
            {
                // CIT rate: 20% standard
                var citRate = context.GetParameter<decimal>("CITRate", 0.20m);
                var taxableIncome = context.NetAmount > 0 ? context.NetAmount : context.Amount;
                
                // Store CIT amount for reporting
                context.SetParameter("CITAmount", taxableIncome * citRate);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CorporateIncomeTaxRule.ApplyAsync");
                throw;
            }
        }
    }

    public class PersonalIncomeTaxRule : IBusinessRule
    {
        private readonly ILogger<PersonalIncomeTaxRule> _logger;

        public PersonalIncomeTaxRule(ILogger<PersonalIncomeTaxRule> logger)
        {
            _logger = logger;
        }

        public string Name => "PersonalIncomeTax";

        public Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            try
            {
                // Apply PIT for salary and wage transactions
                var transactionType = context.GetParameter<string>("TransactionType", "");
                var shouldApply = transactionType == "Salary" || transactionType == "Wage";
                return Task.FromResult(shouldApply);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PersonalIncomeTaxRule.ShouldApplyAsync");
                return Task.FromResult(false);
            }
        }

        public Task ApplyAsync(TemplateContext context)
        {
            try
            {
                // PIT progressive rates: 5%, 10%, 15%, 20%, 25%, 30%, 35%
                var monthlyIncome = context.NetAmount > 0 ? context.NetAmount : context.Amount;
                var pitAmount = CalculatePIT(monthlyIncome);
                
                context.SetParameter("PITAmount", pitAmount);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PersonalIncomeTaxRule.ApplyAsync");
                throw;
            }
        }

        private decimal CalculatePIT(decimal monthlyIncome)
        {
            // Simplified PIT calculation - progressive rates
            if (monthlyIncome <= 5000000) return 0;
            if (monthlyIncome <= 10000000) return monthlyIncome * 0.05m;
            if (monthlyIncome <= 18000000) return monthlyIncome * 0.10m;
            if (monthlyIncome <= 32000000) return monthlyIncome * 0.15m;
            if (monthlyIncome <= 52000000) return monthlyIncome * 0.20m;
            if (monthlyIncome <= 80000000) return monthlyIncome * 0.25m;
            return monthlyIncome * 0.30m;
        }
    }

    public class PeriodClosingValidationRule : IBusinessRule
    {
        private readonly ILogger<PeriodClosingValidationRule> _logger;

        public PeriodClosingValidationRule(ILogger<PeriodClosingValidationRule> logger)
        {
            _logger = logger;
        }

        public string Name => "PeriodClosingValidation";

        public Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            try
            {
                // Apply for period closing transactions
                var isPeriodClosing = context.GetParameter<bool>("IsPeriodClosing", false);
                return Task.FromResult(isPeriodClosing);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PeriodClosingValidationRule.ShouldApplyAsync");
                return Task.FromResult(false);
            }
        }

        public Task ApplyAsync(TemplateContext context)
        {
            try
            {
                // Validate period closing requirements
                var period = context.GetParameter<AccountingPeriod>("Period", null);
                if (period == null)
                {
                    throw new ValidationException("Period is required for period closing");
                }

                // Add validation logic for period closing
                context.SetParameter("PeriodValidated", true);
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PeriodClosingValidationRule.ApplyAsync");
                throw;
            }
        }
    }

    public class AccountingStandardComplianceRule : IBusinessRule
    {
        private readonly ILogger<AccountingStandardComplianceRule> _logger;

        public AccountingStandardComplianceRule(ILogger<AccountingStandardComplianceRule> logger)
        {
            _logger = logger;
        }

        public string Name => "AccountingStandardCompliance";

        public Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            try
            {
                // Apply to all transactions for compliance checking
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AccountingStandardComplianceRule.ShouldApplyAsync");
                return Task.FromResult(false);
            }
        }

        public Task ApplyAsync(TemplateContext context)
        {
            try
            {
                // Validate against Vietnamese Accounting Standards (VAS)
                var transactionType = context.GetParameter<string>("TransactionType", "");
                var amount = context.Amount;
                
                // Basic compliance checks
                if (amount < 0)
                {
                    throw new ValidationException("Transaction amount cannot be negative");
                }

                // Add compliance validation logic
                context.SetParameter("ComplianceValidated", true);
                context.SetParameter("ComplianceVersion", "TT152/2025/TT-BTC");
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AccountingStandardComplianceRule.ApplyAsync");
                throw;
            }
        }
    }

    public class VIPDiscountRule : IBusinessRule
    {
        private readonly ILogger<VIPDiscountRule> _logger;

        public VIPDiscountRule(ILogger<VIPDiscountRule> logger)
        {
            _logger = logger;
        }

        public string Name => "VIPDiscount";

        public Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            try
            {
                // Check if customer is VIP
                var isVip = context.GetParameter<bool>("IsVIP", false);
                return Task.FromResult(isVip);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VIPDiscountRule.ShouldApplyAsync");
                return Task.FromResult(false);
            }
        }

        public Task ApplyAsync(TemplateContext context)
        {
            try
            {
                // Apply 10% discount for VIP customers
                var discountRate = 0.1m;
                context.DiscountAmount = context.Amount * discountRate;
                context.NetAmount = context.Amount - context.DiscountAmount;
                
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in VIPDiscountRule.ApplyAsync");
                throw;
            }
        }
    }
}
