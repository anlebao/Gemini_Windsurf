using System;

namespace VanAn.ShopERP.Services;

/// <summary>
/// Extension methods for DateTime formatting in Vietnam timezone (ICT, UTC+7).
/// Issue 13: Server stores UTC time, but UI displayed raw UTC → wrong time for Vietnam users.
/// These extensions convert UTC → Vietnam time before formatting.
/// </summary>
public static class DateTimeVietnamExtensions
{
    private static readonly TimeZoneInfo VietnamTimeZone = TryGetVietnamTimeZone();

    private static TimeZoneInfo TryGetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "Indochina", "ICT");
            }
        }
    }

    public static DateTime ToVietnamTime(this DateTime dateTime)
    {
        DateTime utc = dateTime.Kind == DateTimeKind.Local
            ? dateTime.ToUniversalTime()
            : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
    }

    public static string ToVietnamString(this DateTime dateTime)
        => ToVietnamTime(dateTime).ToString("dd/MM/yyyy HH:mm");

    public static string ToVietnamTimeString(this DateTime dateTime)
        => ToVietnamTime(dateTime).ToString("HH:mm dd/MM/yyyy");

    /// <summary>
    /// Format as "HH:mm" only (for Kitchen Display compact view).
    /// </summary>
    public static string ToVietnamTimeShort(this DateTime dateTime)
        => ToVietnamTime(dateTime).ToString("HH:mm");

    public static string ToVietnamString(this DateTime? dateTime)
        => dateTime.HasValue ? ToVietnamString(dateTime.Value) : "";
}
