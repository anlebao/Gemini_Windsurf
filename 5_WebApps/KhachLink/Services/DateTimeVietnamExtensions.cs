using System;

namespace VanAn.KhachLink.Services;

/// <summary>
/// Extension methods for DateTime formatting in Vietnam timezone (ICT, UTC+7).
/// Issue 8: Server stores UTC time, but UI displayed raw UTC â†’ wrong time for Vietnam users.
/// These extensions convert UTC â†’ Vietnam time before formatting.
/// </summary>
public static class DateTimeVietnamExtensions
{
    /// <summary>
    /// Vietnam timezone (Indochina Time = UTC+7).
    /// On Linux: "SE Asia Standard Time" maps to Asia/Ho_Chi_Minh via ICU.
    /// On Windows: "SE Asia Standard Time" is the standard registry key.
    /// </summary>
    private static readonly TimeZoneInfo VietnamTimeZone = TryGetVietnamTimeZone();

    private static TimeZoneInfo TryGetVietnamTimeZone()
    {
        try
        {
            // Windows + Linux (with ICU) both support this ID
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch
        {
            try
            {
                // Fallback: IANA timezone name (Linux without ICU)
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                // Last resort: manual UTC+7 offset
                return TimeZoneInfo.CreateCustomTimeZone("ICT", TimeSpan.FromHours(7), "Indochina", "ICT");
            }
        }
    }

    /// <summary>
    /// Convert a DateTime (assumed UTC from server) to Vietnam local time.
    /// If the input is already Local, it's first converted to UTC then to Vietnam.
    /// </summary>
    public static DateTime ToVietnamTime(this DateTime dateTime)
    {
        // If Kind is Unspecified or Local, normalize to UTC first
        DateTime utc = dateTime.Kind == DateTimeKind.Local
            ? dateTime.ToUniversalTime()
            : DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
    }

    /// <summary>
    /// Convert to Vietnam time and format as "dd/MM/yyyy HH:mm" (Vietnamese standard).
    /// </summary>
    public static string ToVietnamString(this DateTime dateTime)
        => ToVietnamTime(dateTime).ToString("dd/MM/yyyy HH:mm");

    /// <summary>
    /// Convert to Vietnam time and format as "HH:mm dd/MM/yyyy".
    /// </summary>
    public static string ToVietnamTimeString(this DateTime dateTime)
        => ToVietnamTime(dateTime).ToString("HH:mm dd/MM/yyyy");

    /// <summary>
    /// Convert nullable DateTime to Vietnam time string.
    /// Returns empty string if null.
    /// </summary>
    public static string ToVietnamString(this DateTime? dateTime)
        => dateTime.HasValue ? ToVietnamString(dateTime.Value) : "";
}
