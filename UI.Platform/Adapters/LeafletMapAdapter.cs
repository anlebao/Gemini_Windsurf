using Microsoft.JSInterop;
using VanAn.UI.Platform.Core.Interfaces;
using VanAn.UI.Platform.Realtime;

namespace VanAn.UI.Platform.Adapters;

/// <summary>
/// Realtime Platform P4 (UI-5): <see cref="IMapJsAdapter"/> implementation calling
/// <c>window.vananMap.*</c> (defined in UI.Platform/wwwroot/js/realtime.js).
///
/// F4: the JS lives in the RCL's wwwroot and is served at
/// <c>_content/VanAn.UI.Platform/js/realtime.js</c> on every host that references the RCL —
/// no per-app copy of the map interop.
/// </summary>
public class LeafletMapAdapter(IJSRuntime js) : IMapJsAdapter
{
    private readonly IJSRuntime _js = js;

    public Task InitMapAsync(string elementId, double centerLat, double centerLng, int zoom)
        => _js.InvokeVoidAsync("vananMap.initMap", elementId, centerLat, centerLng, zoom).AsTask();

    public Task UpsertMarkerAsync(string elementId, string key, double lat, double lng, string label, string color)
        => _js.InvokeVoidAsync("vananMap.upsertMarker", elementId, key, lat, lng, label, color).AsTask();

    public Task UpdateMarkerAsync(string elementId, string key, double lat, double lng)
        => _js.InvokeVoidAsync("vananMap.updateMarker", elementId, key, lat, lng).AsTask();

    public Task DrawRouteAsync(string elementId, double fromLat, double fromLng, double toLat, double toLng)
        => _js.InvokeVoidAsync("vananMap.drawRoute", elementId, fromLat, fromLng, toLat, toLng).AsTask();

    public async Task FitBoundsAsync(string elementId, IReadOnlyList<RealtimeMapPoint> points)
    {
        if (points.Count == 0) return;
        await _js.InvokeVoidAsync("vananMap.fitBounds", elementId,
            points.Select(p => new { lat = p.Lat, lng = p.Lng }).ToList());
    }

    public Task RemoveMapAsync(string elementId)
        => _js.InvokeVoidAsync("vananMap.removeMap", elementId).AsTask();
}
