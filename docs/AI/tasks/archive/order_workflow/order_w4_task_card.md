# TASK CARD — Order Lifecycle Wave 4: KhachLink Polling Optimize (Adaptive Interval)

> **Status:** 📋 PLANNING — awaiting user review
> **Prerequisite:** W0 merged (SignalR wiring — not directly needed but conceptually related) · **Branch:** `feature/order-w4-khachlink-polling-optimize`
> **Estimated sessions:** 1
> **Gap fixed:** G4 (tối ưu polling thay vì SignalR)

## Objective

Giảm 60-70% polling request từ KhachLink bằng adaptive interval + tab visibility. Giữ HTTP polling (không SignalR) phù hợp VPS 2GB RAM với 500-1000 khách.

## Architecture Decision (D1, D5)

- **Giữ HTTP polling** — không SignalR cho KhachLink (500-1000 connections = risky trên VPS 2GB)
- **Adaptive interval:** poll nhanh khi cần (pending, ready), chậm khi ổn (confirmed, preparing), stop khi xong (completed)
- **Tab visibility:** pause polling khi tab hidden (đã có — `CheckTabVisibilityAsync`)
- **D5:** Không dynamic backoff — giữ đơn giản switch theo status

## Prerequisites (to verify in INVESTIGATE)

- [ ] `5_WebApps/KhachLink/Pages/OrderTracking.razor:305` — `PeriodicTimer(TimeSpan.FromSeconds(5))` fixed interval
- [ ] `5_WebApps/KhachLink/Pages/OrderTracking.razor:347-358` — `CheckTabVisibilityAsync` already checks `document.visibilityState`
- [ ] `5_WebApps/KhachLink/Pages/OrderTracking.razor:375-395` — `PollOrderStatusAsync` polls `api/customerorders/{orderId}/status`
- [ ] `order.Status?.Value` available for adaptive logic (line 371)

## Open Questions

| Q | Question | Default answer |
|---|----------|----------------|
| Q1 | `PeriodicTimer` hỗ trợ đổi interval dynamic? | Không — cần dispose + tạo mới, hoặc dùng `Task.Delay` loop |
| Q2 | Đổi `PeriodicTimer` → `Task.Delay` loop? | Yes — linh hoạt đổi interval theo status |
| Q3 | Stop polling hoàn toàn khi completed? | Yes — `_hasShownUpgradeModal` flag đã có, thêm `_pollingStopped` flag |

## Files to Modify (estimated 1 file)

| File | Action | Lines |
|------|--------|-------|
| `5_WebApps/KhachLink/Pages/OrderTracking.razor` | UPDATE — replace `PeriodicTimer` with `Task.Delay` loop + adaptive interval | +30 lines |

## Detailed Task List

### W4-T1: Replace `PeriodicTimer` with adaptive `Task.Delay` loop

```csharp
// OrderTracking.razor — replace StartPolling/PollingLoopAsync

private void StartPolling()
{
    if (_pollingCts != null) return; // Prevent duplicate
    
    _pollingCts = new CancellationTokenSource();
    isPolling = true;
    
    _ = Task.Run(async () => await PollingLoopAsync(_pollingCts.Token));
}

private async Task PollingLoopAsync(CancellationToken cancellationToken)
{
    try
    {
        while (!cancellationToken.IsCancellationRequested && !_pollingStopped)
        {
            // Adaptive interval based on current order status
            int intervalSeconds = GetPollingInterval(order?.Status?.Value);
            
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
            
            if (cancellationToken.IsCancellationRequested) break;
            
            // Skip polling when tab is hidden (battery friendly)
            var isVisible = await CheckTabVisibilityAsync();
            if (!isVisible) continue;
            
            await InvokeAsync(async () => 
            {
                await PollOrderStatusAsync();
                StateHasChanged();
            });
        }
    }
    catch (OperationCanceledException) { /* Expected during shutdown */ }
    catch (Exception ex)
    {
        Console.WriteLine($"Polling error: {ex.Message}");
    }
}

/// <summary>
/// Adaptive polling interval — poll faster when status is about to change, slower when stable.
/// </summary>
private static int GetPollingInterval(string? status) => status switch
{
    "pending"   => 5,   // Chờ xác nhận — khách đang chờ, cần update nhanh
    "confirmed" => 10,  // Đã xác nhận — đang chuẩn bị, ít thay đổi
    "preparing" => 10,  // Đang làm — ít thay đổi
    "ready"     => 5,   // Sẵn sàng — khách cần biết ngay
    "completed" => 0,   // Hoàn thành — STOP polling
    "delivered" => 0,   // Đã giao — STOP polling
    "cancelled" => 0,   // Đã hủy — STOP polling
    _           => 5    // Unknown — default 5s
};
```

### W4-T2: Add `_pollingStopped` flag + stop when completed

```csharp
// In PollOrderStatusAsync, after status change detected:
private async Task PollOrderStatusAsync()
{
    try
    {
        var httpClient = HttpClientFactory.CreateClient("gateway");
        var response = await httpClient.GetAsync($"api/customerorders/{orderId}/status");

        if (response.IsSuccessStatusCode)
        {
            var statusData = await response.Content.ReadFromJsonAsync<OrderStatusDto>();
            if (statusData != null && order != null && order.Status?.Value != statusData.Status)
            {
                await LoadOrderFromGateway();
                BuildStatusTimeline();
                CheckOrderDelivered(); // Existing — shows IdentityUpgradeModal

                // Stop polling when order reaches final state
                if (statusData.Status is "completed" or "delivered" or "cancelled")
                {
                    _pollingStopped = true;
                    isPolling = false;
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Polling order status error: {ex.Message}");
    }
}
```

### W4-T3: Build + verify

- `dotnet build VanAn.sln` — 0 errors
- Manual test: open OrderTracking → poll interval changes based on status
- Verify: completed order → polling stops (no more requests in Network tab)

## Verification Checklist

- [ ] Build 0 errors
- [ ] `PeriodicTimer` replaced with `Task.Delay` loop
- [ ] Adaptive interval: pending=5s, confirmed=10s, preparing=10s, ready=5s
- [ ] Polling STOPS when status = completed/delivered/cancelled
- [ ] Tab visibility check still works (pause when hidden)
- [ ] No SignalR connection (HTTP polling only)
- [ ] No memory leak: `_pollingCts` disposed on `DisposeAsync`

## Rollback Plan

Revert to `PeriodicTimer(TimeSpan.FromSeconds(5))` fixed interval. Build passes (pre-existing polling behavior).

## Performance Impact (estimated)

| Metric | Before (fixed 5s) | After (adaptive) | Reduction |
|--------|-------------------|------------------|-----------|
| Requests per order (pending→completed, ~15 min) | 180 requests | ~60 requests | 67% |
| RPS at 1000 concurrent customers | 200 RPS | ~70 RPS | 65% |
| Bandwidth per customer | ~360 KB | ~120 KB | 67% |
| Battery impact | High (wake every 5s) | Medium (wake 5-10s, stop when done) | Significant |
