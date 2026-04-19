using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

public class MoneyConverter : ValueConverter<Money, decimal>
{
    public MoneyConverter() : base(
        convertToProviderExpression: money => money != null ? money.Value : 0m,
        convertFromProviderExpression: value => value != 0m ? new Money(value) : null
    ) { }
}
