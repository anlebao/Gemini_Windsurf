# Task Card: Phase 2 — Horizontal Scale (P1)

> **Status:** PLANNED (awaiting Phase 1 completion + implementation approval)
> **Priority:** P1 — trước 100 tenant
> **Created:** 2026-08-22
> **Master plan:** `docs/AI/tasks/scaling_1000_tenants/master_plan.md`
> **Prerequisite:** Phase 1 complete (Npgsql pool, rate limiting, memory limit, VPS upgrade)
> **Effort:** 3-5 ngày code + 1 ngày VPS setup

## Problem

Sau Phase 1, hệ thống chịu được ~300 concurrent request + ~80 circuit trên 1 ShopERP VPS. Nhưng:

1. **Product images serve từ ShopERP** — 50 tenant × 150MB/tháng = 7.5GB → vượt Free Tier 37×. 1000 tenant = 150GB/tháng.
2. **No Redis backplane cho SignalR Gateway** — Gateway SignalR single-instance, không scale horizontal.
3. **Redis NOT configured trong ShopERP** — distributed cache = in-memory per container, restart mất cache.
4. **Single ShopERP VPS** — 1000 tenant trên 1 VPS 4GB → OOM. Cần multi-VPS.
5. **ShopInstance capacity KHÔNG enforce (TD-2)** — admin có thể gán 1000 tenant vào 1 VPS.
6. **No JS keepalive ping cho Blazor circuit** — chỉ `guard-camera.js` có 15s ping (`5_WebApps/ShopERP/Program.cs` L78-79 comment). Các trang khác không có → circuit dễ bị proxy idle kill khi user không tương tác lâu.

## Solution

### Task 2.1 — Product images → Cloud Storage/CDN

**Files mới:**

#### `3_CoreHub/Services/IProductImageStorageService.cs`
```csharp
public interface IProductImageStorageService
{
    Task<string> UploadAsync(Guid tenantId, Stream image, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string imageUrl, CancellationToken ct = default);
    Task<TenantImageStats> GetTenantStatsAsync(Guid tenantId, CancellationToken ct = default);
}
```

#### `3_CoreHub/Services/GcsProductImageStorageService.cs`
- Inject: `Google.Cloud.Storage.V1.StorageClient`, `ILogger`
- Upload: `vanan-product-images-{env}` bucket, key = `products/{tenantId}/{guid}.jpg`
- Return: CDN URL `https://cdn.vanan.cloud/products/{tenantId}/{guid}.jpg`
- Delete: xóa object khi product deleted
- Stats: list objects under prefix, count + total size

**Files sửa:**

#### `5_WebApps/ShopERP/Services/ProductImageService.cs`
- Thay vì lưu blob vào SQLite, gọi `IProductImageStorageService.UploadAsync`
- Lưu CDN URL vào `Product.ImageUrl` (đã có field)
- Fallback: nếu GCS không config → giữ behavior cũ (SQLite blob) — backward compatible

#### `5_WebApps/ShopERP/Components/Pages/Products/ProductManagement.razor`
- Upload flow: chọn ảnh → gọi API → nhận CDN URL → lưu product với CDN URL
- Display: `<img src="@product.ImageUrl" />` — browser load từ CDN, không qua ShopERP

#### `docker-compose.shoperp.yml`
```yaml
      - GoogleCloud__Storage__BucketName=${GCS_PRODUCT_IMAGES_BUCKET:-vanan-product-images-prod}
      - GoogleCloud__Storage__CdnBaseUrl=${GCS_CDN_BASE_URL:-https://cdn.vanan.cloud}
      - GOOGLE_APPLICATION_CREDENTIALS=/app/keys/gcs-sa.json
    volumes:
      - shoperp_data:/app/keys
      - ${GCS_SA_KEY_PATH:-./gcs-sa.json}:/app/keys/gcs-sa.json:ro
```

**GCS setup (manual, GCP console):**
1. Tạo bucket `vanan-product-images-prod` (multi-region hoặc same region với ShopERP)
2. Enable Cloud CDN trên bucket
3. Tạo Service Account `vanan-product-uploader` + download JSON key
4. Upload JSON key lên VPS tại `/app/keys/gcs-sa.json`

**Migration existing images:**
- Script `scripts/migrate-product-images-to-gcs.sh` — đọc SQLite blob → upload GCS → update `ImageUrl` field
- Chạy 1 lần, offline, cho mỗi tenant

### Task 2.2 — Redis backplane cho SignalR Gateway

**Files sửa:**

#### `docker-compose.gateway.yml` — thêm redis service
```yaml
  redis:
    image: redis:7-alpine
    command: redis-server --maxmemory 256mb --maxmemory-policy allkeys-lru
    ports:
      - "6379:6379"
    networks:
      - vanan-network
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    deploy:
      resources:
        limits:
          memory: 384m
```

#### `2_Gateway/Program.cs` (L75)
```csharp
// BEFORE:
_ = builder.Services.AddSignalR();

// AFTER:
var signalRBuilder = builder.Services.AddSignalR();
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    signalRBuilder.AddStackExchangeRedis(redisConnection, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("VanAn-SignalR");
    });
}
```

#### `docker-compose.gateway.yml` (gateway service env)
```yaml
      - ConnectionStrings__Redis=redis:6379
```

### Task 2.3 — Redis cho ShopERP distributed cache

**Files sửa:**

#### `docker-compose.shoperp.yml` (L51)
```yaml
# BEFORE:
      - ConnectionStrings__Redis=not-configured

# AFTER:
      - ConnectionStrings__Redis=redis://${GATEWAY_REMOTE_HOST}:6379
```

> **Lưu ý:** ShopERP connect Redis trên gateway VPS qua VPC internal IP (free egress). Code đã có sẵn conditional Redis registration (`5_WebApps/ShopERP/Program.cs` L427-436) — chỉ cần set env var.

### Task 2.4 — Thêm vanan-shop-b VPS + ShopInstance mới

**VPS setup (manual, GCP console):**
1. Tạo VPS `vanan-shop-b` (e2-medium 4GB / 1 vCPU, asia-southeast1-b, same VPC)
2. Generate ShopInstance ID mới (vd: `b3f1a9c2-8d4e-4f7b-a6c1-9e0d2b5a7f33`)
3. SSH vào VPS, chạy `scripts/deploy-shoperp.sh` với `SHOP_INSTANCE_ID=b3f1a9c2-...`
4. Trên Gateway, tạo ShopInstance record: `POST /api/v1/shop-instances` với ID mới + `MaxTenants=300`

**Files sửa:**

#### `docs/operations/Multi_VPS_Deployment_Guide.md` — append section
```markdown
## vanan-shop-b VPS

| VPS | External IP | Internal IP | Spec | Vai trò |
|---|---|---|---|---|
| vanan-shop-b | TBD | 10.148.0.5 | e2-medium, Debian 12 | ShopERP (per-tenant SQLite + NATS subscriber) |

**ShopInstance ID:** `b3f1a9c2-8d4e-4f7b-a6c1-9e0d2b5a7f33`
```

#### `scripts/deploy-shoperp.sh` — parameterize `SHOP_INSTANCE_ID` (đã có, verify)
#### `.github/workflows/cd-multivps.yml` — thêm deploy step cho vanan-shop-b

### Task 2.5 — Enforce capacity check (TD-2 fix)

**Files sửa:**

#### `3_CoreHub/Services/TenantManagementService.cs`
```csharp
public async Task AssignShopInstanceAsync(Guid tenantId, Guid shopInstanceId, CancellationToken ct = default)
{
    // NEW: Check capacity before assigning
    int currentCount = await _shopInstanceService.CountTenantsAsync(shopInstanceId, ct);
    var instance = await _shopInstanceService.GetByIdAsync(shopInstanceId, ct);
    if (instance == null)
        throw new InvalidOperationException($"ShopInstance {shopInstanceId} not found");

    if (currentCount >= instance.MaxTenants)
        throw new InvalidOperationException(
            $"ShopInstance {instance.Label} đã đầy ({currentCount}/{instance.MaxTenants}). " +
            "Vui lòng chọn Instance khác hoặc tăng MaxTenants.");

    // Existing assign logic...
}
```

#### `2_Gateway/Controllers/TenantsController.cs`
- Catch `InvalidOperationException` từ `AssignShopInstanceAsync` → return 409 Conflict với message

#### `5_WebApps/ShopERP/Components/Pages/Admin/TenantManagement.razor`
- Hiển thị error message khi gán tenant vượt capacity

### Task 2.6 — Admin UI capacity dashboard (TD-4 fix)

**Files sửa:**

#### `5_WebApps/ShopERP/Components/Pages/Admin/ShopInstances.razor`
- Thêm cột "Capacity còn lại" = `MaxTenants - CountTenants`
- Thêm cột "RAM usage" (gọi health check endpoint của VPS đó)
- Thêm cột "Bandwidth usage" (GCP monitoring API hoặc manual input)
- Color coding: xanh (< 70%), vàng (70-90%), đỏ (> 90%)

### Task 2.7 — JS keepalive ping cho Blazor circuit (Giải pháp 5)

**Problem:** Chỉ `guard-camera.js` có 15s ping (`5_WebApps/ShopERP/Program.cs` L78-79 comment). Các trang khác (POS, Orders, Kitchen, Admin) không có → circuit dễ bị proxy idle kill khi user không tương tác lâu (vd. nhân viên POS đứng xem danh sách đơn 5 phút).

**Files mới:**

#### `5_WebApps/ShopERP/wwwroot/js/circuit-keepalive.js`
```javascript
// Circuit keepalive — gọi .NET method mỗi 10s để giữ circuit sống qua proxy idle timeout.
// Tương tự guard-camera.js 15s ping (Program.cs L78-79 comment).
// Chỉ chạy khi Blazor circuit active (DotNet.invokeMethodAsync available).
window.vananCircuitKeepalive = (function () {
    var intervalId = null;

    function start() {
        if (intervalId) return;
        intervalId = setInterval(function () {
            try {
                if (window.DotNet && DotNet.invokeMethodAsync) {
                    DotNet.invokeMethodAsync('VanAn.ShopERP', 'CircuitKeepalivePing')
                        .catch(function (e) {
                            // Circuit disposed hoặc đang reconnect — không log, không throw
                        });
                }
            } catch (e) {
                // Blazor chưa load — bỏ qua
            }
        }, 10000); // 10s — nhỏ hơn KeepAliveInterval 15s để double-safe
    }

    function stop() {
        if (intervalId) { clearInterval(intervalId); intervalId = null; }
    }

    // Auto-start khi script load
    document.addEventListener('DOMContentLoaded', start);
    return { start: start, stop: stop };
})();
```

**Files sửa:**

#### `5_WebApps/ShopERP/Components/App.razor` — inject script sau `blazor.web.js`
```html
<script src="_framework/blazor.web.js"></script>
<script src="js/circuit-keepalive.js"></script>  <!-- NEW: keepalive ping 10s -->
```

#### `5_WebApps/ShopERP/Program.cs` — thêm JSInvokable method
```csharp
// Trong Program.cs hoặc một component root (vd. App.razor @code block)
// Method này chỉ để giữ circuit busy — không logic
[JSInvokable]
public static Task<bool> CircuitKeepalivePing()
{
    return Task.FromResult(true);
}
```

> **Lưu ý:** `JSInvokable` method phải thuộc một component hoặc static class được Blazor biết đến. Có thể tạo `5_WebApps/ShopERP/Components/CircuitKeepaliveService.razor` component nhỏ inject vào `App.razor` với `[JSInvokable]` method. Hoặc dùng `DotNet.invokeMethodAsync` với assembly name + static method.

**Verification:**
- Mở trang admin, không tương tác 10 phút → circuit vẫn sống (không hiện toast/modal)
- Check browser DevTools Network → WS connection có ping mỗi 10s
- Check server logs → không có "circuit disconnected" trong 10 phút idle

## Scope Checklist

- [ ] Task 2.1: Product images → GCS/CDN + migration script
- [ ] Task 2.2: Redis backplane SignalR Gateway
- [ ] Task 2.3: Redis distributed cache ShopERP
- [ ] Task 2.4: vanan-shop-b VPS + ShopInstance + routing table
- [ ] Task 2.5: Enforce capacity check trong AssignShopInstanceAsync
- [ ] Task 2.6: Admin UI capacity dashboard
- [ ] Task 2.7: JS keepalive ping 10s cho tất cả trang
- [ ] `dotnet build VanAn.sln` PASS
- [ ] Test: tạo tenant → auto-assign vào VPS capacity thấp nhất
- [ ] Test: upload product image → CDN URL
- [ ] Test: Redis backplane — SignalR broadcast qua 2 Gateway
- [ ] Test: mở trang admin, không tương tác 10 phút → không hiện toast/modal
- [ ] Load test: 200 concurrent request → không 503
- [ ] RV L1-L5 trên cả 2 VPS

## Prerequisites

- Phase 1 complete (Npgsql pool, rate limiting, memory limit, VPS upgrade)
- GCP console access — tạo VPS mới + GCS bucket + Service Account
- User approval cho GCS bucket + Cloud CDN (chi phí ~$1-5/tháng)

## Verification

1. **Build:** `dotnet build VanAn.sln -c Release` → 0 errors
2. **Guard:** `.\guard-check.ps1` → ALL PASSED
3. **Capacity check:** Tạo tenant khi VPS đầy → error 409 với message tiếng Việt
4. **CDN images:** Upload product image → URL trả về `https://cdn.vanan.cloud/...`
5. **Redis backplane:** Mở 2 tab browser → SignalR message broadcast cả 2
6. **Multi-VPS:** Tạo tenant trên vanan-shop-b → order từ KhachLink → NATS deliver đúng VPS
7. **Load test:** 200 concurrent request → p95 < 500ms, không 503
8. **RV L1-L5:** Cả 2 VPS healthy

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R2.1 | GCS migration mất ảnh cũ | Backup SQLite trước migrate, giữ R2 fallback 30 ngày |
| R2.2 | Redis thêm nhưng code không dùng | Verify `IDistributedCache` usage sau deploy |
| R2.3 | vanan-shop-b VPS config sai → order không deliver | Test NATS subject `vanan.cloud.order.created.{shopInstanceId}` trên VPS mới |
| R2.4 | Capacity check quá strict → block onboarding | Set `MaxTenants` đúng theo `ShopInstance_Capacity_Handbook.md` §3.1 |

## Related

- Master plan: `docs/AI/tasks/scaling_1000_tenants/master_plan.md`
- Phase 1 task card: `docs/AI/tasks/scaling_1000_tenants/phase1_critical_bottleneck_task_card.md`
- Phase 3 task card: `docs/AI/tasks/scaling_1000_tenants/phase3_production_hardening_task_card.md`
- `docs/operations/ShopInstance_Capacity_Handbook.md` §5 (CDN images), §6 TD-2/TD-4
