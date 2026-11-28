using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace VanAn.KhachLink.Services
{
    /// <summary>
    /// Rich record of the customer's most recent store interaction (browse storefront,
    /// checkout, QR scan, featured add-to-cart). Slug/Name are optional — consumers
    /// fall back to /store/by-id/{TenantId} when Slug is null.
    /// Stored as compact JSON in localStorage key <see cref="LastInteractionService.StoreKey"/>.
    /// </summary>
    public record LastStoreVisit(
        [property: JsonPropertyName("t")] Guid TenantId,
        [property: JsonPropertyName("s")] string? Slug,
        [property: JsonPropertyName("n")] string? Name,
        [property: JsonPropertyName("v")] DateTimeOffset VisitedAt);

    /// <summary>
    /// Tracks the tenantId from the customer's most recent product interaction
    /// (storefront visit, checkout, QR scan, featured add-to-cart).
    /// Used by Home.razor to show campaigns + store info relevant to the
    /// tenant the customer is currently engaging with, and by BackToStoreChip
    /// to let the customer return to the storefront they came from.
    /// Stored in localStorage so it persists across sessions.
    /// Dual-key (store-context wave 2026-11-28): <see cref="StoreKey"/> holds the rich
    /// JSON record; legacy <see cref="StorageKey"/> keeps the bare GUID for readers
    /// written before the upgrade (FeatureContextService, AnonymousAuthenticationStateProvider).
    /// </summary>
    public class LastInteractionService(IJSRuntime jsRuntime)
    {
        private readonly IJSRuntime _jsRuntime = jsRuntime;
        private const string StorageKey = "khachlink_last_tenant_id";
        private const string StoreKey = "khachlink_last_store";

        /// <summary>
        /// Record a customer interaction with a tenant (storefront visit, checkout,
        /// QR scan, add to cart). Stores the tenantId (+ optional slug/name for
        /// back-to-store links) in localStorage for Home page personalization.
        /// Last write wins — checkout/tracking overwrite earlier browse visits.
        /// </summary>
        public async Task RecordInteractionAsync(Guid tenantId, string? slug = null, string? name = null)
        {
            if (tenantId == Guid.Empty)
            {
                return;
            }

            try
            {
                var visit = new LastStoreVisit(tenantId, slug, name, DateTimeOffset.UtcNow);
                var json = JsonSerializer.Serialize(visit);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StoreKey, json);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, tenantId.ToString());
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("statically rendered"))
            {
                // Prerendering mode - JS not available yet
                Console.WriteLine("LastInteraction save skipped during prerendering");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving last tenant id: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the rich record of the customer's most recent store interaction,
        /// or null if none / unreadable. Falls back to the legacy bare-GUID key
        /// (slug/name null — consumers should use /store/by-id/{TenantId}).
        /// </summary>
        public async Task<LastStoreVisit?> GetLastStoreVisitAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StoreKey);
                if (!string.IsNullOrEmpty(json))
                {
                    LastStoreVisit? visit = null;
                    try { visit = JsonSerializer.Deserialize<LastStoreVisit>(json); }
                    catch (JsonException) { /* corrupt payload — fall through to legacy */ }

                    if (visit != null && visit.TenantId != Guid.Empty)
                    {
                        return visit;
                    }
                }

                var legacy = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
                return Guid.TryParse(legacy, out var tenantId) && tenantId != Guid.Empty
                    ? new LastStoreVisit(tenantId, null, null, DateTimeOffset.MinValue)
                    : null;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("statically rendered"))
            {
                // Prerendering mode - JS not available yet
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting last store visit: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the tenantId from the customer's most recent product interaction.
        /// Returns Guid.Empty if no interaction has been recorded yet (new user).
        /// Prefers the rich record; falls back to the legacy bare-GUID key.
        /// </summary>
        public async Task<Guid> GetLastTenantIdAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StoreKey);
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var visit = JsonSerializer.Deserialize<LastStoreVisit>(json);
                        if (visit != null && visit.TenantId != Guid.Empty)
                        {
                            return visit.TenantId;
                        }
                    }
                    catch (JsonException) { /* corrupt payload — fall through to legacy */ }
                }

                var legacy = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
                return Guid.TryParse(legacy, out var tenantId) ? tenantId : Guid.Empty;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("statically rendered"))
            {
                // Prerendering mode - JS not available yet
                return Guid.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting last tenant id: {ex.Message}");
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Clear the last interaction state (e.g., on logout or reset) — both keys.
        /// </summary>
        public async Task ClearAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StoreKey);
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing last tenant id: {ex.Message}");
            }
        }
    }
}
