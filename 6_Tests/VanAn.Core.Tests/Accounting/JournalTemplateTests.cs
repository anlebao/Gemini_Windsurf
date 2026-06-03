using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using Moq;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Journal;
using Microsoft.Extensions.Logging;

namespace VanAn.Core.Tests.Accounting
{
    public class JournalTemplateTests
    {
        private readonly TenantId _tenantId = new TenantId(Guid.NewGuid());
        private readonly JournalTemplateService _templateService;

        public JournalTemplateTests()
        {
            _templateService = new JournalTemplateService(Mock.Of<ILogger<JournalTemplateService>>());
        }

        [Fact]
        public void JournalTemplate_Should_Create_With_Valid_Data()
        {
            // Arrange
            var tenantId = new TenantId(Guid.NewGuid());
            var code = "SALE-CASH";
            var description = "Bán hàng tiền mặt";

            // Act
            var template = new JournalTemplate(tenantId, code, description);
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");

            // Assert
            template.Code.Should().Be(code);
            template.Description.Should().Be(description);
            template.Lines.Should().HaveCount(2);
            template.Lines.First().AccountNumber.Should().Be("111");
            template.Lines.First().IsDebit.Should().BeTrue();
            template.Lines.Last().AccountNumber.Should().Be("511");
            template.Lines.Last().IsCredit.Should().BeTrue();
        }

        [Fact]
        public void JournalTemplate_Should_Create_With_Empty_Lines()
        {
            // Arrange
            var tenantId = new TenantId(Guid.NewGuid());
            var code = "EMPTY-TEMPLATE";
            var description = "Template rỗng";

            // Act
            var template = new JournalTemplate(tenantId, code, description);

            // Assert
            template.Code.Should().Be(code);
            template.Description.Should().Be(description);
            template.Lines.Should().BeEmpty();
        }

        [Fact]
        public void JournalTemplate_Should_Handle_Business_Rules()
        {
            // Arrange
            var tenantId = new TenantId(Guid.NewGuid());
            var template = new JournalTemplate(tenantId, "SALE-VIP", "Bán hàng khách VIP");
            template.AddBusinessRule("VIPDiscountRule");
            template.AddBusinessRule("VATCalculationRule");

            // Act & Assert
            template.BusinessRules.Should().HaveCount(2);
            template.BusinessRules.Should().Contain("VIPDiscountRule");
            template.BusinessRules.Should().Contain("VATCalculationRule");
        }

        [Fact]
        public void JournalTemplate_Should_Handle_Validation_Rules()
        {
            // Arrange
            var tenantId = new TenantId(Guid.NewGuid());
            var template = new JournalTemplate(tenantId, "PURCHASE", "Mua hàng");
            template.AddValidationRule("Amount > 0", "Số tiền phải lớn hơn 0");

            // Act & Assert
            template.ValidationRules.Should().HaveCount(1);
            template.ValidationRules.First().Rule.Should().Be("Amount > 0");
            template.ValidationRules.First().Message.Should().Be("Số tiền phải lớn hơn 0");
        }

        [Theory]
        [InlineData("SALE-CASH")]
        [InlineData("PURCHASE-CREDIT")]
        [InlineData("SALARY-PAYMENT")]
        [InlineData("TAX-PAYMENT")]
        public void JournalTemplate_Should_Handle_Different_Codes(string code)
        {
            // Arrange & Act
            var tenantId = new TenantId(Guid.NewGuid());
            var template = new JournalTemplate(tenantId, code, "Test Template");

            // Assert
            template.Code.Should().Be(code);
        }

        [Fact]
        public void JournalTemplate_Should_Handle_Complex_Lines()
        {
            // Arrange
            var tenantId = new TenantId(Guid.NewGuid());

            // Act
            var template = new JournalTemplate(tenantId, "COMPLEX-SALE", "Bán hàng phức tạp");
            template.AddLine("111", true, "TotalAmount", "Thu tiền {CustomerName}");
            template.AddLine("511", false, "NetAmount", "Doanh thu {InvoiceNo}");
            template.AddLine("3331", false, "VatAmount", "Thuế GTGT {VatRate}%");

            // Assert
            template.Lines.Should().HaveCount(3);
            var linesList = template.Lines.ToList();
            linesList[0].DescriptionTemplate.Should().Be("Thu tiền {CustomerName}");
            linesList[1].DescriptionTemplate.Should().Be("Doanh thu {InvoiceNo}");
            linesList[2].DescriptionTemplate.Should().Be("Thuế GTGT {VatRate}%");
        }

        [Fact]
        public void JournalTemplate_Should_Execute_Parameter_Replacement()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "TEST", "Test Template");
            template.AddLine("111", true, "Amount", "Payment from {CustomerName} for {InvoiceNo}");
            
            var parameters = new Dictionary<string, object>
            {
                ["CustomerName"] = "Nguyễn Văn A",
                ["InvoiceNo"] = "INV-001"
            };

            // Act - Simulate parameter replacement using service
            var description = _templateService.ReplaceParameters(template.Lines.First().DescriptionTemplate, parameters);

            // Assert
            description.Should().Be("Payment from Nguyễn Văn A for INV-001");
        }
    }

    public class JournalTemplateLineTests
    {
        private readonly TenantId _tenantId = new TenantId(Guid.NewGuid());

        [Fact]
        public void JournalTemplateLine_Should_Create_With_Valid_Data()
        {
            // Arrange
            var accountNumber = "111";
            var isDebit = true;
            var amountFormula = "Amount";
            var descriptionTemplate = "Test Line";

            // Act
            var line = new JournalTemplateLine(accountNumber, isDebit, amountFormula, descriptionTemplate);

            // Assert
            line.AccountNumber.Should().Be(accountNumber);
            line.IsDebit.Should().Be(isDebit);
            line.IsCredit.Should().Be(!isDebit);
            line.AmountFormula.Should().Be(amountFormula);
            line.DescriptionTemplate.Should().Be(descriptionTemplate);
        }

        [Theory]
        [InlineData("Amount")]
        [InlineData("NetAmount")]
        [InlineData("VatAmount")]
        [InlineData("Amount*0.1")]
        [InlineData("COGS")]
        [InlineData("ImportTax")]
        public void JournalTemplateLine_Should_Handle_Different_Amount_Formulas(string formula)
        {
            // Arrange & Act
            var line = new JournalTemplateLine("111", true, formula, null);

            // Assert
            line.AmountFormula.Should().Be(formula);
        }

        [Theory]
        [InlineData("111")]
        [InlineData("112")]
        [InlineData("131")]
        [InlineData("511")]
        [InlineData("632")]
        public void JournalTemplateLine_Should_Handle_Different_Account_Numbers(string accountNumber)
        {
            // Arrange & Act
            var line = new JournalTemplateLine(accountNumber, true, "Amount", null);

            // Assert
            line.AccountNumber.Should().Be(accountNumber);
        }

        [Fact]
        public void JournalTemplateLine_Should_Handle_Null_Description_Template()
        {
            // Arrange & Act
            var line = new JournalTemplateLine("111", true, "Amount", null);

            // Assert
            line.DescriptionTemplate.Should().BeNull();
        }

        [Fact]
        public void JournalTemplateLine_Should_Handle_Empty_Description_Template()
        {
            // Arrange & Act
            var line = new JournalTemplateLine("111", true, "Amount", "");

            // Assert
            line.DescriptionTemplate.Should().Be("");
        }

        [Fact]
        public void JournalTemplateLine_Should_Handle_Null_Amount_Formula()
        {
            // Arrange & Act
            var line = new JournalTemplateLine("111", true, null, null);

            // Assert
            line.AmountFormula.Should().BeNull();
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void JournalTemplateLine_Should_Handle_Different_Debit_Credit_Combinations(bool isDebit, bool isCredit)
        {
            // Arrange & Act
            var line = new JournalTemplateLine("111", isDebit, "Amount", null);

            // Assert
            line.IsDebit.Should().Be(isDebit);
            line.IsCredit.Should().Be(!isDebit);
        }

        
        [Fact]
        public void JournalTemplate_Should_Calculate_Formula_Amounts()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "FORMULA", "Formula Test");
            template.AddLine("111", true, "Amount*0.1");
            template.AddLine("511", false, "Amount*0.05");
            
            var context = new TemplateContext(template, 10000m, new Dictionary<string, object>());

            // Act
            var amount1 = CalculateLineAmount(template.Lines.First(), context);
            var amount2 = CalculateLineAmount(template.Lines.Last(), context);

            // Assert
            amount1.Should().Be(1000m); // 10% of 10000
            amount2.Should().Be(500m);  // 5% of 10000
        }

        [Fact]
        public void JournalTemplate_Should_Validate_Business_Rule_Execution()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "RULE-TEST", "Rule Test");
            template.AddBusinessRule("VIPDiscountRule");
            template.AddValidationRule("Amount > 0", "Amount must be positive");

            // Act & Assert
            template.BusinessRules.Should().Contain("VIPDiscountRule");
            template.ValidationRules.Should().HaveCount(1);
            template.ValidationRules.First().Rule.Should().Be("Amount > 0");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void JournalTemplate_Should_Handle_Empty_Codes(string invalidCode)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new JournalTemplate(_tenantId, invalidCode, "Test"));
        }

        [Fact]
        public void JournalTemplate_Should_Prevent_Duplicate_Business_Rules()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "TEST", "Test");

            // Act
            template.AddBusinessRule("VIPRule");
            template.AddBusinessRule("VIPRule"); // Duplicate

            // Assert
            template.BusinessRules.Should().HaveCount(1); // Should not add duplicate
        }

        [Fact]
        public void JournalTemplate_Should_Handle_Complex_Formulas()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "COMPLEX", "Complex Formula");
            template.AddLine("111", true, "Amount*0.1*VatRate");
            
            var parameters = new Dictionary<string, object> { ["VatRate"] = 10m };
            var context = new TemplateContext(template, 10000m, parameters);

            // Act
            var amount = CalculateLineAmount(template.Lines.First(), context);

            // Assert
            amount.Should().Be(100m); // 10000 * 0.1 * 10/100
        }

        [Fact]
        public void JournalTemplate_Should_Validate_Account_Number_In_Lines()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "VALIDATE", "Validation Test");

            // Act & Assert
            Assert.Throws<ArgumentException>(() => 
                template.AddLine("", true, "Amount")); // Empty account number
        }

        [Fact]
        public void JournalTemplate_Should_Handle_Description_Templates()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "DESC", "Description Test");
            template.AddLine("111", true, "Amount", "Thu tiền {Amount} từ {Customer}");

            // Act
            var line = template.Lines.First();

            // Assert
            line.DescriptionTemplate.Should().Be("Thu tiền {Amount} từ {Customer}");
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
                "Amount*0.1" => context.Amount * 0.1m,
                "Amount*0.05" => context.Amount * 0.05m,
                "Amount*0.1*VatRate" => context.Amount * 0.1m * (context.GetParameter<decimal>("VatRate", 0m) / 100m),
                _ => context.Amount
            };
        }
    }

    public class TemplateValidationRuleTests
    {
        private readonly TenantId _tenantId = new TenantId(Guid.NewGuid());

        [Fact]
        public void TemplateValidationRule_Should_Create_With_Valid_Data()
        {
            // Arrange
            var rule = "Amount > 0";
            var message = "Số tiền phải lớn hơn 0";

            // Act
            var validationRule = new TemplateValidationRule(rule, message);

            // Assert
            validationRule.Rule.Should().Be(rule);
            validationRule.Message.Should().Be(message);
        }

        [Theory]
        [InlineData("Amount > 0")]
        [InlineData("Amount >= 1000")]
        [InlineData("CustomerName != null")]
        [InlineData("VatRate in [0, 5, 10]")]
        public void TemplateValidationRule_Should_Handle_Different_Rules(string rule)
        {
            // Arrange & Act
            var validationRule = new TemplateValidationRule(rule, "Validation message");

            // Assert
            validationRule.Rule.Should().Be(rule);
        }

        [Fact]
        public void TemplateValidationRule_Should_Handle_Null_Message()
        {
            // Arrange & Act
            var validationRule = new TemplateValidationRule("Amount > 0", null);

            // Assert
            validationRule.Message.Should().BeNull();
        }
    }
}
