using System.Text.Json;

namespace VanAn.KhachLink.Services.Http;

/// <summary>
/// Queries the global LoyaltyMode from Gateway (public anonymous endpoint).
/// KhachLink uses this to decide whether to show "Ví liên minh" UI:
///   - Silo mode → hide alliance wallet menu/icon (customer confusion prevention)
///   - Alliance mode → show alliance wallet UI
/// Caches the result for 5 minutes to avoid repeated API calls on every page navigation.
/// </summary>
public class LoyaltyModeHttpService(IHttpClientFactory httpClientFactory, ILogger<LoyaltyModeHttpService> logger)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("gateway");
    private readonly ILogger<LoyaltyModeHttpService> _logger = logger;

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private string? _cachedMode;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Returns true when the system is in Alliance mode (show alliance wallet UI).
    /// Returns false when Silo mode (hide alliance wallet UI).
    /// Cached for 5 minutes.
    /// </summary>
    public async Task<bool> IsAllianceModeAsync()
    {
        var mode = await GetModeAsync();
        return string.Equals(mode, "Alliance", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns the mode string ("Silo" or "Alliance"). Cached for 5 minutes.</summary>
    public async Task<string> GetModeAsync()
    {
        if (_cachedMode != null && DateTime.UtcNow < _cacheExpiry)
            return _cachedMode;

        try
        {
            var resp = await _httpClient.GetAsync("/api/loyalty/mode");
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<ModeResponse>(body, _jsonOpts);
                _cachedMode = data?.Mode ?? "Silo";
                _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
                return _cachedMode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying loyalty mode — defaulting to Silo");
        }

        // Default to Silo on error (safe: hide alliance wallet)
        _cachedMode = "Silo";
        _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
        return _cachedMode;
    }

    /// <summary>Clears the cache (force re-query on next call).</summary>
    public void InvalidateCache()
    {
        _cachedMode = null;
        _cacheExpiry = DateTime.MinValue;
    }

    private class ModeResponse
    {
        public string Mode { get; set; } = "Silo";
    }
}
