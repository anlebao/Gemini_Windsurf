using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Journal;
using CoreTemplateContext = VanAn.Shared.Domain.TemplateContext;
// Mixed interface requirements: IJournalTemplateRepository from CoreHub.Services, others from Shared.Domain
using IJournalTemplateRepository = VanAn.CoreHub.Services.IJournalTemplateRepository;
using IBusinessRuleRegistry = VanAn.Shared.Domain.IBusinessRuleRegistry;
using ITemplateValidator = VanAn.Shared.Domain.ITemplateValidator;

namespace VanAn.Core.Tests.Accounting
{
    public class EnhancedJournalFactoryTests
    {
        private readonly Mock<IJournalTemplateRepository> _templateRepoMock;
        private readonly Mock<IBusinessRuleRegistry> _ruleRegistryMock;
        private readonly Mock<ITemplateValidator> _validatorMock;
        private readonly EnhancedJournalFactory _factory;
        private readonly TenantId _tenantId = new TenantId(Guid.NewGuid());

        public EnhancedJournalFactoryTests()
        {
            _templateRepoMock = new Mock<IJournalTemplateRepository>();
            _ruleRegistryMock = new Mock<IBusinessRuleRegistry>();
            _validatorMock = new Mock<ITemplateValidator>();
            _factory = new EnhancedJournalFactory(_templateRepoMock.Object, _ruleRegistryMock.Object, _validatorMock.Object, new JournalEntryService(Mock.Of<ILogger<JournalEntryService>>()));
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Create_Journal_From_Template()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "SALE-CASH", "Bán hàng tiền mặt");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SALE-CASH")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            var parameters = new Dictionary<string, object>();
            var amount = 10000m;

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "SALE-CASH", amount, parameters);

            // Assert
            result.Should().NotBeNull();
            result.TenantId.Should().Be(_tenantId);
            result.Description.Should().Be("Bán hàng tiền mặt");
            result.Lines.Should().HaveCount(2);
            result.Lines.ToList().Should().Contain(l => l.AccountNumber == "111" && l.DebitAmount == 10000m);
            result.Lines.ToList().Should().Contain(l => l.AccountNumber == "511" && l.CreditAmount == 10000m);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Apply_Business_Rules()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "SALE-VIP", "Bán hàng khách VIP");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "NetAmount");
            template.AddLine("3331", false, "VatAmount");
            template.AddBusinessRule("VIPDiscountRule");
            template.AddBusinessRule("VATCalculationRule");

            var vipRule = new Mock<IBusinessRule>();
            var vatRule = new Mock<IBusinessRule>();

            vipRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            vatRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SALE-VIP")).ReturnsAsync(template);
            _ruleRegistryMock.Setup(x => x.GetRule("VIPDiscountRule")).Returns(vipRule.Object);
            _ruleRegistryMock.Setup(x => x.GetRule("VATCalculationRule")).Returns(vatRule.Object);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            var parameters = new Dictionary<string, object> { ["IsVIP"] = true, ["VatRate"] = 10m };
            var amount = 10000m;

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "SALE-VIP", amount, parameters);

            // Assert
            result.Should().NotBeNull();
            result.Lines.Should().HaveCount(3);
            
            // Verify rules were applied
            vipRule.Verify(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            vipRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            vatRule.Verify(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            vatRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Complex_Scenarios()
        {
            // Arrange - Complex sale with VIP, export, COGS
            var template = new JournalTemplate(_tenantId, "COMPLEX-SALE", "Bán hàng phức tạp");
            template.AddLine("111", true, "TotalAmount");
            template.AddLine("511", false, "NetAmount");
            template.AddLine("3331", false, "VatAmount");
            template.AddLine("632", true, "COGS");
            template.AddLine("152", false, "COGS");
            template.AddBusinessRule("VIPDiscountRule");
            template.AddBusinessRule("VATCalculationRule");
            template.AddBusinessRule("COGSCalculationRule");

            var vipRule = new Mock<IBusinessRule>();
            var vatRule = new Mock<IBusinessRule>();
            var cogsRule = new Mock<IBusinessRule>();

            vipRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            vatRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            cogsRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "COMPLEX-SALE")).ReturnsAsync(template);
            _ruleRegistryMock.Setup(x => x.GetRule("VIPDiscountRule")).Returns(vipRule.Object);
            _ruleRegistryMock.Setup(x => x.GetRule("VATCalculationRule")).Returns(vatRule.Object);
            _ruleRegistryMock.Setup(x => x.GetRule("COGSCalculationRule")).Returns(cogsRule.Object);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            var parameters = new Dictionary<string, object>
            {
                ["IsVIP"] = true,
                ["VIPLevel"] = "Gold",
                ["VatRate"] = 10m,
                ["COGSPercentage"] = 0.6m
            };
            var amount = 10000m;

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "COMPLEX-SALE", amount, parameters);

            // Assert
            result.Should().NotBeNull();
            result.Lines.Should().HaveCount(5);
            result.Description.Should().Be("Bán hàng phức tạp");

            // Verify all rules were applied
            vipRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            vatRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            cogsRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Map_To_HKD_AccountingEntry()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "SALE-CASH", "Bán hàng tiền mặt");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SALE-CASH")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            var parameters = new Dictionary<string, object>();
            var amount = 10000m;

            // Act
            var journalEntry = await _factory.CreateFromTemplateAsync(_tenantId, "SALE-CASH", amount, parameters);

            // Simulate HKD mapping (this would be done in a separate service)
            var accountingEntry = new VanAn.Core.Domain.AccountingEntry(
                _tenantId,
                new VanAn.Core.Domain.Money(journalEntry.Lines.Sum(l => l.DebitAmount)),
                VanAn.Core.Domain.AccountingEntryType.Revenue,
                VanAn.Core.Domain.VatRate.Ten,
                journalEntry.EntryDate,
                VanAn.Core.Domain.AccountingBookType.RevenueBook,
                journalEntry.Description
            );

            // Assert
            accountingEntry.Should().NotBeNull();
            accountingEntry.Amount.Value.Should().Be(10000m);
            accountingEntry.EntryType.Should().Be(VanAn.Core.Domain.AccountingEntryType.Revenue);
            accountingEntry.TenantId.Should().Be(_tenantId);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Throw_Exception_For_Invalid_Template()
        {
            // Arrange
            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "INVALID")).ReturnsAsync((JournalTemplate)null);

            // Act & Assert
            await Assert.ThrowsAsync<VanAn.Shared.Domain.NotFoundException>(
                () => _factory.CreateFromTemplateAsync(_tenantId, "INVALID", 1000m, new Dictionary<string, object>())
            );
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Validate_Template()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "SALE-CASH", "Test");
            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SALE-CASH")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Failure("Test validation failure"));

            // Act & Assert
            await Assert.ThrowsAsync<VanAn.Shared.Domain.ValidationException>(
                () => _factory.CreateFromTemplateAsync(_tenantId, "SALE-CASH", 1000m, new Dictionary<string, object>())
            );

            _validatorMock.Verify(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()), Times.Once);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Empty_Business_Rules()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "SIMPLE-SALE", "Bán hàng đơn giản");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");
            // No business rules

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SIMPLE-SALE")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "SIMPLE-SALE", 5000m, new Dictionary<string, object>());

            // Assert
            result.Should().NotBeNull();
            result.Lines.Should().HaveCount(2);
            
            // Verify no rules were applied
            _ruleRegistryMock.Verify(x => x.GetRule(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Rule_That_Does_Not_Apply()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "SALE-NONVIP", "Bán hàng không VIP");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");
            template.AddBusinessRule("VIPDiscountRule");

            var vipRule = new Mock<IBusinessRule>();
            vipRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(false); // Rule doesn't apply

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SALE-NONVIP")).ReturnsAsync(template);
            _ruleRegistryMock.Setup(x => x.GetRule("VIPDiscountRule")).Returns(vipRule.Object);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "SALE-NONVIP", 8000m, new Dictionary<string, object>());

            // Assert
            result.Should().NotBeNull();
            result.Lines.Should().HaveCount(2);
            
            // Verify rule was checked but not applied
            vipRule.Verify(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            vipRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Never);
        }

        [Theory]
        [InlineData("Amount")]
        [InlineData("NetAmount")]
        [InlineData("VatAmount")]
        [InlineData("COGS")]
        public async Task EnhancedJournalFactory_Should_Handle_Different_Amount_Formulas(string formula)
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "TEST-FORMULA", "Test formula");
            template.AddLine("111", true, formula);

            // Add business rules to populate context values for formulas that need them
            if (formula == "NetAmount" || formula == "VatAmount" || formula == "COGS")
            {
                var rule = new Mock<IBusinessRule>();
                rule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
                rule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                    .Callback<CoreTemplateContext>(ctx =>
                    {
                        ctx.NetAmount = 9000m;
                        ctx.VatAmount = 1000m;
                        ctx.COGS = 6000m;
                    })
                    .Returns(Task.CompletedTask);
                
                template.AddBusinessRule("TestRule");
                _ruleRegistryMock.Setup(x => x.GetRule("TestRule")).Returns(rule.Object);
            }

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "TEST-FORMULA")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "TEST-FORMULA", 10000m, new Dictionary<string, object>());

            // Assert
            result.Should().NotBeNull();
            result.Lines.Should().HaveCount(1);
            result.Lines.First().AccountNumber.Should().Be("111");
            result.Lines.First().DebitAmount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Verify_All_Mock_Behaviors()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "VERIFY", "Verification Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "VERIFY")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "VERIFY", 1000m, new Dictionary<string, object>());

            // Assert - STRONG VERIFICATION
            _templateRepoMock.Verify(x => x.GetByCodeAsync(_tenantId, "VERIFY"), Times.Once);
            _validatorMock.Verify(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()), Times.Once);
            
            result.Should().NotBeNull();
            result.Lines.Should().HaveCount(2);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Calculate_Complex_Formulas()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "FORMULA", "Formula Test");
            template.AddLine("111", true, "Amount*0.1");
            template.AddLine("3331", false, "Amount*0.05");
            template.AddLine("511", false, "NetAmount");

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "FORMULA")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "FORMULA", 10000m, new Dictionary<string, object>());

            // Assert - Formula calculation verification
            result.Lines.Should().HaveCount(3);
            result.Lines.ToList()[0].DebitAmount.Should().Be(1000m); // 10% of 10000
            result.Lines.ToList()[1].CreditAmount.Should().Be(500m); // 5% of 10000
            result.Lines.ToList()[2].CreditAmount.Should().Be(10000m); // NetAmount
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Update_CoreTemplateContext_State()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "CONTEXT", "Context Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "NetAmount");
            template.AddBusinessRule("VIPDiscountRule");

            var vipRule = new Mock<IBusinessRule>();
            vipRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            vipRule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                   .Callback<CoreTemplateContext>(ctx => ctx.NetAmount = 9000m) // Simulate rule effect
                   .Returns(Task.CompletedTask);

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "CONTEXT")).ReturnsAsync(template);
            _ruleRegistryMock.Setup(x => x.GetRule("VIPDiscountRule")).Returns(vipRule.Object);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "CONTEXT", 10000m, new Dictionary<string, object>());

            // Assert - Context state change verification
            vipRule.Verify(x => x.ApplyAsync(It.Is<CoreTemplateContext>(ctx => ctx.Amount == 10000m)), Times.Once);
            result.Lines.ToList()[1].CreditAmount.Should().Be(9000m); // NetAmount after discount
        }

        [Theory]
        [InlineData("Amount*0.1", 1000, 100)]
        [InlineData("Amount*0.05", 2000, 100)]
        [InlineData("Amount*0.1*VatRate", 10000, 100)] // Assuming VatRate = 10%
        public async Task EnhancedJournalFactory_Should_Handle_Different_Formulas(string formula, decimal amount, decimal expected)
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "FORMULA-TEST", "Formula Test");
            template.AddLine("111", true, formula);
            
            var parameters = formula.Contains("VatRate") ? new Dictionary<string, object> { ["VatRate"] = 10m } :
                             new Dictionary<string, object>();

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "FORMULA-TEST")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "FORMULA-TEST", amount, parameters);

            // Assert
            result.Lines.First().DebitAmount.Should().Be(expected);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Validate_Journal_Balance()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "UNBALANCED", "Unbalanced Template");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount*0.5"); // Creates imbalance

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "UNBALANCED")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => 
                _factory.CreateFromTemplateAsync(_tenantId, "UNBALANCED", 1000m, new Dictionary<string, object>()));
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Validate_Account_Numbers()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "INVALID-ACCOUNT", "Invalid Account Test");
            template.AddLine("INVALID", true, "Amount"); // Invalid account number

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "INVALID-ACCOUNT")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => 
                _factory.CreateFromTemplateAsync(_tenantId, "INVALID-ACCOUNT", 1000m, new Dictionary<string, object>()));
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Multiple_Rules_Chaining()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "CHAIN", "Rule Chaining Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "NetAmount");
            template.AddBusinessRule("VIPDiscountRule");
            template.AddBusinessRule("VATCalculationRule");

            var vipRule = new Mock<IBusinessRule>();
            var vatRule = new Mock<IBusinessRule>();

            vipRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            vatRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);

            // Setup rule chaining - VIP affects NetAmount, VAT calculates on NetAmount
            vipRule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                   .Callback<CoreTemplateContext>(ctx => ctx.NetAmount = 9000m)
                   .Returns(Task.CompletedTask);
            vatRule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                   .Callback<CoreTemplateContext>(ctx => ctx.VatAmount = 900m) // 10% of 9000
                   .Returns(Task.CompletedTask);

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "CHAIN")).ReturnsAsync(template);
            _ruleRegistryMock.Setup(x => x.GetRule("VIPDiscountRule")).Returns(vipRule.Object);
            _ruleRegistryMock.Setup(x => x.GetRule("VATCalculationRule")).Returns(vatRule.Object);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "CHAIN", 10000m, new Dictionary<string, object> { ["VatRate"] = 10m });

            // Assert - Rule chaining verification
            vipRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            vatRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            result.Lines.ToList()[1].CreditAmount.Should().Be(9000m); // After VIP discount
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Rule_Execution_Order()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "ORDER", "Rule Order Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "NetAmount");
            template.AddBusinessRule("FirstRule");
            template.AddBusinessRule("SecondRule");

            var firstRule = new Mock<IBusinessRule>();
            var secondRule = new Mock<IBusinessRule>();

            firstRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            secondRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);

            var executionOrder = new List<string>();
            firstRule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                     .Callback<CoreTemplateContext>(_ => executionOrder.Add("First"))
                     .Returns(Task.CompletedTask);
            secondRule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                      .Callback<CoreTemplateContext>(_ => executionOrder.Add("Second"))
                      .Returns(Task.CompletedTask);

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "ORDER")).ReturnsAsync(template);
            _ruleRegistryMock.Setup(x => x.GetRule("FirstRule")).Returns(firstRule.Object);
            _ruleRegistryMock.Setup(x => x.GetRule("SecondRule")).Returns(secondRule.Object);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            await _factory.CreateFromTemplateAsync(_tenantId, "ORDER", 1000m, new Dictionary<string, object>());

            // Assert - Rule execution order
            executionOrder.Should().ContainInOrder("First", "Second");
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Parameter_Edges()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "EDGE", "Edge Case Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount", "Payment {CustomerName}");

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "EDGE")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            var edgeParameters = new Dictionary<string, object>
            {
                ["CustomerName"] = "",
                ["MissingParam"] = "Value"
            };

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "EDGE", 1000m, edgeParameters);

            // Assert
            result.Lines.ToList()[1].Description.Should().Be("Payment "); // Empty parameter handled
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Validate_Template_Activity()
        {
            // Arrange
            var inactiveTemplate = new JournalTemplate(_tenantId, "INACTIVE", "Inactive Template");
            inactiveTemplate.AddLine("111", true, "Amount");
            // Note: Template would need IsActive property set to false

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "INACTIVE")).ReturnsAsync(inactiveTemplate);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(inactiveTemplate, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Failure("Template is not active"));

            // Act & Assert
            await Assert.ThrowsAsync<ValidationException>(() => 
                _factory.CreateFromTemplateAsync(_tenantId, "INACTIVE", 1000m, new Dictionary<string, object>()));
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Null_Parameters()
        {
            // Arrange
            var template = new JournalTemplate(_tenantId, "NULL-PARAM", "Null Parameter Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount", "Payment {CustomerName}");

            _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "NULL-PARAM")).ReturnsAsync(template);
            _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            var nullParams = new Dictionary<string, object>
            {
                ["CustomerName"] = null
            };

            // Act
            var result = await _factory.CreateFromTemplateAsync(_tenantId, "NULL-PARAM", 1000m, nullParams);

            // Assert
            result.Should().NotBeNull();
            result.Lines.Should().HaveCount(2);
        }
    }
}
