namespace VanAn.CoreHub.Services;

/// <summary>
/// Lightweight journal template model for autocomplete matching
/// </summary>
public class JournalTemplateItem
{
    public string Keyword { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Service for journal template matching and autocomplete suggestions
/// </summary>
public static class JournalTemplateLookupService
{
    /// <summary>
    /// Find first matching template by keyword
    /// </summary>
    public static JournalTemplateItem? FindMatchingTemplate(string userInput, List<JournalTemplateItem> templates)
    {
        if (string.IsNullOrWhiteSpace(userInput)) return null;
        return templates.FirstOrDefault(t =>
            t.Keyword.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
            userInput.Contains(t.Keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all matching suggestions for partial keyword
    /// </summary>
    public static List<JournalTemplateItem> GetSuggestions(string userInput, List<JournalTemplateItem> templates)
    {
        if (string.IsNullOrWhiteSpace(userInput)) return new List<JournalTemplateItem>();
        return templates.Where(t =>
            t.Keyword.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
            userInput.Contains(t.Keyword, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
