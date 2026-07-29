using Microsoft.JSInterop;
using System.Timers;

namespace VanAn.KhachLink.Services;

/// <summary>
/// CC-S2 (Sprint 2): GPS polling service for shipper delivery tracking.
/// Polls navigator.geolocation.getCurrentPosition every 10s when tracking is active.
/// Stops automatically when delivery is Delivered/Failed or tab is inactive.
/// </summary>
public class LocationTrackingService(IJSRuntime js, ILogger<LocationTrackingService> logger)
{
    private readonly IJSRuntime _js = js;
    private readonly ILogger<LocationTrackingService> _logger = logger;
    private System.Timers.Timer? _timer;
    private string? _deliveryTaskId;
    private string? _customerToken;
    private Func<string, double, double, Task>? _onLocationUpdate;
    private bool _isTracking;

    /// <summary>
    /// Start GPS polling every 10 seconds.
    /// </summary>
    public void StartTracking(string deliveryTaskId, string customerToken, Func<string, double, double, Task> onLocationUpdate)
    {
        _deliveryTaskId = deliveryTaskId;
        _customerToken = customerToken;
        _onLocationUpdate = onLocationUpdate;
        _isTracking = true;

        _timer?.Dispose();
        _timer = new System.Timers.Timer(10000); // 10s interval
        _timer.Elapsed += async (_, _) => await PollLocationAsync();
        _timer.Start();

        _logger.LogInformation("LocationTracking: Started for task {TaskId}", deliveryTaskId);
    }

    /// <summary>
    /// Stop GPS polling.
    /// </summary>
    public void StopTracking()
    {
        _isTracking = false;
        _timer?.Dispose();
        _timer = null;
        _logger.LogInformation("LocationTracking: Stopped for task {TaskId}", _deliveryTaskId);
    }

    private async Task PollLocationAsync()
    {
        if (!_isTracking || string.IsNullOrEmpty(_deliveryTaskId) || string.IsNullOrEmpty(_customerToken))
            return;

        try
        {
            var pos = await _js.InvokeAsync<GeoPosition?>("vananPWA.getCurrentPosition");
            if (pos != null && _onLocationUpdate != null)
            {
                await _onLocationUpdate(_deliveryTaskId, pos.Lat, pos.Lng);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LocationTracking: GPS poll failed");
        }
    }

    private class GeoPosition
    {
        public double Lat { get; set; }
        public double Lng { get; set; }
    }
}
