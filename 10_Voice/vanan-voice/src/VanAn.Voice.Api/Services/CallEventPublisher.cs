using Microsoft.AspNetCore.SignalR;
using VanAn.Voice.Api.Hubs;

namespace VanAn.Voice.Api.Services;

/// <summary>
/// Sends server→client events to call participants. Implemented on top of
/// <see cref="IHubContext{VoiceHub}"/> so CallService stays hub-agnostic and testable
/// (tests swap in a recording fake).
/// </summary>
public interface ICallEventPublisher
{
    Task SendToAsync(string? connectionId, string method, object? arg);
    Task SendToConnectionsAsync(IEnumerable<string> connectionIds, string method, object? arg);
}

public sealed class CallEventPublisher(IHubContext<VoiceHub> hub) : ICallEventPublisher
{
    public Task SendToAsync(string? connectionId, string method, object? arg)
        => string.IsNullOrEmpty(connectionId)
            ? Task.CompletedTask
            : hub.Clients.Client(connectionId).SendAsync(method, arg);

    public Task SendToConnectionsAsync(IEnumerable<string> connectionIds, string method, object? arg)
    {
        var ids = connectionIds.Where(id => !string.IsNullOrEmpty(id)).ToArray();
        return ids.Length == 0 ? Task.CompletedTask : hub.Clients.Clients(ids).SendAsync(method, arg);
    }
}