using System.Text.Json;

namespace VanAn.ShopERP.Services;

/// <summary>
/// task_ai_p10 — map giữa persona_json / business_profile_json (free-form JSON, inject
/// verbatim vào LLM prompt) và form fields trên trang /admin/tenant-ai/profile.
/// Round-trip merge: unknown keys được giữ nguyên khi owner lưu form.
/// Keys: persona = pronoun/style/greeting/suggestions · business = about/address/phone/opening_hours.
/// </summary>
public static class TenantAiProfileJson
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Parse a free-form JSON object; non-object/invalid → empty dict.</summary>
    public static Dictionary<string, JsonElement> ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(StringComparer.Ordinal);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new(StringComparer.Ordinal);
            return doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new(StringComparer.Ordinal);
        }
    }

    /// <summary>First string value found among candidate keys (fallback aliases).</summary>
    public static string? GetString(IReadOnlyDictionary<string, JsonElement> obj, params string[] keys)
    {
        foreach (var k in keys)
            if (obj.TryGetValue(k, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }

    /// <summary>suggestions: string[] → one-per-line text for the textarea.</summary>
    public static string GetSuggestionsText(IReadOnlyDictionary<string, JsonElement> obj)
    {
        if (!obj.TryGetValue("suggestions", out var v) || v.ValueKind != JsonValueKind.Array)
            return string.Empty;
        var lines = v.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join('\n', lines);
    }

    /// <summary>Merge form fields into the preserved extra keys → serialized persona_json.</summary>
    public static string SerializePersona(Dictionary<string, JsonElement> extra,
        string? pronoun, string? style, string? greeting, IReadOnlyList<string> suggestions)
    {
        SetString(extra, "pronoun", pronoun);
        SetString(extra, "style", style);
        SetString(extra, "greeting", greeting);
        if (suggestions.Count > 0)
            extra["suggestions"] = ToElement(suggestions);
        else
            extra.Remove("suggestions");
        return JsonSerializer.Serialize(extra, JsonOpts);
    }

    /// <summary>Merge form fields into the preserved extra keys → serialized business_profile_json.</summary>
    public static string SerializeBusiness(Dictionary<string, JsonElement> extra,
        string? about, string? address, string? phone, string? openingHours)
    {
        SetString(extra, "about", about);
        SetString(extra, "address", address);
        SetString(extra, "phone", phone);
        SetString(extra, "opening_hours", openingHours);
        return JsonSerializer.Serialize(extra, JsonOpts);
    }

    /// <summary>Split one-per-line text into trimmed non-empty suggestion entries.</summary>
    public static List<string> ParseSuggestions(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

    private static void SetString(Dictionary<string, JsonElement> obj, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) obj.Remove(key);
        else obj[key] = ToElement(value.Trim());
    }

    private static JsonElement ToElement(object value)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonOpts));
        return doc.RootElement.Clone();
    }
}
