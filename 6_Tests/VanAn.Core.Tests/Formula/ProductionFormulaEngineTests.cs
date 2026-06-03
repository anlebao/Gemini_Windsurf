using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Formula;
using VanAn.CoreHub.Services.Data;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace VanAn.Core.Tests.Formula
{
    /// <summary>
    /// Unit Tests for ProductionFormulaEngine
    /// Tests FINAL DSL syntax stability
    /// </summary>
    public class ProductionFormulaEngineTests
    {
        private readonly Mock<IDataProvider> _mockDataProvider;
        private readonly Mock<ILogger<ProductionFormulaEngine>> _mockLogger;
        private readonly ProductionFormulaEngine _formulaEngine;
        private readonly Guid _testTenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
        
        public ProductionFormulaEngineTests()
        {
            _mockDataProvider = new Mock<IDataProvider>();
            _mockLogger = new Mock<ILogger<ProductionFormulaEngine>>();
            _formulaEngine = new ProductionFormulaEngine(_mockDataProvider.Object, _mockLogger.Object);
        }
        
        private FormulaContext CreateStandardContext()
        {
            return new FormulaContext(_testTenantId, AccountingPeriod.Create(2026, 4));
        }
        
        private Dictionary<string, decimal> CreateStandardVariables()
        {
            return new Dictionary<string, decimal>
            {
                ["Revenue"] = 5000m,
                ["Expense"] = 3000m,
                ["Profit"] = 2000m
            };
        }
        
        [Fact]
        public void Evaluate_SumAccountFormula_ReturnsCorrectValue()
        {
            // Arrange
            var formula = @"SUM_ACCOUNT(""5"", ""Credit"")";
            var context = CreateStandardContext();
            
            var expectedContext = new DataProviderContext(
                new TenantId(_testTenantId),
                AccountingPeriod.Create(2026, 4)
            );
            
            _mockDataProvider.Setup(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), "5", "Credit"))
                .Returns(1000m);
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(1000m, result);
            _mockDataProvider.Verify(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), "5", "Credit"), Times.Once);
        }
        
        [Fact]
        public void Evaluate_SumAccountWithPatternFormula_ReturnsCorrectValue()
        {
            // Arrange
            var formula = @"SUM_ACCOUNT(""511"", ""Debit"")";
            var context = CreateStandardContext();
            
            var expectedContext = new DataProviderContext(
                new TenantId(_testTenantId),
                AccountingPeriod.Create(2026, 4)
            );
            
            _mockDataProvider.Setup(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), "511", "Debit"))
                .Returns(500m);
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(500m, result);
        }
        
        [Fact]
        public void Evaluate_BasicSubtraction_ReturnsCorrectValue()
        {
            // Arrange
            var formula = "TotalRevenue - TotalExpense";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["TotalRevenue"] = 1000m,
                ["TotalExpense"] = 600m
            });
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(400m, result);
        }
        
        [Fact]
        public void Evaluate_BasicAddition_ReturnsCorrectValue()
        {
            // Arrange
            var formula = "TotalRevenue + OtherIncome";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["TotalRevenue"] = 1000m,
                ["OtherIncome"] = 200m
            });
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(1200m, result);
        }
        
        [Fact]
        public void Evaluate_BasicMultiplication_ReturnsCorrectValue()
        {
            // Arrange
            var formula = "TotalRevenue * 0.1";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["TotalRevenue"] = 1000m
            });
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(100m, result);
        }
        
        [Fact]
        public void Evaluate_BasicDivision_ReturnsCorrectValue()
        {
            // Arrange
            var formula = "AnnualRevenue / 12";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["AnnualRevenue"] = 12000m
            });
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(1000m, result);
        }
        
        [Fact]
        public void Evaluate_DirectVariable_ReturnsCorrectValue()
        {
            // Arrange
            var formula = "TotalRevenue";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["TotalRevenue"] = 5000m
            });
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(5000m, result);
        }
        
        [Fact]
        public void Evaluate_ComplexFormula_ReturnsCorrectValue()
        {
            // Arrange
            var formula = @"SUM_ACCOUNT(""5"", ""Credit"") - SUM_ACCOUNT(""6"", ""Debit"")";
            var context = CreateStandardContext();
            
            var expectedContext = new DataProviderContext(
                new TenantId(_testTenantId),
                AccountingPeriod.Create(2026, 4)
            );
            
            _mockDataProvider.Setup(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), "5", "Credit"))
                .Returns(1000m);
            _mockDataProvider.Setup(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), "6", "Debit"))
                .Returns(600m);
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(400m, result);
        }
        
        [Fact]
        public void Evaluate_MissingTenantContext_ThrowsException()
        {
            // Arrange
            var formula = @"SUM_ACCOUNT(""5"", ""Credit"")";
            var variables = new Dictionary<string, decimal>
            {
                ["Revenue"] = 1000m
            };
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _formulaEngine.Evaluate(formula, variables));
            Assert.Contains("Tenant context not found", exception.Message);
        }
        
        [Fact]
        public void Evaluate_MissingPeriodContext_ThrowsException()
        {
            // Arrange
            var formula = @"SUM_ACCOUNT(""5"", ""Credit"")";
            var variables = new Dictionary<string, decimal>
            {
                ["_TenantId"] = (decimal)_testTenantId.GetHashCode()
            };
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _formulaEngine.Evaluate(formula, variables));
            Assert.Contains("Period context not found", exception.Message);
        }
        
        [Fact]
        public void Evaluate_MissingVariable_ThrowsException()
        {
            // Arrange
            var formula = "TotalRevenue - TotalExpense";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["TotalRevenue"] = 1000m
            });
            
            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => _formulaEngine.Evaluate(formula, context));
            Assert.Contains("Variable 'TotalExpense' not found", exception.Message);
        }
        
        [Fact]
        public void Evaluate_DivisionByZero_ThrowsException()
        {
            // Arrange
            var formula = "TotalRevenue / ZeroValue";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["TotalRevenue"] = 1000m,
                ["ZeroValue"] = 0m
            });
            
            // Act & Assert
            Assert.Throws<DivideByZeroException>(() => _formulaEngine.Evaluate(formula, context));
        }
        
        [Fact]
        public void ValidateFormula_ValidSumAccount_ReturnsTrue()
        {
            // Arrange
            var formula = @"SUM_ACCOUNT(""5"", ""Credit"")";
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateFormula_ValidSumAccountWithPattern_ReturnsTrue()
        {
            // Arrange
            var formula = @"SUM_ACCOUNT(""511"", ""Debit"")";
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateFormula_ValidArithmetic_ReturnsTrue()
        {
            // Arrange
            var formula = "TotalRevenue - TotalExpense";
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateFormula_ValidVariable_ReturnsTrue()
        {
            // Arrange
            var formula = "TotalRevenue";
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateFormula_InvalidSumAccountSyntax_ReturnsFalse()
        {
            // Arrange
            var formula = @"SUM_ACCOUNT(5, Credit)"; // Missing quotes
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void ValidateFormula_EmptyFormula_ReturnsFalse()
        {
            // Arrange
            var formula = "";
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void ValidateFormula_NullFormula_ReturnsFalse()
        {
            // Arrange
            string formula = null;
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void GetDependencies_SumAccountFormula_ReturnsCorrectDependencies()
        {
            // Arrange
            var formula = @"SUM_ACCOUNT(""5"", ""Credit"")";
            
            // Act
            var dependencies = _formulaEngine.GetDependencies(formula);
            
            // Assert
            Assert.Single(dependencies);
            Assert.Contains("Account_5_Credit", dependencies);
        }
        
        [Fact]
        public void GetDependencies_ArithmeticFormula_ReturnsCorrectDependencies()
        {
            // Arrange
            var formula = "TotalRevenue - TotalExpense";
            
            // Act
            var dependencies = _formulaEngine.GetDependencies(formula);
            
            // Assert
            Assert.Equal(2, dependencies.Count);
            Assert.Contains("TotalRevenue", dependencies);
            Assert.Contains("TotalExpense", dependencies);
        }
        
        [Fact]
        public void GetDependencies_ComplexFormula_ReturnsCorrectDependencies()
        {
            // Arrange
            var formula = @"SUM_ACCOUNT(""5"", ""Credit"") - SUM_ACCOUNT(""6"", ""Debit"") + TaxAmount";
            
            // Act
            var dependencies = _formulaEngine.GetDependencies(formula);
            
            // Assert
            Assert.Equal(3, dependencies.Count);
            Assert.Contains("Account_5_Credit", dependencies);
            Assert.Contains("Account_6_Debit", dependencies);
            Assert.Contains("TaxAmount", dependencies);
        }
        
        [Fact]
        public void GetDependencies_DirectVariable_ReturnsCorrectDependencies()
        {
            // Arrange
            var formula = "TotalRevenue";
            
            // Act
            var dependencies = _formulaEngine.GetDependencies(formula);
            
            // Assert
            Assert.Single(dependencies);
            Assert.Contains("TotalRevenue", dependencies);
        }
        
        [Fact]
        public void Evaluate_BalanceAccountFormula_ReturnsCorrectValue()
        {
            // Arrange
            var formula = @"BALANCE_ACCOUNT(""156"", ""Debit"")";
            var context = CreateStandardContext();
            
            var expectedContext = new DataProviderContext(
                new TenantId(_testTenantId),
                AccountingPeriod.Create(2026, 4)
            );
            
            _mockDataProvider.Setup(x => x.GetAccountBalance(It.IsAny<DataProviderContext>(), "156"))
                .Returns(2000m);
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(2000m, result);
        }
        
        [Fact]
        public void Evaluate_PercentageFormula_ReturnsCorrectValue()
        {
            // Arrange
            var formula = @"PERCENTAGE(""511"", ""TotalRevenue"")";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["TotalRevenue"] = 10000m
            });
            
            var expectedContext = new DataProviderContext(
                new TenantId(_testTenantId),
                AccountingPeriod.Create(2026, 4)
            );
            
            _mockDataProvider.Setup(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), "511", "Credit"))
                .Returns(2500m);
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(25m, result); // 2500 / 10000 * 100 = 25%
        }
        
        [Fact]
        public void Evaluate_RatioFormula_ReturnsCorrectValue()
        {
            // Arrange
            var formula = @"RATIO(""Cost"", ""Revenue"")";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["Cost"] = 6000m,
                ["Revenue"] = 10000m
            });
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(0.6m, result); // 6000 / 10000 = 0.6
        }
        
        [Fact]
        public void Evaluate_PercentageWithAccountPatterns_ReturnsCorrectValue()
        {
            // Arrange
            var formula = @"PERCENTAGE(""511"", ""5*"")";
            var context = CreateStandardContext();
            
            var expectedContext = new DataProviderContext(
                new TenantId(_testTenantId),
                AccountingPeriod.Create(2026, 4)
            );
            
            _mockDataProvider.Setup(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), "511", "Credit"))
                .Returns(2500m);
            _mockDataProvider.Setup(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), "5*", "Credit"))
                .Returns(10000m);
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(25m, result); // 2500 / 10000 * 100 = 25%
        }
        
        [Fact]
        public void Evaluate_RatioWithAccountPatterns_ReturnsCorrectValue()
        {
            // Arrange
            var formula = @"RATIO(""632"", ""511"")";
            var context = CreateStandardContext();
            
            var expectedContext = new DataProviderContext(
                new TenantId(_testTenantId),
                AccountingPeriod.Create(2026, 4)
            );
            
            _mockDataProvider.Setup(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), "632", "Debit"))
                .Returns(3000m);
            _mockDataProvider.Setup(x => x.GetAccountSum(It.IsAny<DataProviderContext>(), "511", "Credit"))
                .Returns(1000m);
            
            // Act
            var result = _formulaEngine.Evaluate(formula, context);
            
            // Assert
            Assert.Equal(3m, result); // 3000 / 1000 = 3
        }
        
        [Fact]
        public void ValidateFormula_ValidBalanceAccount_ReturnsTrue()
        {
            // Arrange
            var formula = @"BALANCE_ACCOUNT(""156"", ""Debit"")";
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateFormula_ValidPercentage_ReturnsTrue()
        {
            // Arrange
            var formula = @"PERCENTAGE(""511"", ""TotalRevenue"")";
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateFormula_ValidRatio_ReturnsTrue()
        {
            // Arrange
            var formula = @"RATIO(""Cost"", ""Revenue"")";
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.True(result);
        }
        
        [Fact]
        public void ValidateFormula_InvalidBalanceAccountSyntax_ReturnsFalse()
        {
            // Arrange
            var formula = @"BALANCE_ACCOUNT(156, Debit)"; // Missing quotes
            
            // Act
            var result = _formulaEngine.ValidateFormula(formula);
            
            // Assert
            Assert.False(result);
        }
        
        [Fact]
        public void GetDependencies_BalanceAccountFormula_ReturnsCorrectDependencies()
        {
            // Arrange
            var formula = @"BALANCE_ACCOUNT(""156"", ""Debit"")";
            
            // Act
            var dependencies = _formulaEngine.GetDependencies(formula);
            
            // Assert
            Assert.Single(dependencies);
            Assert.Contains("Account_156_Balance", dependencies);
        }
        
        [Fact]
        public void GetDependencies_PercentageFormula_ReturnsCorrectDependencies()
        {
            // Arrange
            var formula = @"PERCENTAGE(""511"", ""TotalRevenue"")";
            
            // Act
            var dependencies = _formulaEngine.GetDependencies(formula);
            
            // Assert
            Assert.Single(dependencies);
            Assert.Contains("TotalRevenue", dependencies);
        }
        
        [Fact]
        public void GetDependencies_RatioFormula_ReturnsCorrectDependencies()
        {
            // Arrange
            var formula = @"RATIO(""Cost"", ""Revenue"")";
            
            // Act
            var dependencies = _formulaEngine.GetDependencies(formula);
            
            // Assert
            Assert.Equal(2, dependencies.Count);
            Assert.Contains("Cost", dependencies);
            Assert.Contains("Revenue", dependencies);
        }
        
        [Fact]
        public void Evaluate_PercentageDivisionByZero_ThrowsException()
        {
            // Arrange
            var formula = @"PERCENTAGE(""511"", ""TotalRevenue"")";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["TotalRevenue"] = 0m // Zero total
            });
            
            // Act & Assert
            Assert.Throws<DivideByZeroException>(() => _formulaEngine.Evaluate(formula, context));
        }
        
        [Fact]
        public void Evaluate_RatioDivisionByZero_ThrowsException()
        {
            // Arrange
            var formula = @"RATIO(""Cost"", ""Revenue"")";
            var context = CreateStandardContext().WithVariables(new Dictionary<string, decimal>
            {
                ["Cost"] = 100m, // Provide valid numerator
                ["Revenue"] = 0m // Zero denominator to trigger DivideByZeroException
            });
            
            // Act & Assert
            Assert.Throws<DivideByZeroException>(() => _formulaEngine.Evaluate(formula, context));
        }
    }
}
