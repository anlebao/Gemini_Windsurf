using System.Text.Json;
using Microsoft.Extensions.Logging;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;

namespace VanAn.CoreHub.Services;

/// <summary>
/// Loyalty Points Integrity (Batch 1) — unified PG→SQLite balance mirror publisher.
///
/// Both loyalty earn/spend paths publish the SAME event so ShopERP's LoyaltySyncSubscriber
/// can mirror the PG (Gateway) balance into the local SQLite LoyaltyRewards row:
///   - Silo:     LoyaltyRewardsService.AddPointsAsync / SubtractPointsAsync (was publishing to
///               "loyalty.points.changed" — only consumed by push notifications, never synced → the
///               "points computed but never credited" bug for Gateway-completed orders).
///   - Alliance: AllianceWalletService.Add/Deduct/Refund (previously published directly; now delegated
///               here for one shape).
///
/// Subject:  vanan.cloud.loyalty.changed.{customerDeviceId|customerId}
///           (matches LoyaltySyncSubscriber wildcard "vanan.cloud.loyalty.changed.>"; suffix keeps
///           per-customer ordering, NATS core guarantees ordering per subject)
/// Payload:  { customerDeviceId?, customerId, tenantId, pointBalance, updatedAt, type, points, reason, sourceOrderId? }
///
/// Reliability: same payload is also enqueued to the Outbox (EventTypes.LoyaltyChanged) with
/// RoutingKey = suffix → NatsSyncWorker publishes "vanan.cloud.loyalty.changed.{suffix}" if the
/// direct NATS publish fails (Gateway uses Sync:SubjectPrefix=cloud).
///
/// NOTE: push notifications intentionally keep the legacy "loyalty.points.changed" subject
/// (PushNotificationBackgroundService) — separate concern, unchanged.
/// </summary>
public sealed class LoyaltyBalanceSyncPublisher(
    INatsEventPublisher? natsEventPublisher,
    IOutboxRepository? outboxRepository,
    ILogger<LoyaltyBalanceSyncPublisher> logger)
{
    private readonly INatsEventPublisher? _natsEventPublisher = natsEventPublisher;
    private readonly IOutboxRepository? _outboxRepository = outboxRepository;
    private readonly ILogger<LoyaltyBalanceSyncPublisher> _logger = logger;

    private static readonly JsonSerializerOptions EventJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Publishes a PG→SQLite loyalty balance sync event (direct NATS + Outbox).
    /// </summary>
    /// <param name="customerId">Customer PK (globally unique).</param>
    /// <param name="tenantId">Tenant the transaction occurred at (awarding tenant for EARN, redeeming tenant for SPEND).</param>
    /// <param name="pointBalance">Authoritative (PG) balance AFTER the operation.</param>
    /// <param name="type">EARN | SPEND | ADJUST | REVERSAL — mirrored into SQLite history.</param>
    /// <param name="points">Signed delta (null for legacy balance-only events, e.g. migration).</param>
    /// <param name="reason">Human-readable reason (history entry).</param>
    /// <param name="customerDeviceId">Device identity (subject suffix + device-based match). Null → falls back to customerId.</param>
    /// <param name="sourceOrderId">Order that triggered the change (traceability).</param>
    public async Task PublishAsync(
        Guid customerId,
        Guid tenantId,
        int pointBalance,
        string type,
        int? points,
        string? reason,
        Guid? customerDeviceId = null,
        Guid? sourceOrderId = null,
        CancellationToken cancellationToken = default)
    {
        // Subject suffix must be unique per customer so NATS preserves per-customer ordering.
        string suffix = (customerDeviceId ?? customerId).ToString();

        var payload = new
        {
            customerDeviceId = customerDeviceId?.ToString(),
            customerId,
            tenantId,
            pointBalance,
            updatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            type,
            points,
            reason,
            sourceOrderId
        };

        // 1) Direct NATS publish (fast path — existing pattern in OrderWorkflow/AllianceWallet).
        if (_natsEventPublisher is not null)
        {
            try
            {
                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, EventJsonOptions);
                await _natsEventPublisher.PublishAsync($"vanan.cloud.loyalty.changed.{suffix}", bytes, cancellationToken);
                _logger.LogDebug(
                    "LoyaltyBalanceSync: published subject vanan.cloud.loyalty.changed.{Suffix} (customer={CustomerId}, tenant={TenantId}, type={Type}, points={Points}, balance={Balance})",
                    suffix, customerId, tenantId, type, points?.ToString() ?? "n/a", pointBalance);
            }
            catch (Exception ex)
            {
                // Fire-and-forget — Outbox path below is the reliable fallback.
                _logger.LogWarning(ex, "LoyaltyBalanceSync: direct NATS publish failed for customer {CustomerId}", customerId);
            }
        }

        // 2) Outbox (reliable) — same payload, RoutingKey = suffix → NatsSyncWorker publishes
        //    "vanan.cloud.loyalty.changed.{suffix}" (Gateway prefix "cloud"). Enqueued in the caller's
        //    ambient transaction (same DbContext change tracker) → committed atomically.
        if (_outboxRepository is not null)
        {
            try
            {
                string eventData = JsonSerializer.Serialize(payload, EventJsonOptions);
                var outboxEvent = new OutboxEvent(
                    new TenantId(tenantId),
                    new ElectronicInvoiceId(Guid.Empty),
                    EventTypes.LoyaltyChanged,
                    eventData,
                    routingKey: suffix,
                    correlationId: sourceOrderId);
                await _outboxRepository.EnqueueAsync(outboxEvent, cancellationToken);
                _logger.LogDebug(
                    "LoyaltyBalanceSync: enqueued Outbox event {EventType} routingKey={Suffix} for customer {CustomerId}",
                    EventTypes.LoyaltyChanged, suffix, customerId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LoyaltyBalanceSync: failed to enqueue Outbox event for customer {CustomerId}", customerId);
            }
        }
    }
}
