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

### Phase 1: Project conversion + build green (no behavior change online)
- Change `VanAn.KhachLink.csproj` SDK → `Microsoft.NET.Sdk.BlazorWebAssembly`.
- Update `Program.cs`: `AddInteractiveWebAssemblyComponents()` + `MapRazorComponents<App>().AddInteractiveWebAssemblyRenderMode()`.
- Update `App.razor`: `blazor.webassembly.js` + remove `<HeadOutlet />` server mode.
- Remove `@rendermode InteractiveServer` from all 13 Pages.
- Move `IHttpClientFactory` registration to WASM-compatible `HttpClient` (WASM uses `HttpClient` directly, not `IHttpClientFactory`).
- Verify build PASS + online smoke test (all pages render, navigation works, cart/checkout works).

### Phase 2: Service worker DLL caching
- Update `service-worker.js` `staticUrlsToCache` to include `_framework/blazor.boot.json` + `_framework/blazor.webassembly.js`.
- Add a separate `WASM_CACHE` for `_framework/*.dll` + `*.wasm` (cache on first fetch via `fetch` event listener, not `install` — boot manifest lists DLLs dynamically).
- Update cache version: `vanan-khachlink-v7` → `vanan-khachlink-v8-wasm`.
- Verify: load app online once → disconnect → reload → app still works (UI events fire, navigation works, API calls hit cache fallback).

### Phase 3: Offline API fallback hardening
- Audit `dynamicCachePatterns` in service-worker.js — current list (`/api/menu`, `/api/products`, `/api/orders`) is outdated (Option C uses `/api/tenants/*`, `/api/catalog/*`, `/api/campaigns/*`).
- Update patterns to match current Gateway endpoints:
  - `/api/tenants/search` (Store Finder)
  - `/api/tenants/nearby` (Store Finder)
  - `/api/tenants/{id}/store-info` (Store page)
  - `/api/catalog/recommended` (Home)
  - `/api/campaigns/by-tenant/{id}` (Home campaigns)
- Add stale-while-revalidate strategy for catalog/campaigns (show cached, refresh in background).
- Verify: each page works offline with cached data.

### Phase 4: Offline write queue (checkout POST)
- Implement `OfflineQueueService` (C#) wrapping IndexedDB `sync-queue` store (already defined in `pwa.js` line 310).
- Modify `Checkout.razor` submit handler: if offline, queue order payload in IndexedDB + register Background Sync tag `vanan-checkout-sync`.
- Service worker `sync` event handler: replay queued POSTs to Gateway, mark queue item as sent.
- UI: show "Đơn hàng đã lưu, sẽ gửi khi có mạng" toast when queued.
- Verify: queue checkout offline → reconnect → order appears in Gateway PG + syncs to ShopERP via NATS.

### Phase 5: Push notification + PWA polish
- Verify VAPID key in `pwa.js` line 156 is still valid (or regenerate).
- Wire `subscribeToPush()` into a user settings page (currently no UI calls it).
- Add "Cài đặt thông báo" toggle in Profile.razor.
- Verify: push notification received when app is closed (Android only — iOS Safari requires app open).

### Phase 6: E2E validation + governance
- Update `docs/AI/project_state.md` Section 1: change "Blazor WebAssembly" claim to reality (after conversion, it becomes true).
- Update ADR-001 v3 addendum: KhachLink render mode = WASM.
- Update `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests` — WASM has different startup semantics (no server circuit).
- Playwright E2E: offline scenario (load online → disconnect → navigate → checkout → reconnect → verify order).
- RV on VPS: deploy + verify PWA install + offline on real Android device.

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
- [ ] Offline checkout: order queued in IndexedDB → reconnect → order appears in Gateway PG → syncs to ShopERP SQLite via NATS.
- [ ] PWA install on Android Chrome: icon on Home Screen, standalone launch, push notification received.
- [ ] `project_state.md` Section 1 corrected to "Blazor WebAssembly" (now true).
- [ ] ADR-001 v3 addendum updated.
- [ ] Playwright E2E offline scenario PASS.

---

## 7. References

- **Investigation:** this session (2026-07-21) — PWA audit confirmed Blazor Server, not WASM.
- **Service worker:** `5_WebApps/KhachLink/wwwroot/service-worker.js`
- **PWA helper:** `5_WebApps/KhachLink/wwwroot/js/pwa.js`
- **Install prompt:** `5_WebApps/KhachLink/Components/PWA/PWAInstallPrompt.razor`
- **Manifest:** `5_WebApps/KhachLink/wwwroot/manifest.json`
- **Project file:** `5_WebApps/KhachLink/VanAn.KhachLink.csproj`
- **Blazor WASM docs:** https://learn.microsoft.com/aspnet/core/blazor/webassembly/
- **Background Sync API:** https://developer.chrome.com/docs/workbox/background-sync/
