using Microsoft.AspNetCore.SignalR;
using VanAn.Shared.Domain;

namespace VanAn.Gateway.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): parses the <c>(subjectType, subjectId)</c> pair shared by the
/// generic hubs. SignalR cannot bind enums or guids from a JS client reliably, so both arrive as
/// strings and are validated here — an unknown subject type is rejected rather than coerced.
/// </summary>
internal static class RealtimeSubjectParser
{
    public static (RealtimeSubjectType SubjectType, Guid SubjectId) Parse(string subjectType, string subjectId)
    {
        if (!Enum.TryParse<RealtimeSubjectType>(subjectType, ignoreCase: true, out var type)
            || !Enum.IsDefined(type))
        {
            throw new HubException($"Invalid subjectType '{subjectType}'.");
        }

        if (!Guid.TryParse(subjectId, out var id) || id == Guid.Empty)
            throw new HubException($"Invalid subjectId '{subjectId}'.");

        return (type, id);
    }
}
