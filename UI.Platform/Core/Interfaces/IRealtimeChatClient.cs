using VanAn.UI.Platform.Realtime;

namespace VanAn.UI.Platform.Core.Interfaces;

/// <summary>
/// Realtime Platform P4 (UI-1): subject-agnostic chat client for UI Platform components.
///
/// Implementations talk to the Gateway generic surface (<c>/api/realtime/conversations/*</c>) and
/// must accept a customer token OR a guest device id (P1 D6 — guests chat by device identity).
/// The subject is a string on purpose ("Order", "Shop", ...): it round-trips 1:1 with the server's
/// <c>RealtimeSubjectType</c> enum name and is what the component renders into SignalR group names.
/// </summary>
public interface IRealtimeChatClient
{
    /// <summary>GET /api/realtime/conversations/{subjectType}/{subjectId} — history, oldest first.</summary>
    Task<RealtimeChatHistoryResult> GetHistoryAsync(
        string subjectType,
        Guid subjectId,
        string? customerToken,
        Guid? customerDeviceId,
        int take = 100,
        CancellationToken ct = default);

    /// <summary>POST /api/realtime/conversations/messages — send a message to a subject's conversation.</summary>
    Task<RealtimeSendResult> SendMessageAsync(
        string subjectType,
        Guid subjectId,
        string content,
        string? customerToken,
        Guid? customerDeviceId,
        CancellationToken ct = default);
}
