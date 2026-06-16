using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VanAn.CoreHub.Infrastructure;
using VanAn.CoreHub.Infrastructure.Messaging;
using VanAn.Shared.Domain;
using VanAn.Shared.DTOs;

namespace VanAn.CoreHub.Services.Orchestration;

/// <summary>
/// WebhookService - Webhook processing with idempotency and typed DTO contracts
/// Focused service: ONLY webhook processing
/// Supports Viettel and MISA webhook formats with strict JSON parsing
/// </summary>
public class WebhookService : IWebhookService
{
    private readonly ConcurrentDictionary<string, bool> _processedKeys = new();
    private readonly VanAnDbContext? _dbContext;
    private readonly ILogger<WebhookService> _logger;

    // JSON deserialization options with strict settings
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public WebhookService() : this(null, NullLogger<WebhookService>.Instance) { }

    public WebhookService(VanAnDbContext? dbContext, ILogger<WebhookService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [Obsolete("Use WebhookService(VanAnDbContext, ILogger) — outboxRepository param removed in F5")]
    public WebhookService(VanAnDbContext? dbContext, IOutboxRepository? _, ILogger<WebhookService> logger)
        : this(dbContext, logger) { }

    public async Task ProcessWebhookAsync(
        string providerId,
        string providerInvoiceNumber,
        string callbackData,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentException("ProviderId is required.", nameof(providerId));
        if (string.IsNullOrWhiteSpace(providerInvoiceNumber))
            throw new ArgumentException("ProviderInvoiceNumber is required.", nameof(providerInvoiceNumber));

        var key = BuildKey(providerId, providerInvoiceNumber);

        // Idempotency check — L1 then L2
        var alreadyProcessed = await HasBeenProcessedAsync(providerId, providerInvoiceNumber, cancellationToken);
        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "WebhookService: duplicate webhook suppressed — provider={Provider} invoice={Invoice}",
                providerId, providerInvoiceNumber);
            return;
        }

        var processingSucceeded = false;
        try
        {
            // Parse webhook payload based on provider type
            var (status, failureReason) = ParseWebhookPayload(providerId, callbackData);

            // Atomic: update invoice status + persist idempotency key in single SaveChanges
            if (_dbContext is not null)
            {
                if (status.HasValue)
                    await UpdateInvoiceStatusInternalAsync(providerInvoiceNumber, status.Value, failureReason, cancellationToken);

                // Persist idempotency key atomically with any invoice state change
                _dbContext.ProcessedWebhookKeys.Add(new ProcessedWebhookKey(key));
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            processingSucceeded = true;
            _logger.LogInformation(
                "WebhookService: webhook processed — provider={Provider} invoice={Invoice} status={Status}",
                providerId, providerInvoiceNumber, status);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "WebhookService: Invalid JSON payload from provider={Provider} invoice={Invoice}. Payload logged for manual review.",
                providerId, providerInvoiceNumber);
            // Mark processed in-memory to prevent retry storm with bad payload
            processingSucceeded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "WebhookService: Error processing webhook from provider={Provider} invoice={Invoice}",
                providerId, providerInvoiceNumber);
            // Re-throw — caller returns 500, triggers provider retry
            throw;
        }
        finally
        {
            // L1: always set in-memory cache when processing completed (success or bad-payload)
            if (processingSucceeded)
                _processedKeys[key] = true;
        }
    }

    public async Task<bool> HasBeenProcessedAsync(
        string providerId,
        string providerInvoiceNumber,
        System.Threading.CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(providerInvoiceNumber))
            return false;

        var key = BuildKey(providerId, providerInvoiceNumber);

        // L1: in-memory cache (fast path, same process lifetime)
        if (_processedKeys.ContainsKey(key))
            return true;

        // L2: DB-backed durable store (survives restart)
        if (_dbContext is not null)
        {
            try
            {
                var exists = await _dbContext.ProcessedWebhookKeys
                    .AnyAsync(k => k.IdempotencyKey == key, cancellationToken);
                if (exists) return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check DB for webhook idempotency key — proceeding without L2 guard");
            }
        }

        return false;
    }

    /// <summary>
    /// Parse webhook payload based on provider format with strict typed DTOs
    /// </summary>
    private (InvoiceStatus? Status, string? FailureReason) ParseWebhookPayload(string providerId, string callbackData)
    {
        if (string.IsNullOrWhiteSpace(callbackData))
        {
            _logger.LogWarning("Empty callback data received");
            return (null, null);
        }

        var providerLower = providerId.ToLowerInvariant();

        try
        {
            if (providerLower.Contains("viettel"))
            {
                var dto = JsonSerializer.Deserialize<ViettelWebhookDto>(callbackData, _jsonOptions);
                if (dto is null)
                {
                    _logger.LogWarning("Failed to deserialize Viettel webhook payload");
                    return (null, null);
                }
                return (dto.GetInvoiceStatus(), dto.ErrorMessage);
            }
            else if (providerLower.Contains("misa"))
            {
                var dto = JsonSerializer.Deserialize<MisaWebhookDto>(callbackData, _jsonOptions);
                if (dto is null)
                {
                    _logger.LogWarning("Failed to deserialize MISA webhook payload");
                    return (null, null);
                }
                return (dto.GetInvoiceStatus(), dto.GetFailureReason());
            }
            else
            {
                // Generic fallback: try to extract status from JSON
                using var doc = JsonDocument.Parse(callbackData);
                var root = doc.RootElement;

                if (root.TryGetProperty("status", out var statusElement))
                {
                    var statusStr = statusElement.GetString()?.ToLowerInvariant();
                    var status = statusStr switch
                    {
                        "approved" or "success" => InvoiceStatus.TaxApproved,
                        "rejected" => InvoiceStatus.Rejected,
                        "failed" => InvoiceStatus.Failed,
                        _ => InvoiceStatus.SentToProvider
                    };
                    return (status, null);
                }
            }
        }
        catch (JsonException)
        {
            _logger.LogWarning("JSON parsing failed for provider {Provider}", providerId);
            throw; // Re-throw to trigger error handling in caller
        }

        return (null, null);
    }

    /// <summary>
    /// Update invoice entity state in change tracker (does NOT call SaveChangesAsync — caller owns the UoW).
    /// </summary>
    private async Task UpdateInvoiceStatusInternalAsync(
        string providerInvoiceNumber,
        InvoiceStatus status,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        if (_dbContext is null) return;

        var invoice = await _dbContext.ElectronicInvoices
            .FirstOrDefaultAsync(i => i.ProviderInvoiceNumber == providerInvoiceNumber, cancellationToken);

        if (invoice is null)
        {
            _logger.LogWarning(
                "Invoice with ProviderInvoiceNumber={Number} not found for status update",
                providerInvoiceNumber);
            return;
        }

        // Apply state transition — no SaveChangesAsync here, caller commits atomically
        try
        {
            switch (status)
            {
                case InvoiceStatus.TaxApproved:
                    invoice.MarkAsTaxApproved(providerInvoiceNumber);
                    break;
                case InvoiceStatus.Rejected:
                    invoice.MarkAsRejected(failureReason ?? "Rejected by tax authority");
                    break;
                case InvoiceStatus.Failed:
                    invoice.MarkAsFailed(failureReason ?? "Processing failed");
                    break;
                default:
                    _logger.LogInformation(
                        "No state transition for status {Status} on invoice {Invoice}",
                        status, invoice.InvoiceId.Value);
                    return;
            }

            _logger.LogInformation(
                "Invoice {Invoice} state transition to {Status} staged (pending atomic commit)",
                invoice.InvoiceId.Value, status);
        }
        catch (InvalidOperationException ex)
        {
            // State machine transition failure (e.g., wrong current state)
            _logger.LogWarning(
                ex,
                "Invalid state transition for invoice {Invoice} to status {Status}",
                invoice.InvoiceId.Value, status);
        }
    }

    private static string BuildKey(string providerId, string providerInvoiceNumber)
        => $"{providerId}:{providerInvoiceNumber}";
}
