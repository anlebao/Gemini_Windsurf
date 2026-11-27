namespace VanAn.TenantAi.Domain;

/// <summary>
/// W3f-b — key/value runtime settings, mirror SystemSetting aggregate của CoreHub
/// ("BackgroundServices:Enable*", "ShopFeature:{tenant}:{toggle}"). Job-level flags
/// phải đọc được trong process vanan-ai — KHÔNG tái dùng SystemSettings của Gateway PG
/// (vanan-ai là service biên, chỉ nhận signed tenant ctx vào, không call-back).
/// Keys: "LearningSuggest:Enabled" (global, ops-managed) ·
/// "LearningSuggest:{tenantId}:Enabled" (per-tenant opt-in, absent = OFF).
/// </summary>
public sealed class SystemSetting
{
    public string Key { get; private set; } = "";
    public string Value { get; private set; } = "";
    public DateTime UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }

    private SystemSetting() { } // EF Core

    public static SystemSetting Create(string key, string value, string? updatedBy)
        => new()
        {
            Key = key,
            Value = value,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };

    public void Update(string value, string? updatedBy)
    {
        Value = value;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
