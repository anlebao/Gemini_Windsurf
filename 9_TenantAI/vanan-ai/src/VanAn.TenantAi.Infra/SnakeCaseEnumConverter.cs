using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace VanAn.TenantAi.Infra;

/// <summary>
/// Stores enum values as snake_case strings (PendingReview → pending_review) so DB
/// values match the SRS §7 literal contract. Reversible for reads.
/// </summary>
public sealed class SnakeCaseEnumConverter<T>() : ValueConverter<T, string>(
    v => ToSnake(v),
    v => FromSnake(v))
    where T : struct, Enum
{
    private static string ToSnake(T value)
    {
        var name = value.ToString();
        var sb = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c)) sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static T FromSnake(string value)
    {
        var pascal = string.Concat(value.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        return Enum.Parse<T>(pascal);
    }
}
