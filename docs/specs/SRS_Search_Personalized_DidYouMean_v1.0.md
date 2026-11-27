# SRS — Personalized DidYouMean (Self-Learning Search Correction)
## Học sửa chữa truy vấn theo từng người dùng — voice + typed input · **CLIENT-SIDE STORAGE**

- **Version:** 1.1 (DRAFT — for review) — v1.1: storage chuyển từ Gateway PG → **localStorage trên thiết bị người dùng** (user quyết định 2026-11-28: tránh tăng trưởng PG khi buyer lên hàng triệu; backend ~0 thay đổi, không Gate 5)
- **Date:** 2026-11-28
- **Status:** PROPOSED
- **Scope:** Public search surfaces (KhachLink WASM trước; Directory SSR + tenant search theo sau)
- **Out of scope:** Global/cross-user learning, server sync đa thiết bị, AI-chat suggestions, ShopERP admin search, semantic/LLM search
- **Liên quan:** `docs/design/mobile-search-ux-pattern.md` (Pattern B) · governance "Public Search (anti-drift, W3-2d)"

---

## 1. Bối cảnh & Mục tiêu

### 1.1 User story gốc

Người dùng nói ngọng (hoặc quen cách phát âm riêng) dùng voice search: nói **"tà sua"**
→ STT (Web Speech API vi-VN) transcript `"tà sua"` → fuzzy match → chip DidYouMean
"Ý bạn là: **Trà Sữa**?" → user bấm đúng → re-search.

**Vấn đề:** lần sau cùng người đó nói "tà sua", hệ thống lại hỏi lại y hệt — không nhớ
rằng VỚI NGƯỜI NÀY, "tà sua" = "trà sữa". Misspelling của mỗi người là ổn định và
mang tính cá nhân (speech pattern, quen tay gõ) — đây chính là tín hiệu học tốt nhất.

### 1.2 Mục tiêu

- **O1 — Learn on accept:** khi user bấm chip DidYouMean, client ghi nhận cặp
  `(câu gốc → câu đã chọn)` vào **localStorage của chính thiết bị**.
- **O2 — Auto-resolve:** lần sau cùng người nhập/nói cùng câu gốc → client tự rewrite
  query thành câu đúng TRƯỚC khi gọi API — KHÔNG cần bước gợi ý; kết quả trả về như
  đã search đúng từ đầu.
- **O3 — Per-user:** correction map nằm trên thiết bị → cá nhân hóa theo đúng
  nghĩa vật lý (per-browser-profile + per-origin); không ảnh hưởng người khác,
  không cần identity plumbing.
- **O4 — Transparent + undoable:** khi auto-resolve, UI báo rõ "đã hiểu X = Y" và
  cho phép hoàn tác → tránh cảm giác "search bị sai".

### 1.3 Non-goals

- KHÔNG học từ implicit signals (click kết quả, dwell time) — chỉ học khi user
  **explicit accept** chip. Implicit học dễ nhiễu và khó kiểm chứng.
- KHÔNG lưu server (Gateway PG) — quyết định v1.1: tránh tăng trưởng PG theo số
  buyer; privacy tốt hơn (speech pattern không rời thiết bị). Sync đa thiết bị
  hoặc global dictionary có thể là wave tương lai (opt-in telemetry), đánh giá riêng.
- KHÔNG thay matcher (`NormalizeSearchText`/`SearchSimilarity`) — governance CẤM
  viết matcher riêng; feature này chỉ thêm **Level 0 lookup (dict hit)** phía client.

---

## 2. Hiện trạng (verified 2026-11-28)

| Mảnh | Trạng thái | Evidence |
|---|---|---|
| Voice STT vi-VN | ✅ Production | `vananVoiceInput` JS; `OnVoiceFinal` auto-submit — `ProductSearchResults.razor`, `StoreFinder.razor` (KhachLink), `Home.razor` (Directory) |
| DidYouMean product | ✅ Production | `FeaturedProductSearchService` → `GET /api/catalog/search` (`DidYouMean` khi fuzzy best < 0.9) |
| DidYouMean tenant | ✅ Production | `TenantSearchService.SearchAsync` → `GET /api/tenants/search` |
| Chip 1-tap | ✅ Production | `VanAnDidYouMeanChip` — `OnApply` → re-search với suggestion |
| localStorage usage | ✅ Production | `customer_device_id`, `customer_token`, `device_token` — get-or-create qua `IJSRuntime` khắp KhachLink/Directory |
| Shared normalize | ⚠️ Cần extract | `TenantSearchService.NormalizeSearchText`/`SearchSimilarity` là static trong `2_Gateway` — WASM/Directory không reference được; cả 2 đều đã reference `1_Shared` → extract sang `SearchTextNormalizer` |
| Feedback/learning | ❌ **Chưa có** | `ApplyDidYouMean` chỉ re-search — không ghi nhận |
| Correction storage | ❌ **Chưa có** | Cần `SearchCorrectionStore` client-side (localStorage) |

**Nhận định (v1.1):** backend **0 thay đổi** — không entity/migration/Gate 5,
không endpoint mới, không cache fix (query gửi lên đã là câu đúng → cache
`VaryByQueryKeys` hiện có vẫn đúng và còn tăng hit-rate chung). Toàn bộ logic
học nằm ở client + 1 helper extract sang `1_Shared`.

---

## 3. Requirements

### 3.1 Functional Requirements

| ID | Requirement |
|---|---|
| FR-1 | **Record on accept (client).** Khi user bấm chip DidYouMean, `ApplyDidYouMean` ghi entry vào local map: `{ scope, misspelledNorm → { correctedNorm, correctedDisplay, count, lastUsed, source } }`. Ghi TRƯỚC re-search; fail-open (lỗi storage → vẫn re-search). |
| FR-2 | **Level 0 client lookup.** Trong `Search()`: normalize `_keyword` → tra local map theo `scope` → nếu hit (`count >= MinAccepts`) → rewrite `_keyword = correctedDisplay` → gọi API với câu đúng; set `ResolvedFrom = câu gốc` để UI hiện note. |
| FR-3 | **Suggestion trust.** Suggestion lưu local là giá trị server trả về trong field `DidYouMean` (nguồn tin cậy server-side) — client không tự sinh suggestion. Không cần server validation vì dữ liệu chỉ ảnh hưởng query của chính thiết bị. |
| FR-4 | **No identity needed.** Không gửi `X-Customer-Device-Id`, không auth — localStorage bản chất đã per-browser-profile + per-origin. Search API giữ nguyên AllowAnonymous. |
| FR-5 | **Undo.** Khi `ResolvedFrom != null`, UI hiện note "Đang tìm '{corrected}' — đã học từ cách bạn nhập '{raw}'" + nút **Hoàn tác** → xóa entry local → re-search với câu gốc (rơi lại flow DidYouMean thường). |
| FR-6 | **Scope tách biệt.** Key theo `scope` (`product` \| `tenant`) — "tà sua" học ở product không leak sang tenant search. |
| FR-7 | **Cap + LRU.** Tối đa `MaxCorrections` (default 200) entries/scope; vượt cap evict entry `lastUsed` cũ nhất khi ghi. |
| FR-8 | **Source tag.** Entry lưu `source` = `voice` \| `typed` (client biết query đến từ `OnVoiceFinal` hay gõ tay) — dùng cho analytics/opt-in telemetry sau; không ảnh hưởng matching. |
| FR-9 | **Min-accept threshold.** Auto-resolve chỉ khi `count >= MinAccepts` (constant default 1 — kịch bản "chọn đúng 1 lần là nhớ"). Accept lần sau cùng cặp → `count++` (cho phép tăng threshold sau mà không mất data). |
| FR-10 | **TTL soft-prune.** Khi đọc, bỏ qua + dọn entry `lastUsed` cũ hơn `TtlDays` (default 180) — tự hygiene, không cần job. |

### 3.2 Non-Functional Requirements

| ID | Requirement |
|---|---|
| NFR-1 | **Privacy tối đa.** Speech/typo pattern không rời thiết bị; không PII (chuỗi normalize); không server log correction. |
| NFR-2 | **Perf.** Dict hit trên ≤200 entries trong 1 JSON object — 1 localStorage get/parse per search (~µs). Không thêm network call. |
| NFR-3 | **Fail-open.** localStorage denied/quota/private-mode/JSON corrupt → try/catch → feature tắt êm, search chạy bình thường. |
| NFR-4 | **Zero server cost.** Không bảng, không endpoint, không rate limit, không storage PG — scale vô hạn theo số buyer. |
| NFR-5 | **Cache-friendly.** Client gửi câu đúng → response cache `VaryByQueryKeys` hiện có vẫn hợp lệ, không lẫn user (resolve xảy ra trước request). |
| NFR-6 | **Quota.** localStorage ~5–10MB/origin; 200 entries ≈ ~30–50KB — an toàn. Key tối giản để tiết kiệm. |
| NFR-7 | **Per-origin.** Map học riêng theo domain (timlathay.com ≠ shopai.timlathay.com) — khớp usage thực tế (người quen của 1 shop PWA); ghi nhận là giới hạn đã chấp nhận. |

### 3.3 Anti-drift (governance)

- Normalize client PHẢI dùng `SearchTextNormalizer` trong `1_Shared` (extract từ
  `TenantSearchService` — Gateway delegate lại, single source) — CẤM viết lại.
- UI search: vẫn qua `VanAnPublicSearchBar` + `VanAnDidYouMeanChip`; note "đã học"
  dùng `VanAnAlert`/`VanAnButton` — KHÔNG markup mới ngoài UI.Platform.
- **Không chạm Domain layer** — không entity mới → không cần Gate 5.

---

## 4. Thiết kế

### 4.1 Storage format (localStorage)

Key: `vanan_search_corrections` — 1 JSON object, schema compact:

```json
{
  "v": 1,
  "product": {
    "ta sua": { "c": "tra sua", "d": "Trà Sữa 79", "n": 2, "t": 1732760400000, "s": "voice" }
  },
  "tenant": {
    "antam coffee": { "c": "an tam coffee", "d": "An Tâm Coffee", "n": 1, "t": 1732760400000, "s": "typed" }
  }
}
```

- Key ngoài = `scope`; key trong = `misspelled_norm` (đã `NormalizeSearchText`).
- `c` = correctedNorm · `d` = correctedDisplay · `n` = acceptCount · `t` = lastUsed (unix ms) · `s` = source.
- Version field `v` để migrate schema sau.

### 4.2 Shared normalize (`1_Shared`)

- Class mới `VanAn.Shared.Search.SearchTextNormalizer` — MOVE `DiacriticFold` +
  `NormalizeSearchText` + `SearchSimilarity` + `LevenshteinDistance` từ
  `TenantSearchService` (giữ nguyên logic, copy nguyên bảng fold map).
- `TenantSearchService` giữ 2 static method cũ làm thin wrapper delegate →
  mọi caller/test hiện tại không đổi; `FeaturedProductSearchService` không đổi.
- KhachLink WASM + Directory đã reference `1_Shared` → dùng trực tiếp.

### 4.3 Client service (KhachLink)

`SearchCorrectionStore` (mới, `Services/`):

```csharp
Task<string?> ResolveAsync(string scope, string normalizedQuery);  // → correctedDisplay hoặc null (kèm TTL prune + count check)
Task RecordAsync(string scope, string rawQuery, string suggestion, string source); // upsert + LRU cap, fail-open
Task ForgetAsync(string scope, string normalizedQuery);            // delete entry
```

- Load/parse JSON 1 lần, cache in-memory per service lifetime; ghi thì serialize lại.
- Mọi thao tác bọc try/catch — localStorage throw (private mode) → null no-op.

### 4.4 Page wiring (W1: `ProductSearchResults.razor`)

```
Search():        q = _keyword.Trim()
                 norm = Normalize(q)
                 hit = await _corr.ResolveAsync("product", norm)
                 if hit → _resolvedFrom = q; _keyword = hit; call API(hit)
                 else   → _resolvedFrom = null; call API(q)

ApplyDidYouMean: _corr.RecordAsync("product", _keywordTrước, suggestion,
                                  _lastInputWasVoice ? "voice" : "typed")   [fire-and-forget]
                 → _keyword = suggestion → Search()

Undo note:       "Đang tìm '{_keyword}' — đã học từ '{_resolvedFrom}'  [Hoàn tác]"
                 → _corr.ForgetAsync("product", Normalize(_resolvedFrom))
                 → _keyword = _resolvedFrom → _resolvedFrom = null → Search()

_lastInputWasVoice: set true trong OnVoiceFinal; reset false sau mỗi Search()
```

### 4.5 UX flow

```
Lần 1:  "tà sua" → API fuzzy → chip "Ý bạn là: Trà Sữa?" → [accept]
        → ghi local map + re-search "trà sữa"

Lần 2+: "tà sua" → Level 0 hit (client) → API("trà sữa") → results
        + note: "Đang tìm 'Trà Sữa' — đã học từ 'tà sua'  [Hoàn tác]"

Hoàn tác: xóa entry → API("tà sua") → DidYouMean như cũ
```

---

## 5. Security & Abuse

| Rủi ro | Đánh giá | Ghi chú |
|---|---|---|
| Poisoning | **Không đáng kể** — user chỉ tự sửa map của chính mình; suggestion đến từ server field `DidYouMean` (nguồn tin cậy) | Worst case: tự inject entry sai trong localStorage → query sai → pipeline rơi "suggested" — self-inflicted, harmless |
| Privacy | **Tối đa** — data không rời thiết bị | Không telemetry mặc định; opt-in sau nếu cần global dictionary |
| Storage quota | Thấp — ~50KB/scope cap | FR-7 LRU + NFR-6 |
| Correction rot (học sai 1 lần) | Trung bình — UX | FR-5 undo + FR-9 threshold; (W3: màn quản lý "gợi ý đã học" trong Profile nếu cần) |
| localStorage bị xóa | Mất map → degrade về DidYouMean, học lại | Chấp nhận — graceful degradation |
| Nhiều người chung 1 browser profile | Share corrections | Giống hệt model deviceId — caveat đã chấp nhận |

---

## 6. Phasing

| Wave | Phạm vi | Ghi chú |
|---|---|---|
| **W1 (MVP)** | `SearchTextNormalizer` extract sang `1_Shared` + `SearchCorrectionStore` + `ProductSearchResults.razor` wiring (record/resolve/note/undo) + tests | Task card `task_search_personalized_didyoumean.md` · backend 0 thay đổi |
| **W2** | Tenant search (`StoreFinder`/`Home` KhachLink + `Home`/`StoreFinder` Directory — Directory đã interactive nên đọc localStorage được post-render) — tái dùng `SearchCorrectionStore` với `scope="tenant"` | Card riêng, nhỏ |
| **W3 (optional)** | Màn quản lý "Gợi ý đã học" trong Profile (list/delete/clear) · opt-in telemetry nếu muốn global dictionary sau · sync đa thiết bị cho logged-in (nếu thật sự cần) | Đánh giá riêng |

W1 đã cover 100% user story gốc trên surface chính (KhachLink product search).

---

## 7. Open Questions (chốt trước implement)

1. **Q1 — MinAccepts default = 1?** Đề xuất **1** (đúng kịch bản "chọn đúng là nhớ
   ngay"); undo sẵn nếu học sai. `count` vẫn lưu để nâng threshold sau.
2. **Q2 — TtlDays=180 soft-prune?** Entry cũ không dùng tự dọn khi đọc. OK?
3. **Q3 — Màn quản lý "gợi ý đã học" defer W3?** Undo trên note đã đủ cho lỗi
   học sai điển hình. OK?
4. **Q4 — `source=voice|typed` lưu optional?** Chi phí ~0, giá trị cho analytics sau. OK?
5. **Q5 — Sync đa thiết bị / global learning?** Đã chốt hướng client-only; nếu
   sau này cần → đánh giá riêng (opt-in telemetry hoặc upload cho logged-in).

---

## 8. Acceptance Criteria

- [ ] Nói/gõ "tà sua" → accept "Trà Sữa" → entry `product["ta sua"]` tồn tại trong `vanan_search_corrections` localStorage.
- [ ] Cùng browser search "tà sua" lần 2 → kết quả như search "trà sữa", note "đã học từ 'tà sua'" + Hoàn tác hiển thị; network request thực tế gửi `q=Trà Sữa`.
- [ ] Browser/profile khác search "tà sua" → vẫn DidYouMean thường (không bị học của người khác).
- [ ] Bấm Hoàn tác → entry xóa → search "tà sua" hiện DidYouMean như cũ.
- [ ] localStorage bị disable/corrupt → search chạy bình thường, không exception.
- [ ] Entry product không ảnh hưởng tenant search (scope tách).
- [ ] `dotnet build VanAn.sln` + Core.Tests pass; `SearchCorrectionStore` unit tests; E2E spec mới theo Gate 4 (validation wave).
