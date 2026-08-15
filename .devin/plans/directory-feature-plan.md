# DETAIL CODING PLAN — Directory Feature: Redirect + Blast Radius Isolation

> **Scope:** API + UI + nginx infrastructure
> **Risk:** Low-Medium (nginx misconfig → VPS down, nhưng có `nginx -t` gate + git rollback)
> **Files:** 4 modified, 1 new test file
> **No Domain layer change, no migration, no breaking change**
> **Plan date:** 2026-08-15

---

## PART A: Directory "Tìm hiểu" Redirect (Feature)

### Mục tiêu
Directory instance (`timlathay.com`) → StoreFinder search → mỗi tenant card có nút "Tìm hiểu" → redirect cross-domain đến tenant's KhachLink instance (FullCommerce/Reseller) → profile page có order/payment/loyalty.

### Luồng target
```
timlathay.com (Directory) → /stores (StoreFinder) → search tenants
  → mỗi tenant card có nút "Tìm hiểu"
  → click → redirect sang https://{tenant's KhachLink domain}/store/{slug}
  → FullCommerce/Reseller instance → profile page + order/payment/loyalty
```

### Change A1: API — TenantStoreDto + MapToStoreDto

**File:** `2_Gateway/Controllers/TenantStoreController.cs`

#### A1a. Thêm field vào TenantStoreDto (dòng 256-278)

```csharp
public record TenantStoreDto
{
    // ... existing fields ...
    public string? FooterColor { get; init; }

    /// <summary>Directory redirect: KhachLink instance CustomDomain for this tenant (if any).
    /// Null = tenant has no KhachLink instance → "Tìm hiểu" button hidden.
    /// Non-null = button redirects to https://{KhachLinkDomain}/store/{slug}</summary>
    public string? KhachLinkDomain { get; init; }
}
```

#### A1b. Đổi MapToStoreDto nhận thêm parameter

Hiện tại (dòng 222):
```csharp
private static TenantStoreDto MapToStoreDto(Tenant t, double? distanceKm = null) => new()
{
    // ... mapping ...
};
```

Thành:
```csharp
private static TenantStoreDto MapToStoreDto(Tenant t, double? distanceKm = null,
    Dictionary<Guid, string>? khachLinkDomainMap = null) => new()
{
    // ... existing mapping ...
    KhachLinkDomain = khachLinkDomainMap?.GetValueOrDefault(t.Id.Value)
};
```

**Tại sao truyền dict thay vì query trong method:** Tránh N+1 — batch query 1 lần rồi pass dict vào.

#### A1c. Batch query KhachLinkInstances trong Search endpoint

Trong `Search` (sau dòng 200, trước `return Ok`):
```csharp
// Batch query KhachLink instances for all matched tenants (1 query, no N+1)
var tenantIds = tenants.Select(t => t.Id.Value).ToList();
var khachLinkDomainMap = await _dbContext.KhachLinkInstances
    .AsNoTracking()
    .IgnoreQueryFilters()
    .Where(i => i.OwnerTenantId != null
        && tenantIds.Contains(i.OwnerTenantId.Value)
        && i.IsActive
        && i.Profile != KhachLinkProfile.Directory)  // Directory không có /store page
    .ToDictionaryAsync(i => i.OwnerTenantId!.Value, i => i.CustomDomain);

return Ok(tenants.Select(t =>
{
    double? dist = null;
    if (userLat.HasValue && userLng.HasValue
        && t.Settings?.Latitude.HasValue == true && t.Settings?.Longitude.HasValue == true)
    {
        dist = HaversineKm(userLat.Value, userLng.Value, t.Settings!.Latitude!.Value, t.Settings!.Longitude!.Value);
    }
    return MapToStoreDto(t, dist, khachLinkDomainMap);
}).ToList());
```

#### A1d. Batch query KhachLinkInstances trong Nearby endpoint

Trong `GetNearby` (dòng 132-146) — thêm batch query trước `.Select`:
```csharp
var tenants = await _dbContext.Tenants
    .AsNoTracking()
    .IgnoreQueryFilters()
    .Where(t => t.Status == TenantStatus.Active)
    .ToListAsync();

var nearby = tenants
    .Where(t => t.Settings?.Latitude.HasValue == true && t.Settings?.Longitude.HasValue == true)
    .Select(t => new { Tenant = t, Distance = HaversineKm(lat.Value, lng.Value, t.Settings!.Latitude!.Value, t.Settings!.Longitude!.Value) })
    .Where(x => x.Distance <= radiusKm)
    .OrderBy(x => x.Distance)
    .ToList();

// Batch query KhachLink instances for nearby tenants
var nearbyTenantIds = nearby.Select(x => x.Tenant.Id.Value).ToList();
var khachLinkDomainMap = await _dbContext.KhachLinkInstances
    .AsNoTracking()
    .IgnoreQueryFilters()
    .Where(i => i.OwnerTenantId != null
        && nearbyTenantIds.Contains(i.OwnerTenantId.Value)
        && i.IsActive
        && i.Profile != KhachLinkProfile.Directory)
    .ToDictionaryAsync(i => i.OwnerTenantId!.Value, i => i.CustomDomain);

return Ok(nearby.Select(x => MapToStoreDto(x.Tenant, x.Distance, khachLinkDomainMap)).ToList());
```

#### A1e. Cần thêm using

```csharp
using VanAn.Shared.Domain.Aggregates.KhachLinkAggregate;
```

Controller đã inject `IVanAnDbContext` → `_dbContext.KhachLinkInstances` đã sẵn sàng.

---

### Change A2: UI — StoreFinder.razor

**File:** `5_WebApps/KhachLink/Pages/StoreFinder.razor`

#### A2a. Thêm field vào StoreDto (dòng 489-499)

```csharp
public class StoreDto
{
    // ... existing fields ...
    public string? Slug { get; set; }
    public string? KhachLinkDomain { get; set; }  // NEW — Directory redirect target
}
```

#### A2b. Thêm nút "Tìm hiểu" trong tenant card (sau dòng 197, trước `</div>`)

```razor
@if (!string.IsNullOrEmpty(store.KhachLinkDomain))
{
    <VanAnButton Size="ButtonSize.Small" Variant="ButtonVariant.Primary"
                 OnClick="@(() => Navigation.NavigateTo(
                     $"https://{store.KhachLinkDomain}/store/{store.Slug ?? store.Id.ToString()}",
                     forceLoad: true))">
        <i class="bi bi-box-arrow-up-right me-1"></i> Tìm hiểu
    </VanAnButton>
}
```

**`forceLoad: true`** — bắt buộc cho cross-domain redirect (Blazor router không xử lý external URLs mặc định).

**Behavior:**
- Tenant CÓ KhachLink instance (FullCommerce/Reseller) → `KhachLinkDomain` != null → hiện nút "Tìm hiểu" → click redirect sang domain khác
- Tenant KHÔNG có KhachLink instance → `KhachLinkDomain` = null → nút ẩn → chỉ có nút "Xem trang cửa hàng" (existing, same-domain)

---

### Change A3: Test

**File:** `6_Tests/VanAn.Core.Tests/KhachLink/TenantStoreSearchTests.cs` (NEW)

```csharp
[Fact]
public async Task Search_ReturnsKhachLinkDomain_WhenTenantHasInstance()
{
    // Arrange: create tenant + KhachLinkInstance(OwnerTenantId = tenant.Id, Profile = FullCommerce)
    // Act: GET /api/tenants/search
    // Assert: response contains tenant with KhachLinkDomain = instance.CustomDomain
}

[Fact]
public async Task Search_ReturnsNullKhachLinkDomain_WhenTenantHasNoInstance()
{
    // Arrange: create tenant, no KhachLinkInstance
    // Act: GET /api/tenants/search
    // Assert: response contains tenant with KhachLinkDomain = null
}

[Fact]
public async Task Search_ReturnsNullKhachLinkDomain_WhenInstanceIsDirectory()
{
    // Arrange: create tenant + KhachLinkInstance(OwnerTenantId = tenant.Id, Profile = Directory)
    // Act: GET /api/tenants/search
    // Assert: KhachLinkDomain = null (Directory excluded — no /store page)
}
```

---

## PART B: Blast Radius Isolation (Infrastructure)

### Mục tiêu
Directory instance (`timlathay.com`) ổn định 10k-20k users ngay cả khi FullCommerce/Reseller instances bị nghẽn hay quá tải.

### Directory's Actual Footprint (rất nhẹ)

| API Call | Frequency | Cacheable? | Write? |
|---|---|---|---|
| `GET /api/v1/khachlink-instances/by-domain/{domain}` | 1x per session (cached 5min localStorage) | YES — static config | No |
| `GET /api/tenants/search?name=...` | Per search | YES — tenants change rarely | No |
| `GET /api/tenants/nearby?lat=...&lng=...` | Per search | YES — 1-5 min TTL OK | No |
| Static WASM files (JS/CSS/DLL) | 1x per session (browser cached) | YES — immutable fingerprinted | No |

**Directory = 100% read-only, 100% cacheable, 0 writes.**

### 5 Shared Dependencies (Blast Radius Points)

| # | Shared Resource | Directory Impact | Severity |
|---|---|---|---|
| 1 | nginx (Gateway VPS) — 1 instance routes ALL domains | nginx crash → all domains down | CRITICAL |
| 2 | KhachLink container (256m) — serves ALL domains | Container OOM → all domains down | HIGH |
| 3 | Gateway container (512m) — ALL API calls | Gateway overloaded → Directory search timeout | HIGH |
| 4 | PostgreSQL — 1 DB, ALL tenants | PG overloaded → Directory search slow | MEDIUM |
| 5 | Rate limit zones — shared per-IP | Single IP shares quota across domains | LOW |

### Biện pháp P0: nginx proxy_cache cho Directory API (HIGH impact, LOW cost)

**Mục tiêu:** Directory traffic không cần hit Gateway/PG — nginx cache response trực tiếp.

**File 1:** `nginx/nginx.conf` — thêm cache zone trong http block:

```nginx
# Directory isolation cache zone — serves cached API responses when Gateway is down
proxy_cache_path /var/cache/nginx/dir_api levels=1:2 keys_zone=dir_api_cache:10m
    max_size=100m inactive=10m use_temp_path=off;
```

**File 2:** `nginx/templates/vanan.multivps.conf.template` — timlathay.com HTTPS block (443):

```nginx
# Directory API — cached at nginx level (blast radius isolation from Gateway)
# 10k users search → 1 request hits Gateway, 9999 served from cache
# Gateway down → Directory still serves from cache for 2-10 minutes
location /api/tenants/search {
    proxy_cache dir_api_cache;
    proxy_cache_valid 200 2m;
    proxy_cache_valid 404 1m;
    proxy_cache_key "$scheme$request_method$host$request_uri";
    add_header X-Cache-Status $upstream_cache_status;
    limit_req zone=dir_api burst=200 nodelay;
    limit_conn perip_conn 20;
    proxy_pass         http://${KHACHLINK_REMOTE_HOST}:80;
    proxy_http_version 1.1;
    proxy_set_header   Host $host;
    proxy_set_header   X-Real-IP $remote_addr;
    proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;
    proxy_read_timeout 60s;
}

location /api/tenants/nearby {
    proxy_cache dir_api_cache;
    proxy_cache_valid 200 2m;
    proxy_cache_key "$scheme$request_method$host$request_uri";
    add_header X-Cache-Status $upstream_cache_status;
    limit_req zone=dir_api burst=200 nodelay;
    limit_conn perip_conn 20;
    proxy_pass         http://${KHACHLINK_REMOTE_HOST}:80;
    proxy_http_version 1.1;
    proxy_set_header   Host $host;
    proxy_set_header   X-Real-IP $remote_addr;
    proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;
    proxy_read_timeout 60s;
}

location /api/v1/khachlink-instances/by-domain/ {
    proxy_cache dir_api_cache;
    proxy_cache_valid 200 10m;
    proxy_cache_key "$scheme$request_method$host$request_uri";
    add_header X-Cache-Status $upstream_cache_status;
    limit_req zone=dir_api burst=200 nodelay;
    limit_conn perip_conn 20;
    proxy_pass         http://${KHACHLINK_REMOTE_HOST}:80;
    proxy_http_version 1.1;
    proxy_set_header   Host $host;
    proxy_set_header   X-Real-IP $remote_addr;
    proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;
    proxy_read_timeout 60s;
}

# All other API — no cache, separate rate limit zone
location /api/ {
    limit_req zone=dir_api burst=200 nodelay;
    limit_conn perip_conn 20;
    proxy_pass         http://${KHACHLINK_REMOTE_HOST}:80;
    proxy_http_version 1.1;
    proxy_set_header   Host $host;
    proxy_set_header   X-Real-IP $remote_addr;
    proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;
    proxy_read_timeout 60s;
}
```

**Lưu ý:** Cache locations phải đặt TRƯỚC `location /api/` (nginx longest-prefix match).

**Hiệu quả:**
- 10k users search "tiệm bánh" → 1 request hit Gateway, 9999 served from nginx cache
- Gateway down → Directory vẫn hoạt động 2-10 phút từ cache
- PG down → Directory vẫn hoạt động
- **Directory gần như hoàn toàn độc lập với Gateway health**

### Biện pháp P1: Separate rate limit zone cho Directory (MEDIUM impact, LOW cost)

**Mục tiêu:** FullCommerce traffic không exhaust Directory's rate limit quota.

**File:** `nginx/nginx.conf` — thêm 2 zone riêng:

```nginx
# Directory isolation — separate rate limit zones (not shared with FullCommerce)
limit_req_zone $binary_remote_addr zone=dir_api:10m rate=50r/s;
limit_req_zone $binary_remote_addr zone=dir_web:10m rate=30r/s;
```

**File:** `nginx/templates/vanan.multivps.conf.template` — timlathay.com blocks đổi:
- `zone=api` → `zone=dir_api` (cho tất cả /api/ locations trong timlathay.com block)
- `zone=web` → `zone=dir_web` (cho location / trong timlathay.com block)

**Hiệu quả:** Directory có quota riêng (50r/s API, 30r/s web), không bị FullCommerce "steal" quota.

### Biện pháp P2 + P3: DEFER (không implement lần này)

| Biện pháp | Lý do defer |
|---|---|
| P2: Separate KhachLink container cho Directory | WASM static files only → 256m đủ cho 10k-20k users. Container crash risk thấp. Chỉ cần khi FullCommerce thực sự OOM. |
| P3: CDN cho static assets | Browser đã cache WASM files (immutable). Chỉ cần khi first-load spike (vd: marketing campaign 10k users cùng lúc). |

---

## PART C: Files Summary

| # | File | Part | Change | Lines | Risk |
|---|---|---|---|---|---|
| 1 | `2_Gateway/Controllers/TenantStoreController.cs` | A | DTO field + batch query (Search + Nearby) | ~30 | Low — additive, nullable |
| 2 | `5_WebApps/KhachLink/Pages/StoreFinder.razor` | A | StoreDto field + nút "Tìm hiểu" | ~15 | Low — additive button |
| 3 | `6_Tests/VanAn.Core.Tests/KhachLink/TenantStoreSearchTests.cs` | A | 3 integration tests | ~80 | None — new file |
| 4 | `nginx/nginx.conf` | B | Cache zone + 2 rate limit zones | ~10 | Low — additive |
| 5 | `nginx/templates/vanan.multivps.conf.template` | B | Cache locations + dir_api/dir_web zones | ~50 | Medium — nginx misconfig risk |

**Total: ~185 lines, 5 files, 0 migration, 0 Domain change, 0 breaking change.**

---

## PART D: Verification Plan

### Build & Test

| Step | How | Expected |
|---|---|---|
| 1. Build | `dotnet build VanAn.sln` | 0 errors |
| 2. Guard | `guard-check.ps1` | ALL PASSED |
| 3. Tests | `dotnet test` | ALL PASS (including 3 new tests) |
| 4. nginx syntax | `docker run --rm nginx:1.25-alpine nginx -t` (with config) | Syntax OK |

### Deploy & Smoke Test

| Step | How | Expected |
|---|---|---|
| 5. Deploy | CD auto (push to main) | Gateway + KhachLink + nginx updated |
| 6. API test — search | `curl https://api2.khachvip.online/api/tenants/search` | Response có `khachLinkDomain` field |
| 7. API test — by-domain | `curl https://timlathay.com/api/v1/khachlink-instances/by-domain/timlathay.com` | 200 + Directory profile DTO |
| 8. Cache test | `curl -I https://timlathay.com/api/tenants/search` → check `X-Cache-Status` | First: `MISS`, Second: `HIT` |
| 9. UI test | Mở `timlathay.com/stores` → search | Tenant card có nút "Tìm hiểu" (nếu tenant có KhachLink instance) |
| 10. Redirect | Click "Tìm hiểu" | Browser mở `https://{domain}/store/{slug}` (cross-domain) |
| 11. Isolation test | Stop Gateway container → `curl https://timlathay.com/api/tenants/search` | 200 (served from cache, `X-Cache-Status: HIT`) |

---

## PART E: Edge Cases

| Case | Behavior |
|---|---|
| Tenant có KhachLink instance nhưng `IsActive=false` | `khachLinkDomainMap` filter `i.IsActive` → field null → nút ẩn |
| Tenant có nhiều KhachLink instance | `ToDictionary` throw duplicate key → cần `.GroupBy` + `.First()` hoặc dùng `ToLookup` |
| Tenant có KhachLink instance nhưng không có Slug | Redirect URL dùng `store.Id.ToString()` (`/store/by-id/{id}`) |
| KhachLinkDomain = Directory instance | Filter `i.Profile != KhachLinkProfile.Directory` → excluded → nút ẩn |
| Gateway down + cache expired | nginx returns `502 Bad Gateway` (cache expired, upstream unavailable) → Directory shows error page |
| nginx cache disk full | `max_size=100m` → nginx auto-evicts oldest entries (LRU) |

### Edge case: Tenant có nhiều KhachLink instance

Nếu 1 tenant có cả FullCommerce + Reseller instance, `ToDictionary` sẽ throw. Fix:

```csharp
var khachLinkDomainMap = (await _dbContext.KhachLinkInstances
    .AsNoTracking()
    .IgnoreQueryFilters()
    .Where(i => i.OwnerTenantId != null
        && tenantIds.Contains(i.OwnerTenantId.Value)
        && i.IsActive
        && i.Profile != KhachLinkProfile.Directory)
    .ToListAsync())
    .GroupBy(i => i.OwnerTenantId!.Value)
    .ToDictionary(g => g.Key, g => g.First().CustomDomain);  // prefer first (FullCommerce if sorted)
```

Hoặc ưu tiên FullCommerce > Reseller:
```csharp
    .OrderBy(i => i.Profile)  // FullCommerce=0, Directory=1, Logistics=2, JobMarket=3, Reseller=4
    .GroupBy(i => i.OwnerTenantId!.Value)
    .ToDictionary(g => g.Key, g => g.First().CustomDomain);
```

---

## PART F: Risk Assessment

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| awk/sed strip wrong nginx block | Low | Medium (nginx wrong config) | Already tested — sed line-range deletion verified |
| `ssl_reject_handshake` not supported | Low | Low | nginx 1.25-alpine supports it (added in 1.19.4) |
| envsubst breaks markers | Low | Low | Markers use `@@` not `${}` — envsubst ignores them |
| `ToDictionary` duplicate key | Medium | Low (exception) | Use `GroupBy` + `.First()` (edge case fix above) |
| nginx cache stale data | Low | Low | 2min TTL for search/nearby, 10min for by-domain — acceptable for Directory |
| CD overwrites .env.gateway (flag reset) | Fixed | N/A | Already fixed in previous commit (preserve KHACHLINK_MULTIPROFILE_ENABLED) |

---

## PART G: Implementation Order

1. **Part A first** (feature) — API + UI + tests → build → verify
2. **Part B second** (infrastructure) — nginx config → deploy → verify cache
3. **Commit separately** — 2 commits:
   - Commit 1: `feat(khachlink): Directory "Tìm hiểu" redirect to tenant KhachLink domain`
   - Commit 2: `feat(nginx): Directory blast radius isolation — proxy_cache + separate rate limit zones`

**Lý do tách commit:** Part A là code change (build/test gate), Part B là config change (nginx -t gate). Tách commit giúp rollback dễ hơn nếu 1 phần fail.

---

## PART H: Không cần thay đổi

| File | Lý do |
|---|---|
| Domain.cs / KhachLinkInstance.cs | Entity đã có `OwnerTenantId` + `Profile` |
| Migration | Table đã tồn tại |
| KhachLinkLayout.razor | Không liên quan — layout xử lý by-domain, không xử lý search |
| NavMenu.razor | Không liên quan — NavFlags đã flag-driven |
| Store.razor | Đã có cart/checkout/order — redirect đến đây là đủ |
| Service layer (KhachLinkInstanceService) | Controller query trực tiếp `_dbContext` (follows existing pattern) |
| docker-compose files | Không cần — P2 (separate container) deferred |
