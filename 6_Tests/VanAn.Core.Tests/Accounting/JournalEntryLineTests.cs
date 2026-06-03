using System;
using FluentAssertions;
using Xunit;
using VanAn.Shared.Domain;

namespace VanAn.Core.Tests.Accounting
{
    public class JournalEntryLineTests
    {
        private readonly TenantId _tenantId = new TenantId(Guid.NewGuid());
        private readonly DateTime _testDate = DateTime.UtcNow;
        private JournalEntry _journal;

        public JournalEntryLineTests()
        {
            _journal = new JournalEntry(_tenantId, _testDate, "Test Journal");
        }

        [Fact]
        public void JournalEntryLine_Should_Create_With_Valid_Data()
        {
            // Arrange
            var accountNumber = "111";
            var debitAmount = 1000m;
            var creditAmount = 0m;
            var description = "Test Line";

            // Act
            var line = new JournalEntryLine(_journal, accountNumber, debitAmount, creditAmount, description);

            // Assert
            line.AccountNumber.Should().Be(accountNumber);
            line.DebitAmount.Should().Be(debitAmount);
            line.CreditAmount.Should().Be(creditAmount);
            line.Description.Should().Be(description);
            line.JournalEntryId.Should().Be(_journal.JournalEntryId.Value);
        }

        [Fact]
        public void JournalEntryLine_Should_Create_With_Null_Description()
        {
            // Arrange
            var accountNumber = "111";
            var debitAmount = 1000m;
            var creditAmount = 0m;

            // Act
            var line = new JournalEntryLine(_journal, accountNumber, debitAmount, creditAmount, null);

            // Assert
            line.Description.Should().BeNull();
        }

        [Fact]
        public void JournalEntryLine_Should_Create_With_Empty_Description()
        {
            // Arrange
            var accountNumber = "111";
            var debitAmount = 1000m;
            var creditAmount = 0m;
            var description = "";

            // Act
            var line = new JournalEntryLine(_journal, accountNumber, debitAmount, creditAmount, description);

            // Assert
            line.Description.Should().Be("");
        }

        [Theory]
        [InlineData("111", 1000, 0)]
        [InlineData("112", 0, 500)]
        [InlineData("131", 10000, 0)]
        [InlineData("511", 0, 2500)]
        public void JournalEntryLine_Should_Handle_Valid_Account_Numbers(string accountNumber, decimal debit, decimal credit)
        {
            // Arrange & Act
            var line = new JournalEntryLine(_journal, accountNumber, debit, credit, "Test");

            // Assert
            line.AccountNumber.Should().Be(accountNumber);
            line.DebitAmount.Should().Be(debit);
            line.CreditAmount.Should().Be(credit);
        }

        [Theory]
        [InlineData("", 1000, 0)]
        [InlineData("   ", 1000, 0)]
        [InlineData(null, 1000, 0)]
        public void JournalEntryLine_Should_Handle_Invalid_Account_Numbers(string accountNumber, decimal debit, decimal credit)
        {
            // Arrange & Act
            var line = new JournalEntryLine(_journal, accountNumber, debit, credit, "Test");

            // Assert - Should still create but account number will be empty/null
            line.AccountNumber.Should().Be(accountNumber);
        }

        [Fact]
        public void JournalEntryLine_Should_Assign_Correct_JournalEntryId()
        {
            // Arrange
            var journalId = _journal.JournalEntryId.Value;

            // Act
            var line = new JournalEntryLine(_journal, "111", 1000m, 0m, "Test");

            // Assert
            line.JournalEntryId.Should().Be(journalId);
        }

        [Fact]
        public void JournalEntryLine_Should_Handle_Zero_Amounts()
        {
            // Arrange & Act
            var line = new JournalEntryLine(_journal, "111", 0m, 0m, "Test");

            // Assert
            line.DebitAmount.Should().Be(0m);
            line.CreditAmount.Should().Be(0m);
        }

        [Fact]
        public void JournalEntryLine_Should_Handle_Large_Amounts()
        {
            // Arrange
            var largeAmount = 999999999.99m;

            // Act
            var line = new JournalEntryLine(_journal, "111", largeAmount, 0m, "Test");

            // Assert
            line.DebitAmount.Should().Be(largeAmount);
        }

        [Fact]
        public void JournalEntryLine_Should_Handle_Decimal_Precision()
        {
            // Arrange
            var preciseAmount = 1234.5678m;

            // Act
            var line = new JournalEntryLine(_journal, "111", preciseAmount, 0m, "Test");

            // Assert
            line.DebitAmount.Should().Be(preciseAmount);
        }

        [Theory]
        [InlineData(1000, 500)] // Both positive
        [InlineData(500, 300)] // Both positive
        [InlineData(100, 50)]  // Both positive
        public void JournalEntryLine_Should_Throw_For_Both_Debit_And_Credit_Positive(decimal debit, decimal credit)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => 
                new JournalEntryLine(_journal, "111", debit, credit, "Test"));
        }

        [Theory]
        [InlineData("AB")]
        [InlineData("12345678901")]
        [InlineData("ABC123")]
        [InlineData("11-22")]
        public void JournalEntryLine_Should_Accept_Invalid_Account_Formats_In_Constructor(string invalidAccount)
        {
            // Arrange & Act & Assert
            // Note: Line constructor doesn't validate, but AddLine in JournalEntry does
            // This test documents current behavior for future improvement
            var line = new JournalEntryLine(_journal, invalidAccount, 1000m, 0m, "Test");
            line.AccountNumber.Should().Be(invalidAccount);
        }

        [Fact]
        public void JournalEntryLine_Should_Maintain_Immutability()
        {
            // Arrange
            var line = new JournalEntryLine(_journal, "111", 1000m, 0m, "Test");

            // Act & Assert - All properties should be readonly
            line.AccountNumber.Should().Be("111");
            line.DebitAmount.Should().Be(1000m);
            line.CreditAmount.Should().Be(0m);
            line.JournalEntryId.Should().Be(_journal.JournalEntryId.Value);
        }

        [Fact]
        public void JournalEntryLine_Should_Handle_Negative_Zero()
        {
            // Arrange
            var negativeZero = -0.0m;

            // Act
            var line = new JournalEntryLine(_journal, "111", negativeZero, 0m, "Test");

            // Assert
            line.DebitAmount.Should().Be(0m); // Should normalize to 0
        }

        [Theory]
        [InlineData(0.0001)]
        [InlineData(0.01)]
        [InlineData(0.1)]
        public void JournalEntryLine_Should_Handle_Small_Amounts(decimal smallAmount)
        {
            // Arrange & Act
            var line = new JournalEntryLine(_journal, "111", smallAmount, 0m, "Test");

            // Assert
            line.DebitAmount.Should().Be(smallAmount);
        }

        [Theory]
        [InlineData(-1000, 0)]
        [InlineData(0, -500)]
        [InlineData(-100, -200)]
        public void JournalEntryLine_Should_Throw_For_Negative_Amounts(decimal debit, decimal credit)
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => 
                new JournalEntryLine(_journal, "111", debit, credit, "Test"));
        }
    }
}
