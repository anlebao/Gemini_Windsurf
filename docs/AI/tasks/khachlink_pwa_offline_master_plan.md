# Master Plan — KhachLink PWA True Offline (Blazor Server → WASM Conversion)

> **Created:** 2026-07-21
> **Status:** DRAFT — awaiting Tech Lead approval
> **Priority:** Medium (P2) — UX enhancement, not blocking current flows
> **Related tech debt:** TD-PWA-001 (this plan), TD-MVPS-003 (Integration.Tests infra)
> **ADR impact:** ADR-001 v3 addendum (Option C — KhachLink HTTP-only via Gateway, unchanged)

---

## 1. Problem Statement

`docs/AI/project_state.md` Section 1 documents KhachLink as "Blazor WebAssembly (KhachLink PWA)" — **this is incorrect**. Actual implementation is **Blazor Server**:

- `VanAn.KhachLink.csproj` uses `Microsoft.NET.Sdk.Web` (server SDK), not `Microsoft.NET.Sdk.BlazorWebAssembly`.
- `Program.cs` calls `AddInteractiveServerComponents()` + `AddInteractiveServerRenderMode()`.
- `App.razor` loads `_framework/blazor.web.js` (Server), not `blazor.webassembly.js`.
- All 13 Pages use `@rendermode InteractiveServer`.

### Consequence

PWA install is real (manifest + service worker + install prompt all functional), but **app does NOT work offline**. Blazor Server requires a live WebSocket (SignalR) connection to the server for every UI event. When the network drops:

1. WebSocket circuit dies → UI freezes completely (no clicks, no input, no navigation).
2. Service worker caches static assets (CSS/JS/icons) + API GET responses, but cached assets are useless because no Blazor DLL runs on the client.
3. `App.razor` shows "Đang kết nối lại..." indicator forever — reconnect fails without network.
4. Cached HTML fallback (service-worker.js line 131-134) only shows "Offline — Vui lòng kết nối internet".

### Evidence (verified 2026-07-21)

- `5_WebApps/KhachLink/VanAn.KhachLink.csproj` line 1: `<Project Sdk="Microsoft.NET.Sdk.Web">`
- `5_WebApps/KhachLink/Program.cs` lines 41-42: `.AddInteractiveServerComponents()`
- `5_WebApps/KhachLink/Components/App.razor` line 32: `<script src="_framework/blazor.web.js">`
- All 13 Pages: `@rendermode InteractiveServer` (grep confirmed)

---

## 2. Goal

Convert KhachLink from Blazor Server to **Blazor WebAssembly** (or .NET 8 Blazor Auto hybrid) so that:

1. UI events run locally on the client device (no WebSocket required).
2. App loads from service worker cache when offline (DLLs + static assets cached on first visit).
3. API calls fall back to cached responses (service worker already has network-first + cache fallback for `/api/*`).
4. POST operations (checkout, order creation) queue in IndexedDB + replay via Background Sync API when network returns.
5. Push notifications continue to work (service worker `push` handler unchanged).

**Non-goals:**
- NOT changing Gateway/ShopERP architecture (Option C unchanged).
- NOT adding offline-first SQLite to KhachLink (KhachLink remains HTTP-only via Gateway per governance).
- NOT removing PWA install infrastructure (manifest, install prompt, push subscription).

---

## 3. Architecture Decision

### Option A: Pure Blazor WebAssembly (recommended)

- Convert `VanAn.KhachLink.csproj` SDK → `Microsoft.NET.Sdk.BlazorWebAssembly`.
- Replace `AddInteractiveServerComponents()` → `AddInteractiveWebAssemblyComponents()` + `MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode()`.
- Replace `blazor.web.js` → `blazor.webassembly.js`.
- Remove `@rendermode InteractiveServer` from all Pages (WASM is interactive by default).
- Move all HTTP service implementations to a shared client project (or keep in KhachLink, since they already use `IHttpClientFactory`).
- Service worker caches `_framework/*.dll` + `blazor.boot.json` on first visit.

**Pros:** True offline, simpler mental model, no server circuit state.
**Cons:** Larger initial download (DLLs ~5-10MB), slower first load, no server-side prerendering for SEO.

### Option B: .NET 8 Blazor Auto (Server + WASM hybrid)

- First load: Blazor Server (fast initial render, small download).
- Background: download WASM DLLs in parallel.
- Subsequent loads: WASM (offline-capable).

**Pros:** Best of both worlds — fast first load + offline after first visit.
**Cons:** More complex setup, dual render modes per page, harder to debug.

### Option C: Keep Blazor Server, add offline shell only

- Keep current architecture.
- Add a static "offline page" with cached catalog + read-only order history.
- Queue checkout POSTs in IndexedDB, replay when online.

**Pros:** Minimal change.
**Cons:** Half-measure — most interactive features still dead offline. Doesn't satisfy "app runs offline" expectation.

### Recommendation: **Option A (Pure WASM)**

KhachLink is a customer-facing PWA installed on phones. Initial download size is a one-time cost (cached by service worker forever). True offline is the explicit goal. Blazor Auto adds complexity for marginal first-load benefit.

---

## 4. Phases

### Phase Dependencies

```
Phase 0 (Quick fix tạm thời) ──── independent, deploy ngay
                                      │
Phase 1 (SDK conversion) ─────────── depends on: approval
                                      │
Phase 2 (SW DLL caching) ─────────── depends on: Phase 1
                                      │
Phase 3 (Offline API fallback) ───── depends on: Phase 2
                                      │
Phase 4 (Offline write queue) ────── depends on: Phase 3
                                      │
Phase 5 (Push notification) ──────── independent (can parallel Phase 2-4)
                                      │
Phase 6 (E2E + governance) ───────── depends on: Phase 1-5 ALL complete
```

### Phase 0: Quick fix tạm thời (deploy ngay, không cần convert)
- Replace cached "Offline" HTML fallback (service-worker.js line 131-134) với trang đẹp: logo + "App cần internet để đặt hàng" + nút "Thử lại".
- Cache catalog snapshot + store list trên trang offline (read-only, không interaction).
- Sửa `PWAInstallPrompt.razor` text: "Cài đặt để truy cập nhanh — cần internet để đặt hàng" (manage expectation).
- Deploy ngay → user không thấy trắng trang khi mất mạng.
- **Không block Phase 1-6** — quick fix tách biệt, sẽ bị thay thế khi WASM convert xong.

### Phase 1: Project conversion + build green (no behavior change online)
- Change `VanAn.KhachLink.csproj` SDK → `Microsoft.NET.Sdk.BlazorWebAssembly`.
- Add `PackageReference Microsoft.AspNetCore.Components.WebAssembly.Dev` (dev tooling).
- Update `Program.cs`:
  - Replace `AddInteractiveServerComponents()` → `AddInteractiveWebAssemblyComponents()`.
  - Replace `MapRazorComponents<App>().AddInteractiveServerRenderMode()` → `AddInteractiveWebAssemblyRenderMode()`.
  - Replace `IHttpClientFactory` registration với WASM pattern: `builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) })`.
  - `appsettings.json` loading: WASM dùng `WebAssemblyHostBuilder.CreateDefault(args)` (tự load config, không cần `WebApplication.CreateBuilder`).
- Update `App.razor`: replace `blazor.web.js` → `blazor.webassembly.js`. **GIỮ `<HeadOutlet />`** (WASM vẫn cần cho `PageTitle`).
- Remove `@rendermode InteractiveServer` from all 13 Pages (WASM interactive by default).
- **Audit `HttpContext` usage** — WASM không có `HttpContext`. Replace với `NavigationManager` cho URL state.
- **Audit `IJSRuntime` calls** — most work identically trong WASM; `vananPWA.*` functions là pure browser APIs (OK).
- **Audit `Services/` directory** — verify không có server-only dependencies (EF Core, file system). KhachLink đã HTTP-only per governance → should be clean.
- Verify build PASS + online smoke test (all 13 pages render, navigation works, cart/checkout works, QR scan works).

### Phase 2: Service worker DLL caching
- Update `service-worker.js` `staticUrlsToCache` to include `_framework/blazor.boot.json` + `_framework/blazor.webassembly.js`.
- **`blazor.boot.json` cache strategy:** network-first + cache fallback (detect updates, offline still works). Không cache-first (sẽ kẹt old version).
- **`_framework/*.dll` + `*.wasm` cache strategy:** cache-first (immutable, hashed filenames — safe to cache forever).
- Reference `service-worker-assets.js` (auto-generated by Blazor WASM SDK) cho danh sách DLLs chính xác — không hand-roll.
- Add separate `WASM_CACHE` for `_framework/*` assets.
- Update cache version: `vanan-khachlink-v7` → `vanan-khachlink-v8-wasm`.
- Verify: load app online once → disconnect → reload → app still works (UI events fire, navigation works, API calls hit cache fallback).

### Phase 3: Offline API fallback hardening
- Audit `dynamicCachePatterns` in service-worker.js — current list (`/api/menu`, `/api/products`, `/api/orders`) is outdated (Option C uses new endpoints).
- Update patterns to match current Gateway endpoints:
  - `/api/tenants/search` (Store Finder)
  - `/api/tenants/nearby` (Store Finder)
  - `/api/tenants/{id}/store-info` (Store page)
  - `/api/catalog/recommended` (Home)
  - `/api/campaigns/by-tenant/{id}` (Home campaigns)
  - `/api/orders/{id}` (Order Tracking — read-only)
  - `/api/orders/history` (Order History — read-only, if endpoint exists)
  - Customer auth endpoints (if any GET — POST không cache)
- Add stale-while-revalidate strategy for catalog/campaigns (show cached, refresh in background).
- Add cache expiration: API responses expire after 24h (avoid stale data forever).
- Verify: each page works offline with cached data.

### Phase 4: Offline write queue (checkout POST)
- **Client-side UUIDv7 generation** — order ID generated on client BEFORE queue, stable across retries (Gateway không regenerate).
- **Idempotency key** — mỗi queued order có `Idempotency-Key` header (UUIDv7). Gateway `PublicOrdersController.checkout` phải check duplicate (if `Idempotency-Key` seen → return existing order, không tạo mới). **CRITICAL** — Background Sync có thể fire nhiều lần.
- Implement `OfflineQueueService` (C#) wrapping IndexedDB `sync-queue` store (already defined in `pwa.js` line 310).
- Modify `Checkout.razor` submit handler: if offline, queue order payload in IndexedDB + call `serviceWorkerRegistration.sync.register('vanan-checkout-sync')` via JS interop.
- Add service worker `sync` event handler (currently missing in service-worker.js):
  ```js
  self.addEventListener('sync', event => {
    if (event.tag === 'vanan-checkout-sync') {
      event.waitUntil(replayQueuedCheckouts());
    }
  });
  ```
- `replayQueuedCheckouts()`: read IndexedDB `sync-queue`, POST each to Gateway with `Idempotency-Key` header, mark as sent on 2xx.
- **iOS Safari fallback** (no Background Sync API): replay queue on `online` event + `visibilitychange` (app focus).
- UI: show "Đơn hàng đã lưu, sẽ gửi khi có mạng" toast when queued.
- **Gateway change required:** `PublicOrdersController.checkout` add `Idempotency-Key` header check (small server-side change — within scope).
- Verify: queue checkout offline → reconnect → order appears in Gateway PG + syncs to ShopERP via NATS. Verify duplicate sync fires don't create duplicate orders.

### Phase 5: Push notification + PWA polish
- **Verify VAPID key** in `pwa.js` line 156 — if invalid, regenerate via `npx web-push generate-vapid-keys` + update both client + server.
- **Gateway push endpoint (NEW — not just "wire up"):** Currently Gateway has NO push notification sending endpoint. Need to add:
  - `POST /api/push/subscribe` — store push subscription (endpoint + keys) in PG `PushSubscriptions` table (new entity, tenant-scoped).
  - `POST /api/push/send` — SystemAdmin sends push to tenant's subscribers (uses `WebPush` NuGet package with VAPID keys).
  - Trigger: order status change → auto-push to customer (if subscribed).
- Wire `subscribeToPush()` into Profile.razor — add "Cài đặt thông báo" toggle.
- Verify: push notification received when app is closed (Android only — iOS Safari requires app open + iOS 16.4+ for web push).

### Phase 6: E2E validation + governance
- Update `docs/AI/project_state.md` Section 1: change "Blazor Server (NOT WASM)" → "Blazor WebAssembly" (now true after conversion).
- Update ADR-001 v3 addendum: KhachLink render mode = WASM.
- **Rewrite `KhachLinkStartupTests`** — WASM test approach khác:
  - Server startup tests (DI container smoke) → replace với bUnit `TestContext` for component rendering tests.
  - Or: keep DI smoke tests by instantiating `Program` partially (WASM `WebAssemblyHostBuilder`).
- Playwright E2E: offline scenario (load online → disconnect → navigate → checkout → reconnect → verify order).
- RV on VPS: deploy + verify PWA install + offline on real Android device.
- **Remove Phase 0 quick fix** (replaced by real WASM offline).

---

## 5. Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| WASM initial download size (~5-10MB DLLs) | Slow first load on 3G | Service worker caches forever after first load; Brotli compression; lazy-load assemblies |
| `IHttpClientFactory` not available in WASM | Build break | Phase 1 replaces with `HttpClient` registered in DI |
| JS interop differences (Server vs WASM) | Runtime errors | Audit all `IJSRuntime` calls — most work identically; `vananPWA.*` functions are pure browser APIs |
| Authentication cookie vs JWT | Auth breaks | KhachLink uses `customer_token` in localStorage (not server cookie) — WASM compatible |
| Blazor Server-only APIs (`HttpContext`, `IHttpContextAccessor`) | Build break | Audit + replace with WASM equivalents (`NavigationManager` for URL state) |
| Service worker cache size limit (~50MB on iOS Safari) | Cache eviction | Prune old API responses; cache only essential DLLs |
| Background Sync API not supported on iOS Safari | Offline checkout queue doesn't replay on iOS | Fallback: replay queue on `online` event + on app focus (`visibilitychange`) |

---

## 6. Acceptance Criteria

- [ ] `dotnet build VanAn.KhachLink.csproj` PASS with WASM SDK.
- [ ] Online smoke test: all 13 pages render, navigation works, cart/checkout works, QR scan works.
- [ ] Offline test (Chrome DevTools → Network → Offline): app loads from cache, UI events fire, navigation works, Store Finder shows cached stores, Home shows cached catalog.
- [ ] Offline checkout: order queued in IndexedDB → reconnect → order appears in Gateway PG + syncs to ShopERP SQLite via NATS.
- [ ] **Idempotency:** Background Sync fires 3 times for same queued order → only 1 order created in Gateway PG (Idempotency-Key dedup).
- [ ] PWA install on Android Chrome: icon on Home Screen, standalone launch, push notification received.
- [ ] `project_state.md` Section 1 corrected to "Blazor WebAssembly" (now true).
- [ ] ADR-001 v3 addendum updated.
- [ ] Playwright E2E offline scenario PASS.
- [ ] **Performance budget:** initial download <8MB compressed (Brotli), time-to-interactive <15s on 4G, <3s on WiFi (cached).

---

## 7. Rollback Plan

- **Branch strategy:** All work on `feature/khachlink-wasm` branch. Fast-forward merge to `main` only after Phase 6 RV PASS.
- **If Phase 1 build fails irrecoverably:** `git checkout main` — no production impact (changes never merged).
- **If Phase 2-4 offline behavior broken after merge:** Revert merge commit on `main` → CD redeploys previous Server version. KhachLink is stateless (no server-side state) → safe revert.
- **If Phase 5 push notifications break:** Disable push toggle in Profile.razor (feature flag) — doesn't affect core app.
- **Data safety:** IndexedDB `sync-queue` is client-side only. Reverting to Server mode loses queued offline orders (acceptable — user sees "cần internet" message, re-submits).

---

## 8. Task Cards

| Phase | Task Card | Effort | Dependencies |
|---|---|---|---|
| 0 — Quick fix tạm thời | `khachlink_pwa_phase0_quickfix_task_card.md` | 1 session | None (deploy ngay) |
| 1 — SDK conversion | `khachlink_pwa_phase1_sdk_conversion_task_card.md` | 3-5 sessions | Tech Lead approval |
| 2 — SW DLL caching | `khachlink_pwa_phase2_sw_dll_caching_task_card.md` | 1-2 sessions | Phase 1 |
| 3 — Offline API fallback | `khachlink_pwa_phase3_offline_api_task_card.md` | 1-2 sessions | Phase 2 |
| 4 — Offline write queue | `khachlink_pwa_phase4_offline_write_queue_task_card.md` | 3-4 sessions | Phase 3 |
| 5 — Push notification | `khachlink_pwa_phase5_push_notification_task_card.md` | 3-4 sessions | None (parallel Phase 2-4) |
| 6 — E2E + governance | `khachlink_pwa_phase6_e2e_governance_task_card.md` | 2-3 sessions | Phase 1-5 ALL |

**Total estimated effort:** 14-21 sessions (~3-5 weeks with approval gates).

---

## 9. References

- **Investigation:** this session (2026-07-21) — PWA audit confirmed Blazor Server, not WASM.
- **Service worker:** `5_WebApps/KhachLink/wwwroot/service-worker.js`
- **PWA helper:** `5_WebApps/KhachLink/wwwroot/js/pwa.js`
- **Install prompt:** `5_WebApps/KhachLink/Components/PWA/PWAInstallPrompt.razor`
- **Manifest:** `5_WebApps/KhachLink/wwwroot/manifest.json`
- **Project file:** `5_WebApps/KhachLink/VanAn.KhachLink.csproj`
- **Blazor WASM docs:** https://learn.microsoft.com/aspnet/core/blazor/webassembly/
- **Background Sync API:** https://developer.chrome.com/docs/workbox/background-sync/
