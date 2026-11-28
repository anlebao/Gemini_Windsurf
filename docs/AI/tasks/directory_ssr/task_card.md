# Task Card: Directory SSR — Blazor Server-Side Rendering cho KhachLink Directory Profile

> **Status:** PLANNED (awaiting implementation approval)
> **Priority:** P1 — performance improvement cho Directory users
> **Created:** 2026-08-23
> **Master plan:** `docs/AI/tasks/directory_ssr/master_plan.md`
> **Prerequisite:** Phase 2 scaling complete (Redis, multi-VPS, CDN images)
> **Effort:** 3-4 ngày (~27h)

## Problem

KhachLink Directory profile (Type 1) tải 22.8MB Blazor WASM cho 3 trang đơn giản (Home + StoreFinder + Profile). First load 10-15s trên 4G — quá chậm cho directory user (chỉ cần xem danh bạ + tìm cửa hàng).

**Root cause:** Blazor WASM tải toàn bộ .NET runtime + tất cả DLL trước khi render. Profile (Directory/FullCommerce) chỉ xác định sau khi Blazor boot xong → không thể skip WASM tải cho Directory.

## Solution

Tạo **Blazor SSR container riêng** (`vanan-directory`) cho Directory profile:
- Directory domain → serve SSR HTML (~200-500KB, <2s load)
- Commerce domain → serve Blazor WASM như hiện tại (22.8MB)
- nginx routing bằng map file + Gateway API check
- Reuse UI Platform components + Shared DTOs + theme system

## Scope Checklist

### Phase A — Project scaffold + layout (Day 1)
- [ ] A1: Tạo `5_WebApps/Directory/VanAn.Directory.csproj` (Blazor SSR, net8.0)
- [ ] A2: `Program.cs` — AddRazorComponents + AddServerSideBlazor + HttpClient + IMemoryCache
- [ ] A3: `Components/App.razor` + `Components/Routes.razor`
- [ ] A4: `Services/InstanceConfigService.cs` — Gateway /api/v1/khachlink-instances/by-domain
- [ ] A5: `Services/ShopConfigService.cs` — Gateway /api/tenants/{id}/store-info
- [ ] A6: `Services/CatalogService.cs` — Gateway /api/tenants/search + /api/tenants/nearby
- [ ] A7: `Components/Layout/DirectoryLayout.razor` — adapt KhachLinkLayout (theme, NavFlags, style)
- [ ] A8: `dotnet build VanAn.sln` PASS

### Phase B — Pages (Day 2-3)
- [ ] B1: Copy static assets (bootstrap.min.css, app.css, lib/leaflet/)
- [ ] B2: `Components/Pages/Home.razor` — store directory + search box
- [ ] B3: `Components/Pages/StoreFinder.razor` — Leaflet map + geolocation + nearby search
- [ ] B4: `dotnet build VanAn.sln` PASS
- [ ] B5: Local smoke test — `dotnet run` + curl http://localhost:5000/

### Phase C — Docker + nginx + CI/CD (Day 3-4)
- [ ] C1: `5_WebApps/Directory/Dockerfile` — multi-stage build (sdk → aspnet)
- [ ] C2: `docker-compose.directory.yml` — vanan-directory service (port 8080, 256MB limit)
- [ ] C3: Update `5_WebApps/KhachLink/nginx.conf` — Directory routing (map + proxy_pass)
- [ ] C4: `scripts/add-directory-domain.sh` — script thêm Directory domain + reload nginx
- [ ] C5: Update `docker-compose.khachlink.yml` — thêm vanan-directory service
- [ ] C6: Update `.github/workflows/cd-multivps.yml` — Build & push Directory + Deploy job
- [ ] C7: Update `VanAn.sln` — thêm VanAn.Directory.csproj

### Phase D — RV (Day 4)
- [ ] D1: `dotnet build VanAn.sln -c Release` → 0 errors
- [ ] D2: CI pipeline PASS (build + unit tests + integration tests)
- [ ] D3: CD pipeline PASS — Directory container deployed to KhachLink VPS
- [ ] D4: Directory domain (timlathay.com) load <3s (verify via curl + browser)
- [ ] D5: Home page render OK — stores list hiển thị
- [ ] D6: StoreFinder page render OK — map + nearby stores
- [ ] D7: Commerce domain vẫn hoạt động (WASM không bị ảnh hưởng)
- [ ] D8: `docker stats` — Directory container <256MB RAM

## Prerequisites

- Phase 2 scaling complete (Redis, multi-VPS)
- KhachLink VPS (vanan-khachlink) RUNNING — e2-small 2GB
- Gateway API endpoints anonymous accessible:
  - `GET /api/tenants/search`
  - `GET /api/tenants/nearby`
  - `GET /api/tenants/{id}/store-info`
  - `GET /api/v1/khachlink-instances/by-domain/{domain}`
- UI Platform csproj (`VanAn.UI.Platform.csproj`) — class library, net8.0
- Shared csproj (`VanAn.Shared.csproj`) — domain DTOs

## Verification

1. **Build:** `dotnet build VanAn.sln -c Release` → 0 errors
2. **Local smoke test:** `dotnet run --project 5_WebApps/Directory` → http://localhost:5000/ render Home page
3. **Docker build:** `docker build -t vanan-directory -f 5_WebApps/Directory/Dockerfile .` → success
4. **Deploy:** CD pipeline → vanan-directory container running on KhachLink VPS
5. **Load time:** `curl -o /dev/null -w '%{time_total}' http://timlathay.com/` → <3s
6. **Home page:** Browser mở timlathay.com → stores list hiển thị, search box hoạt động
7. **StoreFinder:** Browser mở timlathay.com/stores → map render, nearby stores hiển thị
8. **Commerce unaffected:** Browser mở diemthuong2.khachvip.online → Blazor WASM load như cũ
9. **Memory:** `docker stats vanan-directory-1` → <256MB

## Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | UI Platform components không tương thích SSR | Test VanAnCard sớm — nếu fail, dùng plain HTML cho Phase đầu |
| R2 | Leaflet map không chạy trong SSR | Map JS inject client-side (`<script>` trong page), SSR chỉ render HTML shell |
| R3 | nginx map file manual khi thêm Directory domain | Script `add-directory-domain.sh` — gọi API + update map + reload |
| R4 | Directory container ăn RAM quá nhiều | Memory limit 256MB, monitor `docker stats` |
| R5 | Blazor SSR render chậm nếu Gateway API chậm | IMemoryCache 5min cho instance config + store data |
| R6 | KhachLink VPS quá tải (3 container + Directory) | e2-small 2GB: seq(256MB) + khachlink(256MB) + certbot(50MB) + directory(256MB) = ~820MB, còn 1.2GB headroom |

## Related

- Master plan: `docs/AI/tasks/directory_ssr/master_plan.md`
- Detail coding plan: `docs/AI/tasks/directory_ssr/detail_coding_plan.md`
- KhachLink source: `5_WebApps/KhachLink/`
- KhachLinkLayout: `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor`
- Home page: `5_WebApps/KhachLink/Pages/Home.razor`
- StoreFinder: `5_WebApps/KhachLink/Pages/StoreFinder.razor`
- Gateway API: `2_Gateway/Controllers/TenantStoreController.cs`, `2_Gateway/Controllers/KhachLinkInstanceController.cs`
- UI Platform: `UI.Platform/VanAn.UI.Platform.csproj`
