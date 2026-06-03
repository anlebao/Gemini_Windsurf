using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Interfaces;
using System.Collections.Concurrent;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Background task queue implementation
/// Phase 2.5: Backend Consolidation
/// </summary>
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly ConcurrentQueue<Func<IServiceScope, CancellationToken, Task>> _workItems = new();
    private readonly SemaphoreSlim _signal = new(0);
    
    public void QueueBackgroundWorkItem(Func<IServiceScope, CancellationToken, Task> workItem)
    {
        if (workItem == null)
        {
            throw new ArgumentNullException(nameof(workItem));
        }
        
        _workItems.Enqueue(workItem);
        _signal.Release();
    }
    
    public async Task<Func<IServiceScope, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
        _workItems.TryDequeue(out var workItem);
        
        if (workItem == null)
        {
            throw new InvalidOperationException("Work item queue is empty");
        }
        
        return workItem;
    }
}
