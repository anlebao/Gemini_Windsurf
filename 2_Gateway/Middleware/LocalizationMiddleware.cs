using VanAn.Shared.Services;

namespace VanAn.Gateway.Middleware;

public class LocalizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LocalizationMiddleware> _logger;
    
    private static readonly Action<ILogger, string, Exception?> LogCultureSet = 
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "Localization"), "Culture set to: {Culture}");
    
    private static readonly Action<ILogger, string, Exception?> LogCultureFallback = 
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "Localization"), "Fallback culture: {Culture}");

    public LocalizationMiddleware(
        RequestDelegate next,
        ILogger<LocalizationMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // Resolve scoped service from HttpContext request services
            var localizationService = context.RequestServices
                .GetRequiredService<ILocalizationService>();
            
            ArgumentNullException.ThrowIfNull(localizationService);

            // Get culture from Accept-Language header
            var acceptLanguage = context.Request.Headers["Accept-Language"].FirstOrDefault();
            var culture = DetermineCulture(acceptLanguage, localizationService);

            // Set culture for the current request - ASYNC!
            await localizationService.SetCultureAsync(culture);
            LogCultureSet(_logger, culture, null);

            // Add culture to response headers
            context.Response.Headers["Content-Language"] = culture;

            // Add localization info to HttpContext for controllers to use
            context.Items["Culture"] = culture;

            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LocalizationMiddleware");
            throw; // Fail-Fast - don't swallow exceptions
        }
    }

    private string DetermineCulture(string? acceptLanguage, ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        
        if (string.IsNullOrEmpty(acceptLanguage))
        {
            var defaultCulture = localizationService.GetCulture();
            LogCultureFallback(_logger, defaultCulture, null);
            return defaultCulture;
        }

        // Parse Accept-Language header
        var cultures = acceptLanguage.Split(',')
            .Select(c => c.Split(';')[0].Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();

        var supportedCultures = localizationService.GetSupportedCultures().ToList();
        
        // Try to find supported culture
        foreach (var culture in cultures)
        {
            // Try exact match
            if (supportedCultures.Contains(culture))
            {
                return culture;
            }

            // Try language-only match (e.g., "en-US" -> "en")
            var languageOnly = culture.Split('-')[0];
            if (supportedCultures.Contains(languageOnly))
            {
                return languageOnly;
            }
        }

        // Fall back to current/default culture
        var fallbackCulture = localizationService.GetCulture();
        LogCultureFallback(_logger, fallbackCulture, null);
        return fallbackCulture;
    }
}
