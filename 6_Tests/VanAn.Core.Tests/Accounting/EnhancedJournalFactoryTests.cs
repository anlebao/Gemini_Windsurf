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
        private readonly TenantId _tenantId = new(Guid.NewGuid());

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
            JournalTemplate template = new(_tenantId, "SALE-CASH", "Bán hàng tiền mặt");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SALE-CASH")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            Dictionary<string, object> parameters = [];
            decimal amount = 10000m;

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "SALE-CASH", amount, parameters);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.TenantId.Should().Be(_tenantId);
            _ = result.Description.Should().Be("Bán hàng tiền mặt");
            _ = result.Lines.Should().HaveCount(2);
            _ = result.Lines.ToList().Should().Contain(l => l.AccountNumber == "111" && l.DebitAmount == 10000m);
            _ = result.Lines.ToList().Should().Contain(l => l.AccountNumber == "511" && l.CreditAmount == 10000m);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Apply_Business_Rules()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "SALE-VIP", "Bán hàng khách VIP");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "NetAmount");
            template.AddLine("3331", false, "VatAmount");
            template.AddBusinessRule("VIPDiscountRule");
            template.AddBusinessRule("VATCalculationRule");

            Mock<IBusinessRule> vipRule = new();
            Mock<IBusinessRule> vatRule = new();

            _ = vipRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            _ = vatRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SALE-VIP")).ReturnsAsync(template);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("VIPDiscountRule")).Returns(vipRule.Object);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("VATCalculationRule")).Returns(vatRule.Object);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            Dictionary<string, object> parameters = new() { ["IsVIP"] = true, ["VatRate"] = 10m };
            decimal amount = 10000m;

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "SALE-VIP", amount, parameters);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.Lines.Should().HaveCount(3);

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
            JournalTemplate template = new(_tenantId, "COMPLEX-SALE", "Bán hàng phức tạp");
            template.AddLine("111", true, "TotalAmount");
            template.AddLine("511", false, "NetAmount");
            template.AddLine("3331", false, "VatAmount");
            template.AddLine("632", true, "COGS");
            template.AddLine("152", false, "COGS");
            template.AddBusinessRule("VIPDiscountRule");
            template.AddBusinessRule("VATCalculationRule");
            template.AddBusinessRule("COGSCalculationRule");

            Mock<IBusinessRule> vipRule = new();
            Mock<IBusinessRule> vatRule = new();
            Mock<IBusinessRule> cogsRule = new();

            _ = vipRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            _ = vatRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            _ = cogsRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "COMPLEX-SALE")).ReturnsAsync(template);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("VIPDiscountRule")).Returns(vipRule.Object);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("VATCalculationRule")).Returns(vatRule.Object);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("COGSCalculationRule")).Returns(cogsRule.Object);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            Dictionary<string, object> parameters = new()
            {
                ["IsVIP"] = true,
                ["VIPLevel"] = "Gold",
                ["VatRate"] = 10m,
                ["COGSPercentage"] = 0.6m
            };
            decimal amount = 10000m;

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "COMPLEX-SALE", amount, parameters);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.Lines.Should().HaveCount(5);
            _ = result.Description.Should().Be("Bán hàng phức tạp");

            // Verify all rules were applied
            vipRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            vatRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            cogsRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Map_To_HKD_AccountingEntry()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "SALE-CASH", "Bán hàng tiền mặt");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SALE-CASH")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            Dictionary<string, object> parameters = [];
            decimal amount = 10000m;

            // Act
            JournalEntry journalEntry = await _factory.CreateFromTemplateAsync(_tenantId, "SALE-CASH", amount, parameters);

            // Simulate HKD mapping (this would be done in a separate service)
            Core.Domain.AccountingEntry accountingEntry = new(
                _tenantId,
                new Core.Domain.Money(journalEntry.Lines.Sum(l => l.DebitAmount)),
                Core.Domain.AccountingEntryType.Revenue,
                Core.Domain.VatRate.Ten,
                journalEntry.EntryDate,
                Core.Domain.AccountingBookType.RevenueBook,
                journalEntry.Description
            );

            // Assert
            _ = accountingEntry.Should().NotBeNull();
            _ = accountingEntry.Amount.Value.Should().Be(10000m);
            _ = accountingEntry.EntryType.Should().Be(Core.Domain.AccountingEntryType.Revenue);
            _ = accountingEntry.TenantId.Should().Be(_tenantId);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Throw_Exception_For_Invalid_Template()
        {
            // Arrange
            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "INVALID")).ReturnsAsync((JournalTemplate)null);

            // Act & Assert
            _ = await Assert.ThrowsAsync<NotFoundException>(
                () => _factory.CreateFromTemplateAsync(_tenantId, "INVALID", 1000m, [])
            );
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Validate_Template()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "SALE-CASH", "Test");
            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SALE-CASH")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Failure("Test validation failure"));

            // Act & Assert
            _ = await Assert.ThrowsAsync<ValidationException>(
                () => _factory.CreateFromTemplateAsync(_tenantId, "SALE-CASH", 1000m, [])
            );

            _validatorMock.Verify(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()), Times.Once);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Empty_Business_Rules()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "SIMPLE-SALE", "Bán hàng đơn giản");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");
            // No business rules

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SIMPLE-SALE")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "SIMPLE-SALE", 5000m, []);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.Lines.Should().HaveCount(2);

            // Verify no rules were applied
            _ruleRegistryMock.Verify(x => x.GetRule(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Rule_That_Does_Not_Apply()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "SALE-NONVIP", "Bán hàng không VIP");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");
            template.AddBusinessRule("VIPDiscountRule");

            Mock<IBusinessRule> vipRule = new();
            _ = vipRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(false); // Rule doesn't apply

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "SALE-NONVIP")).ReturnsAsync(template);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("VIPDiscountRule")).Returns(vipRule.Object);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "SALE-NONVIP", 8000m, []);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.Lines.Should().HaveCount(2);

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
            JournalTemplate template = new(_tenantId, "TEST-FORMULA", "Test formula");
            template.AddLine("111", true, formula);

            // Add business rules to populate context values for formulas that need them
            if (formula is "NetAmount" or "VatAmount" or "COGS")
            {
                Mock<IBusinessRule> rule = new();
                _ = rule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
                _ = rule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                    .Callback<CoreTemplateContext>(ctx =>
                    {
                        ctx.NetAmount = 9000m;
                        ctx.VatAmount = 1000m;
                        ctx.COGS = 6000m;
                    })
                    .Returns(Task.CompletedTask);

                template.AddBusinessRule("TestRule");
                _ = _ruleRegistryMock.Setup(x => x.GetRule("TestRule")).Returns(rule.Object);
            }

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "TEST-FORMULA")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>())).ReturnsAsync(ValidationResult.Success());

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "TEST-FORMULA", 10000m, []);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.Lines.Should().HaveCount(1);
            _ = result.Lines.First().AccountNumber.Should().Be("111");
            _ = result.Lines.First().DebitAmount.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Verify_All_Mock_Behaviors()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "VERIFY", "Verification Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount");

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "VERIFY")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "VERIFY", 1000m, []);

            // Assert - STRONG VERIFICATION
            _templateRepoMock.Verify(x => x.GetByCodeAsync(_tenantId, "VERIFY"), Times.Once);
            _validatorMock.Verify(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()), Times.Once);

            _ = result.Should().NotBeNull();
            _ = result.Lines.Should().HaveCount(2);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Calculate_Complex_Formulas()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "FORMULA", "Formula Test");
            template.AddLine("111", true, "Amount*0.1");
            template.AddLine("3331", false, "Amount*0.05");
            template.AddLine("511", false, "NetAmount");

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "FORMULA")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "FORMULA", 10000m, []);

            // Assert - Formula calculation verification
            _ = result.Lines.Should().HaveCount(3);
            _ = result.Lines.ToList()[0].DebitAmount.Should().Be(1000m); // 10% of 10000
            _ = result.Lines.ToList()[1].CreditAmount.Should().Be(500m); // 5% of 10000
            _ = result.Lines.ToList()[2].CreditAmount.Should().Be(10000m); // NetAmount
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Update_CoreTemplateContext_State()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "CONTEXT", "Context Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "NetAmount");
            template.AddBusinessRule("VIPDiscountRule");

            Mock<IBusinessRule> vipRule = new();
            _ = vipRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            _ = vipRule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                   .Callback<CoreTemplateContext>(ctx => ctx.NetAmount = 9000m) // Simulate rule effect
                   .Returns(Task.CompletedTask);

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "CONTEXT")).ReturnsAsync(template);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("VIPDiscountRule")).Returns(vipRule.Object);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "CONTEXT", 10000m, []);

            // Assert - Context state change verification
            vipRule.Verify(x => x.ApplyAsync(It.Is<CoreTemplateContext>(ctx => ctx.Amount == 10000m)), Times.Once);
            _ = result.Lines.ToList()[1].CreditAmount.Should().Be(9000m); // NetAmount after discount
        }

        [Theory]
        [InlineData("Amount*0.1", 1000, 100)]
        [InlineData("Amount*0.05", 2000, 100)]
        [InlineData("Amount*0.1*VatRate", 10000, 100)] // Assuming VatRate = 10%
        public async Task EnhancedJournalFactory_Should_Handle_Different_Formulas(string formula, decimal amount, decimal expected)
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "FORMULA-TEST", "Formula Test");
            template.AddLine("111", true, formula);

            Dictionary<string, object> parameters = formula.Contains("VatRate") ? new Dictionary<string, object> { ["VatRate"] = 10m } :
                             [];

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "FORMULA-TEST")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "FORMULA-TEST", amount, parameters);

            // Assert
            _ = result.Lines.First().DebitAmount.Should().Be(expected);
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Validate_Journal_Balance()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "UNBALANCED", "Unbalanced Template");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount*0.5"); // Creates imbalance

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "UNBALANCED")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ValidationException>(() =>
                _factory.CreateFromTemplateAsync(_tenantId, "UNBALANCED", 1000m, []));
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Validate_Account_Numbers()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "INVALID-ACCOUNT", "Invalid Account Test");
            template.AddLine("INVALID", true, "Amount"); // Invalid account number

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "INVALID-ACCOUNT")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ValidationException>(() =>
                _factory.CreateFromTemplateAsync(_tenantId, "INVALID-ACCOUNT", 1000m, []));
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Multiple_Rules_Chaining()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "CHAIN", "Rule Chaining Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "NetAmount");
            template.AddBusinessRule("VIPDiscountRule");
            template.AddBusinessRule("VATCalculationRule");

            Mock<IBusinessRule> vipRule = new();
            Mock<IBusinessRule> vatRule = new();

            _ = vipRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            _ = vatRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);

            // Setup rule chaining - VIP affects NetAmount, VAT calculates on NetAmount
            _ = vipRule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                   .Callback<CoreTemplateContext>(ctx => ctx.NetAmount = 9000m)
                   .Returns(Task.CompletedTask);
            _ = vatRule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                   .Callback<CoreTemplateContext>(ctx => ctx.VatAmount = 900m) // 10% of 9000
                   .Returns(Task.CompletedTask);

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "CHAIN")).ReturnsAsync(template);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("VIPDiscountRule")).Returns(vipRule.Object);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("VATCalculationRule")).Returns(vatRule.Object);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "CHAIN", 10000m, new Dictionary<string, object> { ["VatRate"] = 10m });

            // Assert - Rule chaining verification
            vipRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            vatRule.Verify(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()), Times.Once);
            _ = result.Lines.ToList()[1].CreditAmount.Should().Be(9000m); // After VIP discount
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Rule_Execution_Order()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "ORDER", "Rule Order Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "NetAmount");
            template.AddBusinessRule("FirstRule");
            template.AddBusinessRule("SecondRule");

            Mock<IBusinessRule> firstRule = new();
            Mock<IBusinessRule> secondRule = new();

            _ = firstRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);
            _ = secondRule.Setup(x => x.ShouldApplyAsync(It.IsAny<CoreTemplateContext>())).ReturnsAsync(true);

            List<string> executionOrder = [];
            _ = firstRule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                     .Callback<CoreTemplateContext>(_ => executionOrder.Add("First"))
                     .Returns(Task.CompletedTask);
            _ = secondRule.Setup(x => x.ApplyAsync(It.IsAny<CoreTemplateContext>()))
                      .Callback<CoreTemplateContext>(_ => executionOrder.Add("Second"))
                      .Returns(Task.CompletedTask);

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "ORDER")).ReturnsAsync(template);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("FirstRule")).Returns(firstRule.Object);
            _ = _ruleRegistryMock.Setup(x => x.GetRule("SecondRule")).Returns(secondRule.Object);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            // Act
            _ = await _factory.CreateFromTemplateAsync(_tenantId, "ORDER", 1000m, []);

            // Assert - Rule execution order
            _ = executionOrder.Should().ContainInOrder("First", "Second");
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Parameter_Edges()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "EDGE", "Edge Case Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount", "Payment {CustomerName}");

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "EDGE")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            Dictionary<string, object> edgeParameters = new()
            {
                ["CustomerName"] = "",
                ["MissingParam"] = "Value"
            };

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "EDGE", 1000m, edgeParameters);

            // Assert
            _ = result.Lines.ToList()[1].Description.Should().Be("Payment "); // Empty parameter handled
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Validate_Template_Activity()
        {
            // Arrange
            JournalTemplate inactiveTemplate = new(_tenantId, "INACTIVE", "Inactive Template");
            inactiveTemplate.AddLine("111", true, "Amount");
            // Note: Template would need IsActive property set to false

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "INACTIVE")).ReturnsAsync(inactiveTemplate);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(inactiveTemplate, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Failure("Template is not active"));

            // Act & Assert
            _ = await Assert.ThrowsAsync<ValidationException>(() =>
                _factory.CreateFromTemplateAsync(_tenantId, "INACTIVE", 1000m, []));
        }

        [Fact]
        public async Task EnhancedJournalFactory_Should_Handle_Null_Parameters()
        {
            // Arrange
            JournalTemplate template = new(_tenantId, "NULL-PARAM", "Null Parameter Test");
            template.AddLine("111", true, "Amount");
            template.AddLine("511", false, "Amount", "Payment {CustomerName}");

            _ = _templateRepoMock.Setup(x => x.GetByCodeAsync(_tenantId, "NULL-PARAM")).ReturnsAsync(template);
            _ = _validatorMock.Setup(x => x.ValidateTemplateAsync(template, It.IsAny<Dictionary<string, object>>()))
                           .ReturnsAsync(ValidationResult.Success());

            Dictionary<string, object> nullParams = new()
            {
                ["CustomerName"] = null
            };

            // Act
            JournalEntry result = await _factory.CreateFromTemplateAsync(_tenantId, "NULL-PARAM", 1000m, nullParams);

            // Assert
            _ = result.Should().NotBeNull();
            _ = result.Lines.Should().HaveCount(2);
        }
    }
}
