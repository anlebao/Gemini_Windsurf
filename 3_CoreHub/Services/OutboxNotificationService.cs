using System.Text.Json;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;

namespace VanAn.CoreHub.Services
{
    /// <summary>
    /// Outbox-backed notification service.
    /// Implements INotificationService by persisting notification requests as
    /// OutboxEvent entities, which are stored atomically in the CoreOutboxMessage
    /// table via IOutboxRepository. No direct external IO (SMTP, SMS gateway) is
    /// performed — actual dispatch is handled by a separate outbox processor.
    /// </summary>
    public class OutboxNotificationService(IOutboxRepository outboxRepository) : INotificationService
    {
        private readonly IOutboxRepository _outboxRepository = outboxRepository;

        public async Task<bool> SendEmailAsync(string email, string subject, string message)
        {
            var payload = JsonSerializer.Serialize(new
            {
                Channel = "Email",
                Recipient = email,
                Subject = subject,
                Body = message
            });

            var outboxEvent = new OutboxEvent(
                TenantId.Empty,
                new ElectronicInvoiceId(Guid.Empty),
                "NotificationEmail",
                payload);

            await _outboxRepository.EnqueueAsync(outboxEvent);
            return true;
        }

        public async Task<bool> SendSMSAsync(string phoneNumber, string message)
        {
            var payload = JsonSerializer.Serialize(new
            {
                Channel = "SMS",
                Recipient = phoneNumber,
                Body = message
            });

            var outboxEvent = new OutboxEvent(
                TenantId.Empty,
                new ElectronicInvoiceId(Guid.Empty),
                "NotificationSMS",
                payload);

            await _outboxRepository.EnqueueAsync(outboxEvent);
            return true;
        }

        public async Task<bool> SendPushNotificationAsync(Guid customerId, string title, string message)
        {
            var payload = JsonSerializer.Serialize(new
            {
                Channel = "Push",
                CustomerId = customerId,
                Title = title,
                Body = message
            });

            var outboxEvent = new OutboxEvent(
                TenantId.Empty,
                new ElectronicInvoiceId(Guid.Empty),
                "NotificationPush",
                payload);

            await _outboxRepository.EnqueueAsync(outboxEvent);
            return true;
        }
    }
}
