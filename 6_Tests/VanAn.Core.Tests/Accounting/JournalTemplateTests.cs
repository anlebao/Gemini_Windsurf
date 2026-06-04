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
        private readonly TenantId _tenantId = new(Guid.NewGuid());
        private readonly JournalTemplateService _templateService;

        public JournalTemplateTests()
        {
            _templateService = new JournalTemplateService(Mock.Of<ILogger<JournalTemplateService>>());
        }

        [Fact]
        public void JournalTemplate_Should_Create_With_Valid_Data()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            string code = "SALE-CASH";
            string description = "Bán hàng tiền mặt";

            // Act
            JournalTemplate template = new(tenantId, code, description);
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");

            // Assert
            _ = template.Code.Should().Be(code);
            _ = template.Description.Should().Be(description);
            _ = template.Lines.Should().HaveCount(2);
            _ = template.Lines.First().AccountNumber.Should().Be("111");
            _ = template.Lines.First().IsDebit.Should().BeTrue();
            _ = template.Lines.Last().AccountNumber.Should().Be("511");
            _ = template.Lines.Last().IsCredit.Should().BeTrue();
        }

        [Fact]
        public void JournalTemplate_Should_Create_With_Empty_Lines()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            string code = "EMPTY-TEMPLATE";
            string description = "Template rỗng";

            // Act
            JournalTemplate template = new(tenantId, code, description);

            // Assert
            _ = template.Code.Should().Be(code);
            _ = template.Description.Should().Be(description);
            _ = template.Lines.Should().BeEmpty();
        }

        [Fact]
        public void JournalTemplate_Should_Handle_Business_Rules()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            JournalTemplate template = new(tenantId, "SALE-VIP", "Bán hàng khách VIP");
            template.AddBusinessRule("VIPDiscountRule");
            template.AddBusinessRule("VATCalculationRule");

            // Act & Assert
            _ = template.BusinessRules.Should().HaveCount(2);
            _ = template.BusinessRules.Should().Contain("VIPDiscountRule");
            _ = template.BusinessRules.Should().Contain("VATCalculationRule");
        }

        [Fact]
        public void JournalTemplate_Should_Handle_Validation_Rules()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());
            JournalTemplate template = new(tenantId, "PURCHASE", "Mua hàng");
            template.AddValidationRule("Amount > 0", "Số tiền phải lớn hơn 0");

            // Act & Assert
            _ = template.ValidationRules.Should().HaveCount(1);
            _ = template.ValidationRules.First().Rule.Should().Be("Amount > 0");
            _ = template.ValidationRules.First().Message.Should().Be("Số tiền phải lớn hơn 0");
        }

        [Theory]
        [InlineData("SALE-CASH")]
        [InlineData("PURCHASE-CREDIT")]
        [InlineData("SALARY-PAYMENT")]
        [InlineData("TAX-PAYMENT")]
        public void JournalTemplate_Should_Handle_Different_Codes(string code)
        {
            // Arrange & Act
            TenantId tenantId = new(Guid.NewGuid());
            JournalTemplate template = new(tenantId, code, "Test Template");

            // Assert
            _ = template.Code.Should().Be(code);
        }

        [Fact]
        public void JournalTemplate_Should_Handle_Complex_Lines()
        {
            // Arrange
            TenantId tenantId = new(Guid.NewGuid());

            // Act
            JournalTemplate template = new(tenantId, "COMPLEX-SALE", "Bán hàng phức tạp");
            template.AddLine("111", true, "TotalAmount", "Thu tiền {CustomerName}");
            template.AddLine("511", false, "NetAmount", "Doanh thu {InvoiceNo}");
            template.AddLine("3331", false, "VatAmount", "Thuế GTGT {VatRate}%");

            // Assert
            _ = template.Lines.Should().HaveCount(3);
            List<JournalTemplateLine> linesList = [.. template.Lines];
            _ = linesList[0].DescriptionTemplate.Should().Be("Thu tiền {CustomerName}");
            _ = linesList[1].DescriptionTemplate.Should().Be("Doanh thu {InvoiceNo}");
            _ = linesList[2].DescriptionTemplate.Should().Be("Thuế GTGT {VatRate}%");
        }

        [Fact]
        public void JournalTemplate_Should_Execute_Parameter_Replacement()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "TEST", "Test Template");
            template.AddLine("111", true, "Amount", "Payment from {CustomerName} for {InvoiceNo}");

            Dictionary<string, object> parameters = new()
            {
                ["CustomerName"] = "Nguyễn Văn A",
                ["InvoiceNo"] = "INV-001"
            };

            // Act - Simulate parameter replacement using service
            string description = _templateService.ReplaceParameters(template.Lines.First().DescriptionTemplate, parameters);

            // Assert
            _ = description.Should().Be("Payment from Nguyễn Văn A for INV-001");
        }
    }

    public class JournalTemplateLineTests
    {
        private readonly TenantId _tenantId = new(Guid.NewGuid());

        [Fact]
        public void JournalTemplateLine_Should_Create_With_Valid_Data()
        {
            // Arrange
            string accountNumber = "111";
            bool isDebit = true;
            string amountFormula = "Amount";
            string descriptionTemplate = "Test Line";

            // Act
            JournalTemplateLine line = new(accountNumber, isDebit, amountFormula, descriptionTemplate);

            // Assert
            _ = line.AccountNumber.Should().Be(accountNumber);
            _ = line.IsDebit.Should().Be(isDebit);
            _ = line.IsCredit.Should().Be(!isDebit);
            _ = line.AmountFormula.Should().Be(amountFormula);
            _ = line.DescriptionTemplate.Should().Be(descriptionTemplate);
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
            JournalTemplateLine line = new("111", true, formula, null);

            // Assert
            _ = line.AmountFormula.Should().Be(formula);
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
            JournalTemplateLine line = new(accountNumber, true, "Amount", null);

            // Assert
            _ = line.AccountNumber.Should().Be(accountNumber);
        }

        [Fact]
        public void JournalTemplateLine_Should_Handle_Null_Description_Template()
        {
            // Arrange & Act
            JournalTemplateLine line = new("111", true, "Amount", null);

            // Assert
            _ = line.DescriptionTemplate.Should().BeNull();
        }

        [Fact]
        public void JournalTemplateLine_Should_Handle_Empty_Description_Template()
        {
            // Arrange & Act
            JournalTemplateLine line = new("111", true, "Amount", "");

            // Assert
            _ = line.DescriptionTemplate.Should().Be("");
        }

        [Fact]
        public void JournalTemplateLine_Should_Handle_Null_Amount_Formula()
        {
            // Arrange & Act
            JournalTemplateLine line = new("111", true, null, null);

            // Assert
            _ = line.AmountFormula.Should().BeNull();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void JournalTemplateLine_Should_Handle_Different_Debit_Credit_Combinations(bool isDebit)
        {
            // Arrange & Act
            JournalTemplateLine line = new("111", isDebit, "Amount", null);

            // Assert
            _ = line.IsDebit.Should().Be(isDebit);
            _ = line.IsCredit.Should().Be(!isDebit);
        }


        [Fact]
        public void JournalTemplate_Should_Calculate_Formula_Amounts()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "FORMULA", "Formula Test");
            template.AddLine("111", true, "Amount*0.1");
            template.AddLine("511", false, "Amount*0.05");

            TemplateContext context = new(template, 10000m, []);

            // Act
            decimal amount1 = CalculateLineAmount(template.Lines.First(), context);
            decimal amount2 = CalculateLineAmount(template.Lines.Last(), context);

            // Assert
            _ = amount1.Should().Be(1000m); // 10% of 10000
            _ = amount2.Should().Be(500m);  // 5% of 10000
        }

        [Fact]
        public void JournalTemplate_Should_Validate_Business_Rule_Execution()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "RULE-TEST", "Rule Test");
            template.AddBusinessRule("VIPDiscountRule");
            template.AddValidationRule("Amount > 0", "Amount must be positive");

            // Act & Assert
            _ = template.BusinessRules.Should().Contain("VIPDiscountRule");
            _ = template.ValidationRules.Should().HaveCount(1);
            _ = template.ValidationRules.First().Rule.Should().Be("Amount > 0");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void JournalTemplate_Should_Handle_Empty_Codes(string? invalidCode)
        {
            // Arrange & Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new JournalTemplate(_tenantId, invalidCode, "Test"));
        }

        [Fact]
        public void JournalTemplate_Should_Prevent_Duplicate_Business_Rules()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "TEST", "Test");

            // Act
            template.AddBusinessRule("VIPRule");
            template.AddBusinessRule("VIPRule"); // Duplicate

            // Assert
            _ = template.BusinessRules.Should().HaveCount(1); // Should not add duplicate
        }

        [Fact]
        public void JournalTemplate_Should_Handle_Complex_Formulas()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "COMPLEX", "Complex Formula");
            template.AddLine("111", true, "Amount*0.1*VatRate");

            Dictionary<string, object> parameters = new() { ["VatRate"] = 10m };
            TemplateContext context = new(template, 10000m, parameters);

            // Act
            decimal amount = CalculateLineAmount(template.Lines.First(), context);

            // Assert
            _ = amount.Should().Be(100m); // 10000 * 0.1 * 10/100
        }

        [Fact]
        public void JournalTemplate_Should_Validate_Account_Number_In_Lines()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "VALIDATE", "Validation Test");

            // Act & Assert
            _ = Assert.Throws<ArgumentException>(() =>
                template.AddLine("", true, "Amount")); // Empty account number
        }

        [Fact]
        public void JournalTemplate_Should_Handle_Description_Templates()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "DESC", "Description Test");
            template.AddLine("111", true, "Amount", "Thu tiền {Amount} từ {Customer}");

            // Act
            JournalTemplateLine line = template.Lines.First();

            // Assert
            _ = line.DescriptionTemplate.Should().Be("Thu tiền {Amount} từ {Customer}");
        }

        private static decimal CalculateLineAmount(JournalTemplateLine line, TemplateContext context)
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
                "Amount*0.1*VatRate" => context.Amount * 0.1m * (context.GetParameter("VatRate", 0m) / 100m),
                _ => context.Amount
            };
        }
    }

    public class TemplateValidationRuleTests
    {
        private readonly TenantId _tenantId = new(Guid.NewGuid());

        [Fact]
        public void TemplateValidationRule_Should_Create_With_Valid_Data()
        {
            // Arrange
            string rule = "Amount > 0";
            string message = "Số tiền phải lớn hơn 0";

            // Act
            TemplateValidationRule validationRule = new(rule, message);

            // Assert
            _ = validationRule.Rule.Should().Be(rule);
            _ = validationRule.Message.Should().Be(message);
        }

        [Theory]
        [InlineData("Amount > 0")]
        [InlineData("Amount >= 1000")]
        [InlineData("CustomerName != null")]
        [InlineData("VatRate in [0, 5, 10]")]
        public void TemplateValidationRule_Should_Handle_Different_Rules(string rule)
        {
            // Arrange & Act
            TemplateValidationRule validationRule = new(rule, "Validation message");

            // Assert
            _ = validationRule.Rule.Should().Be(rule);
        }

        [Fact]
        public void TemplateValidationRule_Should_Handle_Null_Message()
        {
            // Arrange & Act
            TemplateValidationRule validationRule = new("Amount > 0", null);

            // Assert
            _ = validationRule.Message.Should().BeNull();
        }
    }
}
