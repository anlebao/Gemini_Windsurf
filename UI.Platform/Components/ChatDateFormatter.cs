namespace VanAn.UI.Platform.Components;

/// <summary>
/// P2 R11 (chat_ui_unification): date separators "Hôm nay / Hôm qua / dd/MM/yyyy"
/// dùng chung cho AiChatWidget + RealtimeChatPanel.
/// </summary>
public static class ChatDateFormatter
{
    /// <summary>
    /// Kind=Utc → convert sang local; Unspecified/Local → giữ nguyên
    /// (server timestamps parse từ ISO không-Z đã là local wall-time).
    /// </summary>
    public static DateTime ToLocal(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;

    public static string Label(DateTime value)
    {
        var d = ToLocal(value).Date;
        var today = DateTime.Today;
        if (d == today) return "Hôm nay";
        if (d == today.AddDays(-1)) return "Hôm qua";
        return d.ToString("dd/MM/yyyy");
    }

    /// <summary>
    /// Trả label separator khi <paramref name="current"/> mở đầu 1 ngày mới so với
    /// message trước đó; null khi cùng ngày hoặc timestamp không hợp lệ (default).
    /// </summary>
    public static string? SeparatorLabel(DateTime? previous, DateTime current)
    {
        if (current == default) return null;
        var cur = ToLocal(current).Date;
        if (previous is null || previous == default)
            return Label(current);
        return ToLocal(previous.Value).Date == cur ? null : Label(current);
    }
}
