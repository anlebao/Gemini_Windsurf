using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

/// <summary>
/// 2-way ValueConverter for ProductId Value Object
/// </summary>
public class ProductIdConverter : ValueConverter<ProductId, Guid>
{
    public ProductIdConverter() : base(
        id => id.Value,
        value => new ProductId(value))
    {
    }
}
