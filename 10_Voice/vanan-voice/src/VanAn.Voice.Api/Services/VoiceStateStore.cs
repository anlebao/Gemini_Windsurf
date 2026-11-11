using System.Collections.Concurrent;
using VanAn.Voice.Domain;

namespace VanAn.Voice.Api.Services;

/// <summary>
/// In-memory runtime state (SRS v1.1 §3.4): active calls + per-user active-call index
/// (FR-007) + user→connections registry (presence derived from connection lifecycle).
/// Single instance by default; Redis replaces this at Phase C when the module scales out.
/// </summary>
public sealed class VoiceStateStore
{
    public sealed class ActiveCall
    {
        public required VoiceCall Call { get; init; }
        public string? CallerConnectionId { get; set; }
        public string? ReceiverConnectionId { get; set; }
        public DateTime RingStartedAt { get; set; }
        public DateTime? ConnectedAt { get; set; }
        public int IceRestartCount { get; set; }
        public CancellationTokenSource? Timer { get; set; }
        public int IceCandidatesCount { get; set; }
    }

    private readonly ConcurrentDictionary<Guid, ActiveCall> _calls = new();
    private readonly ConcurrentDictionary<Guid, Guid> _activeCallByUser = new();   // userId -> callId
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connectionsByUser = new();
    private readonly ConcurrentDictionary<string, Guid> _callByConnection = new(); // connectionId -> callId

    public bool TryAddActive(ActiveCall active, out Guid collidedUserId)
    {
        collidedUserId = Guid.Empty;
        if (!_calls.TryAdd(active.Call.Id, active)) return false;

        if (!_activeCallByUser.TryAdd(active.Call.CallerUserId, active.Call.Id))
        {
            _calls.TryRemove(active.Call.Id, out _);
            collidedUserId = active.Call.CallerUserId;
            return false;
        }
        return true;
    }

    public ActiveCall? Get(Guid callId) => _calls.TryGetValue(callId, out var c) ? c : null;

    public bool HasActiveCall(Guid userId) => _activeCallByUser.ContainsKey(userId);

    public Guid? ActiveCallIdOf(Guid userId) => _activeCallByUser.TryGetValue(userId, out var id) ? id : null;

    public ActiveCall? ActiveCallOf(Guid userId)
        => _activeCallByUser.TryGetValue(userId, out var id) && _calls.TryGetValue(id, out var c) ? c : null;

    public void Remove(Guid callId)
    {
        if (!_calls.TryRemove(callId, out var active)) return;
        _activeCallByUser.TryRemove(active.Call.CallerUserId, out _);
        _activeCallByUser.TryRemove(active.Call.ReceiverUserId, out _);
        if (active.CallerConnectionId != null) _callByConnection.TryRemove(active.CallerConnectionId, out _);
        if (active.ReceiverConnectionId != null) _callByConnection.TryRemove(active.ReceiverConnectionId, out _);
        active.Timer?.Cancel();
    }

    // ---- connection registry (presence) ----

    public void AddConnection(Guid userId, string connectionId)
    {
        _connectionsByUser.AddOrUpdate(userId,
            _ => new HashSet<string> { connectionId },
            (_, set) => { lock (set) set.Add(connectionId); return set; });
    }

    public void RemoveConnection(Guid userId, string connectionId)
    {
        if (_connectionsByUser.TryGetValue(userId, out var set))
            lock (set) { set.Remove(connectionId); }
    }

    public IReadOnlyList<string> ConnectionsOf(Guid userId)
    {
        if (!_connectionsByUser.TryGetValue(userId, out var set))
            return [];
        lock (set)
        {
            return set.ToArray();
        }
    }

    public void MapConnectionToCall(string connectionId, Guid callId) => _callByConnection[connectionId] = callId;

    public Guid? CallIdOfConnection(string connectionId) => _callByConnection.TryGetValue(connectionId, out var id) ? id : null;
}