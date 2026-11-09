# Task P1: KhachLink `/onboard` — Self-Serve Merchant Wizard

> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` — FR-7..12
> **Master plan:** `docs/AI/plans/self-serve-onboarding-master-plan.md` §5.1
> **Mode:** IMPLEMENT (sau P0 chốt API contract)
> **Layer:** 5_WebApps/KhachLink (Blazor WASM, public)
> **Prerequisite:** P0 DONE (endpoints + DTOs contract cố định)

## 1. OBJECTIVE

Trang public `/onboard` trên KhachLink PWA: merchant tự onboarding 6 bước — case select → giấy tờ/cơ bản (OCR) → biển hiệu + menu (OCR) → GPS + bản đồ → account (credentials + SĐT) → review + submit. Mobile-first, tiếng Việt, không cần đăng nhập.

## 2. FILES

### Mới
| File | Nội dung |
|---|---|
| `5_WebApps/KhachLink/Pages/Onboard.razor` (+`.razor.css`) | Wizard 6 bước — port cấu trúc `OcrOnboardingWizard.razor` sang WASM |
| `5_WebApps/KhachLink/wwwroot/js/onboarding-ocr.js` | **Copy nguyên** từ ShopERP (Tesseract vie+eng, extractGpdk/SignName/Menu) — chỉ đổi comment header |
| `5_WebApps/KhachLink/wwwroot/js/onboarding-wizard.js` | Port camera capture + geolocation + initMap (MapLibre `vananMap` — check KhachLink đã load lib chưa; nếu chưa → copy pattern App.razor/index.html) |
| `5_WebApps/KhachLink/wwwroot/js/lib/ocr/*` | Copy assets Tesseract từ `5_WebApps/ShopERP/wwwroot/js/lib/ocr/` (tesseract.min.js, worker.min.js, core wasm, vie.traineddata.gz, eng.traineddata nếu có) |
| `5_WebApps/KhachLink/Services/Http/MerchantSignupHttpService.cs` | Client gọi P0 endpoints (submit, status) |
| `5_WebApps/KhachLink/Pages/OnboardStatus.razor` | `/onboard/status` — tra trạng thái bằng signupId+phone |

### Sửa
| File | Change |
|---|---|
| `5_WebApps/KhachLink/Components/Layout/KhachLinkLayout.razor` hoặc NavMenu | Thêm link "Đăng ký cửa hàng" → `/onboard` (public) |
| `5_WebApps/KhachLink/Program.cs` | Đăng ký `MerchantSignupHttpService`; check JS lib leaflet/maplibre đã load trong `index.html`/`App.razor` — thiếu thì thêm |
| `5_WebApps/KhachLink/wwwroot/index.html` | Nếu thiếu: script tags OCR/map libs (lazy — chỉ load khi vào /onboard nếu được) |
| `5_WebApps/KhachLink/wwwroot/js/vanan-interop.js` hoặc file mới | `getTurnstileSiteKey`/`getTurnstileToken`/`renderTurnstile` — **check đã có chưa** (Register.razor dùng `vanan.*` — có sẵn, reuse) |
| `5_WebApps/KhachLink/Pages/Register.razor` (`/claim`) | KHÔNG đổi flow; optional: thêm banner/link "Đăng ký nhanh bằng quét giấy tờ → /onboard" |

## 3. CHANGES CHI TIẾT

### 3.1 Wizard structure (mirror `OcrOnboardingWizard.razor`)

Steps: `0=Case` · `1=Giấy tờ/Cơ bản` · `2=Biển hiệu+Menu` · `3=Vị trí` · `4=Account` · `5=Review+Submit` · `6=Kết quả`

- **Case select (3 card):**
  - `CompanyWithLicense` — "Tôi có giấy phép kinh doanh (GPKD)" — mô tả: chụp GPKD để điền tự động.
  - `SmallShopNoLicense` — "Tôi có cửa hàng, chưa có GPKD" — mô tả: chụp biển hiệu + menu.
  - `StreetVendorUnregistered` — "Tôi bán hàng rong / chưa đăng ký kinh doanh" — mô tả: chỉ cần tên + menu; hệ thống ghi nhận là hộ kinh doanh nhóm miễn thuế, đội ngũ sẽ hướng dẫn đăng ký sau.
- **Step 1:** Case A → file input + camera modal (`extractGpdk`) → prefill MST/tên/địa chỉ/loại hình editable + confidence badge (port `ConfidenceBadge` renderfragment). Case B/Rong → form: Tên (prefill sau từ biển hiệu), Địa chỉ (**optional với Rong** — label "(không bắt buộc nếu bán rong)"), **HKDGroup helper** (3.2).
- **Step 2:** Biển hiệu (camera `extractSignName` → prefill tên Case B/Rong) + menu 1..n ảnh (`extractMenu` → bảng review/edit/thêm/xóa — port `ProductRow` pattern).
- **Step 3:** GPS button (`vananOnboardingWizard.getCurrentPosition`) + lat/lng inputs **luôn hiện** (kể cả chưa GPS) + map (`initMap`) + click-chọn marker (nếu JS hỗ trợ — nếu không MVP: input tay + GPS + map hiển thị) + địa chỉ optional.
- **Step 4 Account:** Họ tên*, SĐT* (+ nút "Gửi mã OTP" — **chỉ render khi `RequireOtp=true`**; P0 trả config public `GET /api/v1/merchant-signups/config` → `{ requireOtp: bool }` — nếu chưa có endpoint này, P1 hardcode false + TODO P4), Email, **Username** (validate realtime `^[a-z0-9._-]{3,30}$` + hint "dùng để đăng nhập"), **Password** (min 8) + confirm + cảnh báo lưu lại.
- **Step 5 Review:** tóm tắt case/tên/địa chỉ/nhóm thuế (text dễ hiểu)/số sản phẩm/ảnh thumbnails (photoUrls)/account info (ẩn password) → Turnstile widget + honeypot hidden → Submit.
- **Step 6 Kết quả:** "Đã ghi nhận hồ sơ" + signupId + link `/onboard/status` + hướng dẫn "chờ duyệt 1-2 ngày làm việc; khi được duyệt đăng nhập bằng username+password đã đặt".

### 3.2 HKDGroup helper (FR-9 — component inline trong wizard)

Thay dropdown trần: 2 câu hỏi → gợi ý:
- Doanh thu/tháng: `<5tr` / `5-100tr` / `>100tr` / `Không rõ`
- "Có đang nộp thuế GTGT không?": `Không biết` / `Có` / `Không`

Mapping gợi ý (đơn giản hóa TT152 — **ghi rõ "gợi ý, đội ngũ xác nhận khi duyệt"**):
- `<5tr` → Group1 ("Nhóm không phải nộp thuế GTGT/TNCN — phù hợp hộ kinh doanh nhỏ")
- `5-100tr` hoặc `>100tr` hoặc `Không rõ` → Group2 ("Nhóm nộp thuế GTGT + TNCN")
- `Có` GTGT → Group2/Group3 gợi ý Group2 + note
- Vẫn cho `<select>` đổi tay bên dưới (collapsed "Tôi muốn tự chọn nhóm thuế").

Case A legalForm=HKD → prefill gợi ý tương tự (không hardcode Group2 — fix inconsistency).

### 3.3 Photo upload → R2 (FR-10)

- `ImageUploadService` KhachLink đã có — check signature (có hỗ trợ folder param? nếu chưa → thêm param `folder` vào request `?folder=onboarding`).
- Sau mỗi capture (camera `captureAndOcr`/`captureMenuShot`/`finishMenuCapture`) hoặc file input → song song: OCR local (điền form) **và** upload ảnh → giữ `{ url, kind: "gpdk"|"sign"|"menu", thumbBlob? }` list.
- Ảnh lỗi upload → toast warning, vẫn cho submit (ảnh optional [G7]).
- Hiển thị thumbnail + nút "Chụp lại"/"Xóa" ở step review.
- Rate-limit `image-upload` 10/h/IP — wizard cần ~3-6 ảnh → **P0 đã xử lý policy `onboarding` folder**; nếu P0 chưa xong → P1 vẫn code, ghi dependency.

### 3.4 Draft autosave (FR-11)

- `localStorage["onboard-draft"]` = JSON `{ step, case, fields..., productRows, photoUrls, hkdGroupHint... }` — ghi sau mỗi thay đổi quan trọng (debounce ~500ms) + mỗi step chuyển.
- `OnInitializedAsync`: đọc draft → hỏi "Tiếp tục hồ sơ đang nhập?" (modal) → restore hoặc xóa.
- Submit success hoặc user bấm "Bắt đầu lại" → xóa key.
- Password/Turnstile token **KHÔNG** lưu draft (bảo mật — nhập lại ở step account).

### 3.5 Status page `/onboard/status` (FR-12)

- Form: signupId (từ localStorage `last-signup-id` tự điền) + SĐT → gọi `GET /api/v1/merchant-signups/status`.
- Hiển thị: Pending ("Đang chờ đội ngũ duyệt — thường 1-2 ngày làm việc") / Active (slug + link đăng nhập `{slug}.app2...` theo PF1) / Rejected (lý do generic) / NotFound.
- Rate-limit client-side: disable nút 5s sau submit.

### 3.7 OCR cloud-assist hook (P6 — code sẵn interface, không block P1)

- Trong `OnOcrResult` handler: sau khi nhận `confidence`, nếu field quan trọng (MST/tên Case A, tên hiệu Case B) < ngưỡng → hiện nút **"Nhận diện nâng cao (đám mây)"** + checkbox consent (lưu `localStorage consentCloudOcr`).
- Nút → upload ảnh (đã có URL R2 hoặc base64) → `POST /api/v1/ocr-assist` (P6 endpoint — P1 chỉ cần gọi qua `MerchantSignupHttpService.OcrAssistAsync` trả null/501-tolerant nếu endpoint chưa có → ẩn nút khi 404).
- Provider response merge vào form y như OCR local.

### 3.8 On-device OCR engine slot (P6 spike)

- `onboarding-ocr.js` thiết kế `_getWorker()` tách riêng → P6 thêm engine thứ 2 (PaddleOCR.js/ONNX web) mà không phá Tesseract path. P1 KHÔNG implement — chỉ giữ code sạch để P6 cắm vào.

### 3.9 Deep-link params + flags gate (FR-19, user chốt 2026-11-09)

- OnInitialized: `NavigationManager.Uri` parse query → `case`, `name`, `address`, `lat`, `lng`, `src` (+`shop` display).
- Trước tiên gọi `GET /api/v1/platform/onboarding-flags` (cache sessionStorage): `selfServeEnabled=false` → render trang "Đăng ký tự phục vụ tạm đóng — vui lòng liên hệ hỗ trợ" (không cho điền form); `deeplinkPrefill=false` → bỏ qua params, vào step 0 bình thường.
- Prefill: `case`→chọn sẵn card (a→Case A, b/rong→Case B/Rong — map theo wizard step names); `name`→điền bước 1; `address`→điền bước 1; `lat/lng`→điền bước GPS; `src`→giữ trong state → gửi kèm `MerchantSignupRequest.Src` khi submit.
- Sanitize: trim, độ dài giới hạn (name/address ≤200), `lat`∈[-90,90] `lng`∈[-180,180], `src` whitelist `directory|audit|qr|zalo|other|direct` (else "other").
- `MerchantSignupHttpService` += `GetOnboardingFlagsAsync()` + `SubmitAsync` gửi `Src`.

### 3.6 OCR JS port — checklist

- [ ] Copy `onboarding-ocr.js` → đổi `OCR_BASE='/js/lib/ocr'` (giữ nguyên path — copy assets cùng cấu trúc).
- [ ] Copy `onboarding-wizard.js` → giữ `vananOnboardingWizard`/`vananOnboardingOcr` global names (tránh đụng `vanan.*` KhachLink).
- [ ] Assets: `tesseract.min.js`, `worker.min.js`, `tesseract-core*.wasm.js`, `vie.traineddata.gz`, `eng.traineddata.gz` (nếu có — check thư mục gốc).
- [ ] WASM interop: `DotNetObjectReference.Create(this)` + `[JSInvokable]` — giống Server, WASM hỗ trợ đầy đủ.
- [ ] Camera: `getUserMedia` cần HTTPS — production OK; dev localhost OK.
- [ ] Map: check `window.vananMap`/`maplibregl`/`L` đã load trong KhachLink `index.html` — nếu chưa, thêm script tags (pattern ShopERP `App.razor`).

## 4. ACCEPTANCE CRITERIA

- [ ] `/onboard` public 200 không cần login; render được trên mobile viewport
- [ ] 3 case chọn đúng → step flow đúng; Rong không hỏi GPKD/MST
- [ ] Case A: chụp/upload GPKD → MST/tên/địa chỉ prefill editable + confidence badge
- [ ] HKDGroup helper gợi ý đúng mapping; đổi tay được
- [ ] Biển hiệu → tên prefill Case B/Rong; menu nhiều ảnh → bảng sản phẩm review/edit được
- [ ] GPS + lat/lng manual + map hiển thị (fallback khi GPS fail vẫn submit được nếu nhập tay)
- [ ] Account step: validate username format + password ≥8 + confirm khớp
- [ ] Ảnh upload R2 → thumbnail hiện; submit kèm photoUrls; ảnh lỗi không chặn
- [ ] Turnstile + honeypot hoạt động (reuse `vanan.*` JS)
- [ ] Submit → gọi đúng `POST /api/v1/merchant-signups` → màn kết quả + lưu `last-signup-id`
- [ ] Draft autosave: reload giữa chừng → restore; submit xong → xóa
- [ ] `/onboard/status` tra được Pending/Active/Rejected đúng cặp signupId+phone
- [ ] `/onboard?src=audit&case=b&name=X&lat=..&lng=..` → prefill đúng step, `src` đi kèm submit
- [ ] Flags endpoint trả `selfServeEnabled=false` → trang đóng, không render form
- [ ] `deeplinkPrefill=false` → params bị bỏ qua
- [ ] `dotnet build VanAn.sln` 0 errors · guard PASS · (nếu có WASM test infra → component tests)

## 5. VERIFICATION

```powershell
dotnet build VanAn.sln
# Dev: chạy KhachLink local → /onboard → đi hết flow với ảnh test
# E2E sẽ cover ở P5
```

## 6. RISKS / NOTES

- **Tesseract assets size**: `vie.traineddata.gz` ~422KB + core wasm ~2-4MB + eng — lazy-load chỉ khi vào step OCR đầu tiên (dynamic import script, không block initial load). PWA cache giúp lần sau nhanh.
- **WASM vs Server JS interop**: `[JSInvokable]` trên WASM OK; `DotNetObjectReference` phải dispose (`IAsyncDisposable` trên component) tránh leak.
- **Camera trên iOS Safari**: `capture="environment"` + getUserMedia — test kỹ; fallback file input luôn sẵn.
- **Map lib**: nếu KhachLink chưa có MapLibre/Leaflet → copy `vananMap` từ `UI.Platform/wwwroot/js` hoặc realtime.js pattern; MVP chấp nhận map chỉ-đọc (marker), click-chọn = nice-to-have.
- **Blazor WASM circuit**: autosave localStorage là bắt buộc (WASM ít bị disconnect hơn Server nhưng reload/mất điện thoại vẫn mất state).
- **Ngôn ngữ**: KHÔNG dùng thuật ngữ "HKDGroup/GPKD/BusinessType" trong UI — "nhóm thuế (sẽ được đội ngũ xác nhận)", "giấy phép kinh doanh", tooltip giải thích.
