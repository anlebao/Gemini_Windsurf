using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VanAn.Shared.Domain;

namespace VanAn.Core.Domain
{
    // Mock base classes and interfaces
    public abstract class BaseEntity
    {
        public Guid Id { get; protected set; }
        
        protected BaseEntity() { }
        
        protected BaseEntity(TenantId tenantId)
        {
            Id = Guid.NewGuid();
        }
    }
    
    public interface IMustHaveTenant
    {
        TenantId TenantId { get; }
    }
    
    public record JournalEntryId(Guid Value);
    
    public record AccountingPeriod(int Year, int Month)
    {
        public static AccountingPeriod FromDateTime(DateTime date) => new(date.Year, date.Month);
    }

    // Mock Domain Classes for Testing
    public class JournalEntry : BaseEntity, IMustHaveTenant
    {
        public TenantId TenantId { get; private set; }
        public JournalEntryId JournalEntryId { get; private set; } = new JournalEntryId(Guid.NewGuid());
        public string JournalNo { get; private set; } = string.Empty;
        public DateTime EntryDate { get; private set; }
        public AccountingPeriod Period { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public string? ReferenceType { get; private set; }
        public Guid? ReferenceId { get; private set; }
        public bool IsReversal { get; private set; } = false;
        public JournalEntryId? ReversedJournalId { get; private set; }

        private readonly List<JournalEntryLine> _lines = new();
        public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();

        public JournalEntry() { } // EF Core

        public JournalEntry(TenantId tenantId, DateTime entryDate, string description, 
                           string? referenceType = null, Guid? referenceId = null)
            : base(tenantId)
        {
            TenantId = tenantId;
            EntryDate = entryDate;
            Period = AccountingPeriod.FromDateTime(entryDate);
            Description = description;
            ReferenceType = referenceType;
            ReferenceId = referenceId;
            JournalNo = GenerateJournalNo();
        }

        public void AddLine(string accountNumber, decimal debit, decimal credit, string? desc = null)
        {
            if (debit < 0 || credit < 0) throw new ArgumentException("Amount cannot be negative");
            if (debit > 0 && credit > 0) throw new ArgumentException("Cannot have both debit and credit");

            // Vietnamese account number validation
            if (string.IsNullOrWhiteSpace(accountNumber) || accountNumber.Length < 3 || accountNumber.Length > 10 || !accountNumber.All(char.IsDigit))
                throw new ArgumentException("Invalid Vietnamese account number", nameof(accountNumber));

            _lines.Add(new JournalEntryLine(this, accountNumber, debit, credit, desc));
        }

        private string GenerateJournalNo() => $"J{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N").Substring(0,8)}";
    }

    public class JournalEntryLine
    {
        public Guid JournalEntryId { get; private set; }
        public string AccountNumber { get; private set; } = string.Empty;
        public decimal DebitAmount { get; private set; }
        public decimal CreditAmount { get; private set; }
        public string? Description { get; private set; }

        public JournalEntryLine(JournalEntry journal, string accountNumber, 
                              decimal debit, decimal credit, string? desc)
        {
            JournalEntryId = journal.Id;
            AccountNumber = accountNumber;
            DebitAmount = debit;
            CreditAmount = credit;
            Description = desc;
        }
    }

    public class JournalTemplate
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<JournalTemplateLine> Lines { get; set; } = new();
        public List<string> BusinessRules { get; set; } = new();
        public List<TemplateValidationRule> ValidationRules { get; set; } = new();
    }

    public class JournalTemplateLine
    {
        public string AccountNumber { get; set; } = string.Empty;
        public bool IsDebit { get; set; }
        public bool IsCredit { get; set; }
        public string? DescriptionTemplate { get; set; }
        public string? AmountFormula { get; set; }
    }

    // Mock AccountingEntry for testing
    public class AccountingEntry
    {
        public TenantId TenantId { get; set; }
        public Money Amount { get; set; }
        public AccountingEntryType EntryType { get; set; }
        public VatRate VatRate { get; set; }
        public DateTime TransactionDate { get; set; }
        public AccountingBookType AccountingBookType { get; set; }
        public string Description { get; set; }

        public AccountingEntry(TenantId tenantId, Money amount, AccountingEntryType entryType, 
                            VatRate vatRate, DateTime transactionDate, AccountingBookType accountingBookType, 
                            string description)
        {
            TenantId = tenantId;
            Amount = amount;
            EntryType = entryType;
            VatRate = vatRate;
            TransactionDate = transactionDate;
            AccountingBookType = accountingBookType;
            Description = description;
        }
    }

    public enum AccountingEntryType
    {
        Revenue,
        Expense,
        TaxPayment,
        Adjustment
    }

    public enum AccountingBookType
    {
        RevenueBook,
        ExpenseBook,
        CashBankBook,
        TaxDeclarationBook
    }

    public enum VatRate
    {
        Exempt = 0,
        Zero = 0,
        Five = 5,
        Ten = 10
    }

    public record Money(decimal Value);

    public class TemplateValidationRule
    {
        public string Rule { get; set; } = string.Empty;
        public string? Message { get; set; }
    }

    public class TemplateContext
    {
        public JournalTemplate Template { get; }
        public decimal Amount { get; }
        public Dictionary<string, object> Parameters { get; }
        public decimal VatAmount { get; set; }
        public decimal NetAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal COGS { get; set; }
        public decimal ImportTaxAmount { get; set; }

        public TemplateContext(JournalTemplate template, decimal amount, Dictionary<string, object> parameters)
        {
            Template = template;
            Amount = amount;
            Parameters = parameters ?? new Dictionary<string, object>();
        }
    }

    // Business Rule Interfaces and Mock Implementations
    public interface IBusinessRule
    {
        Task<bool> ShouldApplyAsync(TemplateContext context);
        Task ApplyAsync(TemplateContext context);
    }

    public class VIPDiscountRule : IBusinessRule
    {
        public async Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            return context.Parameters.ContainsKey("IsVIP") && (bool)context.Parameters["IsVIP"];
        }

        public async Task ApplyAsync(TemplateContext context)
        {
            var discountRate = context.Parameters.ContainsKey("VIPLevel") 
                ? context.Parameters["VIPLevel"] switch
                  {
                      "Gold" => 0.15m,
                      "Silver" => 0.10m,
                      "Bronze" => 0.05m,
                      _ => 0.10m
                  }
                : 0.10m;
            
            context.NetAmount = context.Amount * (1 - discountRate);
            context.DiscountAmount = context.Amount * discountRate;
        }
    }

    public class VATCalculationRule : IBusinessRule
    {
        public async Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            return !context.Parameters.ContainsKey("IsExport") || !(bool)context.Parameters["IsExport"];
        }

        public async Task ApplyAsync(TemplateContext context)
        {
            var vatRate = context.Parameters.ContainsKey("VatRate") 
                ? (decimal)context.Parameters["VatRate"] 
                : 10m; // Default 10%
                
            context.VatAmount = context.NetAmount * vatRate / 100;
        }
    }

    public class ExportVATRule : IBusinessRule
    {
        public async Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            return context.Parameters.ContainsKey("IsExport") && (bool)context.Parameters["IsExport"];
        }

        public async Task ApplyAsync(TemplateContext context)
        {
            context.VatAmount = 0m;
        }
    }

    public class COGSCalculationRule : IBusinessRule
    {
        public async Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            return true; // Always apply for sales
        }

        public async Task ApplyAsync(TemplateContext context)
        {
            var cogsPercentage = context.Parameters.ContainsKey("COGSPercentage")
                ? (decimal)context.Parameters["COGSPercentage"]
                : 0.6m; // Default 60%
                
            context.COGS = context.Amount * cogsPercentage;
        }
    }

    public class ImportTaxCalculationRule : IBusinessRule
    {
        public async Task<bool> ShouldApplyAsync(TemplateContext context)
        {
            return context.Parameters.ContainsKey("IsImport") && (bool)context.Parameters["IsImport"];
        }

        public async Task ApplyAsync(TemplateContext context)
        {
            var importTaxRate = context.Parameters.ContainsKey("ImportTaxRate")
                ? (decimal)context.Parameters["ImportTaxRate"]
                : 5m; // Default 5%
                
            decimal baseAmount;
            if (context.Parameters.ContainsKey("ForeignAmount") && context.Parameters.ContainsKey("ExchangeRate"))
            {
                baseAmount = (decimal)context.Parameters["ForeignAmount"] * (decimal)context.Parameters["ExchangeRate"];
            }
            else
            {
                baseAmount = context.Amount;
            }
                
            context.ImportTaxAmount = baseAmount * importTaxRate / 100;
            context.NetAmount = baseAmount;
        }
    }

    // Mock interfaces for testing
    public interface IJournalTemplateRepository
    {
        Task<JournalTemplate> GetByCodeAsync(string code);
    }

    public interface IBusinessRuleRegistry
    {
        IBusinessRule GetRule(string ruleName);
    }

    public interface ITemplateValidator
    {
        Task<bool> ValidateTemplateAsync(JournalTemplate template, Dictionary<string, object> parameters);
    }

    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }

    // Mock Factory for Testing
    public class EnhancedJournalFactory
    {
        private readonly IJournalTemplateRepository _templateRepo;
        private readonly IBusinessRuleRegistry _ruleRegistry;
        private readonly ITemplateValidator _validator;

        public EnhancedJournalFactory(
            IJournalTemplateRepository templateRepo,
            IBusinessRuleRegistry ruleRegistry,
            ITemplateValidator validator)
        {
            _templateRepo = templateRepo;
            _ruleRegistry = ruleRegistry;
            _validator = validator;
        }

        public async Task<JournalEntry> CreateFromTemplateAsync(
            TenantId tenantId,
            string templateCode,
            decimal amount,
            Dictionary<string, object> parameters)
        {
            // 1. Get template
            var template = await _templateRepo.GetByCodeAsync(templateCode);
            if (template == null)
                throw new NotFoundException($"Template not found: {templateCode}");

            // 2. Validate template
            var isValid = await _validator.ValidateTemplateAsync(template, parameters);
            if (!isValid)
                throw new ValidationException("Template validation failed");

            // 3. Create context
            var context = new TemplateContext(template, amount, parameters);

            // 4. Apply business rules
            foreach (var ruleName in template.BusinessRules)
            {
                var rule = _ruleRegistry.GetRule(ruleName);
                if (await rule.ShouldApplyAsync(context))
                {
                    await rule.ApplyAsync(context);
                }
            }

            // 5. Create journal entry
            var journal = new JournalEntry(tenantId, DateTime.UtcNow, template.Description);

            // 6. Generate lines from template
            foreach (var line in template.Lines)
            {
                var calculatedAmount = CalculateLineAmount(line, context);
                var description = ReplaceParameters(line.DescriptionTemplate, parameters);
                
                journal.AddLine(
                    line.AccountNumber,
                    line.IsDebit ? calculatedAmount : 0,
                    line.IsCredit ? calculatedAmount : 0,
                    description
                );
            }

            return journal;
        }

        private decimal CalculateLineAmount(JournalTemplateLine line, TemplateContext context)
        {
            return line.AmountFormula switch
            {
                "Amount" => context.Amount,
                "NetAmount" => context.NetAmount > 0 ? context.NetAmount : context.Amount,
                "VatAmount" => context.VatAmount > 0 ? context.VatAmount : context.Amount,
                "COGS" => context.COGS > 0 ? context.COGS : context.Amount,
                "ImportTax" => context.ImportTaxAmount > 0 ? context.ImportTaxAmount : context.Amount,
                "TotalAmount" => context.Amount,
                _ => context.Amount
            };
        }

        private string ReplaceParameters(string? template, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            
            var result = template;
            foreach (var param in parameters)
            {
                result = result.Replace($"{{{param.Key}}}", param.Value?.ToString() ?? "");
            }
            return result;
        }
    }
}
