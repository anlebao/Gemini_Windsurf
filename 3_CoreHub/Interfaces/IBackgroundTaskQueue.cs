using VanAn.Shared.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace VanAn.CoreHub.Interfaces;

/// <summary>
/// Background task queue interface for queueing work items
/// Phase 2.5: Backend Consolidation
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// Queue a background work item
    /// </summary>
    void QueueBackgroundWorkItem(Func<IServiceScope, CancellationToken, Task> workItem);
    
    /// <summary>
    /// Get the next work item from the queue
    /// </summary>
    Task<Func<IServiceScope, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}
