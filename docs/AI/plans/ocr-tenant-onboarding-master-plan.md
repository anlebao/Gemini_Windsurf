# MASTER PLAN: OCR Tenant Onboarding — Scan GPKD + Biển hiệu + Menu → Tạo Tenant + Import Sản Phẩm

> **Created:** 2026-10-25
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT, 7 steps)
> **Source:** User request 2026-10-25 — kết hợp tính năng (1) Tạo tenant + onboarding (`app2.khachvip.online/admin/tenants`) và (2) OCR scan (kế thừa hạ tầng OCR biển số xe) để tạo luồng onboarding tenant bằng cách **chụp ảnh GPKD + biển hiệu + menu**, đồng thời xác định vị trí tenant theo GPS hiện tại của sysadmin. **User update 2026-10-25:** tách 2 case — (A) công ty/HKD **có GPKD** → scan GPKD + biển hiệu + menu; (B) shop nhỏ **không GPKD** → chỉ scan biển hiệu + menu.
> **Domain modification:** CÓ (1 thay đổi nhỏ, cần approval — §3.5): `Tenant.CreateUnverified` mở rộng optional `BusinessType`/`HKDGroup` để tạo Pending tenant loại HKD (hiện hardcode `Company`). Ngoài ra tái dùng `TenantSettings` + `CrawlSource`; DTO mới trong 1_Shared.
> **Architecture change:** CÓ — module mới: OCR onboarding wizard (ShopERP) + Gateway OCR-onboarding API + ShopERP product import API. OCR chạy **client-side** (zero infra mới).
> **Approval:** ⏳ Chờ user duyệt plan này (Step 4)

---

## 0. Quyết định đã chốt (2026-10-25)

| # | Quyết định | Chốt |
|---|---|---|
| D1 | Hạ tầng OCR | **Client-side** (zero infra mới) — **điều chỉnh kỹ thuật bắt buộc:** PaddleOCR hiện có (`paddle/dict.txt`) là **dict Trung-Anh — KHÔNG đọc tiếng Việt có dấu** (đã verify 0/6622 ký tự VN). Model này là plate-model đang chạy production (guard scan) — **KHÔNG được thay thế**. → MVP dùng **Tesseract.js + `vie.traineddata`** — **đã verify: `vie.traineddata.gz` (422KB, tessdata_fast) ĐÃ CÓ SẴN tại `wwwroot/js/lib/ocr/`** (guard-camera.js đã dùng `eng+vie` cho chữ "Đ" biển xe máy điện — không cần thêm asset). Cần **worker riêng cho onboarding** (guard dùng char-whitelist riêng → không dùng chung được). Phase 2 nâng cấp **PaddleOCR VN model** (load song song, không đụng plate model) |
| D2 | Import menu → sản phẩm | **CÓ trong Phase 1** — OCR menu → tạo `Product` trong ShopERP SQLite |
| D3 | Flow tạo tenant | **Pending → Verify** — reuse pattern Crawl-to-Onboard (`OnboardUnverifiedAsync` + `VerifyAsync` + auto owner credentials hiện 1 lần — pattern `ActivatedTenantCredential` của `CrawlController.PostBatchImport`) |
| D4 | Phân loại 2 case (user 2026-10-25) | **CÓ** — **Case A**: Công ty / HKD **có GPKD** → scan GPKD (MST/tên/địa chỉ/loại hình) + biển hiệu + menu. **Case B**: Shop nhỏ **không GPKD** → chỉ scan biển hiệu (tên hiệu) + menu, không MST, `BusinessType = HouseholdBusiness` + sysadmin chọn `HKDGroup` (thuế) — **wizard chọn case ở bước 1** |
| D5 | Flow Case B | ✅ **ĐÃ DUYỆT (2026-10-25): Pending → Verify (uniform với Case A)** — 1 code path, đủ audit. Kéo theo 1 domain change nhỏ §3.5 (`CreateUnverified` + optional `BusinessType`/`HKDGroup`) — **user đã duyệt domain change** |

---

## 1. Problem Statement

### Hiện trạng
1. **Onboarding tenant thủ công** (`TenantManagement.razor` / `/admin/tenants`): sysadmin phải nhập tay toàn bộ thông tin (tên DN, MST, địa chỉ, SĐT, tọa độ) — chậm, dễ sai, không tận dụng được dữ liệu sẵn có trên giấy tờ thực tế.
2. **Onboarding tự động (Crawl-to-Onboard)** chỉ lấy dữ liệu từ web crawler (doanhnghiep.vn / trangvangvietnam) — không phủ hết cửa hàng nhỏ lẻ, địa chỉ/lat-lng từ crawl không chính xác, không có ảnh giấy tờ.
3. **OCR biển số xe đã có** (Tesseract.js + PaddleOCR WASM, guard-camera.js) nhưng là **plate-tuned** (chữ không dấu, regex biển VN) — không dùng trực tiếp cho tài liệu tiếng Việt.
4. **Menu input (OCR F&B)** từng được hoạch định trong `ocr_hub/master_plan.md` Sprint 4 (EasyOCR server) nhưng bị DEFERRED (RAM VPS 2GB + chưa có demand) — demand giờ đã có.
5. **Rất nhiều shop nhỏ lẻ KHÔNG có GPKD** (chỉ có biển hiệu) — onboarding hiện tại ép nhập MST/loại hình → không onboarding được nhóm này.

### Kết quả mong muốn
Sysadmin đứng tại cửa hàng, **chọn case rồi chụp ảnh tương ứng** → hệ thống tự động:

**Case A — Công ty / HKD có GPKD** (3 ảnh: GPKD + biển hiệu + menu):
1. Trích xuất + prefill thông tin tenant (tên, MST, địa chỉ, loại hình pháp lý) từ **GPKD**
2. Lấy tên hiệu từ **biển hiệu** (cross-check với GPKD)
3. Đọc **menu** → danh sách sản phẩm (tên + giá) → import vào ShopERP SQLite

**Case B — Shop nhỏ không GPKD** (2 ảnh: biển hiệu + menu):
1. Lấy **tên hiệu từ biển hiệu** làm tên tenant (nguồn chính)
2. Sysadmin chọn `BusinessType = HouseholdBusiness` + `HKDGroup` (1/2/3 — quyết định sổ sách TT152)
3. Đọc **menu** → danh sách sản phẩm → import

**Chung cả 2 case:**
4. Ghi nhận **vị trí GPS hiện tại** của sysadmin làm tọa độ tenant (xác nhận trên bản đồ, chỉnh tay được)
5. Tạo tenant theo **Pending → Verify** (nhất quán với crawl pipeline, owner credentials auto-sinh hiện 1 lần)

---

## 2. Solution — Luồng tổng

```
┌─ ShopERP (app2) — Wizard /admin/onboarding-ocr (SystemAdmin) — PAGE RIÊNG (user 2026-10-26, tách khỏi /admin/tenants) ────────────────────┐
│                                                                             │
│  BƯỚC 0: CHỌN CASE                                                          │
│  ● Case A: Công ty / HKD có GPKD   ● Case B: Shop nhỏ KHÔNG có GPKD        │
│  └──────────────┬──────────────────────┬────────────────────────────────────┘
│                 ▼                      ▼                                    │
│  BƯỚC 1: GPKD               BƯỚC 1': (BỎ QUA GPKD)                          │
│  📷 chụp GPKD               chọn BusinessType=Hộ KD + HKDGroup (1/2/3)     │
│  → OCR → MST/tên/địa chỉ/   (quyết định sổ sách TT152)                      │
│    loại hình → prefill                                                      │
│                 └──────────────┬────────────────────────────────────────────┘
│                                ▼                                            │
│  BƯỚC 2: Biển hiệu + Menu     (cả 2 case)                                   │
│  📷 chụp biển hiệu → OCR → tên hiệu (Case B: nguồn tên CHÍNH)               │
│  📷 chụp menu (1..n ảnh) → OCR → lines (tên+giá) → review/edit list         │
│                                │                                            │
│  BƯỚC 3: Vị trí + Xác nhận     (cả 2 case)                                  │
│  📡 navigator.geolocation → Leaflet map → xác nhận/chỉnh tay                │
│  → review toàn bộ field → SUBMIT                                            │
└──────────────────────────────┬──────────────────────────────────────────────┘
                               │
         1) POST /api/v1/ocr-onboarding          (Gateway — PG source of truth)
                               │
        ┌──────────────────────▼─────────────────────────┐
        │ TenantOcrOnboardingService (3_CoreHub)          │
        │  a. OnboardUnverifiedAsync(listing, lat/lng,     │  → Pending tenant
        │     businessType, hkdGroup)  [D5/Domain §3.5]    │  (Company | HKD)
        │  b. CrawlSource (SourceSite="OCR-GPKD" |         │  → provenance ảnh
        │     "OCR-SIGN", payload = cloudinary URLs +      │
        │     extracted fields)                            │
        │  c. Dedup: MST (Case A — có sẵn trong            │
        │     OnboardUnverifiedAsync) / tên+địa chỉ        │
        │     (Case B — gợi ý, không chặn)                 │
        │  d. VerifyAsync (Pending→Active + owner + groups)│  → auto credentials
        │  e. Outbox → NATS → TenantSyncSubscriber         │  → SQLite mirror
        └──────────────────────┬─────────────────────────┘
                               │
         2) (sau NATS sync) POST /api/products/import      (ShopERP — SQLite)
                               │
        ┌──────────────────────▼─────────────────────────┐
        │ ProductImportService (ShopERP)                  │
        │  f. Tạo Product từ OcrProductLineDto[]          │
        │     (tên + giá → ProductService pattern)        │
        └────────────────────────────────────────────────┘
```

### Vai trò các lớp (Option C — không phá kiến trúc)
- **Gateway PG**: tạo Pending tenant + Verify + provenance (như crawl pipeline). KHÔNG đụng Products (Option C — Products ở ShopERP SQLite).
- **ShopERP SQLite**: import sản phẩm local (như QuickSetup/`ProductsController` hiện tại).
- **OCR**: 100% client-side (browser sysadmin) — không gọi server OCR, không thêm service Python.

---

## 3. Chi tiết kỹ thuật

### 3.1 Client-side OCR (JS — `onboarding-ocr.js` mới)

| Mục | Chi tiết |
|---|---|
| Engine | Tesseract.js v5 (WASM đã có trong `wwwroot/js/lib/ocr/`) + **`vie.traineddata.gz`** (asset mới, ~15-30MB, lazy-load khi mở wizard, cache PWA) |
| Preprocessing | grayscale + contrast + upscale 2x (ảnh chụp hay tối/mờ); crop theo hướng dẫn chụp |
| **GPKD field extraction (CHỈ Case A)** | Regex + line-heuristic trên text lines có bbox: MST `\b\d{10}(?:-\d{3})?\b`; tên DN (dòng chứa "tên doanh nghiệp/tên công ty" hoặc dòng in đậm nhất); địa chỉ (dòng chứa "địa chỉ"); loại hình (TNHH / Cổ phần / HKD / Hộ kinh doanh / DN tư nhân → map `BusinessType` + gợi ý `HKDGroup`); chủ hộ (dòng chứa "người đại diện/chủ hộ") |
| Biển hiệu (**Case A: cross-check · Case B: nguồn tên CHÍNH**) | Dòng text lớn nhất (max font height heuristic) → tên hiệu. Case A: gợi ý nếu chứa trong tên GPKD. Case B: dùng làm `Tenant.Name` (prefill, sysadmin sửa được) |
| Menu parsing (cả 2 case) | Nhóm line theo bbox y (dòng menu); tách tên/giá bằng price heuristic `(\d[\d.,]*)\s*(k|nghìn|vnđ|đ)?\s*$`; ignore header/footer/watermark (điểm ảnh lặp lại giữa các ảnh) |
| Output | Cấu trúc `OcrExtractionResult { Case, Gpdk: {Mst?, Name?, Address?, LegalForm?, Owner?} /* Case A only */, SignName, HkdGroup?, ProductLines: [{Name, Price}] , ConfidencePerField }` — `Gpdk` null với Case B; UI hiện confidence thấp → highlight cần sửa |
| Geolocation | `navigator.geolocation.getCurrentPosition` (JS interop, ShopERP Blazor Server) + accuracy radius hiển thị trên **Leaflet** (`UI.Platform/wwwroot/lib/leaflet` có sẵn); fallback nhập tọa độ tay |

### 3.2 Backend — Gateway (PG)

**DTO mới (1_Shared):**
- `OcrOnboardingCase { CompanyWithLicense = 0, SmallShopNoLicense = 1 }`
- `OcrOnboardingRequest { Case, GpdkInfo? /* null với Case B */, SignName, HkdGroup? /* Case B: bắt buộc; Case A: gợi ý từ OCR */, BusinessType, ProductLines: OcrProductLineDto[], Latitude, Longitude, ShopInstanceId? }`
- `OcrProductLineDto { Name, Price, LineNo }`
- `OcrOnboardingResult { TenantId, OwnerUsername, OwnerPassword (hiện 1 lần), Slug, ProductsCreated? }`

**Endpoint mới (`OcrOnboardingController` — `[Authorize(Policy="SystemAdmin")]`):**
- `POST /api/v1/ocr-onboarding` → `TenantOcrOnboardingService.ExecuteAsync`:
  1. Validate theo case: **Case A** — bắt buộc có MST (nếu OCR không ra MST → 422, sysadmin nhập tay trước khi submit); **Case B** — không cần MST, bắt buộc có `HkdGroup` + `SignName`
  2. Dedup: **Case A** theo MST (pattern `PostBatchImport` — skip nếu đã tồn tại, hoặc trả conflict để sysadmin quyết định); **Case B** theo tên+địa chỉ (gợi ý duplicate, không chặn — không có MST để dedup chắc chắn)
  3. `OnboardUnverifiedAsync(listing, lat/lng, businessType, hkdGroup)` → Pending tenant (Company | **HKD** — xem D5/§3.5)
  4. Lưu `CrawlSource` (`SourceSite="OCR-GPKD"` | `"OCR-SIGN"`; payload = JSON: cloudinary ảnh URLs + extracted fields + confidence) — **provenance giấy tờ**
  5. `VerifyAsync(tenantId, VerifyTenantRequest { OwnerUsername auto-sinh, OwnerPassword auto-sinh (pattern `GenerateOwnerPassword`), ApprovedByUserId = sysadmin })` → Active + owner + groups + NATS sync
  6. Trả credentials hiện 1 lần (`ActivatedTenantCredential` pattern)
- **Ảnh upload**: reuse `ImageUploadController` + `CloudinaryImageStorageService` (lưu ảnh GPKD/biển hiệu/menu để audit; policy private, xóa sau X ngày — data minimization pattern `CrawledPhone`)

### 3.3 Backend — ShopERP (SQLite) — Import sản phẩm

- `POST /api/products/import` (ShopERP local, `[Authorize]` StaffOrAbove + tenant scope): nhận `OcrProductLineDto[]` → tạo `Product` (name + price) trong SQLite tenant
- Gọi **sau khi** NATS sync tenant (poll/retry tối đa ~10s — `TenantSyncSubscriber` upsert); nếu chưa sync → trả 409 + UI retry
- Không tạo Recipe/Ingredient (MVP) — chỉ Product; owner bổ sung sau qua QuickSetup/ProductManagement

### 3.4 UI — Wizard `/admin/onboarding-ocr` (ShopERP) — **PAGE RIÊNG** (user 2026-10-26: tách khỏi /admin/tenants)

- Page `Components/Pages/Admin/OcrOnboardingWizard.razor` — `@page "/admin/onboarding-ocr"` + AdminLayout + SystemAdmin — **UI Platform components** (Gate 4)
- **Bước 0 — Chọn case** (card radio, ảnh minh họa):
  - **Case A — Công ty / HKD có GPKD**: cần ảnh GPKD + biển hiệu + menu; bắt buộc có MST trước khi submit
  - **Case B — Shop nhỏ không GPKD**: cần ảnh biển hiệu + menu; form thêm bộ chọn `HKDGroup` (Group 1/2/3 kèm mô tả thuế TT152) — ảnh hưởng sổ sách kế toán, hiện rõ warning
- **Bước 1 (Case A) — GPKD**: chụp/upload → OCR → prefill MST/tên/địa chỉ/loại hình; mỗi field **editable** (human-in-the-loop bắt buộc — dấu đỏ/stamp nên OCR không thể 100%); MST không ra được → phải nhập tay (validation 10/13 số)
- **Bước 1' (Case B) — Thông tin cơ bản**: nhập tên (sẽ prefill từ biển hiệu ở bước 2), chọn HKDGroup
- **Bước 2 (cả 2 case) — Biển hiệu + Menu**: camera capture reuse pattern `guard-camera.js` (ShopERP camera đã chạy production) hoặc `<input type="file" accept="image/*" capture="environment">`; biển hiệu → prefill tên (Case B: nguồn chính); menu 1..n ảnh → list sản phẩm review/edit (thêm/xóa/dòng giá sai)
- **Bước 3 — Vị trí + Xác nhận**: Leaflet map (reuse `UI.Platform/wwwroot/lib/leaflet`) + accuracy badge + nút "Lấy lại vị trí"; review toàn bộ field + ảnh đính kèm → SUBMIT
- Kết quả: hiện credentials 1 lần (modal copy), nút "Import sản phẩm" (chạy product import sau sync) + điều hướng /admin/tenants
- NavMenu + Sitemap + route guard SystemAdmin

### 3.5 Domain (CÓ 1 thay đổi nhỏ — ✅ user đã duyệt 2026-10-25, D5)

**Modeling defect cần sửa:** `Tenant.CreateUnverified` hiện **hardcode `BusinessType = Company`** (chỉ phù hợp crawl listing) → **không thể tạo Pending tenant loại HKD** (Case B) bằng flow Pending → Verify.

| Mục | Chi tiết |
|---|---|
| Thay đổi | `Tenant.CreateUnverified(TenantId id, string name, TenantSettings? settings, string pendingSlug, BusinessType businessType = BusinessType.Company, HKDGroup? hkdGroup = null)` — default giữ nguyên hành vi cũ (Company), **backward compat 100%**; khi `hkdGroup != null` → set `HKDGroup` + `Type = TenantType.HKD` (đúng semantics `CreateHouseholdBusiness`) |
| Tại sao cần | Case B (shop không GPKD) phải là HKD để: (1) sổ sách kế toán TT152 đúng (`HKDGroup` quyết định S1a/S2x/S3a), (2) feature-flag routing đúng (`Type=HKD`), (3) UI báo cáo HKD đúng |
| Không thay đổi gì khác | `TenantSettings` đủ (Name, TaxCode nullable — Case B để null, Address, **Latitude/Longitude**); `CrawlSource` đủ provenance; AccountingEntry immutability không đụng; Single-Identity không đụng |
| Tests đi kèm | `CreateUnverified` HKD: status Pending + BusinessType=HouseholdBusiness + HKDGroup + Type=HKD; backward compat: gọi cũ → Company như trước; Verify HKD tenant vẫn OK |
| Phương án thay thế (nếu KHÔNG duyệt domain change) | Case B tạo **Active ngay** qua `CreateHouseholdBusiness` (bỏ Pending) — 2 code path cho 2 case, kém nhất quán, audit ít hơn — **không khuyến nghị** |

**Chỉ thêm DTO record trong 1_Shared** (thuần data, không entity) — phần còn lại 0 thay đổi domain.

---

## 4. Scope

### In scope (Phase 1)
- Wizard ShopERP `/admin/onboarding-ocr` — **Bước 0 chọn case** (A: có GPKD / B: không GPKD) + 3 bước
- OCR client-side tiếng Việt: Tesseract.js + `vie.traineddata` + `onboarding-ocr.js` (GPKD field extraction **Case A** + biển hiệu + menu lines)
- Gateway `OcrOnboardingController` + `TenantOcrOnboardingService` (Pending → Verify, credentials 1 lần, provenance, dedup MST **Case A** / gợi ý tên+địa chỉ **Case B**)
- **Domain: `CreateUnverified` mở rộng optional BusinessType/HKDGroup** (D5 — chỉ khi được duyệt)
- ShopERP product import API + UI nút import sau sync
- Geolocation sysadmin + Leaflet confirm
- Tests: Core.Tests (service + domain change) + ShopERP.Tests (product import) + E2E (2 case wizard happy path)

### Out of scope (Phase 2+)
- PaddleOCR VN model song song (nâng độ chính xác — load thêm, không đụng plate model)
- EasyOCR server-side (S4 un-defer — khi cần accuracy 95%+ hoặc OCR lưu lượng cao)
- Google Vision API fallback (cost-based)
- Auto-seed Recipe/Ingredient từ menu
- OCR invoice/bill

---

## 5. Sprint Dependency

```
PR 1: feature/ocr-onboarding
  S0 (Domain: CreateUnverified + BusinessType/HKDGroup + tests — CHỈ khi D5 duyệt)
      │
      ▼
  S1 (OCR client core: vie.traineddata asset + onboarding-ocr.js + field extraction + case branch)
      │
      ├── S2 (Gateway: DTO + OcrOnboardingController + TenantOcrOnboardingService + Core.Tests)
      │       │
      │       └── S3 (ShopERP: POST /api/products/import + ShopERP.Tests)
      │               │
      │               └── S4 (Wizard UI: bước 0 chọn case + 3 bước + GPS + Leaflet + credentials modal)
      │                       │
      │                       └── S5 (E2E spec 2 case + deploy + RV L1-L4)
```

- **S0 trước** (domain change — **ĐÃ duyệt D5**): `CreateUnverified` + BusinessType/HKDGroup + tests backward compat
- **S1 trước** vì mọi thứ phụ thuộc dữ liệu OCR; **S2/S3 độc lập nhau** (2 lớp khác nhau — Gateway vs ShopERP)
- Mỗi sprint: guard-check.ps1 + dotnet build VanAn.sln + Core.Tests PASS trước khi merge

---

## 6. Success Criteria

1. ✅ **Case A**: Sysadmin chụp GPKD → MST/tên/địa chỉ/loại hình tự điền (editable) — ảnh GPKD rõ ràng, không dấu đỏ đè chữ; thiếu MST → chặn submit (422) cho nhập tay
2. ✅ **Case B**: Chọn case "không GPKD" → bỏ bước GPKD, chỉ biển hiệu + menu; tên tenant = tên hiệu (editable); `HKDGroup` bắt buộc chọn
3. ✅ Chụp biển hiệu → tên hiệu prefill (cả 2 case)
4. ✅ Chụp menu → danh sách sản phẩm (tên+giá) review được → import thành công vào ShopERP
5. ✅ GPS sysadmin → tọa độ tenant hiển thị trên bản đồ → lưu vào `TenantSettings.Latitude/Longitude`
6. ✅ Tenant tạo theo Pending → Verify (cả 2 case), owner credentials hiện 1 lần, NATS sync SQLite OK
7. ✅ (Nếu D5 duyệt) `CreateUnverified` hỗ trợ HKD: Case B tenant có `BusinessType=HouseholdBusiness` + `HKDGroup` + `Type=HKD` — sổ sách TT152 hiển thị đúng
8. ✅ Guard-check + build + Core.Tests PASS; E2E PASS (2 case); CD deploy + RV PASS

---

## 7. RV Plan

| Layer | Test | Expected |
|---|---|---|
| L1 | Markers production: `TenantOcrOnboardingService` + `OcrOnboardingController` trong CoreHub.dll/Gateway.dll | Present |
| L2 | Health 200 · route `/admin/onboarding-ocr` 302→Login · `POST /api/v1/ocr-onboarding` 401 khi no-auth | PASS |
| L3 | Playwright production (sysadmin): wizard → mock OCR input → tạo tenant Pending→Active → product import → GPS prefill | 5/5 |
| L4 | Manual browser: chụp ảnh thật trên mobile, kiểm tra độ chính xác OCR tiếng Việt, chỉnh tay, credentials handover | User |

---

## 8. Risk Assessment

| Risk | Impact | Mitigation |
|---|---|---|
| Tesseract VN accuracy thấp (~75-85%) | Prefill sai | Bắt buộc review/edit tay; highlight field confidence thấp; Phase 2: PaddleOCR VN |
| `vie.traineddata` ~15-30MB | Tải chậm mobile | Lazy-load khi mở wizard + PWA cache + loading progress |
| Dấu đỏ/stamp trên GPKD phá OCR | MST/tên sai | Hướng dẫn chụp góc thẳng + preprocessing contrast; MST tự validate (10/13 số); Case A bắt buộc MST trước submit |
| **Case B không có MST → dedup yếu** | Tạo tenant trùng (cùng tên shop khác chi nhánh) | Dedup gợi ý theo tên+địa chỉ chuẩn hóa (không chặn); sysadmin xác nhận duplicate prompt |
| **Case B chọn sai HKDGroup** | Sổ sách kế toán TT152 sai nhóm thuế | UI hiện mô tả thuế từng Group (1: không GTGT/TNCN · 2: nộp GTGT+TNCN · 3: thuế khác); cho sửa lại sau qua admin |
| NATS sync chưa kịp khi import sản phẩm | 409 conflict | Retry/poll ≤10s; UI nút "Thử lại" |
| GPS trong nhà kém chính xác | Tọa độ sai chỗ | Hiển thị accuracy radius + chỉnh tay trên Leaflet |
| Ảnh GPKD nhạy cảm (MST, tên chủ) | Rò rỉ dữ liệu | Cloudinary private + audit qua CrawlSource + xóa sau onboarding (pattern `CrawledPhone` — Luật 91/2025) |
| PaddleOCR plate model bị ảnh hưởng | Guard scan hỏng | KHÔNG thay dict/model chung — VN model Phase 2 load riêng |

---

## 9. Việc điều tra còn lại trước IMPLEMENT (Step 2 — impact analysis)

- [ ] Đọc `ImageUploadController` + `CloudinaryImageStorageService` — contract upload + policy private
- [ ] Đọc ShopERP `ProductsController` — pattern tạo Product local để bắt chước import
- [ ] Đọc `TenantApiClient` (ShopERP) — pattern gọi Gateway API
- [ ] Xác nhận `vie.traineddata` source (tessdata_fast — kích thước nhỏ hơn)
- [ ] Verify `TenantSyncSubscriber` timing — retry window cho product import
- [ ] Xác nhận shop instance default cho tenant mới (ShopInstanceId từ đâu — UI chọn hay default)
- [ ] Verify `OnboardUnverifiedAsync`/`VerifyAsync` với HKD tenant: `CreateUnverified` + HKDGroup → Verify flow + NATS sync không vỡ (S0 tests) — **phụ thuộc D5 duyệt**
- [ ] Map loại hình pháp lý OCR → `BusinessType`/`HKDGroup` (hộ KD → Group mặc định nào? gợi ý Group2 — phổ biến nhất; sysadmin sửa được)