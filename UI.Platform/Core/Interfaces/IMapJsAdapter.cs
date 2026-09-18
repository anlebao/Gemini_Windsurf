using VanAn.UI.Platform.Realtime;

namespace VanAn.UI.Platform.Core.Interfaces;

/// <summary>
/// Realtime Platform P4 (UI-3): map JS interop abstraction — same pattern as <see cref="ICssAdapter"/>.
///
/// Implementations call a JS namespace (e.g. <c>window.vananMap.*</c>) so components never touch
/// Leaflet directly and the map library can be swapped (Leaflet → MapLibre → Google) per host.
/// </summary>
public interface IMapJsAdapter
{
    /// <summary>Create (or recreate) the map inside the element and centre it.</summary>
    Task InitMapAsync(string elementId, double centerLat, double centerLng, int zoom);

    /// <summary>Add the marker if it does not exist yet, otherwise move it (used on every render).</summary>
    Task UpsertMarkerAsync(string elementId, string key, double lat, double lng, string label, string color);

    /// <summary>Move an existing marker (no panning — avoids fighting the user while dragging).</summary>
    Task UpdateMarkerAsync(string elementId, string key, double lat, double lng);

    /// <summary>Draw a dashed route line between two points and fit the view.</summary>
    Task DrawRouteAsync(string elementId, double fromLat, double fromLng, double toLat, double toLng);

    /// <summary>Fit the view to show all given points.</summary>
    Task FitBoundsAsync(string elementId, IReadOnlyList<RealtimeMapPoint> points);

    /// <summary>Remove the map instance (component disposal).</summary>
    Task RemoveMapAsync(string elementId);
}
