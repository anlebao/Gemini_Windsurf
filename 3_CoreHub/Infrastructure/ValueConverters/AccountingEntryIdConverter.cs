using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

public class AccountingEntryIdConverter : ValueConverter<AccountingEntryId, Guid>
{
    public AccountingEntryIdConverter() : base(
        convertToProviderExpression: id => id != null ? id.Value : Guid.Empty,
        convertFromProviderExpression: value => value != Guid.Empty ? new AccountingEntryId(value) : new AccountingEntryId(Guid.Empty)
    ) { }
}
