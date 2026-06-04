using Microsoft.Extensions.DependencyInjection;
using VanAn.CoreHub.Interfaces;
using System.Collections.Concurrent;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Background task queue implementation
    /// Phase 2.5: Backend Consolidation
    /// </summary>
    public class BackgroundTaskQueue : IBackgroundTaskQueue, IDisposable
    {
        private readonly ConcurrentQueue<Func<IServiceScope, CancellationToken, Task>> _workItems = new();
        private readonly SemaphoreSlim _signal = new(0);

        public void QueueBackgroundWorkItem(Func<IServiceScope, CancellationToken, Task> workItem)
        {
            ArgumentNullException.ThrowIfNull(workItem);

            _workItems.Enqueue(workItem);
            _ = _signal.Release();
        }

        public async Task<Func<IServiceScope, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);
            _ = _workItems.TryDequeue(out Func<IServiceScope, CancellationToken, Task>? workItem);

            return workItem ?? throw new InvalidOperationException("Work item queue is empty");
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
