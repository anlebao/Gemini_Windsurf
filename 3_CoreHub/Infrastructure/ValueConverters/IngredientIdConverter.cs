using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

/// <summary>
/// 2-way ValueConverter for IngredientId Value Object
/// </summary>
public class IngredientIdConverter : ValueConverter<IngredientId, Guid>
{
    public IngredientIdConverter() : base(
        id => id.Value,
        value => new IngredientId(value))
    {
    }
}
