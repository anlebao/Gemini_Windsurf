using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

/// <summary>
/// 2-way ValueConverter for LeadId Value Object
/// Ensures proper bidirectional mapping between LeadId and Guid
/// </summary>
public class LeadIdConverter : ValueConverter<LeadId, Guid>
{
    public LeadIdConverter()
        : base(
            v => v.Value,                    // LeadId -> Guid (to database)
            v => new LeadId(v))              // Guid -> LeadId (from database)
    {
    }
}
