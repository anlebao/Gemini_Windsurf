# TASK CARD: Store Context — "Cửa hàng của bạn" hiệu lực + Back-to-Store (GP-A + GP-B)

> Created: 2026-11-28 · Mode: IMPLEMENT · TDD · Sweep-first
> Nguồn: review session 2026-11-28 (2 findings cùng 1 root cause — tenant context
> không được persist trong luồng storefront → checkout).
> User đã chốt: **record khi VISIT storefront** (browse cũng là tương tác),
> **checkout ghi đè lần cuối** (last-write-wins tự nhiên).
> Rule: **KHÔNG chạm Domain — không cần Gate 5** (pure client localStorage +
> controller read-path). UI Platform components only (`VanAnButton`).

## OBJECTIVE

1. Fix "Cửa hàng của bạn" bật ON nhưng không hiện: ghi `lastTenantId` ở các
   touchpoint thật (storefront visit, checkout success, order-tracking re-entry).
2. Fix "không quay lại được storefront sau khi xem lịch sử điểm thưởng":
   - GP-A: nút "Tiếp tục mua tại {shop}" trên `OrderTracking`.
   - GP-B: persist store context `{tenantId, slug, name, visitedAt}` → chip
     "Quay lại {shop}" trên `LoyaltyCard` (+ các trang account khác) + nút
     "Vào cửa hàng" trong Home section.
3. Consistency: `GET api/platform/khachlink-home-settings` áp `InstanceFeatureGate`
   (AND semantics, giống `feature-settings`).

## CURRENT STATE — SWEEP (verified 2026-11-28)

### Điểm GHI context (ghi `khachlink_last_tenant_id` qua `LastInteractionService`)

| Điểm | Trạng thái |
|---|---|
| `Scan.razor` (3 chỗ — QR scan add-to-cart) | ✅ ĐÃ có |
| `Home.razor:974` (`AddFeaturedToCart`) | ✅ ĐÃ có |
| `Store.razor` (visit `/store/{slug}` + `/store/by-id/{tenantId}`) | ❌ THIẾU — root cause |
| `Checkout.razor` (order created) | ❌ THIẾU — root cause |
| `OrderTracking.razor` (re-entry từ `/my-orders`) | ❌ THIẾU |

### Điểm ĐỌC context (consumers `lastTenantId` / store context)

| Consumer | Trạng thái |
|---|---|
| `Home.razor` `_lastTenantId` gate → section "Cửa Hàng Của Bạn" (`_homeStoreEnabled`) | render card info nhưng **KHÔNG có nút "Vào cửa hàng"** — chỉ Chỉ đường Maps |
| `FeatureContextService` → per-tenant feature-settings | OK (đọc `GetLastTenantIdAsync`) |
| `AnonymousAuthenticationStateProvider` | OK (đọc cùng key) |
| `LoyaltyCard.razor` (`/my-loyalty`) | ❌ không có back-link (GP-B) |
| `OrderTracking.razor` | ❌ không có "tiếp tục mua" (GP-A) — đã có sẵn `_orderTenantId` + `_tenantName` |
| `/my-orders`, `/rewards`, `/cart` | optional — cùng pattern chip |

### Gate inconsistency

- `KhachLinkHomeSettingsController.GetSettings` (anonymous) **không qua**
  `InstanceFeatureGate` → instance `ShowStores=false` chặn `/stores` +
  `feature-settings` nhưng KHÔNG chặn home section global.

### Fact nền đã verify

- `/store/by-id/{TenantId}` route ĐÃ tồn tại → back-link chỉ cần tenantId.
- `ShopDto` có sẵn `TenantId` + `Name` + `Slug` (`Store.razor` `_store`).
- `OrderTracking` DTO có `TenantId` + `TenantName`.
- Checkout `_createdOrders[]` có `TenantId` + `TenantName` (multi-tenant cart OK).
- `LastInteractionService` = scoped, IJSRuntime, fail-open, prerender-guard.
- Test pattern: `6_Tests/VanAn.Core.Tests/KhachLink/SearchCorrectionStoreTests.cs`
  (FakeJsRuntime dict localStorage) — mirror cho `LastInteractionServiceTests`.

## IMPACT-NGUỢC GUARDS (không được vỡ)

- `GetLastTenantIdAsync()` signature + behavior — GIỮ (Scan/Home/FeatureContext/
  AnonymousAuthStateProvider không đổi).
- Legacy key `khachlink_last_tenant_id` — vẫn GHI (reader cũ hoạt động) + vẫn ĐỌC
  được làm fallback (data cũ của user không mất).
- `GET api/platform/khachlink-home-settings` — GIỮ anonymous + default-all-ON
  khi chưa có row. Gate chỉ AND-off, KHÔNG force-on.
- `ApplyOverrides(ShopFeatureSettingsDto,…)` + `InstanceFeatureGateTests` 7/7 — GIỮ.
- Anonymous/guest flow — không header/identity mới. localStorage fail-open
  (private mode/quota/corrupt → no-op, không crash page).
- UI Platform: `VanAnButton`/`VanAnCard` — KHÔNG raw HTML button mới.
- Domain layer — KHÔNG đụng.

## IMPLEMENTATION CHECKLIST

### Phase A — `LastStoreVisit` + `LastInteractionService` upgrade (TDD — test trước)

- [ ] `LastStoreVisit` record (KhachLink `Services/` hoặc `Models/`):
  `{ Guid TenantId; string? Slug; string? Name; DateTimeOffset VisitedAt }`.
- [ ] `RecordInteractionAsync(Guid tenantId, string? slug = null, string? name = null)`
  — ghi 2 key: `khachlink_last_store` (JSON `{t,s,n,v}`) + `khachlink_last_tenant_id`
  (bare GUID, giữ reader cũ). `Guid.Empty` → no-op.
- [ ] `GetLastStoreVisitAsync()` → `LastStoreVisit?` — parse key mới; fallback build
  từ legacy key (tenantId only). Corrupt JSON/JS throw → null.
- [ ] `GetLastTenantIdAsync()` — đọc key mới `.t` trước, fallback legacy (giữ behavior).
- [ ] `ClearAsync()` — xóa CẢ 2 key.

**Gate A (TDD):** `6_Tests/VanAn.Core.Tests/KhachLink/LastInteractionServiceTests.cs`
(FakeJsRuntime mirror SearchCorrectionStoreTests) — ≥8 tests: record ghi cả 2 key ·
roundtrip rich record · slug/name null OK · legacy bare-guid đọc được (migration) ·
Guid.Empty no-op · corrupt JSON → null · JS throw → null (fail-open) · last-write-wins ·
Clear xóa cả 2.

### Phase B — Record touchpoints

- [ ] `Store.razor`: inject `LastInteractionService`; sau khi `_store` load thành công
  (cả 2 route slug + by-id, ~line 1546) →
  `RecordInteractionAsync(_store.TenantId, _store.Slug ?? Slug, _store.Name)`
  (fire-and-forget có thể; KHÔNG chặn render).
- [ ] `Checkout.razor`: sau `_createdOrders` success (~line 1592) → loop
  `RecordInteractionAsync(o.TenantId, null, o.TenantName)` — multi-tenant cart:
  last-write-wins = tenant cuối (đúng "ghi đè lần cuối").
- [ ] `OrderTracking.razor`: sau load order DTO có `_orderTenantId` (~line 384) →
  `RecordInteractionAsync(_orderTenantId, null, _tenantName)` — cover re-entry
  từ `/my-orders`/deep-link.
- [ ] `Scan.razor` + `Home.AddFeaturedToCart` — GIỮ nguyên (slug/name null OK,
  fallback by-id).

### Phase C — Consumers: back-to-store (GP-A + GP-B)

- [ ] Component mới `Components/Shared/BackToStoreChip.razor`: inject
  `LastInteractionService` + `Navigation`; render `VanAnButton` Outline
  "Quay lại {Name}" (Name null → "Quay lại cửa hàng") → `/store/{slug}` nếu có
  slug, else `/store/by-id/{tenantId}`; render NOTHING khi không có context.
  Param optional `CssClass`.
- [ ] `OrderTracking.razor` (GP-A): nút "Tiếp tục mua tại {_tenantName}" —
  `VanAnButton` → `/store/by-id/{_orderTenantId}`, đặt cạnh/duoi loyalty banner
  + cuối trang. Dùng `_orderTenantId` trực tiếp (đã có, không chờ service).
- [ ] `LoyaltyCard.razor` (GP-B): `<BackToStoreChip />` đầu container (trong vùng
  `_isLoggedIn` hoặc luôn hiện khi có context — chọn luôn hiện để cover guest
  vừa mua xong).
- [ ] `Home.razor` store section: trong `_store` card thêm `VanAnButton` Primary
  "Vào cửa hàng" → `LastStoreVisit.Slug != null ? /store/{slug} : /store/by-id/{_lastTenantId}`.
- [ ] Optional (same pattern, 1 dòng mỗi page): `<BackToStoreChip />` trên
  `OrderHistory` (/my-orders), `RedemptionCatalog` (/rewards), `Cart` (/cart).

**Gate C:** bUnit/light tests nếu có harness cho BackToStoreChip (render-null khi
no context · href đúng slug vs by-id). Markup hooks: `data-testid="back-to-store"`.

### Phase D — Gate consistency

- [ ] `InstanceFeatureGate` += `ApplyHomeOverrides(bool campaignEnabled,
  bool storeEnabled, KhachLinkNavFlags? flags) → (bool campaign, bool store)`:
  null → trả nguyên; else `campaign && flags.ShowCampaigns`,
  `store && flags.ShowStores`. **Dùng primitives — KHÔNG nhận
  `KhachLinkHomeSettingsDto`** vì DTO này đang duplicate 3 nơi (Gateway
  controller / ShopERP client / KhachLink service), không nằm trong Shared —
  CoreHub không tham chiếu được. (Verified 2026-11-28.)
- [ ] `KhachLinkHomeSettingsController` inject `IInstanceFeatureGate`; `GetSettings`
  sau khi load (cả nhánh DB row lẫn default dto) →
  `(dto.Home_CampaignSection_Enabled, dto.Home_StoreSection_Enabled) =
  _gate.ApplyHomeOverrides(dto.Home_CampaignSection_Enabled,
  dto.Home_StoreSection_Enabled, await _gate.GetNavFlagsAsync())`.
- [ ] `KhachLinkHomeSettingsHttpService` (KhachLink) — không đổi (header
  `X-KhachLink-Domain` đã gắn bởi `InstanceDomainHeaderHandler`).

**Gate D (TDD):** `InstanceFeatureGateTests` += ≥3 tests: ShowStores=false →
Home_StoreSection off · ShowCampaigns=false → Home_CampaignSection off ·
flags=null → unchanged · các field khác (Featured/SocialHub/ShowNavMenu) không động.

### Phase E — Ship

- [ ] `dotnet build VanAn.sln` 0 err · `dotnet test` Core.Tests · guard-check.ps1 ALL PASSED
- [ ] E2E spec `6_Testing/e2e-tests/store-context.spec.ts` (Gate 4 — UI mới):
  visit `/store/{slug}` → `localStorage.khachlink_last_store` có JSON → Home hiện
  "Cửa Hàng Của Bạn" + nút "Vào cửa hàng" → `/my-loyalty` hiện chip →
  `/order-tracking` hiện "Tiếp tục mua" (validation wave sau implement —
  Playwright tắt trong IMPLEMENT).
- [ ] Commit → push → CD → **RV prod sweep** `6_Testing/rv-scripts/store-context-rv-sweep.mjs`:
  L1 `api/platform/khachlink-home-settings` có/không header `X-KhachLink-Domain`
  (gate AND đúng) · L2 WASM marker `khachlink_last_store` · L4 UI flow trên
  1 tenant thật (visit → home section → back chip → order-tracking button).

## OUT OF SCOPE

- Nút back generic `history.back()` (PWA standalone không có history — đã loại
  trong review).
- Server-side per-user "recent stores" (PG table, sync đa thiết bị) — wave sau
  nếu cần.
- Đổi UX "Cửa Hàng Của Bạn" thành danh sách NHIỀU store gần đây — W này chỉ
  last-1 (đủ fix bug).
- `Directory` app — SSR, không dùng LastInteractionService.

## DECISIONS (đã chốt với user)

- D1: record khi VISIT storefront (browse = tương tác) + checkout/tracking ghi đè
  lần cuối (last-write-wins).
- D2: storage = dual-key (rich JSON + legacy bare GUID) — không breaking reader cũ.
- D3: shared component `BackToStoreChip` thay vì rải logic mỗi page.
- D4: home-settings GET áp InstanceFeatureGate AND-semantics (không force-on).
- D5: KHÔNG đụng Domain/DB/migration — pure client + read-path.
