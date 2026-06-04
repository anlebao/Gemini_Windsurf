using VanAn.CoreHub.Infrastructure.ValueConverters;
using VanAn.Shared.Domain;
using Xunit;
using FluentAssertions;

namespace VanAn.Core.Tests.Infrastructure.ValueConverters
{
    public class AccountingBookTypeConverterTests
    {
        private readonly AccountingBookTypeConverter _converter = new();

        [Fact]
        public void Should_Convert_To_Database_Value()
        {
            // Arrange & Act & Assert for all 4 HKD Books
            _ = _converter.ConvertToProvider(AccountingBookType.RevenueBook).Should().Be(1);
            _ = _converter.ConvertToProvider(AccountingBookType.ExpenseBook).Should().Be(2);
            _ = _converter.ConvertToProvider(AccountingBookType.CashBankBook).Should().Be(3);
            _ = _converter.ConvertToProvider(AccountingBookType.TaxDeclarationBook).Should().Be(4);
        }

        [Fact]
        public void Should_Convert_From_Database_Value()
        {
            // Arrange & Act & Assert for all 4 HKD Books
            _ = _converter.ConvertFromProvider(1).Should().Be(AccountingBookType.RevenueBook);
            _ = _converter.ConvertFromProvider(2).Should().Be(AccountingBookType.ExpenseBook);
            _ = _converter.ConvertFromProvider(3).Should().Be(AccountingBookType.CashBankBook);
            _ = _converter.ConvertFromProvider(4).Should().Be(AccountingBookType.TaxDeclarationBook);
        }

        [Fact]
        public void Should_Handle_All_4_HKD_Book_Types()
        {
            // Arrange
            AccountingBookType[] bookTypes =
            [
                AccountingBookType.RevenueBook,
                AccountingBookType.ExpenseBook,
                AccountingBookType.CashBankBook,
                AccountingBookType.TaxDeclarationBook
            ];

            // Act & Assert
            foreach (AccountingBookType bookType in bookTypes)
            {
                object? dbValue = _converter.ConvertToProvider(bookType);
                object? convertedBack = _converter.ConvertFromProvider(dbValue);
                _ = convertedBack.Should().Be(bookType);
            }
        }
    }
}
