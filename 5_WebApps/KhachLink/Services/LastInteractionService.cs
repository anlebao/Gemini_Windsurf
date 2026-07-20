using Microsoft.JSInterop;

namespace VanAn.KhachLink.Services
{
    /// <summary>
    /// Tracks the tenantId from the customer's most recent product interaction
    /// (add to cart via Featured product or QR scan).
    /// Used by Home.razor to show campaigns + store info relevant to the
    /// tenant the customer is currently engaging with.
    /// Stored in localStorage so it persists across sessions.
    /// </summary>
    public class LastInteractionService(IJSRuntime jsRuntime)
    {
        private readonly IJSRuntime _jsRuntime = jsRuntime;
        private const string StorageKey = "khachlink_last_tenant_id";

        /// <summary>
        /// Record a customer interaction with a tenant (add to cart, scan QR).
        /// Stores the tenantId in localStorage for Home page personalization.
        /// </summary>
        public async Task RecordInteractionAsync(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
            {
                return;
            }

            try
            {
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
        /// Get the tenantId from the customer's most recent product interaction.
        /// Returns Guid.Empty if no interaction has been recorded yet (new user).
        /// </summary>
        public async Task<Guid> GetLastTenantIdAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);

                if (string.IsNullOrEmpty(json))
                {
                    return Guid.Empty;
                }

                return Guid.TryParse(json, out var tenantId) ? tenantId : Guid.Empty;
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
        /// Clear the last interaction tenantId (e.g., on logout or reset).
        /// </summary>
        public async Task ClearAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing last tenant id: {ex.Message}");
            }
        }
    }
}
