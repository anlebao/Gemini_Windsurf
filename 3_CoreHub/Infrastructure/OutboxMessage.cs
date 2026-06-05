using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Infrastructure
{
    /// <summary>
    /// Outbox Message entity for reliable asynchronous event processing
    /// Implements Eventual Consistency pattern for Week 1 implementation
    /// </summary>
    public class OutboxMessage : IMustHaveTenant
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string EventData { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? Error { get; set; }
        public int RetryCount { get; set; }
        public DateTime? NextRetryAt { get; set; }

        // IMustHaveTenant implementation
        public TenantId TenantId { get; set; } = new TenantId(Guid.Empty);

        // Properties for EF Core
        public OutboxMessageStatus Status { get; set; }

        // EF Core constructor for materialization
        public OutboxMessage() { }

        public static OutboxMessage Create(
            string eventType,
            string eventData,
            TenantId tenantId)
        {
            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                EventData = eventData,
                CreatedAt = DateTime.UtcNow,
                TenantId = tenantId,
                Status = OutboxMessageStatus.Pending,
                RetryCount = 0,
                NextRetryAt = DateTime.UtcNow
            };
        }

        public void MarkAsProcessed()
        {
            Status = OutboxMessageStatus.Processed;
            ProcessedAt = DateTime.UtcNow;
            Error = null;
            NextRetryAt = null;
        }

        public void MarkAsFailed(string error)
        {
            Status = OutboxMessageStatus.Failed;
            Error = error;
            RetryCount++;

            // Exponential backoff: 1min, 2min, 4min, 8min, 16min, max 1hour
            double delayMinutes = Math.Min(60, Math.Pow(2, RetryCount));
            NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);
        }
    }

    public enum OutboxMessageStatus
    {
        Pending = 0,
        Processing = 1,
        Processed = 2,
        Failed = 3
    }
}
