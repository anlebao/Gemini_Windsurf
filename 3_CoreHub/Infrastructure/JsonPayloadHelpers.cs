using System.Text.Json;

namespace VanAn.CoreHub.Infrastructure;

/// <summary>
/// Strict optional-Guid parsing for NATS sync event payloads (notification fan-out, NF-4).
/// NO TryParse fallback (stub pattern): absent or JSON null → null ("no recipient" sentinel);
/// present but malformed → throws → the caller's catch rejects the event loudly instead of
/// silently treating a corrupt id as missing. Guid.Empty → null (never a real customer id).
/// Shared by DataSyncSubscriber (Gateway) and PushNotificationBackgroundService (ShopERP).
/// </summary>
public static class JsonPayloadHelpers
{
    public static Guid? GetOptionalGuid(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var el) || el.ValueKind == JsonValueKind.Null)
            return null;
        var value = el.GetGuid();
        return value == Guid.Empty ? null : value;
    }
}
