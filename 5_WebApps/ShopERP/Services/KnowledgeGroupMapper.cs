namespace VanAn.ShopERP.Services;

/// <summary>
/// W3c-multi — mapping nhóm extract → knowledge doc type + sourceRef (plan D3/D4).
/// menu→menu-note, about→story, policy→policy, promotion→policy (title prefix).
/// </summary>
public static class KnowledgeGroupMapper
{
    private static readonly IReadOnlyDictionary<string, string> DocTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["menu"] = "menu-note",
            ["about"] = "story",
            ["policy"] = "policy",
            ["promotion"] = "policy",
        };

    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["menu"] = "Menu & dịch vụ",
            ["about"] = "Giới thiệu quán",
            ["policy"] = "Chính sách",
            ["promotion"] = "Khuyến mãi",
        };

    public static string DocTypeFor(string group)
        => DocTypes.TryGetValue(group, out var t) ? t : "policy";

    public static string LabelFor(string group)
        => Labels.TryGetValue(group, out var l) ? l : group;

    /// <summary>sourceRef per group — dedupe theo batch extract: cùng 1 lần
    /// trích xuất → cùng guid, bấm lại không tạo doc trùng.</summary>
    public static string SourceRefFor(string group, string? batchId = null)
        => $"extract:{group}:{batchId ?? Guid.NewGuid().ToString("N")}";

    public static string DocTitleFor(string group)
        => $"Trích xuất {LabelFor(group)} — {DateTime.Now:dd/MM/yyyy HH:mm}";
}
