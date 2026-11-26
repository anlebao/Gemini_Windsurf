# TASK CARD: Public Search Bar Unification (UI Platform)

> Created: 2026-11-26 · Owner: Devin session
> Mode: IMPLEMENT (user-approved refactor) · TDD
> Rule: governance "Public Search (anti-drift, W3-2d)" — mọi search UI public surface
> PHẢI dùng `VanAnPublicSearchBar`; pattern doc `docs/design/mobile-search-ux-pattern.md`

## OBJECTIVE

Quy 3 search box public về **một vỏ input chuẩn** `VanAnPublicSearchBar`
(UI.Platform): fix/nâng cấp search-input (mic, clear, enterkeyhint, mobile)
chỉ sửa 1 file. Backend + result rendering giữ riêng — KHÔNG merge domain
(Directory tìm tenant ≠ Store tìm sản phẩm/AI).

## CURRENT DRIFT

| Surface | Input hiện tại | Vấn đề |
|---|---|---|
| Directory Home + StoreFinder (timlathay.com) | `VanAnPublicSearchBar` ✅ | Chuẩn rồi |
| KhachLink StoreFinder | `VanAnPublicSearchBar` ✅ | Chuẩn rồi |
| HoptacxaPortal | `VanAnPublicSearchBar` ✅ | Chuẩn rồi |
| shopAI `Store.razor` → `StoreSearchBar` | custom `.store-search-box` | Drift — input shell riêng |
| KhachLink `Home.razor` | custom `.home-search-box` | Drift — input shell riêng |

## IMPACT-NGUỢC GUARDS (không được vỡ)

- e2e `ai-chat-widget.spec.ts` hooks: `.store-search-input`, `.store-search-btn`,
  `.store-search-mic`, `.store-search-answer`, `.store-search-product`,
  `.store-search-add`, `.store-search-ask`, `.store-search-empty`,
  `.store-search-result`, `.store-search-voice-error` — GIỮ NGUYÊN tất cả.
- e2e `w3-2-polish.spec.ts` + archived issue-142: `#home-search-input`,
  `.home-voice-mic-btn` — GIỮ NGUYÊN.
- StoreSearchBar behavior: Enter/button → `SearchAsync`; clear-on-success /
  keep-on-fail; `OnVoiceInterim/Final/Error/End` JSInvokable; maxlength=500;
  spinner khi searching; "Hỏi AI thêm" chip; fail-open NFR-3.
- Home.razor behavior: tab store/product → navigate `/stores?q=`|`/products?q=`;
  voice flow `startRecording`/`stopRecording`/`SetTranscriptionText`/
  `UpdateVoiceTranscript`/`OnRecordingEnd`; recording hint + voice error;
  `.recording` pulse CSS (map qua `VoiceButtonCssClass` dynamic).
- `VanAnPublicSearchBar` consumers hiện tại (Directory ×2, StoreFinder,
  HoptacxaPortal) — params mới PHẢI optional, default không đổi markup.

## IMPLEMENTATION CHECKLIST

### Phase A — `VanAnPublicSearchBar` additive params

- [ ] `GroupCssClass` → root `.input-group` (Home dùng `input-group-lg`)
- [ ] `SearchButtonCssClass` → VanAnButton `CssClass` (e2e hook)
- [ ] `VoiceButtonCssClass` → mic button (e2e hook)
- [ ] `InputMaxLength` (int?) → `maxlength` attr khi set
- [ ] `IsListening` → thêm class `listening` (giữ `text-danger`) — pulse CSS
- [ ] Search `VanAnButton` nhận `Loading="@Loading"` → spinner (giữ UX cũ
  của StoreSearchBar — BaseComponent.Loading đã có spinner sẵn)

**Gate A:** `VanAnPublicSearchBarTests` mới (params render đúng, Enter→OnSearch,
clear→Value="", IsListening→listening class, Loading→spinner+disabled) pass.

### Phase B — `StoreSearchBar` compose `VanAnPublicSearchBar`

- [ ] `.store-search` + `.store-search-box` wrapper GIỮ (pill chrome)
- [ ] Bên trong: `<VanAnPublicSearchBar @bind-Value="_query"
  InputCssClass="store-search-input" SearchButtonCssClass="store-search-btn"
  VoiceButtonCssClass="store-search-mic" InputMaxLength="500" SearchText="Tìm"
  ShowVoiceButton="@(VoiceEnabled && _speechSupported)" IsListening="_listening"
  OnVoiceToggle="ToggleVoiceAsync" Loading="_searching" OnSearch="SearchAsync" />`
- [ ] `.razor.css`: input phần viết lại bằng `::deep` (scoped CSS không chạm
  child component); result/mic-pulse giữ nguyên
- [ ] Giữ: `@onkeydown` Enter→SearchAsync, clear-on-success, voice JS invokable

**Gate B:** `StoreSearchBarTests` — 15 test hiện có vẫn pass (hooks giữ nguyên)
+ test mới: clear-X, `enterkeyhint="search"`, maxlength=500.

### Phase C — `Home.razor` search box

- [ ] Thay `input-group input-group-lg` custom bằng `VanAnPublicSearchBar`
  trong wrapper `.home-search-box` (giữ radius+overflow+shadow)
- [ ] Params: `InputId="home-search-input"`, `GroupCssClass="input-group-lg"`,
  `SearchText=""` (icon-only như cũ), `ShowVoiceButton`, `IsListening`,
  `OnVoiceToggle="ToggleHomeVoiceSearch"`, `OnSearch="SubmitHomeSearch"`,
  `VoiceButtonCssClass` dynamic (`recording` khi đang ghi)
- [ ] Tabs + recording hint + `_homeVoiceError` div giữ nguyên
- [ ] Bỏ `OnHomeSearchKeyUp` (component tự handle Enter→OnSearch)

**Gate C:** build pass; manual check e2e hooks trong markup.

### Phase D — ship

- [ ] `dotnet build` KhachLink + UI.Platform 0 err; `dotnet test` (Core.Tests)
- [ ] guard-check.ps1 ALL PASSED → commit → push → CD → RV
  (timlathay.com `/`, storefront `/store/...` search UI)

## OUT OF SCOPE

- Merge search backend (tenant search vs tenant-ai search) — giữ riêng
- Đổi kết quả render (store cards / AI answer panel)
- `VanAnSearchBar` admin (ShopERP TransactionHistory) — không phải public surface
