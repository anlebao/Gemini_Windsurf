using VanAn.Atrs.Infra;

namespace VanAn.Atrs.Tests.Services;

/// <summary>Stub notification writer — ghi lại calls để assert; không đụng DB.</summary>
public sealed class StubNotificationWriter : IAtrsNotificationWriter
{
    public List<(Guid TrustSubjectId, string EventType, string Title, string Body)> Calls { get; } = [];

    public Task NotifyAsync(Guid trustSubjectId, string eventType, string title, string body, string? link = null, CancellationToken ct = default)
    {
        Calls.Add((trustSubjectId, eventType, title, body));
        return Task.CompletedTask;
    }
}