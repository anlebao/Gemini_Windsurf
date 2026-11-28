# TASK CARD: Personalized DidYouMean — Self-Learning Search Correction (W1 ✅ → W2)

> Created: 2026-11-28 · Updated: 2026-11-28 (v1.3 — W2 done + deployed + RV 24/24)
> Mode: W1 ✅ deployed `fa934f62` · W2 ✅ deployed `8ba617bb` RV 24/24 · TDD · sweep-first RV
> SRS: `docs/specs/SRS_Search_Personalized_DidYouMean_v1.0.md` (v1.1)
> Rule: governance "Public Search (anti-drift, W3-2d)" — tái dùng `NormalizeSearchText`,
> `VanAnPublicSearchBar`, `VanAnDidYouMeanChip`; **KHÔNG chạm Domain — không cần Gate 5**

## OBJECTIVE

Khi user bấm chip "Ý bạn là: X?" → client ghi nhận `(scope, raw → suggestion)` vào
**localStorage** (`vanan_search_corrections`). Lần sau cùng browser nhập/nói cùng
`raw` → client rewrite query thành X TRƯỚC khi gọi API → kết quả như search đúng
+ note "đã học từ X" + Hoàn tác. **Backend 0 thay đổi** (không entity, migration,
endpoint, cache fix). W1 cover **product search** (`ProductSearchResults.razor`).

## CURRENT STATE (verified 2026-11-28)

- `FeaturedProductSearchService`/`TenantSearchService` — pipeline stateless, DidYouMean <0.9.
  `NormalizeSearchText`/`SearchSimilarity`/`LevenshteinDistance`/`DiacriticFold` là
  static trong `TenantSearchService` (2_Gateway) — WASM không reference được.
- `CatalogController.Search` `[AllowAnonymous]` + `ResponseCache` — KHÔNG cần đổi
  (query gửi lên đã là câu đúng sau client rewrite).
- `ProductSearchResults.ApplyDidYouMean` — chỉ re-search, không ghi nhận.
- `vananVoiceInput` → `OnVoiceFinal` auto-submit — transcript đi cùng pipeline text.
- localStorage đã dùng khắp KhachLink (`customer_device_id` get-or-create qua
  IJSRuntime) — `SearchCorrectionStore` theo cùng pattern.
- KhachLink + Directory đều đã `ProjectReference` `1_Shared` → extract shared helper OK.

## IMPACT-NGUỢC GUARDS (không được vỡ)

- `TenantSearchService.NormalizeSearchText`/`SearchSimilarity` — GIỮ signature static
  (thin wrapper delegate sang `SearchTextNormalizer`) → mọi caller + tests hiện tại pass.
- `FeaturedProductSearchService` + `CatalogController` — KHÔNG đổi gì.
- E2E hooks: `#product-search-input`, chip DidYouMean, `w3-2d-khachlink-search.spec.ts`,
  `w3-2c-search-ux.spec.ts`, `search-unification.spec.ts` — GIỮ NGUYÊN.
- `Voice_Note_Enabled` flag gate mic — không đổi.
- `CatalogSearchMatchingTests` + fuzzy/level tests — vẫn pass (logic chỉ move, không sửa).
- Domain layer — KHÔNG đụng (không entity mới).
- Anonymous flow — không header/identity nào thêm vào search calls.

## IMPLEMENTATION CHECKLIST

### Phase A — Extract `SearchTextNormalizer` → `1_Shared`

- [ ] `1_Shared/Search/SearchTextNormalizer.cs` — MOVE `DiacriticFold` map (67 ký tự
  TV + Latin-1), `NormalizeSearchText`, `SearchSimilarity`, `LevenshteinDistance`
  nguyên xi từ `TenantSearchService` (comment InvariantGlobalization giữ nguyên).
- [ ] `TenantSearchService`: 2 static method cũ → thin wrapper delegate
  (`NormalizeSearchText(s) => SearchTextNormalizer.Normalize(s)` v.v.) — caller không đổi.
- [ ] Namespace chọn: `VanAn.Shared.Search` (không đặt trong Domain aggregates).

**Gate A:** build 0 err · `CatalogSearchMatchingTests` + fuzzy tests pass y nguyên.

### Phase B — `SearchCorrectionStore` (KhachLink `Services/`)

- [ ] Storage key `vanan_search_corrections` — JSON `{v:1, <scope>: {<norm>: {c,d,n,t,s}}}`
- [ ] `ResolveAsync(scope, norm)` → correctedDisplay | null (TTL prune 180d + count≥1)
- [ ] `RecordAsync(scope, raw, suggestion, source)` — normalize keys qua
  `SearchTextNormalizer`; upsert `n++`, `t=now`; LRU evict >200/scope; fail-open
- [ ] `ForgetAsync(scope, norm)` — delete entry
- [ ] Load/parse 1 lần cache in-memory; mọi op try/catch (private mode/quota/corrupt → no-op)

**Gate B (TDD):** `SearchCorrectionStoreTests` (bUnit + JSInterop mock localStorage)
— record/resolve hit+miss/scope tách/count++/TTL prune/LRU evict/corrupt JSON fail-open/
forget. Target ≥10 tests.

### Phase C — `ProductSearchResults.razor` wiring

- [ ] Inject `SearchCorrectionStore`
- [ ] `Search()`: normalize `_keyword` → `ResolveAsync("product", …)` → hit thì
  `_resolvedFrom = raw`, `_keyword = corrected` → `CatalogHttp.SearchProductsAsync(corrected)`
- [ ] `_lastInputWasVoice`: set true trong `OnVoiceFinal`, reset sau mỗi `Search()`
- [ ] `ApplyDidYouMean`: `RecordAsync("product", _keyword, suggestion, voice|typed)`
  fire-and-forget (nuốt lỗi) TRƯỚC `_keyword = suggestion` → `Search()`
- [ ] UX note `_resolvedFrom != null` → `VanAnAlert` nhỏ "Đang tìm '{_keyword}' —
  đã học từ '{_resolvedFrom}'" + `VanAnButton` "Hoàn tác" → `ForgetAsync` →
  `_keyword = _resolvedFrom` → `_resolvedFrom = null` → `Search()` (fail-open)

**Gate C:** bUnit — chip accept gọi RecordAsync (mock store); personal-hit path render
note; undo → re-search raw. Markup hooks giữ nguyên.

### Phase D — ship

- [ ] `dotnet build VanAn.sln` 0 err · `dotnet test` Core.Tests · guard-check.ps1 ALL PASSED
- [ ] E2E spec `search-personalization.spec.ts` (Gate 4 — UI note mới):
  accept → reload → cùng query → note hiện · undo → DidYouMean trở lại
  (chạy validation wave sau Step 7 — Playwright tắt trong IMPLEMENT)
- [x] Commit → push → CD → RV prod — **DEPLOYED `fa934f62` (2026-11-28)**,
  sweep `6_Testing/rv-scripts/personalized-didyoumean-rv-sweep.mjs` **16/17 PASS**:
  resolve→note→request rewrite (`q=trà sữa`)→undo→forget→re-search raw đều
  verify trên prod. 1 FAIL = featured catalog trống trên prod (data limitation —
  DidYouMean chip product-scope không thể fire; record-path cover bởi 5/5 bUnit).

## W2 — Tenant-scope wiring + Search Load Hardening ✅ DONE — deployed `8ba617bb`, RV 24/24

### Sweep findings (verified 2026-11-28)

- **Chip surfaces còn lại:** KhachLink `StoreFinder` (tenant + debounce 300ms, OnSearch
  cancel) · Directory `Home` (CẢ 2 scope — `_searchMode` store|product) · Directory
  `StoreFinder` (tenant, OnSearch-only). KhachLink `Home`/`StoreSearchBar`/`HoptacxaPortal`
  chỉ navigate — chip nằm ở page đích, KHÔNG wire.
- **Store placement:** `SearchCorrectionStore` đang ở KhachLink — Directory (SSR) không
  reference được → **MOVE sang `UI.Platform/Services/`** (namespace
  `VanAn.UI.Platform.Services`); UI.Platform đã dùng `IJSRuntime` (LeafletMapAdapter,
  Realtime panels). DI `AddScoped` cả 2 app. SSR: store chỉ gọi trong handler
  post-render (Search/Apply/undo) → an toàn, không đụng prerender.
- **Load root causes đã verify:**
  - `StoreFinder.LoadStores` `client.GetAsync(url)` **không truyền CT** → debounce
    chỉ chặn request mới, request in-flight vẫn chạy tới DB (wasted work).
  - **Không min-length** — 1 ký tự đã fire full pipeline.
  - **Không client cache** — gõ rồi xóa rồi gõ lại = 2 request y hệt.
  - `/api/tenants/search` (`TenantStoreController:145`) **không ResponseCache**
    (`/api/catalog/search` đã có 60s).
  - Fuzzy path: `OrderBy(Name).Take(FuzzyCandidateLimit=500)` → normalize +
    Levenshtein in-memory/request; khi >500 tenants chỉ quét 500 đầu alphabet
    (correctness tiềm ẩn, giải quyết ở B2/B3 — DEFER Gate 5).

### W2 Implementation checklist (TDD — test trước)

- [x] **W2a. Move store:** `SearchCorrectionStore` → `UI.Platform/Services/` +
  update 14 `SearchCorrectionStoreTests` (namespace only) + DI `AddScoped` cả
  KhachLink `Program.cs` + Directory `Program.cs`. Tests vẫn 14/14.
- [x] **W2b. Client guards (`StoreFinder` KhachLink):**
  - A1 CT: `LoadStores(ct)` — `_searchDebounce` CT truyền vào `GetAsync`/`ReadFromJsonAsync`
    (cancel in-flight khi gõ tiếp); dispose CTS cũ.
  - A2 min-length ≥2 mới trigger keyword search (1 ký tự → giữ list, không call).
  - A3 `SearchResultCache` scoped service — map `normQuery → (dto, ts)` TTL 60s,
    cap 50; StoreFinder check cache trước HTTP (mirror server ResponseCache).
  - Fix thêm: `LoadStores` thiếu `StateHasChanged()` (bUnit bắt được — caller qua
    dispatcher/JSInvokable không re-render) → thêm trong `finally`.
- [x] **W2c. Tenant-scope wiring:** `StoreFinder` (KhachLink + Directory) +
  `Home` (Directory — cả 2 mode: `scope="tenant"` cho store mode, `"product"` cho
  product mode) — resolve-before-search + record-on-accept + note + undo (copy
  pattern `ProductSearchResults`).
- [x] **W2d. B1:** `[ResponseCache(Duration=60, Location=Any, VaryByQueryKeys=
  {"name","lat","lng"})]` trên `TenantStoreController.Search`.
- [x] **W2e. Ship:** build 0 err · Core.Tests 3136 PASS / 0 FAIL · guard-check ALL
  PASSED · commit `8ba617bb` → push → CD Multi-VPS success (Gateway+KhachLink+Dir).
- [x] **W2 RV prod (2026-11-28):** sweep **24/24 PASS** — record-path tenant scope
  verify được trên prod (`antaam` → chip "An Tâm" → accept → `scopes.tenant` entry);
  seeded pair `xyzq`→"Cafe Tân Quy" → rewrite `name=Cafe Tân Quy` ra network; undo
  → forget + raw re-search; A2 1-char→0 req · A1 debounce 1 req/burst ≤2 · A3 repeat
  →0 req · B1 `cache-control: public,max-age=60`; Dir StoreFinder note+rewrite+undo
  (SSR: assert DOM vì API call server-side); 0 console/page errors.

### W2 TDD test list (viết trước khi sửa page)

- `StoreFinderTests` (bUnit, fake HttpMessageHandler đếm call + delay):
  1. rapid typing → ≤1 call sau 300ms idle (debounce giữ nguyên)
  2. 1 ký tự → 0 call (min-length)
  3. keystroke mới → request cũ bị cancel (handler assert CT cancelled)
  4. cùng query 2 lần → 1 HTTP call (cache hit)
  5. learned entry → request rewritten `name=<corrected>` + note hiện
  6. undo → entry xóa + re-search raw
- `DirectorySearchTests` (bUnit nếu test infra cover SSR; nếu không → RV sweep cover):
  7. Home store-mode learned resolve → `name=` rewrite; product-mode → `q=` rewrite
- Existing 14 store tests pass y nguyên sau move (đổi using duy nhất).

### W2 RV sweep-first (extend `personalized-didyoumean-rv-sweep.mjs`)

- Tenant scope CÓ data prod (An Tâm, Cafe Tân Quy…) → **full loop test được kể cả
  record-path**: misspell tenant thật (vd "cafe tan qui") → chip → accept →
  localStorage `scopes.tenant` → repeat → resolve. Trên `commienphi.timlathay.com/stores`
  + `timlathay.com` (Directory Home/StoreFinder).
- Check A1-A3 trên prod: typing burst → đếm `/api/tenants/search` requests trong
  Network (expect ≤2) · 1-char → 0 request · repeat query → cache hit.
- Check B1: 2 cùng request liên tiếp → response header `Age`/cache phù hợp (hoặc
  Gateway log không có query mới).

### W2 Definition of 100% done

1. Store ở `UI.Platform`, 14 store tests + page tests ALL PASS, DI cả 2 app
2. StoreFinder: CT abort in-flight + min-length + 60s client cache (tests 1-4 pass)
3. Tenant scope learned flow trên 3 surface (note + undo + request rewrite)
4. `[ResponseCache]` trên `/api/tenants/search`
5. `dotnet build VanAn.sln` 0 err · Core.Tests ALL PASS · guard-check ALL PASSED
6. RV sweep tenant-scope: full loop incl. record-path (tenant data prod có sẵn) + load checks — ALL PASS

## OUT OF SCOPE (waves sau)

- W3: Profile "Gợi ý đã học" management · opt-in telemetry/global dictionary · sync đa thiết bị
- B2 (`name_norm` computed column + index) · B3 (`pg_trgm` GIN index) — **defer,
  cần Gate 5** (EF config + migration)
- C1 metrics request-rate/slow-query · C2 dedicated search engine — defer
- KHÔNG làm: bảng PG, endpoint corrections, `X-Customer-Device-Id` trên search calls,
  `VaryByHeader` (thiết kế v1.0 server-side đã bỏ theo quyết định user 2026-11-28)

## OPEN QUESTIONS (từ SRS §7 — chờ chốt)

Q1 MinAccepts=1 · Q2 TtlDays=180 · Q3 defer màn quản lý sang W3 · Q4 source optional · Q5 client-only đã chốt
