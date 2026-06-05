using VanAn.Shared.Domain;
using VanAn.ShopERP.Infrastructure;

namespace VanAn.ShopERP.Services
{
    /// <summary>
    /// ShopERP-specific OrderQueueService for SQLite concurrency testing
    /// Infrastructure service - not part of CoreHub shared services
    /// </summary>
    public class OrderQueueService(ShopERPDbContext context, ILogger<OrderQueueService> logger) : IDisposable
    {
        private readonly ShopERPDbContext _context = context;
        private readonly ILogger<OrderQueueService> _logger = logger;
        private readonly List<Order> _queuedOrders = [];
        private int _processedBatches;
        private TimeSpan _totalProcessingTime;
        private DateTime _lastProcessedAt;
        private int _failedBatches;
        private bool _disposed;

        public async Task EnqueueOrderAsync(Order order)
        {
            _queuedOrders.Add(order);
            await ProcessBatchAsync();
        }

        private async Task ProcessBatchAsync()
        {
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Simulate batch processing
                await Task.Delay(100);
                _processedBatches++;
                _lastProcessedAt = DateTime.UtcNow;
                _totalProcessingTime = stopwatch.Elapsed;
                _logger.LogInformation("Processed batch {BatchCount}", _processedBatches);
            }
            catch
            {
                _failedBatches++;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public async Task<QueueMetrics> GetQueueMetricsAsync()
        {
            await Task.CompletedTask;
            return new QueueMetrics
            {
                ProcessedBatches = _processedBatches,
                AverageProcessingTime = _processedBatches > 0 ? _totalProcessingTime / _processedBatches : TimeSpan.Zero,
                LastProcessedAt = _lastProcessedAt,
                FailedBatches = _failedBatches
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _queuedOrders.Clear();
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
    }

    public class QueueMetrics
    {
        public int ProcessedBatches { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public DateTime LastProcessedAt { get; set; }
        public int FailedBatches { get; set; }
    }
}
