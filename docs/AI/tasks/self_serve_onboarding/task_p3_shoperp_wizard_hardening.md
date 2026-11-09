# Task P3: Sysadmin Wizard Hardening (Track A — `/admin/onboarding-ocr`)

> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` — FR-14
> **Master plan:** §5 — độc lập, có thể song song P1/P2
> **Mode:** IMPLEMENT
> **Layer:** 5_WebApps/ShopERP
> **Prerequisite:** P0 image-upload policy `onboarding` (nếu P0 chưa xong → vẫn code, ghi dependency)

## 1. OBJECTIVE

Fix các gap/rào cản còn lại trong wizard sysadmin hiện có: ảnh provenance không upload (`PhotoUrls` luôn null), HKDGroup bắt buộc tri thức thuế, Case A HKD auto-Group2 ngầm, địa chỉ bắt buộc Case B, GPS bắt buộc, không autosave, import 409 phải bấm lại, thiếu option "chưa đăng ký".

## 2. FILES

| File | Change |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Admin/OcrOnboardingWizard.razor` | Toàn bộ mục 3 |
| `5_WebApps/ShopERP/Components/Pages/Admin/OcrOnboardingWizard.razor.css` | Style nhỏ cho thumbnail/draft banner/helper |
| `5_WebApps/ShopERP/wwwroot/js/onboarding-wizard.js` | Thêm `saveDraft`/`loadDraft`/`clearDraft` + map click-to-set (nếu feasible) |
| `5_WebApps/ShopERP/Services/OcrOnboardingApiClient.cs` | Kiểm tra DTO có `PhotoUrls`/`Unregistered` chưa — thêm nếu thiếu |
| `1_Shared` hoặc CoreHub DTO `OcrOnboardingRequest` | Thêm optional `bool Unregistered = false` + đảm bảo `PhotoUrls` truyền được (DTO đã có PhotoUrls — chỉ UI chưa gửi) |
| `3_CoreHub/Services/Onboarding/TenantOcrOnboardingService.cs` | Nếu `Unregistered=true` → force `HkdGroup=Group1` + warning "chưa đăng ký" vào result.Warnings (service-level, không đổi validation Case B) |
| `6_Tests/VanAn.ShopERP.Tests/Components/Admin/OcrOnboardingWizardTests.cs` | bUnit: photo upload collect, HKDGroup helper, unregistered flag |

## 3. CHANGES CHI TIẾT

### 3.1 Photo upload → `PhotoUrls` (fix gap lớn nhất)

- Inject `IHttpClientFactory` đã có sẵn trong wizard.
- Sau mỗi `OnOcrResult` (gpdk/sign/menu): upload file/ảnh vừa chụp → `POST api/v1/images/upload?folder=onboarding` (qua Gateway — check base address pattern `OcrOnboardingApiClient`/`MintSystemAdminTokenAsync` — upload endpoint AllowAnonymous nên không cần token).
  - **Lưu ý kỹ thuật:** camera capture tạo `File` trong JS → OCR nhận file; để upload cần đưa file lên Blazor — JS interop `DotNetStreamReference` hoặc base64 qua callback. Chọn cách đơn giản: JS upload trực tiếp từ browser (`fetch` tới Gateway URL) rồi trả URL về Blazor qua `OnOcrResult` mở rộng — **tránh stream Blazor phức tạp**. File input (không camera) cũng upload từ JS.
  - Collect `_photoUrls` list `{url, kind}`; hiển thị thumbnail nhỏ + xóa được.
- Submit: `PhotoUrls: _photoUrls.Select(u => u.Url).ToList()`.

### 3.2 HKDGroup helper + Case A HKD fix

- Thay `<select>` trần (Case B step 1) bằng helper FR-9 (copy spec từ P1 — cùng mapping gợi ý).
- Case A: `_gpdkLegalForm=="HKD"` → hiện helper gợi ý thay vì hardcode `HKDGroup.Group2` (line ~668 `SubmitAsync`). Case A Company → HkdGroup=null như cũ.

### 3.3 Địa chỉ optional + GPS manual + map click

- Case B: `_address` không còn bắt buộc trong `NextFromDocs` (chỉ Case A từ GPKD). Label "(không bắt buộc nếu kinh doanh lưu động)".
- Step 3: lat/lng inputs hiện **luôn** (kể cả `_lat==null` — cho nhập tay trực tiếp, validate range); nút GPS chỉ điền sẵn.
- Map: sau `initMap`, thêm click handler set `_lat/_lng` (JS `map.on('click')` → invoke `OnMapClick(lat,lng)` `[JSInvokable]` → update inputs) — nếu MapLibre engine, check `vananMap` hỗ trợ click; fallback bỏ qua (inputs tay đủ).

### 3.4 Option "chưa đăng ký" (Case B)

- Case select card thứ 3 trong wizard sysadmin? — **KHÔNG**: sysadmin wizard giữ 2 case (sysadmin biết rõ trường hợp). Thay vào: trong Case B form thêm checkbox "Chủ hộ CHƯA đăng ký hộ kinh doanh" → set `Unregistered=true` → submit gửi flag → service force Group1 + warning.
- `OcrOnboardingRequest` += `bool Unregistered = false` (optional, backward compat); `TenantOcrOnboardingService`: nếu true → `hkdGroup ??= Group1` + `warnings.Add("Tenant chưa đăng ký HKD — sysadmin lưu ý pháp lý")`.

### 3.5 Draft autosave + auto-retry import

- `sessionStorage["ocr-wizard-draft"]` qua `vananOnboardingWizard.saveDraft/loadDraft` — lưu fields text + productRows + photoUrls + step (KHÔNG password — wizard không có password; OCR images blob không lưu).
- Restore khi `OnAfterRenderAsync(firstRender)` + confirm.
- `ImportProductsAsync`: nếu 409 → tự retry tối đa 5 lần × 3s (poll) trước khi báo "chờ sync"; giữ nút manual retry.

### 3.6 Dọn dẹp nhỏ (nếu chạm file)

- `ResetWizard` += clear draft.
- `NextFromDocs` Case B: address optional — cập nhật validation message.

## 4. ACCEPTANCE CRITERIA

- [ ] Sau mỗi ảnh OCR → upload R2 → `PhotoUrls` submit có data (verify qua network tab / CrawlSource payload)
- [ ] Case B: địa chỉ trống vẫn Next được; Case A không đổi
- [ ] HKDGroup helper gợi ý đúng; Case A HKD không còn auto-Group2
- [ ] lat/lng nhập tay không cần GPS; map click set tọa độ (nếu implement được)
- [ ] Checkbox "chưa đăng ký" → Group1 forced + warning trong result
- [ ] Reload giữa wizard → draft restore; submit/reset → draft xóa
- [ ] Import 409 → auto-retry; thành công sau sync không cần bấm lại tay
- [ ] `OcrOnboardingRequest.Unregistered` backward compat (callers cũ không vỡ)
- [ ] bUnit mới PASS; `dotnet build VanAn.sln` 0 errors; guard PASS

## 5. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.ShopERP.Tests --filter "FullyQualifiedName~OcrOnboarding"
```

Manual dev: wizard → chụp GPKD → check network `images/upload` 200 → submit → check `CrawlSources.PayloadJson` có photoUrls.

## 6. RISKS / NOTES

- Upload ảnh từ JS trực tiếp (fetch) cần base URL Gateway — ShopERP wizard đang gọi Gateway API qua `OcrOnboardingApiClient` (check baseUri config); ảnh upload AllowAnonymous nên đơn giản.
- File input path (không camera): `processFile` nhận File — upload song song trong cùng JS function, trả `{ocrResult, photoUrl}` gộp về Blazor (đổi contract `OnOcrResult` — version cẩn thận, wizard là file duy nhất dùng).
- `Unregistered` flag: chỉ là dữ liệu — UI báo cáo/kế toán vẫn tính HKD Group1; sysadmin chịu trách nhiệm khuyến nghị đăng ký (warning rõ).
