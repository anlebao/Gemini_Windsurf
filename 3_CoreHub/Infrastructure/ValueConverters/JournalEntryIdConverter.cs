using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Infrastructure.ValueConverters;

/// <summary>
/// 2-way ValueConverter for JournalEntryId Value Object
/// </summary>
public class JournalEntryIdConverter : ValueConverter<JournalEntryId, Guid>
{
    public JournalEntryIdConverter() : base(
        id => id.Value,
        value => new JournalEntryId(value))
    {
    }
}
