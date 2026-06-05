using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters
{
    public class AccountingBookTypeConverter : ValueConverter<AccountingBookType, int>
    {
        public AccountingBookTypeConverter() : base(
            convertToProviderExpression: bookType => (int)bookType,
            convertFromProviderExpression: value => (AccountingBookType)value
        )
        { }
    }
}
