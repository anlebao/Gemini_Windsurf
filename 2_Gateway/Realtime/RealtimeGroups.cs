using VanAn.Shared.Domain;

namespace VanAn.Gateway.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): the single place that names SignalR groups, so a hub that
/// joins and a controller that pushes can never drift apart.
///
/// Naming: <c>msg_{subjectType}_{subjectId}</c> (chat) and <c>loc_{subjectType}_{subjectId}</c>
/// (live location). The subject type is part of the name so an Order and a Shop with the same guid
/// can never share a group. Legacy groups (<c>chat_</c>, <c>order_</c>) stay in the legacy hubs
/// untouched — old KhachLink builds still join those.
///
/// The string overloads exist because a client leaving a group passes whatever it joined with
/// (raw route/JS strings), and re-parsing just to build a name would add a failure mode.
/// </summary>
public static class RealtimeGroups
{
    public static string Messaging(RealtimeSubjectType subjectType, Guid subjectId)
        => Messaging(subjectType.ToString(), subjectId.ToString());

    public static string Messaging(string subjectType, string subjectId)
        => $"msg_{subjectType}_{subjectId}";

    public static string Tracking(RealtimeSubjectType subjectType, Guid subjectId)
        => Tracking(subjectType.ToString(), subjectId.ToString());

    public static string Tracking(string subjectType, string subjectId)
        => $"loc_{subjectType}_{subjectId}";
}
