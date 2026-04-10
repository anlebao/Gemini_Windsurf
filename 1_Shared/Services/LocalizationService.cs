using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using VanAn.Shared.Domain;
using System.Globalization;

namespace VanAn.Shared.Services;

public partial class LocalizationService : ILocalizationService
{
    private readonly ILogger<LocalizationService> _logger;
    private readonly IMemoryCache _cache;
    private readonly string _resourcesPath;
    private readonly string[] _supportedCultures = { "vi", "en" };
    private string _currentCulture = "vi";
    private Dictionary<string, Dictionary<string, object>> _resources = new();

    public LocalizationService(ILogger<LocalizationService> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
        _resourcesPath = Path.Combine(Directory.GetCurrentDirectory(), "1_Shared", "Localization");
        
        // Load default culture resources
        _ = LoadResourcesAsync(_currentCulture);
    }

    public string GetString(string key, string? culture = null)
    {
        culture ??= _currentCulture;
        
        if (!_resources.TryGetValue(culture, out var cultureResources))
        {
            LogCultureNotLoaded(culture);
            _ = LoadResourcesAsync(culture);
            return key; // Return key as fallback
        }

        return GetValueFromKey(cultureResources, key) ?? key;
    }

    public string GetString(string key, params object[] args)
    {
        var format = GetString(key);
        return args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, format, args) : format;
    }

    public string GetString(string key, string culture, params object[] args)
    {
        var format = GetString(key, culture);
        return args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, format, args) : format;
    }

    public async Task<string> GetStringAsync(string key, string? culture = null)
    {
        culture ??= _currentCulture;
        
        if (!_resources.TryGetValue(culture, out var cultureResources))
        {
            await LoadResourcesAsync(culture);
        }

        if (_resources.TryGetValue(culture, out cultureResources))
        {
            return GetValueFromKey(cultureResources, key) ?? key;
        }

        return key;
    }

    public async Task<string> GetStringAsync(string key, params object[] args)
    {
        var format = await GetStringAsync(key);
        return args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, format, args) : format;
    }

    public async Task<string> GetStringAsync(string key, string culture, params object[] args)
    {
        var format = await GetStringAsync(key, culture);
        return args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, format, args) : format;
    }

    public string GetCulture()
    {
        return _currentCulture;
    }

    public void SetCulture(string culture)
    {
        if (!_supportedCultures.Contains(culture))
        {
            LogUnsupportedCulture(culture);
            return;
        }

        _currentCulture = culture;
        
        // Fire-and-forget for backward compatibility
        if (!_resources.ContainsKey(culture))
        {
            _ = LoadResourcesAsync(culture);
        }
    }

    public async Task SetCultureAsync(string culture)
    {
        if (!_supportedCultures.Contains(culture))
        {
            LogUnsupportedCulture(culture);
            return;
        }

        await LoadResourcesAsync(culture);
        _currentCulture = culture;
    }

    public async Task<bool> SetCultureWithResultAsync(string culture)
    {
        if (!_supportedCultures.Contains(culture))
        {
            LogUnsupportedCulture(culture);
            return false;
        }

        await LoadResourcesAsync(culture);
        _currentCulture = culture;
        return true;
    }

    public IEnumerable<string> GetSupportedCultures()
    {
        return _supportedCultures;
    }

    public async Task<Dictionary<string, object>> GetAllStringsAsync(string? culture = null)
    {
        culture ??= _currentCulture;
        
        if (_resources.TryGetValue(culture, out var cultureResources))
        {
            return FlattenDictionary(cultureResources);
        }

        await LoadResourcesAsync(culture);
        return FlattenDictionary(_resources[culture]);
    }

    private async Task LoadResourcesAsync(string culture)
    {
        var cacheKey = $"localization_{culture}";
        
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, object>? cached))
        {
            _resources[culture] = cached ?? new();
            return;
        }

        try
        {
            var filePath = Path.Combine(_resourcesPath, $"Resources.{culture}.json");
            
            if (!File.Exists(filePath))
            {
                LogResourceFileNotFound(filePath);
                _resources[culture] = new Dictionary<string, object>();
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var resources = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            
            if (resources != null)
            {
                var flattened = FlattenDictionary(resources);
                _resources[culture] = flattened;
                
                // Cache for 1 hour
                _cache.Set(cacheKey, flattened, TimeSpan.FromHours(1));
                
                LogLocalizationLoaded(flattened.Count, culture);
            }
        }
        catch (Exception ex)
        {
            LogLocalizationLoadError(ex, culture);
            _resources[culture] = new Dictionary<string, object>();
        }
    }

    private static Dictionary<string, object> FlattenDictionary(Dictionary<string, object> dict, string? prefix = null)
    {
        var result = new Dictionary<string, object>();

        foreach (var kvp in dict)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";
            
            if (kvp.Value is Dictionary<string, object> nestedDict)
            {
                var nested = FlattenDictionary(nestedDict, key);
                foreach (var nestedKvp in nested)
                {
                    result[nestedKvp.Key] = nestedKvp.Value;
                }
            }
            else if (kvp.Value is JsonElement jsonElement)
            {
                // Handle JsonElement properly
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.String:
                        result[key] = jsonElement.GetString() ?? string.Empty;
                        break;
                    case JsonValueKind.Number:
                        result[key] = jsonElement.ToString();
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        result[key] = jsonElement.GetBoolean().ToString();
                        break;
                    case JsonValueKind.Object:
                        // Recursively flatten nested JSON objects
                        var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                        if (jsonObject != null)
                        {
                            var nested = FlattenDictionary(jsonObject, key);
                            foreach (var nestedKvp in nested)
                            {
                                result[nestedKvp.Key] = nestedKvp.Value;
                            }
                        }
                        break;
                    case JsonValueKind.Array:
                        // Convert arrays to string representation
                        result[key] = jsonElement.GetRawText();
                        break;
                    default:
                        result[key] = jsonElement.GetRawText();
                        break;
                }
            }
            else
            {
                result[key] = kvp.Value?.ToString() ?? string.Empty;
            }
        }

        return result;
    }

    private static string? GetValueFromKey(Dictionary<string, object> resources, string key)
    {
        if (resources.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }

        // Try to find partial matches for nested keys
        var parts = key.Split('.');
        for (int i = parts.Length - 1; i > 0; i--)
        {
            var partialKey = string.Join(".", parts[..i]);
            if (resources.TryGetValue(partialKey, out var partialValue))
            {
                return partialValue?.ToString();
            }
        }

        return null;
    }

    // High-Performance Logging Methods
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Culture {Culture} not loaded, attempting to load")]
    private partial void LogCultureNotLoaded(string culture);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Unsupported culture: {Culture}")]
    private partial void LogUnsupportedCulture(string culture);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Resource file not found: {FilePath}")]
    private partial void LogResourceFileNotFound(string filePath);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Loaded {Count} localization strings for culture {Culture}")]
    private partial void LogLocalizationLoaded(int count, string culture);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Error loading localization resources for culture {Culture}")]
    private partial void LogLocalizationLoadError(Exception ex, string culture);
}
