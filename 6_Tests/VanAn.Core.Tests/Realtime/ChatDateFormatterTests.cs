using VanAn.UI.Platform.Components;
using Xunit;

namespace VanAn.Core.Tests.Realtime;

/// <summary>P2 R11 (chat_ui_unification): date separator labels cho chat UI.</summary>
public class ChatDateFormatterTests
{
    [Fact]
    public void SeparatorLabel_FirstMessage_ReturnsToday()
    {
        var label = ChatDateFormatter.SeparatorLabel(null, DateTime.UtcNow);
        Assert.Equal("Hôm nay", label);
    }

    [Fact]
    public void SeparatorLabel_SameDay_ReturnsNull()
    {
        var t1 = DateTime.UtcNow;
        var t2 = t1.AddHours(2);
        Assert.Null(ChatDateFormatter.SeparatorLabel(t1, t2));
    }

    [Fact]
    public void SeparatorLabel_DayChanged_ReturnsCurrentDayLabel()
    {
        // Label mô tả NGÀY của message hiện tại: hôm nay → "Hôm nay",
        // hôm qua → "Hôm qua", cũ hơn → dd/MM/yyyy.
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);

        Assert.Equal("Hôm nay",
            ChatDateFormatter.SeparatorLabel(today.AddDays(-1), today));
        Assert.Equal("Hôm qua",
            ChatDateFormatter.SeparatorLabel(today.AddDays(-2), yesterday));
    }

    [Fact]
    public void SeparatorLabel_OlderThanYesterday_ReturnsDate()
    {
        var old = DateTime.Today.AddDays(-10);
        var label = ChatDateFormatter.SeparatorLabel(null, old);
        Assert.Matches(@"^\d{2}/\d{2}/\d{4}$", label!);
    }

    [Fact]
    public void SeparatorLabel_DefaultTimestamp_ReturnsNull()
    {
        Assert.Null(ChatDateFormatter.SeparatorLabel(null, default));
        Assert.Null(ChatDateFormatter.SeparatorLabel(DateTime.UtcNow, default));
    }

    [Fact]
    public void SeparatorLabel_UnspecifiedKind_TreatedAsLocal()
    {
        // SentAt parse từ ISO không-Z → Kind=Unspecified → không convert.
        var local = new DateTime(DateTime.Today.Year, DateTime.Today.Month,
            DateTime.Today.Day, 10, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal("Hôm nay", ChatDateFormatter.SeparatorLabel(null, local));
    }
}
