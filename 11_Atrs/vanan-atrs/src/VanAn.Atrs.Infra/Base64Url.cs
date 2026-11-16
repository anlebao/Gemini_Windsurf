namespace VanAn.Atrs.Infra;

/// <summary>
/// Base64Url encode/decode (RFC 4648 §5) — dùng cho credential ID/challenge/public key.
/// Tự chứa (không phụ thuộc ASP.NET shared framework) vì Infra là class library thuần.
/// </summary>
public static class Base64Url
{
    public static string Encode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static byte[] Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
            case 1: throw new FormatException("Invalid base64url string length.");
        }
        return Convert.FromBase64String(s);
    }
}