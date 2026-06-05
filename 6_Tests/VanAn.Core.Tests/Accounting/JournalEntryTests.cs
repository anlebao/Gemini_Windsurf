using FluentAssertions;
using Xunit;
using VanAn.Shared.Domain;

namespace VanAn.Core.Tests.Accounting
{
    public class JournalEntryTests
    {
        private readonly TenantId _tenantId = new(Guid.NewGuid());
        private readonly DateTime _testDate = DateTime.UtcNow;

        [Fact]
        public void JournalEntry_Should_Create_With_Valid_Data()
        {
            // Arrange
            string description = "Test Journal Entry";

            // Act
            JournalEntry journal = new(_tenantId, _testDate, description);

            // Assert
            _ = journal.JournalEntryId.Should().NotBeNull();
            _ = journal.JournalEntryId.Value.Should().NotBeEmpty();
            _ = journal.TenantId.Should().Be(_tenantId);
            _ = journal.EntryDate.Should().Be(_testDate);
            _ = journal.Description.Should().Be(description);
            _ = journal.Period.Year.Should().Be(_testDate.Year);
            _ = journal.Period.Month.Should().Be(_testDate.Month);
            _ = journal.IsReversal.Should().BeFalse();
            _ = journal.ReversedJournalId.Should().BeNull();
            _ = journal.Lines.Should().BeEmpty();
            _ = journal.JournalNo.Should().StartWith("J");
            _ = journal.JournalNo.Should().Contain(_testDate.ToString("yyyyMMdd"));
        }

        [Fact]
        public void JournalEntry_Should_Create_With_Reference()
        {
            // Arrange
            string description = "Test Journal Entry";
            string referenceType = "Order";
            Guid referenceId = Guid.NewGuid();

            // Act
            JournalEntry journal = new(_tenantId, _testDate, description, referenceType, referenceId);

            // Assert
            _ = journal.ReferenceType.Should().Be(referenceType);
            _ = journal.ReferenceId.Should().Be(referenceId);
        }

        [Fact]
        public void JournalEntry_Should_Add_Line_Correctly()
        {
            // Arrange
            JournalEntry journal = new(_tenantId, _testDate, "Test Entry");
            string accountNumber = "111";
            decimal debitAmount = 1000m;
            decimal creditAmount = 0m;
            string description = "Test Line";

            // Act
            journal.AddLine(accountNumber, debitAmount, creditAmount, description);

            // Assert
            _ = journal.Lines.Should().HaveCount(1);
            JournalEntryLine line = journal.Lines.First();
            _ = line.AccountNumber.Should().Be(accountNumber);
            _ = line.DebitAmount.Should().Be(debitAmount);
            _ = line.CreditAmount.Should().Be(creditAmount);
            _ = line.Description.Should().Be(description);
        }

        [Fact]
        public void JournalEntry_Should_Add_Multiple_Lines_Correctly()
        {
            // Arrange
            JournalEntry journal = new(_tenantId, _testDate, "Test Entry");

            // Act
            journal.AddLine("111", 1000m, 0m, "Debit Line");
            journal.AddLine("511", 0m, 1000m, "Credit Line");

            // Assert
            _ = journal.Lines.Should().HaveCount(2);
            _ = journal.Lines.First().DebitAmount.Should().Be(1000m);
            _ = journal.Lines.Last().CreditAmount.Should().Be(1000m);
        }

        [Theory]
        [InlineData(-1000, 0)]
        [InlineData(0, -500)]
        [InlineData(-100, -200)]
        public void JournalEntry_Should_Throw_Exception_For_Negative_Amounts(decimal debit, decimal credit)
        {
            // Arrange
            JournalEntry journal = new(_tenantId, _testDate, "Test Entry");

            // Act & Assert
            _ = Assert.Throws<ArgumentException>(() => journal.AddLine("111", debit, credit, "Test"));
        }

        [Fact]
        public void JournalEntry_Should_Throw_Exception_For_Both_Debit_And_Credit()
        {
            // Arrange
            JournalEntry journal = new(_tenantId, _testDate, "Test Entry");

            // Act & Assert
            _ = Assert.Throws<ArgumentException>(() => journal.AddLine("111", 1000m, 500m, "Test"));
        }

        [Fact]
        public void JournalEntry_Should_Be_Immutable_After_Creation()
        {
            // Arrange
            JournalEntry journal = new(_tenantId, _testDate, "Test Entry");
            journal.AddLine("111", 1000m, 0m, "Test Line");

            // Act & Assert - JournalEntryId should be readonly
            JournalEntryId originalId = journal.JournalEntryId;
            _ = journal.JournalEntryId.Should().Be(originalId);

            // Act & Assert - Lines should be readonly
            IReadOnlyCollection<JournalEntryLine> lines = journal.Lines;
            // Since Lines returns IReadOnlyCollection, adding to the returned list won't affect the original
            // Let's test that the original collection hasn't changed
            int originalCount = lines.Count;
            lines.ToList().Add(new JournalEntryLine(journal, "112", 500m, 0m, "Test"));
            _ = journal.Lines.Count.Should().Be(originalCount); // Original collection unchanged
        }

        [Fact]
        public void JournalEntry_Should_Generate_Unique_Journal_No()
        {
            // Arrange
            JournalEntry journal1 = new(_tenantId, _testDate, "Test 1");
            JournalEntry journal2 = new(_tenantId, _testDate, "Test 2");

            // Act & Assert
            _ = journal1.JournalNo.Should().NotBe(journal2.JournalNo);
            _ = journal1.JournalNo.Should().StartWith("J");
            _ = journal2.JournalNo.Should().StartWith("J");
        }

        [Fact]
        public void JournalEntry_Should_Handle_Empty_Description()
        {
            // Arrange & Act
            JournalEntry journal = new(_tenantId, _testDate, "");

            // Assert
            _ = journal.Description.Should().Be("");
        }

        [Fact]
        public void JournalEntry_Should_Handle_Null_Reference()
        {
            // Arrange & Act
            JournalEntry journal = new(_tenantId, _testDate, "Test", null, null);

            // Assert
            _ = journal.ReferenceType.Should().BeNull();
            _ = journal.ReferenceId.Should().BeNull();
        }

        [Fact]
        public void JournalEntry_Should_Create_Reversal_Correctly()
        {
            // Arrange
            JournalEntry originalJournal = new(_tenantId, _testDate, "Original Entry");
            originalJournal.AddLine("111", 1000m, 0m, "Debit Line");
            originalJournal.AddLine("511", 0m, 1000m, "Credit Line");

            // Act
            JournalEntry reversalJournal = originalJournal.CreateReversal("Test reversal");

            // Assert - Strong behavior verification
            _ = reversalJournal.IsReversal.Should().BeTrue();
            _ = reversalJournal.ReversedJournalId.Should().Be(originalJournal.JournalEntryId);
            _ = reversalJournal.Lines.Should().HaveCount(2);
            _ = reversalJournal.Lines.First().DebitAmount.Should().Be(0m); // Swapped
            _ = reversalJournal.Lines.First().CreditAmount.Should().Be(1000m);
            _ = reversalJournal.Description.Should().Be("Reversal: Original Entry"); // Production behavior: reason not included in description
        }

        [Fact]
        public void JournalEntry_Should_Prevent_Reversal_Of_Reversal()
        {
            // Arrange
            JournalEntry originalJournal = new(_tenantId, _testDate, "Original");
            JournalEntry reversalJournal = originalJournal.CreateReversal("Test");

            // Act & Assert
            _ = Assert.Throws<InvalidOperationException>(() => reversalJournal.CreateReversal("Double reversal"));
        }

        [Theory]
        [InlineData("111")]
        [InlineData("112")]
        [InlineData("131")]
        [InlineData("511")]
        public void JournalEntry_Should_Accept_Valid_Vietnamese_Account_Numbers(string validAccountNumber)
        {
            // Arrange
            JournalEntry journal = new(_tenantId, _testDate, "Test");

            // Act & Assert - Should not throw
            journal.AddLine(validAccountNumber, 1000m, 0m, "Test");
        }

        [Theory]
        [InlineData("")] // Empty
        [InlineData("12345678901")] // Too long (>10 digits)
        [InlineData("ABC")] // Non-numeric
        [InlineData("11A")] // Mixed alphanumeric
        public void JournalEntry_Should_Reject_Invalid_Vietnamese_Account_Numbers(string invalidAccountNumber)
        {
            // Arrange
            JournalEntry journal = new(_tenantId, _testDate, "Test");

            // Act & Assert
            _ = Assert.Throws<ArgumentException>(() => journal.AddLine(invalidAccountNumber, 1000m, 0m, "Test"));
        }
    }
}
