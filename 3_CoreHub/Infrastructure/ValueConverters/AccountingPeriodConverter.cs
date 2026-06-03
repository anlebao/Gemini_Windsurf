using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

public class AccountingPeriodConverter : ValueConverter<AccountingPeriod?, string?>
{
    public AccountingPeriodConverter() : base(
        convertToProviderExpression: period => period != null ? $"{period.Year:D4}-{period.Month:D2}" : null,
        convertFromProviderExpression: value => !string.IsNullOrEmpty(value) ? 
            AccountingPeriod.FromDateTime(DateTime.ParseExact(value + "-01", "yyyy-MM-dd", null)) : null
    ) { }
}
