using VanAn.Shared.Domain.Common;

namespace VanAn.Shared.Domain
{
    /// <summary>
    /// Outbox Message entity for reliable event publishing
    /// Implements atomic transaction pattern with orders
    /// </summary>
    public sealed class OutboxMessage : BaseEntity
    {
        public string EventType { get; private set; } = string.Empty;
        public string Payload { get; private set; } = string.Empty;
        public DateTime OccurredOn { get; private set; }
        public DateTime? ProcessedOn { get; private set; }
        public int RetryCount { get; private set; }
        public string? LastError { get; private set; }
        public DateTime? NextRetryAt { get; private set; }

        private OutboxMessage() { }

        /// <summary>
        /// Creates new outbox message with event data
        /// </summary>
        public static OutboxMessage Create(string eventType, object payload)
        {
            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                Payload = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                }),
                OccurredOn = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Marks message as successfully processed
        /// </summary>
        public void MarkAsProcessed()
        {
            ProcessedOn = DateTime.UtcNow;
            NextRetryAt = null;
            LastError = null;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks message as failed with retry schedule
        /// </summary>
        public void MarkAsFailed(string error)
        {
            RetryCount++;
            LastError = error;

            // Exponential backoff: 1, 2, 4, 8, 16 minutes
            double delayMinutes = Math.Min(60, Math.Pow(2, RetryCount));
            NextRetryAt = DateTime.UtcNow.AddMinutes(delayMinutes);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Event type constants for type safety
    /// </summary>
    public static class EventTypes
    {
        public const string OrderCompleted = "OrderCompleted";
        public const string AccountingEntryCreated = "AccountingEntryCreated";
        public const string HKDBooksGenerated = "HKDBooksGenerated";
    }
}
