# Sprint 2 Detailed Plan — Delivery Workflow + GPS Tracking

TDD plan (10 test cases), coding plan (5 sessions), SignalR hub spec, Leaflet integration, GPS polling.

---

## 1. API SPECIFICATIONS

### 1.1 Delivery Status Endpoints
```
POST /api/community/orders/{orderId}/pickup
POST /api/community/orders/{orderId}/delivering
POST /api/community/orders/{orderId}/delivered
POST /api/community/orders/{orderId}/failed  (body: { "reason": "string" })
Header: X-Customer-Token
Response 200: { "deliveryTaskId": "guid", "status": "PickedUp|OutForDelivery|Delivered|Failed", "timestamp": "..." }
Response 409: Invalid state transition
Response 404: Order/DeliveryTask not found
```

### 1.2 Location Update
```
POST /api/community/location/update
Body: { "deliveryTaskId": "guid", "lat": 10.8, "lng": 106.7 }
Header: X-Customer-Token
Response 200: { "recordedAt": "..." }
```

### 1.3 SignalR LocationHub
```
Hub URL: /hubs/location
Client methods:
  - JoinOrderTracking(orderId: string) → join group "order_{orderId}"
  - LeaveOrderTracking(orderId: string) → leave group
Server→Client events:
  - LocationUpdate(deliveryTaskId: string, lat: double, lng: double, recordedAt: string)
  - DeliveryStatusUpdate(orderId: string, status: string, timestamp: string)
```

---

## 2. SERVICE SPECIFICATIONS

### 2.1 IDeliveryWorkflowService
```csharp
public interface IDeliveryWorkflowService
{
    Task<DeliveryTask?> TransitionStatusAsync(Guid orderId, DeliveryTaskStatus newStatus, string? failureReason = null);
    Task RecordLocationAsync(Guid deliveryTaskId, double lat, double lng);
    Task<List<DeliveryTracking>> GetTrackingHistoryAsync(Guid deliveryTaskId);
}
```

### 2.2 DeliveryWorkflowService
- `TransitionStatusAsync`: Load DeliveryTask by OrderId (active). Call domain method (MarkPickedUp etc). Save. If Delivered → call OrderWorkflowService.TransitionStatusAsync(orderId, "completed"). Publish SignalR event.
- `RecordLocationAsync`: Create DeliveryTracking record. Save. Publish SignalR LocationUpdate to order group.
- `GetTrackingHistoryAsync`: Query DeliveryTracking WHERE DeliveryTaskId, sort by RecordedAt.

---

## 3. SIGNALR HUB SPEC

### LocationHub.cs (v1.4 — auth via X-Customer-Token query string, NOT [Authorize] JWT)
> **v1.4 CORRECTION (CRITICAL-3):** Same pattern as ChatHub (A3 fix). Customer auth là custom `X-Customer-Token`, KHÔNG phải JWT. `[Authorize]` sẽ FAIL cho customer.

```csharp
// v1.4: KHÔNG dùng [Authorize] (JWT) — customer auth là X-Customer-Token (custom)
public class LocationHub : Hub
{
    private readonly ICustomerTokenService _tokenService;

    public LocationHub(ICustomerTokenService tokenService) { _tokenService = tokenService; }

    public override async Task OnConnectedAsync()
    {
        // v1.4: Token qua query string (SignalR client truyền được)
        var token = Context.GetHttpContext()?.Request.Query["customerToken"].ToString();
        if (string.IsNullOrEmpty(token))
            throw new HubException("Missing customerToken");

        var customerId = await _tokenService.ValidateTokenAsync(token);
        if (customerId == null)
            throw new HubException("Invalid customerToken");

        Context.Items["CustomerId"] = customerId.Value;
        await base.OnConnectedAsync();
    }

    public async Task JoinOrderTracking(string orderId)
    {
        // Verify customer có quyền join ( là ShipperId hoặc CustomerId của Order)
        var customerId = (Guid)Context.Items["CustomerId"]!;
        // ... query Order WHERE Id=orderId AND (ShipperId=customerId OR CustomerId=customerId)
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
    }

    public Task LeaveOrderTracking(string orderId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order_{orderId}");
}
```

**Client-side connection (KhachLink WASM):**
```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${gatewayBase}/hubs/location?customerToken=${customerToken}`)  // v1.4: query string
    .build();
```

**Note:** Same auth pattern applied cho ChatHub (Sprint 3, A3 fix). Consistent across all community hubs.

### Hub mapping in Program.cs
```csharp
app.MapHub<LocationHub>("/hubs/location");
```

---

## 4. LEAFLET MAP SPEC

### LeafletMap.razor
```
Parameters:
  - ShopLat, ShopLng: double? — shop marker (red)
  - ShipperLat, ShipperLng: double? — shipper marker (blue, moves)
  - CustomerLat, CustomerLng: double? — customer marker (green)
  - Zoom: int = 14

JS interop (wwwroot/js/leaflet.js):
  - initMap(elementId, centerLat, centerLng, zoom) → create Leaflet map
  - addMarker(elementId, lat, lng, label, color) → add marker
  - updateMarker(elementId, lat, lng) → move existing marker
  - drawRoute(elementId, fromLat, fromLng, toLat, toLng) → draw line

Vendored (v1.3 — NO CDN, consistent với zero-dependency rule):
  - /lib/leaflet/leaflet.css  (download from unpkg.com/leaflet@1.9.4/dist/leaflet.css, vendor locally)
  - /lib/leaflet/leaflet.js   (download from unpkg.com/leaflet@1.9.4/dist/leaflet.js, vendor locally)
  - SRI hash trong <script>/<link> tag để detect tampering
  - Map tiles: OSM standard (https://tile.openstreetmap.org/{z}/{x}/{y}.png) — free, rate limit OK cho PoC
  - Post-PoC: self-host tile server hoặc CartoDB free tier (10K loads/month)
```

---

## 5. GPS POLLING SPEC

### LocationTrackingService.cs (KhachLink)
```csharp
public class LocationTrackingService
{
    private Timer? _timer;
    private readonly IJSRuntime _js;
    private readonly CommunityHttpService _http;
    private string? _deliveryTaskId;
    private bool _isTracking;

    public async Task StartTrackingAsync(string deliveryTaskId)
    {
        _deliveryTaskId = deliveryTaskId;
        _isTracking = true;
        _timer = new Timer(10000); // 10s
        _timer.Elapsed += async (_, _) => await PollLocationAsync();
        _timer.Start();
    }

    public void StopTracking()
    {
        _isTracking = false;
        _timer?.Dispose();
    }

    private async Task PollLocationAsync()
    {
        if (!_isTracking) return;
        var pos = await _js.InvokeAsync<GeoPosition>("getCurrentPosition");
        if (pos != null)
            await _http.UpdateLocationAsync(_deliveryTaskId!, pos.Lat, pos.Lng);
    }
}
```

---

## 6. TDD PLAN (10 TEST CASES)

| # | Test Name | What It Verifies |
|---|---|---|
| 1 | `Transition_PickedUp_FromAssigned_Success` | Status=PickedUp, PickedUpAt set |
| 2 | `Transition_OutForDelivery_FromPickedUp_Success` | Status=OutForDelivery |
| 3 | `Transition_Delivered_FromOutForDelivery_Success` | Status=Delivered, Order.Completed called |
| 4 | `Transition_Failed_WithReason` | Status=Failed, FailureReason set |
| 5 | `Transition_InvalidState_Throws` | e.g. Assigned→Delivered throws |
| 6 | `RecordLocation_CreatesTracking` | DeliveryTracking record exists |
| 7 | `RecordLocation_AppendOnly` | Multiple records, all preserved |
| 8 | `GetTrackingHistory_SortsByRecordedAt` | Chronological order |
| 9 | `Transition_OrderNotFound_ReturnsNull` | Null result for missing order |
| 10 | `Transition_NoActiveTask_ReturnsNull` | Null when no active DeliveryTask |

---

## 7. CODING PLAN — 5 SESSIONS

| Session | JIT Planning | Pure Execution |
|---|---|---|
| **S1** | Service interface + test cases | DeliveryWorkflowService + 10 unit tests |
| **S2** | Hub spec + controller endpoints | LocationHub + CommunityController additions + DI |
| **S3** | Leaflet JS interop + component | LeafletMap.razor + leaflet.js + CDN |
| **S4** | GPS polling + delivery UI | LocationTrackingService + DeliveryTracking.razor + OrderTracking.razor |
| **S5** | E2E flow + regression | community-delivery-flow.spec.ts + guard-check + build |

---

## 8. VPS VERIFICATION (Sprint 2)

| # | Test | Expected |
|---|---|---|
| RV2-1 | Pickup API | 200 + PickedUpAt |
| RV2-2 | Delivering API | 200 + OutForDeliveryAt |
| RV2-3 | Delivered API | 200 + Order status=completed |
| RV2-4 | Location update | 200 + DeliveryTracking record |
| RV2-5 | SignalR hub | Playwright: connect → receive location push |
| RV2-6 | E2E Playwright | community-delivery-flow.spec.ts PASS |
| RV2-7 | DB tracking | ≥3 rows per delivery in DeliveryTracking |
