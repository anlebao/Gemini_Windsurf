# TASK CARD: Personalized DidYouMean — Self-Learning Search Correction (W1)

> Created: 2026-11-28 · Updated: 2026-11-28 (v1.1 — client-side storage, user chốt)
> Mode: IMPLEMENT (sau khi SRS duyệt) · TDD
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

## OUT OF SCOPE (waves sau)

- W2: tenant search — `StoreFinder`/`Home` (KhachLink) + `Home`/`StoreFinder` (Directory,
  post-render localStorage OK vì đã interactive) — tái dùng store với `scope="tenant"`
- W3: Profile "Gợi ý đã học" management · opt-in telemetry/global dictionary · sync đa thiết bị
- KHÔNG làm: bảng PG, endpoint corrections, `X-Customer-Device-Id` trên search calls,
  `VaryByHeader` (thiết kế v1.0 server-side đã bỏ theo quyết định user 2026-11-28)

## OPEN QUESTIONS (từ SRS §7 — chờ chốt)

Q1 MinAccepts=1 · Q2 TtlDays=180 · Q3 defer màn quản lý sang W3 · Q4 source optional · Q5 client-only đã chốt
