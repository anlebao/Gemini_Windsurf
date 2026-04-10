using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

/// <summary>
/// 2-way ValueConverter for RecipeId Value Object
/// </summary>
public class RecipeIdConverter : ValueConverter<RecipeId, Guid>
{
    public RecipeIdConverter() : base(
        id => id.Value,
        value => new RecipeId(value))
    {
    }
}
