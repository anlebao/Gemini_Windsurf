using System.Collections.Concurrent;

namespace VanAn.Voice.Api.Services;

/// <summary>
/// Minimal sliding-window rate limiter (SEC-005): StartCall 10/min·60/h, signaling 30 msg/s,
/// missed-call notifications 5/h. Per-user keys; stale windows are dropped lazily.
/// </summary>
public sealed class RateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _windows = new();

    public bool TryAcquire(string key, int max, TimeSpan window, DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var queue = _windows.GetOrAdd(key, _ => new Queue<DateTime>());

        lock (queue)
        {
            while (queue.Count > 0 && now - queue.Peek() > window)
                queue.Dequeue();

            if (queue.Count >= max) return false;

            queue.Enqueue(now);
            return true;
        }
    }
}