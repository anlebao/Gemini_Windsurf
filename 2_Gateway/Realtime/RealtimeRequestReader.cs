namespace VanAn.Gateway.Realtime;

/// <summary>
/// Realtime Platform P3 (2026-09-17): reads a credential from a query string or a header.
///
/// Query comes first because a browser WebSocket handshake cannot carry custom headers — SignalR
/// only exposes the URL. HTTP endpoints can and do use the header, so both paths share one reader
/// instead of each caller picking a transport (F3).
/// </summary>
internal static class RealtimeRequestReader
{
    public static string? ReadCredential(HttpContext httpContext, string queryKey, string headerKey)
    {
        var fromQuery = httpContext.Request.Query[queryKey].ToString();
        if (!string.IsNullOrEmpty(fromQuery))
            return fromQuery;

        if (httpContext.Request.Headers.TryGetValue(headerKey, out var header))
        {
            var value = header.ToString();
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return null;
    }
}
