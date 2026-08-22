# Task Card: Phase 1 — Critical Bottleneck Fix (P0)

> **Status:** PLANNED (awaiting implementation approval)
> **Priority:** P0 — BẮT BUỘC trước khi thêm tenant
> **Created:** 2026-08-22
> **Master plan:** `docs/AI/tasks/scaling_1000_tenants/master_plan.md`
> **Effort:** 1-2 ngày code + 30 phút VPS upgrade

## Problem

Hệ thống hiện 3 VPS e2-small 2GB, capacity tối đa ~8-12 tenant (Free Tier). 5 bottleneck sẽ sập ngay khi tăng tenant:

1. **Npgsql pool = 100 (default)** — 100 concurrent query → pool exhaust → 503
2. **PostgreSQL `max_connections` = 100 (default)** — không headroom cho admin/migration
3. **Memory limit 512MB/container** — Gateway OOM khi 50+ concurrent request, ShopERP OOM khi 30+ circuit
4. **No rate limiting trên Gateway API** — 1 customer spam checkout → PG CPU 100%
5. **No response caching trên Gateway** — mỗi request catalog hit PG → overload
6. **Blazor circuit disconnect UX** — `KeepAliveInterval=30s` + `ClientTimeoutInterval=60s` + nginx WebSocket idle < 120s → modal "Kết nối bị gián đoạn" hiện mỗi vài chục giây. Modal full-screen che hết UI gây phiền user trên cả trang không-realtime.

## Solution

### Task 1.1 — Tăng Npgsql pool + PostgreSQL max_connections

**Files sửa:**

#### `2_Gateway/Program.cs` (L78-87)
```csharp
// BEFORE:
options.UseNpgsql(connectionString);

// AFTER:
options.UseNpgsql(connectionString, npgsql =>
{
    npgsql.EnableRetryOnFailure(
        maxRetryCount: 3,
        maxRetryDelay: TimeSpan.FromSeconds(2),
        errorCodesToAdd: null);
});
// Pool size set via connection string below
```

Connection string builder — append `MaximumPoolSize=300;MinimumPoolSize=10`:
```csharp
// If connectionString doesn't already contain MaximumPoolSize, append it
if (!connectionString.Contains("MaximumPoolSize", StringComparison.OrdinalIgnoreCase))
{
    connectionString += ";MaximumPoolSize=300;MinimumPoolSize=10;ConnectionIdleLifetime=300";
}
```

#### `docker-compose.gateway.yml` (postgres service, L10-34)
```yaml
  postgres:
    image: postgres:15-alpine
    command: postgres -c max_connections=300 -c shared_buffers=256MB -c work_mem=4MB
    environment:
      - POSTGRES_DB=${POSTGRES_DB:-VanAnCoreHub}
      - POSTGRES_USER=${POSTGRES_USER:-vanan_admin}
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
    # ... rest unchanged
```

### Task 1.2 — Tăng memory limit container + VPS upgrade

**Files sửa:**

#### `docker-compose.gateway.yml` (gateway service, L124-127)
```yaml
    deploy:
      resources:
        limits:
          memory: 1g  # was 512m
```

#### `docker-compose.shoperp.yml` (shoperp service, L75-78)
```yaml
    deploy:
      resources:
        limits:
          memory: 1536m  # was 512m
```

**VPS upgrade (manual, GCP console):**
- vanan-gateway: e2-small → e2-medium (4GB / 1 vCPU)
- vanan-shop-a: e2-small → e2-medium (4GB / 1 vCPU)
- vanan-khachlink: giữ e2-small (không thay đổi)

> **Lưu ý:** GCP VPS upgrade yêu cầu stop instance → change machine type → start. Downtime ~5 phút. Làm ngoài giờ.

### Task 1.3 — Rate limiting trên Gateway API

**Files sửa:**

#### `2_Gateway/Program.cs` — thêm sau L75 (`AddSignalR`)
```csharp
using System.Threading.RateLimiting;

// Rate limiting — classify by endpoint type
_ = builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("checkout", context =>
    {
        string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.AddPolicy("catalog", context =>
    {
        string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.AddPolicy("auth", context =>
    {
        string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});
```

#### `2_Gateway/Program.cs` — thêm `app.UseRateLimiter()` trước `app.MapRazorComponents`

#### `2_Gateway/Controllers/OrdersController.cs` — thêm `[EnableRateLimiting("checkout")]` trên checkout endpoint
#### `2_Gateway/Controllers/CatalogController.cs` — thêm `[EnableRateLimiting("catalog")]` trên catalog endpoints
#### `2_Gateway/Controllers/AuthController.cs` — thêm `[EnableRateLimiting("auth")]` trên login endpoint

### Task 1.4 — Response caching cho catalog/recommended endpoints

**Files sửa:**

#### `2_Gateway/Program.cs` — thêm `services.AddResponseCaching()` + `app.UseResponseCaching()`

#### `2_Gateway/Controllers/CatalogController.cs`
```csharp
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "tenantId", "page", "pageSize" })]
public async Task<IActionResult> GetCatalog(...)
{
    // ...
}
```

#### `2_Gateway/Controllers/RecommendedController.cs` (nếu có)
```csharp
[ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "tenantId" })]
public async Task<IActionResult> GetRecommended(...)
{
    // ...
}
```

### Task 1.5 — Health check endpoint chi tiết

**Files sửa:**

#### `2_Gateway/Program.cs` — thêm `app.MapHealthChecks("/health/detail", new HealthCheckOptions { ... })` với custom response
**Files mới:**

#### `2_Gateway/HealthChecks/GatewayHealthCheck.cs`
```csharp
public class GatewayHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        // Check: PG connection count, NATS connection, memory usage, circuit count
        // Return detailed JSON
    }
}
```

### Task 1.6 — Circuit disconnect UX fix (Giải pháp 2 + 3)

**Problem:** `KeepAliveInterval=30s` + `ClientTimeoutInterval=60s` (`5_WebApps/ShopERP/Program.cs` L97-104) + nginx WebSocket idle timeout < 120s → modal "Kết nối bị gián đoạn" hiện mỗi vài chục giây. Modal full-screen che hết UI (`App.razor` L20-27 + CSS L75-87).

**Files sửa:**

#### `5_WebApps/ShopERP/Program.cs` (L97-104)
```csharp
// BEFORE:
options.KeepAliveInterval = TimeSpan.FromSeconds(30);
options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);

// AFTER:
options.KeepAliveInterval = TimeSpan.FromSeconds(15);   // ping dày hơn, khó bị proxy kill
options.ClientTimeoutInterval = TimeSpan.FromSeconds(120); // chờ lâu hơn trước tuyên bố chết
```

#### `nginx/templates/vanan.multivps.conf.template` — WebSocket timeout
```nginx
# Location block cho Blazor WebSocket (_blazor endpoint)
location /_blazor {
    proxy_pass http://$SHOPERP_REMOTE_HOST;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_read_timeout 3600s;   # was default 60s — tăng để WebSocket không bị kill
    proxy_send_timeout 3600s;
    proxy_buffering off;
}
```

**Cloudflare WebSocket timeout (manual, dashboard):**
- Cloudflare dashboard → Network → WebSockets → enable
- Cloudflare dashboard → Rules → Page Rule cho `*vanan.cloud/_blazor*` → WebSocket timeout 120s
- Hoặc tắt Cloudflare proxy cho WebSocket endpoint (gray cloud)

#### `5_WebApps/ShopERP/Components/App.razor` (L20-27 + CSS L75-87) — modal → toast
```html
<!-- BEFORE: modal full-screen che hết UI -->
<div id="components-reconnect-modal" class="reconnect-modal">
    <div class="reconnect-modal-content">
        <p class="reconnect-title">Kết nối bị gián đoạn</p>
        <p class="reconnect-status components-reconnect-show">Đang kết nối lại…</p>
        <p class="reconnect-status components-reconnect-failed">Không thể kết nối lại. <button onclick="location.reload()">Tải lại trang</button></p>
        <p class="reconnect-status components-reconnect-rejected">Phiên đã hết hạn. <button onclick="location.reload()">Tải lại trang</button></p>
    </div>
</div>
```

```html
<!-- AFTER: toast nhỏ cho "reconnecting" + "failed", modal to chỉ cho "rejected" -->
<!-- Toast: góc phải dưới, không che UI -->
<div id="components-reconnect-modal" class="reconnect-toast">
    <div class="reconnect-toast-content">
        <span class="reconnect-icon">⟳</span>
        <span class="reconnect-status components-reconnect-show">Đang kết nối lại…</span>
        <span class="reconnect-status components-reconnect-failed">Không thể kết nối lại. <button onclick="location.reload()" class="vanan-button vanan-button--primary vanan-button--small">Tải lại</button></span>
    </div>
</div>

<!-- Modal to: chỉ cho "rejected" (phiên hết hạn) -->
<div id="components-reconnect-rejected-modal" class="reconnect-modal-rejected">
    <div class="reconnect-modal-content">
        <p class="reconnect-title">Phiên đã hết hạn</p>
        <p>Vui lòng tải lại trang để đăng nhập lại.</p>
        <button onclick="location.reload()" class="vanan-button vanan-button--primary">Tải lại trang</button>
    </div>
</div>
```

```css
/* Toast: góc phải dưới, nhỏ gọn, không che UI */
.reconnect-toast { display: none; position: fixed; bottom: 1rem; right: 1rem; z-index: 9999; }
.reconnect-toast-content { background: #fef3c7; border: 1px solid #f59e0b; border-radius: 8px; padding: 0.75rem 1rem; box-shadow: 0 2px 8px rgba(0,0,0,0.1); display: flex; align-items: center; gap: 0.5rem; }
.reconnect-icon { font-size: 1.25rem; color: #f59e0b; }
.reconnect-status { display: none; color: #6b7280; }
.components-reconnect-show .reconnect-status.components-reconnect-show,
.components-reconnect-failed .reconnect-status.components-reconnect-failed { display: inline; }
#components-reconnect-modal.components-reconnect-show,
#components-reconnect-modal.components-reconnect-failed { display: block; }

/* Modal to: chỉ cho rejected (che hết UI — cần reload) */
.reconnect-modal-rejected { display: none; position: fixed; top: 0; left: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.6); z-index: 9999; justify-content: center; align-items: center; }
.reconnect-modal-rejected.components-reconnect-rejected { display: flex; }
```

> **Lưu ý:** Blazor 8 chỉ add class `components-reconnect-show` / `components-reconnect-failed` / `components-reconnect-rejected` lên `#components-reconnect-modal`. Cần JS nhỏ để khi `rejected` thì show modal thứ 2, hoặc dùng CSS selector `#components-reconnect-modal.components-reconnect-rejected` để ẩn toast + show modal to.

## Scope Checklist

- [ ] Task 1.1: Npgsql pool 300 + PG max_connections=300
- [ ] Task 1.2: Memory limit Gateway 1g + ShopERP 1536m + VPS upgrade e2-medium
- [ ] Task 1.3: Rate limiting (checkout 10/min, catalog 60/min, auth 5/min)
- [ ] Task 1.4: Response caching catalog (60s) + recommended (300s)
- [ ] Task 1.5: Health check endpoint chi tiết
- [ ] Task 1.6: Circuit UX — KeepAliveInterval 15s + ClientTimeoutInterval 120s + nginx WebSocket timeout ≥ 120s + modal → toast
- [ ] `dotnet build VanAn.sln` PASS
- [ ] Load test: 100 concurrent request → không 503, không OOM
- [ ] Circuit test: mở trang admin, không tương tác 5 phút → không hiện modal "Kết nối bị gián đoạn"
- [ ] `guard-check.ps1` PASS
- [ ] RV L1-L3 trên VPS sau deploy

## Prerequisites

- User approval để upgrade VPS (gây ~5 phút downtime mỗi VPS)
- GCP console access (đã có — `vanan-prod` project)

## Verification

1. **Build:** `dotnet build VanAn.sln -c Release` → 0 errors
2. **Guard:** `.\guard-check.ps1` → ALL PASSED
3. **Local load test:** 100 concurrent request tới Gateway `/api/catalog` → không 503
4. **Deploy:** push `main` → `cd-multivps.yml` deploy 3 VPS
5. **RV L1:** API checks — `/health` trả 200, `/health/detail` trả JSON chi tiết
6. **RV L2:** Static assets — Blazor UI load OK
7. **RV L3:** PG connection count — `SELECT count(*) FROM pg_stat_activity` < 50 khi idle

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1.1 | VPS upgrade gây downtime | Upgrade ngoài giờ, có rollback plan (revert machine type) |
| R1.2 | Npgsql pool 300 nhưng PG max 100 (quên tăng) | Cả 2 cùng thay đổi trong Task 1.1 |
| R1.3 | Rate limiting quá strict → block user hợp lệ | Test với 60 req/min catalog — đủ cho browse |
| R1.4 | Response caching cache stale data | Duration 60s cho catalog là chấp nhận được (product thay đổi không liên tục) |

## Related

- Master plan: `docs/AI/tasks/scaling_1000_tenants/master_plan.md`
- Phase 2 task card: `docs/AI/tasks/scaling_1000_tenants/phase2_horizontal_scale_task_card.md`
- Existing rate limit task card: `docs/AI/tasks/api_rate_limit_classification_task_card.md` (Phase 1 implement subset)
