# MASTER PLAN: ADMIN MOBILE PWA (SHOPERP OPS ON PHONE/TABLET)

> **Status:** ACTIVE — APPROVED 2026-10-30 (user duyệt G1–G7 theo đề xuất mặc định SRS)
> **SRS:** `docs/requirements/van_an_admin_mobile_pwa_srs_v1.md` (v1.0)
> **Task card:** `docs/AI/tasks/task_admin_mobile_pwa.md`
> **Branch:** `main` · **KHÔNG migration · KHÔNG Domain · KHÔNG project mới**

---

## 1. MỤC TIÊU

Biến ShopERP thành **Admin Mobile PWA installable** cho nhân viên vận hành: cài icon "ShopERP" lên màn hình điện thoại/tablet → login → vào thẳng trang làm việc theo role (bếp→`/kitchen`, guard→`/guard/scan`, staff→`/pos`, owner→`/sitemap`), chạy standalone như app native. Tái dùng 100% pattern PWA đã proven ở KhachLink.

**Constraint cốt lõi (G1 — đã chốt):** Blazor Server = online-only. PWA tăng tốc khởi động + installable + deep-link; mất mạng → `offline.html`, KHÔNG offline nghiệp vụ.

## 2. HIỆN TRẠNG (đã xác minh code 2026-10-30)

| Tài sản | Vị trí | Ghi chú |
|---|---|---|
| PWA manifest mẫu | `5_WebApps/KhachLink/wwwroot/manifest.json` — standalone, `lang: vi`, icons 72→512 | Clone + đổi |
| Service worker | `KhachLink/wwwroot/service-worker.js` | Port nguyên logic |
| PWA bootstrap | `KhachLink/wwwroot/js/pwa.js` — SW register + update toast + **SRI-deadlock fix** | **Bắt buộc tái dùng** |
| Icons | `KhachLink/wwwroot/icons/` | Upscale ShopERP favicon tạm (G7) |
| Ops pages đã responsive | `POS/Create` · `Kitchen` · `Guard/Scan` · `Orders/Index`+`Detail`+`PrintBill` · `Booking/BookingCreate` · `Inventory/dashboard`+`alerts` · `Sitemap` | 7 trang vào scope |
| Viewport meta | `5_WebApps/ShopERP/Components/App.razor:6` | Đã có — chỉ thêm manifest/theme-color |
| Auth | Cookie login `/api/platform/login` → Blazor `[Authorize]` role | Giữ nguyên — PWA không đổi auth |

**Khoảng trống:** ShopERP chưa có `manifest.json`, `service-worker.js`, `offline.html`, `apple-touch-icon`, theme-color meta.

## 3. QUYẾT ĐỊNH ĐÃ CHỐT (user 2026-10-30 — theo đề xuất SRS §11)

| # | Quyết định |
|---|---|
| G1 | **Không offline nghiệp vụ** — chỉ shell/offline.html (Blazor Server bắt buộc online) |
| G2 | Accounting/admin pages có `@media` → **để browser**, app chỉ 7 trang ops |
| G3 | Role landing = **hardcode map** (role→url) trong code |
| G4 | 70 trang admin desktop → **ghi nợ**, không scope MVP |
| G5 | SW **không cache API** — chỉ static shell + offline.html |
| G6 | POS mất kết nối → circuit rớt → reload trang; đơn đã save còn nguyên, draft chưa lưu mất (ghi nhận) |
| G7 | Icon PWA = **upscale favicon hiện có** tạm thời, thiết kế sau |

## 4. SCOPE (phases + task card)

| Phase | Deliverable | Gate |
|---|---|---|
| P0 | `manifest.json` + icons + meta (App.razor) — installable icon | Cài được icon từ Chrome |
| P1 | `pwa.js` + `service-worker.js` + `offline.html` (port KhachLink) | Shell cache + offline fallback + update toast |
| P2 | Role landing redirect (hardcode map) + verify 7 ops pages standalone | T2/T3/T4 |
| P3 | Tests: bUnit/SSR render + E2E `shoperp-admin-pwa.spec.ts` (Gate 4) | Tests pass |
| P4 | Push → CD → RV production (install Android/iOS + T1-T10) | RV pass |

Checkpoint commit sau MỖI phase (pattern phiên liên tục đã chạy thành công với Tra soát).

## 5. THIẾT KẾ CHI TIẾT

### 5.1 `5_WebApps/ShopERP/wwwroot/manifest.json` (P0)

```json
{
  "id": "/",
  "name": "Vạn An ShopERP",
  "short_name": "ShopERP",
  "start_url": "/",
  "display": "standalone",
  "orientation": "any",            // G3: POS/kitchen cần landscape
  "background_color": "<brand>",
  "theme_color": "<brand>",
  "scope": "/", "lang": "vi",
  "icons": [ 72→512, purpose "any" + "maskable" ]
}
```

`App.razor` `<head>` thêm (sau viewport line 6):
```html
<link rel="manifest" href="manifest.json" />
<meta name="theme-color" content="<brand>" />
<link rel="apple-touch-icon" href="icons/icon-192x192.png" />
<meta name="apple-mobile-web-app-capable" content="yes" />
<meta name="apple-mobile-web-app-status-bar-style" content="default" />
```

Icons: copy `favicon.png` → `icons/icon-{72,96,128,144,152,192,384,512}x512.png` (upscale tạm — G7).

### 5.2 `service-worker.js` + `pwa.js` + `offline.html` (P1)

- Port `KhachLink/wwwroot/js/pwa.js` nguyên trạng → `ShopERP/wwwroot/js/pwa.js` (giữ SRI deadlock fix + update toast).
- `service-worker.js` scope `/`:
  - `CACHE_NAME = 'shoperp-pwa-v1'` (bump mỗi release)
  - Pre-cache: `/`, `offline.html`, `app.css`, `_framework/*` essentials, icons, `js/pwa.js`
  - **network-first**: mọi HTML request + SignalR negotiate (fallback `offline.html` khi network fail)
  - **cache-first**: GET static (`_framework/`, `*.css`, `*.js`, `icons/`)
  - **KHÔNG cache**: POST/PUT/DELETE + mọi `/api/*` (G5) + auth endpoints
- `offline.html` — trang tĩnh "Mất kết nối mạng — chạm để thử lại" + auto-reload khi `navigator.onLine`.
- `App.razor` thêm `<script src="js/pwa.js"></script>` + inline `vananPWA.registerServiceWorker()` (theo đúng pattern KhachLink `index.html`).

### 5.3 Role landing (P2 — hardcode map, G3)

Trang `/` (Home.razor hoặc index redirect) — sau `OnInitializedAsync` có auth state:

```csharp
var target = role switch {
    "Masterchef" => "/kitchen",
    "Guard" => "/guard/scan",
    "Staff" or "StoreKeeper" => "/pos",
    _ => "/sitemap"           // Owner/ChiefAccountant/SystemAdmin
};
Nav.NavigateTo(target, replace: true);
```

Kiểm tra Home.razor hiện tại redirect theo role thế nào (NavMenu đã map Masterchef→`/kitchen`) — tái dùng logic có sẵn nếu đã tồn tại, chỉ cần chuẩn hóa cho PWA entry.

### 5.4 Ops pages — verify standalone (P2)

7 trang chạy trong standalone — chủ yếu **verify**, chỉ sửa nếu phát hiện lỗi layout (dùng VanA* components, không HTML tùy ý):
- `/pos`, `/kitchen`, `/guard/scan`, `/orders` (+Detail+PrintBill), `/booking/create`, `/inventory/dashboard`, `/inventory/alerts`, `/sitemap`
- Kiểm: camera getUserMedia trong PWA (Guard), SignalR reconnect banner, internal nav không reload.

### 5.5 Tests (P3 — Gate 4 bắt buộc vì UI/PWA)

- `6_Testing/e2e-tests/shoperp-admin-pwa.spec.ts` — storageState role → login → assert landing route + standalone markers (manifest fetch 200, SW registered, offline.html tồn tại). Playwright không cài PWA thật → assert ở mức manifest/SW/route.
- SSR/bUnit nhẹ cho redirect map nếu implement trong Home.razor.

## 6. RỦI RO & GIẢM THIỂU

| Rủi ro | Ảnh hưởng | Giảm thiểu |
|---|---|---|
| iOS PWA không push/cache giới hạn | chỉ partial parity | Ghi nhận T8 — không cam kết push MVP |
| SW cache stale → SRI deadlock (wasm mismatch) | trắng trang sau deploy | Tái dùng pwa.js fix đã proven + version bump `shoperp-pwa-v{N}` |
| SignalR reconnect UX xấu khi mạng chập chờn | user tưởng app hỏng | Banner reconnect Blazor mặc định + offline.html fallback |
| SW cache nhầm API POST | data leak/stale | G5 — chỉ cache GET static, exclude `/api/*` |
| Desktop regression | admin desktop hỏng | Chỉ THÊM meta/script — không sửa CSS/layout desktop |

## 7. VALIDATION GATES (mỗi phase)

- Sau P0/P1: `dotnet build` 0 error + guard-check + commit.
- Sau P2: manual verify local (Chrome DevTools → Application → Manifest/SW) + commit.
- Sau P3: bUnit + E2E spec pass + commit.
- P4: push → CD → **RV**: L1 (manifest 200 + SW 200 trên prod) · L2 (install thật Android) · L4 (landing role + T1-T10 manual) · report.

## 8. NGUỒN THAM KHẢO

`docs/requirements/van_an_admin_mobile_pwa_srs_v1.md` · `5_WebApps/KhachLink/wwwroot/{manifest.json,service-worker.js,js/pwa.js,index.html}` · `5_WebApps/ShopERP/Components/App.razor` · `docs/AI/VAPID_KEY_GENERATION_GUIDE.md` (phase 2) · `docs/user-guide/System_Features_UseCase_View.md`.
