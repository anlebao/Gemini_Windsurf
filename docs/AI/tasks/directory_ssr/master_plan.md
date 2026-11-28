# Directory SSR — Master Plan

**Created:** 2026-08-23
**Status:** PLANNED (awaiting implementation)
**Branch target:** `main`
**Source:** User request — KhachLink Directory profile (Type 1) tải 22.8MB Blazor WASM cho 3 trang đơn giản (Home + StoreFinder). Cần <3s load time cho Directory users.

## Problem

### Hiện trạng (verify 2026-08-23)

KhachLink là **Blazor WebAssembly SPA** — tất cả 22.8MB tải về **trước khi** render bất cứ gì. Profile (Directory/FullCommerce/...) chỉ được xác định **SAU khi** Blazor boot xong, qua `KhachLinkInstanceHttpService.GetByCurrentDomainAsync()` gọi Gateway API.

```
Luồng hiện tại:
Browser → tải 22.8MB WASM → Blazor boot (~10-15s trên 4G) → fetch /api/v1/khachlink-instances/by-domain/{domain}
→ nhận Profile → ẩn/hiện nav items (NavFlags)
```

**Tất cả user tải cùng 22.8MB bất kể profile.** NavFlags chỉ ẩn UI, không giảm tải.

### Phân tích dung lượng (verify từ vanan-khachlink VPS)

| Thành phần | Dung lượng | Directory có cần? |
|---|---|---|
| `_framework/` (.NET runtime + DLLs + WASM) | **20.6MB** raw / ~6MB gz | **CÓ** — Blazor runtime bắt buộc |
| `html5-qrcode.min.js` + `jsQR.js` | ~450KB | KHÔNG (Scan page) |
| `leaflet.js` + `leaflet.css` | ~400KB | CÓ (StoreFinder dùng map) |
| `cart-animation.js` + `fingerprint.js` + `qrcode.js` + `qr-wallet.js` + `voice-note.js` | ~90KB | KHÔNG |
| `bootstrap.min.css` + `app.css` + `VanAn.KhachLink.styles.css` | ~300KB | CÓ |
| **Total** | **22.8MB** | Directory cần ~700KB |

### Directory profile = 3 trang (từ KhachLinkNavFlags.ForProfile)

| Trang | Route | Dữ liệu cần | Interactivity |
|---|---|---|---|
| Home | `/` | Danh bạ stores — `GET /api/tenants/search` | Search box, click vào store |
| StoreFinder | `/stores` | Tìm cửa hàng gần — `GET /api/tenants/nearby` | Geolocation, Leaflet map, search |
| Profile | `/profile` | Thông tin user | Login, xem profile |

**Phase đầu: chỉ Home + StoreFinder** (theo user decision 2026-08-23). Profile defer — Directory user có thể login qua Commerce domain.

### Tại sao bỏ cách 1 (Conditional JS) và cách 2 (Lazy Assembly)

| Cách | Tiết kiệm | Đạt 3-5s? | Verdict |
|---|---|---|---|
| 1. Conditional JS loading | ~540KB / 22.8MB | KHÔNG (vẫn 20.6MB WASM) | **BỎ** — gain không đáng |
| 2. Lazy Assembly Loading | ~5-8MB | KHÔNG (12-14MB còn lại) | **BỎ** — refactor lớn, vẫn không đạt |
| 3. Static HTML / Blazor SSR | 22MB | CÓ (<1-2s) | **CHỌN** — cách duy nhất đạt target |

## Solution

### Kiến trúc tổng thể

```
vanan-khachlink VPS (136.85.111.51)
├── vanan-khachlink-1 (existing, port 80, nginx serving Blazor WASM static files)
│   ├── Directory domain → proxy_pass http://vanan-directory:8080
│   └── Commerce domain  → /usr/share/nginx/html (WASM, existing behavior)
├── vanan-directory-1 (NEW, Blazor SSR, port 8080)
│   ├── / (Home — danh bạ stores)
│   └── /stores (StoreFinder — map + nearby)
├── vanan-seq-1 (existing, logging)
└── vanan-certbot-1 (existing, SSL)
```

### Tech stack: Blazor SSR (.NET 8)

**Lý do chọn Blazor SSR thay vì Plain HTML+JS:**
1. **Reuse 100%** — UI Platform components, theme system, NavFlags, style override (Issue #143), ShopConfig logic
2. **1 codebase** — sửa 1 chỗ, áp dụng cho cả Directory + Commerce. Maintenance thấp
3. **Admin-friendly** — SystemAdmin sửa label/theme qua ShopERP UI (đã có), Directory SSR render tự động
4. **Tốc độ đạt target** — 1-2s first load (server render + HTML transport), trong 3-5s
5. **Upgrade path** — sau này muốn thêm interactivity, switch render mode, không rewrite

### VPS hosting: KhachLink VPS (chia sẻ)

Directory container chạy trên `vanan-khachlink` VPS (e2-small 2GB):
- Chia sẻ nginx (vanan-khachlink-1) cho routing
- Không thêm VPS mới → tiết kiệm chi phí
- Directory container memory limit: 256MB (Blazor SSR lightweight)

### nginx routing: Gateway API check + map file

```nginx
# Trong nginx config của vanan-khachlink-1
# Directory domains → proxy_pass http://vanan-directory:8080
# Commerce domains  → root /usr/share/nginx/html (existing)

# Cách implement: nginx map file + reload script
map $host $is_directory {
    default 0;
    timlathay.com 1;          # Directory domain (đã có)
    # Thêm domain mới qua script add-directory-domain.sh
}

server {
    listen 80;
    server_name _;

    if ($is_directory) {
        proxy_pass http://vanan-directory:8080;
    }

    # Default: serve WASM static files (existing behavior)
    root /usr/share/nginx/html;
    # ...
}
```

**Script `add-directory-domain.sh`:**
- Gọi Gateway API kiểm tra profile domain
- Nếu Directory → thêm vào map file + reload nginx
- Nếu không → báo lỗi

## Component reuse map

| KhachLink WASM hiện tại | Directory SSR mới | Reuse strategy |
|---|---|---|
| `KhachLinkLayout.razor` (theme, NavFlags, style) | `DirectoryLayout.razor` | **ADAPT** — bỏ IJSRuntime/PWAService, giữ theme + NavFlags + style logic |
| `ShopConfigHttpService` | `ShopConfigService.cs` | **ADAPT** — server-side HttpClient, không cần IHttpClientFactory |
| `KhachLinkInstanceHttpService` | `InstanceConfigService.cs` | **ADAPT** — server-side, bỏ localStorage cache, dùng IMemoryCache |
| `Home.razor` | `Home.razor` (SSR) | **ADAPT** — simplify, chỉ store directory + search, bỏ cart/commerce |
| `StoreFinder.razor` | `StoreFinder.razor` (SSR) | **ADAPT** — Leaflet JS inject client-side, SSR render HTML shell |
| UI Platform components | **REUSE** — ProjectReference `VanAn.UI.Platform.csproj` |
| `VanAn.Shared` domain DTOs | **REUSE** — ProjectReference `VanAn.Shared.csproj` |

## Gateway API endpoints (đã có, anonymous)

| Endpoint | Method | Purpose | Controller |
|---|---|---|---|
| `/api/tenants/search?name=&lat=&lng=` | GET | Search stores by name/location | TenantStoreController |
| `/api/tenants/nearby?lat=&lng=&radiusKm=` | GET | Find nearby stores | TenantStoreController |
| `/api/tenants/{tenantId}/store-info` | GET | Get store details | TenantStoreController |
| `/api/tenants/by-slug/{slug}` | GET | Get store by slug | TenantStoreController |
| `/api/v1/khachlink-instances/by-domain/{domain}` | GET | Get instance config (profile, navflags, style) | KhachLinkInstanceController |

## Files mới cần tạo

```
5_WebApps/Directory/
├── VanAn.Directory.csproj              (Blazor SSR, net8.0, ProjectReference UI.Platform + Shared)
├── Program.cs                          (AddRazorComponents + AddServerSideBlazor + HttpClient + IMemoryCache)
├── Dockerfile                          (multi-stage: sdk build → aspnet runtime)
├── nginx-directory.conf                (optional — nếu Directory container serve trực tiếp)
├── Components/
│   ├── App.razor                       (Root component, SSR render mode)
│   ├── Routes.razor                    (Router — chỉ 2 routes: / + /stores)
│   ├── Layout/
│   │   └── DirectoryLayout.razor       (Adapt từ KhachLinkLayout — theme, NavFlags, style)
│   └── Pages/
│       ├── Home.razor                  (/ — danh bạ stores + search)
│       └── StoreFinder.razor           (/stores — Leaflet map + geolocation + nearby)
├── Services/
│   ├── InstanceConfigService.cs        (HttpClient → Gateway /api/v1/khachlink-instances/by-domain, IMemoryCache 5min)
│   ├── ShopConfigService.cs            (HttpClient → Gateway /api/tenants/{id}/store-info)
│   └── CatalogService.cs              (HttpClient → Gateway /api/tenants/search + /api/tenants/nearby)
└── wwwroot/
    ├── bootstrap.min.css               (copy từ KhachLink)
    ├── app.css                         (copy từ KhachLink)
    └── lib/leaflet/                    (copy từ KhachLink — leaflet.js + leaflet.css)

docker-compose.directory.yml            (new — vanan-directory service, chạy trên KhachLink VPS)
scripts/add-directory-domain.sh         (new — script thêm Directory domain vào nginx map + reload)
```

## Files sửa

```
5_WebApps/KhachLink/nginx.conf          (thêm Directory routing logic — map + proxy_pass)
docker-compose.khachlink.yml            (thêm vanan-directory service)
.github/workflows/cd-multivps.yml       (thêm Build & push Directory + Deploy Directory)
VanAn.sln                               (thêm VanAn.Directory.csproj)
```

## Phases

### Phase A — Project scaffold + layout (Day 1)
- Tạo `5_WebApps/Directory/` project
- `VanAn.Directory.csproj` — Blazor SSR, ProjectReference UI.Platform + Shared
- `Program.cs` — bootstrap, DI, HttpClient, IMemoryCache
- `App.razor` + `Routes.razor`
- `DirectoryLayout.razor` — adapt KhachLinkLayout
- Services: InstanceConfigService, ShopConfigService, CatalogService
- Build PASS

### Phase B — Pages (Day 2-3)
- `Home.razor` — store directory + search
- `StoreFinder.razor` — Leaflet map + geolocation + nearby
- Copy static assets (bootstrap, app.css, leaflet)
- Build PASS + local smoke test

### Phase C — Docker + nginx + CI/CD (Day 3-4)
- `Dockerfile` — multi-stage build
- `docker-compose.directory.yml` — vanan-directory service
- `nginx.conf` update — Directory routing
- `scripts/add-directory-domain.sh`
- CI/CD: Build & push Directory image + Deploy job
- Deploy to KhachLink VPS

### Phase D — RV (Day 4)
- Build PASS
- Deploy SUCCESS
- Directory domain load <3s (verify via curl + browser)
- Home page render OK (stores list)
- StoreFinder page render OK (map + nearby)
- Commerce domain vẫn hoạt động (WASM không bị ảnh hưởng)

## Hard stops (governance)

- **Domain PURE** — không sửa Domain.cs
- **UI Platform components bắt buộc** — không bypass
- **Gateway = Order Creator + Routed Async Delivery (Option C)** — không revert
- **Multi-tenancy enforced at every layer**
- **No new DbContext** — Directory không inject IVanAnDbContext, chỉ gọi Gateway API qua HttpClient
- **KhachLink boundary** — Directory không query local DB, chỉ HTTP via Gateway

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | UI Platform components không tương thích SSR | Verify sớm — test 1 component (VanAnCard) trước khi build toàn bộ |
| R2 | Leaflet map không chạy trong SSR | Map JS inject client-side (`<script>` trong page), SSR chỉ render HTML shell |
| R3 | nginx map file manual khi thêm Directory domain | Script `add-directory-domain.sh` — gọi API + update map + reload |
| R4 | Directory container ăn RAM quá nhiều trên KhachLink VPS | Memory limit 256MB, monitor qua `docker stats` |
| R5 | Blazor SSR render chậm nếu Gateway API chậm | IMemoryCache 5min cho instance config + store data |
| R6 | Auth cookie không share giữa Directory + Commerce | Phase đầu không có Profile page — defer đến Phase 2 |

## Related

- Task card: `docs/AI/tasks/directory_ssr/task_card.md`
- Detail coding plan: `docs/AI/tasks/directory_ssr/detail_coding_plan.md`
- KhachLink source: `5_WebApps/KhachLink/`
- UI Platform: `UI.Platform/`
- Gateway API: `2_Gateway/Controllers/TenantStoreController.cs`, `2_Gateway/Controllers/KhachLinkInstanceController.cs`
