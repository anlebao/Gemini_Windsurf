namespace VanAn.Shared.Services;

public interface ILocalizationService
{
    string GetString(string key, string? culture = null);
    string GetString(string key, params object[] args);
    string GetString(string key, string culture, params object[] args);
    Task<string> GetStringAsync(string key, string? culture = null);
    Task<string> GetStringAsync(string key, params object[] args);
    Task<string> GetStringAsync(string key, string culture, params object[] args);
    string GetCulture();
    void SetCulture(string culture);
    Task SetCultureAsync(string culture);
    Task<bool> SetCultureWithResultAsync(string culture);
    IEnumerable<string> GetSupportedCultures();
    Task<Dictionary<string, object>> GetAllStringsAsync(string? culture = null);
}
