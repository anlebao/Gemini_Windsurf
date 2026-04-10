using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

/// <summary>
/// 2-way ValueConverter for ShopId Value Object
/// </summary>
public class ShopIdConverter : ValueConverter<ShopId, Guid>
{
    public ShopIdConverter() : base(
        id => id.Value,
        value => new ShopId(value))
    {
    }
}
