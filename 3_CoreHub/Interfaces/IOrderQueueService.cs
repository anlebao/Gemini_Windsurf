using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Interfaces;

/// <summary>
/// Order Queue Service interface for background order processing
/// Phase 2.5: Backend Consolidation
/// </summary>
public interface IOrderQueueService
{
    /// <summary>
    /// Queue order for background processing
    /// </summary>
    Task QueueOrderAsync(Order order);
    
    /// <summary>
    /// Process queued orders
    /// </summary>
    Task ProcessQueuedOrdersAsync(CancellationToken cancellationToken);
    
    /// <summary>
    /// Get queue status
    /// </summary>
    Task<int> GetQueueCountAsync();
}
