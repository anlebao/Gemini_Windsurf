using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

/// <summary>
/// 2-way ValueConverter for CustomerId Value Object
/// Ensures proper bidirectional mapping between CustomerId and Guid
/// </summary>
public class CustomerIdConverter : ValueConverter<CustomerId, Guid>
{
    public CustomerIdConverter()
        : base(
            v => v.Value,                    // CustomerId -> Guid (to database)
            v => new CustomerId(v))          // Guid -> CustomerId (from database)
    {
    }
}
