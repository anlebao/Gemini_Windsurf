# Master Plan — KhachLink PWA True Offline (Blazor Server → WASM Conversion)

> **Created:** 2026-07-21
> **Status:** IN PROGRESS — Phase 1 COMPLETE, Phase 2 COMPLETE + hotfixes, Phase 4 DESCOPE, Phase 3 next
> **Priority:** Medium (P2) — UX enhancement, not blocking current flows
> **Related tech debt:** TD-PWA-001 (this plan), TD-MVPS-003 (Integration.Tests infra)
> **ADR impact:** ADR-001 v3 addendum (Option C — KhachLink HTTP-only via Gateway, unchanged)

## Phase Progress Summary

| Phase | Status | Date | Commit |
|---|---|---|---|
| 0 — Quick fix tạm thời | SKIPPED (superseded by Phase 1 fast completion) | — | — |
| 1 — SDK conversion | COMPLETE | 2026-07-21 | `b642662b` + 3 follow-ups |
| 2 — SW DLL caching | COMPLETE + hotfixes | 2026-07-22 | `ec15bc01` + 3 hotfixes |
| 2b — Price validation + online guard | COMPLETE | 2026-07-22 | `51b7e624` |
| 3 — Offline API fallback | NEXT | — | — |
| 4 — Offline write queue | **DESCOPE** (checkout = online-only per architecture review) | 2026-07-22 | — |
| 5 — Push notification | Pending | — | — |
| 6 — E2E + governance | Pending | — | — |

### Architecture Decision: Phase 4 Descope (2026-07-22)

**Decision:** Phase 4 (offline write queue / IndexedDB + Background Sync for checkout POST) is **DESCOPED** from the master plan. Checkout is now **online-only** — `navigator.onLine` guard in Checkout.razor blocks submission when offline.

**Rationale (from architecture review):**
1. **Financial integrity:** Offline checkout creates "ghost orders" — order timestamp, price, and inventory state are ambiguous when replayed later. Gateway is order creator (Option C) and must validate in real-time.
2. **Price validation:** Tier 0+1 price validation (commit `51b7e624`) requires Gateway PG access — cannot run offline. Client-sent prices must be validated server-side before order creation.
3. **Inventory overselling:** Without real-time inventory check, offline orders can cause overbooking. Gateway has no inventory table (products live in ShopERP SQLite per-tenant).
4. **Token expiry:** Background Sync replay may fire after auth token expires → 401 → order stuck in queue silently.
5. **UX expectation:** Customer-facing PWA for food ordering — "order saved, will send later" is confusing for time-sensitive F&B orders. Better UX: clear error "no connection, please check 4G/Wifi".

**What this means for offline capability:**
- **Offline READ works:** catalog browse, store finder, order history, campaign view — all cached by service worker (Phase 2+3).
- **Offline WRITE blocked:** checkout, order creation — requires real-time Gateway validation. `navigator.onLine` guard + clear error message.
- **iOS Safari:** no Background Sync API needed (was a risk in original plan — now moot).

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
Phase 0 (Quick fix tạm thời) ──── SKIPPED
                                      │
Phase 1 (SDK conversion) ─────────── COMPLETE
                                      │
Phase 2 (SW DLL caching) ─────────── COMPLETE + hotfixes
                                      │
Phase 2b (Price validation + guard) ─ COMPLETE (inline, not a numbered phase)
                                      │
Phase 3 (Offline API fallback) ───── NEXT — depends on: Phase 2
                                      │
Phase 4 (Offline write queue) ────── DESCOPE (checkout = online-only)
                                      │
Phase 5 (Push notification) ──────── independent (can parallel Phase 3)
                                      │
Phase 6 (E2E + governance) ───────── depends on: Phase 1-3 + 5 complete
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

### Phase 2: Service worker DLL caching — COMPLETE (2026-07-22)
- Update `service-worker.js` `staticUrlsToCache` to include `_framework/blazor.boot.json` + `_framework/blazor.webassembly.js`.
- **`blazor.boot.json` cache strategy:** network-first + cache fallback (detect updates, offline still works). Không cache-first (sẽ kẹt old version).
- **`_framework/*.dll` + `*.wasm` cache strategy:** cache-first (immutable, hashed filenames — safe to cache forever).
- Reference `service-worker-assets.js` (auto-generated by Blazor WASM SDK) cho danh sách DLLs chính xác — không hand-roll.
- Add separate `WASM_CACHE` for `_framework/*` assets.
- Update cache version: `vanan-khachlink-v9-wasm` → `v10-batched` (post-hotfix).
- Verify: load app online once → disconnect → reload → app still works (UI events fire, navigation works, API calls hit cache fallback).

**Phase 2 completion (commit `ec15bc01`):** SW updated with `WASM_CACHE` for `_framework/*`, `importScripts('/service-worker-assets.js')` for SDK manifest precaching, `blazor.boot.json` network-first + cache fallback, `_framework/*` cache-first, navigation 3-tier fallback. Cache version `v8-offline-shell` → `v9-wasm`. `_framework/` = 19.5MB (under 50MB iOS Safari limit). VPS RV PASS.

**Phase 2 hotfixes (2026-07-22, 3 commits):** Post-deploy browser testing revealed 3 runtime issues:
1. **Rate limit 503 (commit `0186723f`):** SW install event fired 80 concurrent `cache.add()` for `/_framework/*` → front proxy nginx rate limiter (`burst=20`) blocked 60/80 with 503 → SRI integrity fail → Blazor boot crash. Fix: (a) batch SW precache into chunks of 5 (sequential per batch), (b) nginx template — move `limit_req` from server block into `location /` + `location /_blazor` blocks, exempting `location /_framework/` from rate limiting. Cache version `v9-wasm` → `v10-batched`.
2. **CannotResolveService AuthStateProvider (commit `dabc3698`):** Phase 1 WASM conversion removed server-side Blazor infrastructure providing default `AuthenticationStateProvider`. `UI.Platform.TenantService` requires it via constructor injection → `CannotResolveService` at render. Fix: added `AnonymousAuthenticationStateProvider` stub (returns anonymous `ClaimsPrincipal`, no TenantId claim). Tenant context comes from `LastInteractionService` (localStorage), not auth claims.
3. **NullabilityInfoContext_NotSupported (commit `b8a94413`):** Blazor WASM SDK disables `NullabilityInfoContext` feature switch by default. `System.Text.Json` `DefaultJsonTypeInfoResolver` tries to read nullable annotations via reflection → throws → crashes all HTTP JSON deserialization. Fix: `<NullabilityInfoContextSupport>true</NullabilityInfoContextSupport>` MSBuild property in csproj. Reference: [dotnet/runtime#118333](https://github.com/dotnet/runtime/issues/118333).

**RV (2026-07-22): 9/9 PASS** — 80 concurrent `/_framework/` requests all 200 (0× 503), homepage loads 200, SW v10-batched deployed, catalog API returns valid JSON, nginx `/_framework/` exempt confirmed, 4 key WASM assets accessible.

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

### Phase 2b: Price validation + navigator.onLine guard — COMPLETE (2026-07-22, commit `51b7e624`)

Implemented as inline hardening after Phase 2, not a numbered phase. Addresses price validation gap identified during architecture review.

**Tier 0 — Sanity checks (Gateway, 0ms):**
- Reject 400 if `UnitPrice <= 0`, `Quantity <= 0`, `VatRate < 0` or `> 1.0`
- Returns specific error per item (product name + invalid value)
- Catches client bugs, DevTools manipulation, corrupted cache

**Tier 1 — FeaturedProducts cross-check (Gateway, ~5ms):**
- Query `FeaturedProducts` from Gateway PG (local, does NOT call ShopERP)
- Compare client `UnitPrice` vs `FeaturedProduct.DisplayPrice` with 5% tolerance
- If mismatch > 5% → reject 400 "price has changed, please refresh"
- QR-scanned products (not in FeaturedProducts) skip Tier 1 — QR price is system-generated, trustworthy

**navigator.onLine guard (KhachLink Checkout.razor):**
- Check `navigator.onLine` before submit
- If offline → show error "no connection, check 4G/Wifi to send order"
- Financial transactions = online real-time only

**Tier 2 — Async reconciliation (DEFERRED):** ShopERP-side price comparison via NATS reply. Not needed for MVP — Tier 0+1 covers Featured products (most common checkout path). Can add later if non-Featured product price manipulation becomes a real problem.

### Phase 4: Offline write queue (checkout POST) — DESCOPE (2026-07-22)

**Status: DESCOPE.** Checkout is online-only. `navigator.onLine` guard blocks offline submission with clear error message.

**Original plan (archived for reference):**
- ~~Client-side UUIDv7 generation — order ID generated on client BEFORE queue~~
- ~~Idempotency key — each queued order has `Idempotency-Key` header~~
- ~~Implement `OfflineQueueService` (C#) wrapping IndexedDB `sync-queue` store~~
- ~~Modify `Checkout.razor` submit handler: if offline, queue in IndexedDB + Background Sync~~
- ~~Add service worker `sync` event handler~~
- ~~iOS Safari fallback: replay queue on `online` event + `visibilitychange`~~

**Why descope:** See "Architecture Decision: Phase 4 Descope" in Phase Progress Summary above. Key reasons: financial integrity (ghost orders), price validation requires real-time Gateway access, inventory overselling risk, token expiry, F&B UX expectation (time-sensitive orders).

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
- Playwright E2E: offline READ scenario (load online → disconnect → navigate → browse catalog → verify cached data). **No offline checkout test** (Phase 4 descope — checkout is online-only).
- Playwright E2E: price validation scenario (submit checkout with manipulated price → verify Tier 0/1 rejection).
- RV on VPS: deploy + verify PWA install + offline READ on real Android device.
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
| Service worker cache size limit (~50MB on iOS Safari) | Cache eviction | Prune old API responses; cache only essential DLLs. Verified: `_framework/` = 19.5MB (under limit) |
| ~~Background Sync API not supported on iOS Safari~~ | ~~Offline checkout queue doesn't replay on iOS~~ | **MOOT** — Phase 4 descope, checkout is online-only. No Background Sync needed. |

---

## 6. Acceptance Criteria

- [x] `dotnet build VanAn.KhachLink.csproj` PASS with WASM SDK. (Phase 1)
- [x] Online smoke test: all 13 pages render, navigation works, cart/checkout works, QR scan works. (Phase 1 RV)
- [ ] Offline test (Chrome DevTools → Network → Offline): app loads from cache, UI events fire, navigation works, Store Finder shows cached stores, Home shows cached catalog. (Phase 3 — pending)
- [x] **Offline checkout: BLOCKED by design** — `navigator.onLine` guard prevents offline submission. Checkout requires real-time Gateway validation (Tier 0+1 price checks). (Phase 2b — DESCOPE Phase 4)
- [x] **Price validation:** Tier 0 sanity checks + Tier 1 FeaturedProducts cross-check reject invalid prices at Gateway. (Phase 2b)
- [ ] PWA install on Android Chrome: icon on Home Screen, standalone launch, push notification received. (Phase 5 — pending)
- [ ] `project_state.md` Section 1 corrected to "Blazor WebAssembly" (now true). (Phase 6 — pending)
- [ ] ADR-001 v3 addendum updated. (Phase 6 — pending)
- [ ] Playwright E2E offline scenario PASS. (Phase 6 — pending)
- [x] **Performance budget:** initial download `_framework/` = 19.5MB uncompressed, Brotli compressed ~5-7MB. time-to-interactive <15s on 4G, <3s on WiFi (cached). (Phase 2 RV)

---

## 7. Rollback Plan

- **Branch strategy:** All work on `feature/khachlink-wasm` branch. Fast-forward merge to `main` only after Phase 6 RV PASS.
- **If Phase 1 build fails irrecoverably:** `git checkout main` — no production impact (changes never merged).
- **If Phase 2-3 offline behavior broken after merge:** Revert merge commit on `main` → CD redeploys previous version. KhachLink is stateless (no server-side state) → safe revert.
- **If Phase 5 push notifications break:** Disable push toggle in Profile.razor (feature flag) — doesn't affect core app.
- **Data safety:** No IndexedDB write queue (Phase 4 descope). Checkout is online-only — no client-side order data to lose on revert.

---

## 8. Task Cards

| Phase | Task Card | Effort | Status |
|---|---|---|---|
| 0 — Quick fix tạm thời | `khachlink_pwa_phase0_quickfix_task_card.md` | 1 session | SKIPPED |
| 1 — SDK conversion | `khachlink_pwa_phase1_sdk_conversion_task_card.md` | 3-5 sessions | COMPLETE |
| 2 — SW DLL caching | `khachlink_pwa_phase2_sw_dll_caching_task_card.md` | 1-2 sessions | COMPLETE + hotfixes |
| 2b — Price validation + guard | (inline, no task card) | 1 session | COMPLETE |
| 3 — Offline API fallback | `khachlink_pwa_phase3_offline_api_task_card.md` | 1-2 sessions | NEXT |
| 4 — Offline write queue | `khachlink_pwa_phase4_offline_write_queue_task_card.md` | 3-4 sessions | **DESCOPE** |
| 5 — Push notification | `khachlink_pwa_phase5_push_notification_task_card.md` | 3-4 sessions | Pending |
| 6 — E2E + governance | `khachlink_pwa_phase6_e2e_governance_task_card.md` | 2-3 sessions | Pending (Phase 6 depends on 1-3 + 5, NOT 4) |

**Revised estimated effort (remaining):** 6-9 sessions (~1.5-2.5 weeks) — Phase 3 (1-2) + Phase 5 (3-4) + Phase 6 (2-3). Phase 4 descope saves 3-4 sessions.

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
