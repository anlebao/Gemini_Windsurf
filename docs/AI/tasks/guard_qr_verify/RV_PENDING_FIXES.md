# RV Guard Fullflow — Pending Fixes Plan

**Created:** 2026-08-17
**Updated:** 2026-08-18 — Verify pass: phát hiện Bug 2 fix CHƯA ĐỦ, bổ sung OCR accuracy requirements, thêm file chưa đề cập
**Status:** 2 bugs đã fix+deploy (1 fix CHƯA ĐỦ — xem Bug 2 mới), 1 bug đang điều tra, 1 bug cần test ảnh thật
**Commits:** `c56a91dc` (CustomerPhotoKey optional + Tesseract langPath), `b9594509` (ClaimAsync fallback — INCOMPLETE)

---

## TÓM TẮT 3 BUG THẬT (xác nhận bằng runtime debug, không guess)

### Bug 1: OCR không nhận diện biển số thật (từ điện thoại)
- **Trạng thái:** Đã fix 1 phần (Tesseract langPath 404 → tessdata.projectnaptha.com)
- **Synthetic test:** PASS (`51F-12345` → `51F-12345`)
- **Còn nghi vấn:** Chưa test với ảnh thật (glare, góc nghiêng, mờ)
- **Plan test thêm:** Sau deploy, chụp ảnh biển số thật từ điện thoại → test OCR
- **⚠️ Verify mới (2026-08-18):** OCR hiện tại có NHỮNG POINT yếu cấu trúc — xem mục "OCR ACCURACY — REQUIREMENTS MỚI" bên dưới

### Bug 2: QR quét bằng KhachLink app không được ← FIX CHƯA ĐỦ (verify 2026-08-18)
- **Root cause:** QR payload format mismatch
  - IssueAsync tạo: `{"t":"<token>","tn":"<tenantId>"}` (KHÔNG có `sid` — xem GuardService.cs line 74)
  - PrintTicket.razor tạo: `{"sc":"<shortCode>","sid":"<sessionId>"}`
  - VerifyAsync (guard) có fallback parse `{sc,sid}` → OK
  - ClaimAsync (KhachLink app) KHÔNG có fallback → "QR session not found"
- **Fix (commit b9594509):** ClaimAsync SERVICE thêm `TryLookupByAlternativePayloadAsync` fallback
- **⚠️ BUG MỚI PHÁT HIỆN (verify code 2026-08-18):** Fix b9594509 **KHÔNG HOẠT ĐỘNG** cho `{sc,sid}` payload!
  - `GuardController.Claim` (line 326-334) gọi `ExtractTenantIdFromPayload(request.QrPayload)` TRƯỚC khi gọi service
  - `ExtractTenantIdFromPayload` (line 409-425) **chỉ** parse field `"tn"` → `{sc,sid}` payload không có `"tn"` → trả `Guid.Empty`
  - Controller check `if (tenantId == Guid.Empty) return BadRequest(...)` (line 333-334) → **reject trước khi đến service fallback**
  - → Service `TryLookupByAlternativePayloadAsync` KHÔNG BAO GIỜ được gọi cho `{sc,sid}` payload
  - **Fix cần thêm:** Controller `Claim` phải extract tenantId từ `{sc,sid}` payload (lookup session by sessionId trước, lấy session.TenantId) HOẶC bỏ require tenantId khi có `sid` trong payload
- **Plan verify:** Test API `POST /api/guard/claim` với payload `{sc,sid}` + customer token → expect 200 (trước fix controller: 400 "Could not determine tenant")

### Bug 3: In vé không được ← CHƯA FIX, đang điều tra
- **Root cause (xác nhận bằng runtime debug):**
  - PrintTicket page hiển thị "Không tìm thấy phiên QR." → session == null
  - `GuardApi.GetSessionAsync(SessionId)` throw exception
  - Session vừa tạo (issue thành công) nhưng GetSession fail
- **Debug output:**
  ```
  Print page URL: https://app2.khachvip.online/guard/print/3486aa8b-df1f-4fad-a4ae-885c489ba4bd
  HTML: <p>Không tìm thấy phiên QR.</p>
  Canvas: {"found":false}
  ```
- **⚠️ Verify code 2026-08-18 — root cause hypothesis mạnh nhất (chưa test runtime):**
  - `PrintTicket.razor` mở bằng `NavigationManager.NavigateTo(..., forceLoad: true)` (Scan.razor.cs line 291) → **tab mới, Blazor circuit fresh**
  - `PrintTicket.OnInitializedAsync` (line 89-109) gọi `GuardApi.GetSessionAsync(SessionId)`
  - `GuardApiClient.MintUserTokenAsync` (line 50-79) đọc `_authStateProvider.GetAuthenticationStateAsync()`
  - Trên fresh circuit, auth state có thể **chưa sẵn sàng** (cookie/ClaimsPrincipal chưa hydrate) → `tenant_id` claim = null → `tenantId = Guid.Empty`
  - JWT minted có `tenant_id = Guid.Empty` → `GuardController.GetSession` (line 294) lấy `tenantId = Guid.Empty`
  - `GuardService.GetSessionAsync` (line 254-257) gọi `_sessionRepo.GetByIdAsync(sessionId, Guid.Empty)` → filter `WHERE TenantId = '00000000-...'` → **no match** → `KeyNotFoundException` → 404
  - `GuardApiClient.SendAndReadAsync` (line 96) gọi `EnsureSuccessStatusCode()` → throw `HttpRequestException` → catch ở PrintTicket → `session = null`
  - **Tại sao IssueAsync lại work?** Vì Issue chạy trên circuit CŨ (đã hydrate auth state), PrintTicket chạy trên circuit MỚI (forceLoad)
  - **Fix đề xuất (chờ approval):**
    1. PrintTicket retry `GetSessionAsync` sau delay 1-2s (đợi auth state hydrate), HOẶC
    2. `GuardApiClient.MintUserTokenAsync` throw rõ lỗi khi `tenantId == Guid.Empty` thay vì mint token rỗng, HOẶC
    3. `GetSession` endpoint bỏ filter tenantId khi guard role (chỉ filter cho customer endpoints), HOẶC
    4. Truyền tenantId qua URL/query param thay vì依赖 JWT trên fresh circuit
- **Khả năng khác (vẫn cần test):**
  1. TenantId mismatch thật — guard token có tenantId khác session.tenantId
  2. API 500 — exception trong GetSessionAsync (R2 presigned URL fail?)

---

## DESIGN DEFECT PHÁT HIỆN (verify 2026-08-18)

### DD-1: `SessionDetailResultDto` thiếu field `QrPayload` — root cause của Bug 2
- `GuardService.IssueAsync` (line 97) trả `IssueResult(session.Id, payload, shortCode)` — `payload` = `{"t":"...","tn":"..."}`
- `Scan.razor.cs` (line 234) generate QR image từ `result.QrPayload` → QR trên màn hình Issue = format `{t,tn}`
- `PrintTicket.razor` (line 119) KHÔNG có `QrPayload` (chỉ có `SessionId` từ URL) → phải tự construct `{"sc":"...","sid":"..."}` → QR trên vé = format `{sc,sid}`
- **2 QR format khác nhau** cho cùng 1 session → ClaimAsync (KhachLink) chỉ handle `{t,tn}` (hash lookup) → fail với `{sc,sid}`
- **Fix đúng (chờ approval):** Thêm `QrPayload` vào `SessionDetailResult` + `SessionDetailResultDto` → PrintTicket render đúng payload gốc → không cần fallback `{sc,sid}` → Bug 2 tự biến mất
- **Lưu ý:** `QrPayload` chứa `qrToken` (secret) — in lên vé là OK (vé chỉ guard dùng), nhưng cân nhắc security nếu QR này khách cũng quét được

### DD-2: `GuardApiClient.PublicGatewayBaseUrl` fallback sai (verify code)
- `GuardApiClient.cs` line 115-117: `PublicGatewayBaseUrl` dùng `?? string.Empty` — nhưng `TrimEnd('/')` trên null sẽ NRE, không return null được
- Thực tế: `(_configuration["Gateway:PublicBaseUrl"] ?? string.Empty).TrimEnd('/')` — nếu config missing → `string.Empty.TrimEnd('/')` = `""` → fallback `?? string.Empty` không kích hoạt (vì "" không null)
- **Impact:** Nếu `Gateway:PublicBaseUrl` chưa config → JS fetch tới `"" + "/api/guard/upload-photo"` = relative URL → fail trên page `/guard/scan`
- **Fix:** Đổi logic: nếu empty sau trim → return `GatewayBaseUrl` (internal) làm fallback cuối cùng

---

## FILES CHƯA ĐỀ CẬP (verify 2026-08-18 — cần thêm vào điều tra)

| File | Lý do |
|---|---|
| `5_WebApps/ShopERP/Components/Pages/Guard/Scan.razor.cs` | Code-behind Issue flow — `IssueQrAsync` (line 125-269), `PrintTicketAsync` (line 286-294) dùng `forceLoad: true` → Bug 3 root cause. `preloadOcrWorker` fire-and-forget (line 83) — chưa handle load fail. |
| `3_CoreHub/Repositories/IVehicleSessionRepository.cs` | `GetByIdAsync(sessionId, tenantId)` — query filter. Bug 3: nếu tenantId=Empty → no match. Cần verify có overload `GetByIdAsync(sessionId)` không filter tenant không. |
| `3_CoreHub/Services/IGuardService.cs` | `SessionDetailResult` record — thiếu `QrPayload` field (DD-1). Cần thêm field này để PrintTicket render đúng QR. |
| `5_WebApps/ShopERP/wwwroot/js/qr-scanner.js` | QR scanner cho Verify tab — chưa audit xem handle `{sc,sid}` payload không (VerifyAsync có fallback nhưng scanner có decode đúng không) |
| `5_WebApps/KhachLink/` (QR claim flow) | KhachLink `/qr/claim` gọi `POST /api/guard/claim` — bị Bug 2 controller block. Cần verify KhachLink client gửi payload format gì. |

---

## OCR ACCURACY — REQUIREMENTS MỚI (verify 2026-08-18)

**Mục tiêu:** Tăng accuracy OCR biển số từ ~70-85% (hiện tại) lên ≥90% với ảnh thật (điện thoại, glare, góc nghiêng, mờ).

### Vấn đề cấu trúc hiện tại (guard-camera.js `_preprocessForOcr` line 664-686)
1. **KHÔNG có plate ROI detection** — OCR chạy trên TOÀN BỘ ảnh (chỉ upscale 1.5x + grayscale). Background (xe, đường, người) gây noise → Tesseract nhảm. **Đây là #1 accuracy killer.**
2. **Chỉ grayscale, không threshold** — không có Otsu/adaptive binarization. Glare + low contrast → Tesseract fail.
3. **Không deskew** — biển số nghiêng (góc chụp) → Tesseract nhận sai ký tự.
4. **Camera resolution thấp** — `width: { ideal: 1280 }` (line 89) → biển số chỉ ~200-300px trong frame → quá nhỏ cho OCR.
5. **Double JPEG compression** — capture quality 0.85 (line 115) → compress quality 0.7 (line 219) → artifact积累 → OCR giảm accuracy.
6. **PSM chỉ 7 + 6** (line 621) — thiếu PSM 8 (single word), PSM 13 (raw line), PSM 4 (single column) cho edge cases.
7. **whitelist có "Đ"** (line 598) nhưng Tesseract `eng` model không train cho Vietnamese Đ → có thể gây confusion. Nên dùng `eng+vie` hoặc bỏ Đ + post-process map.
8. **Không dùng confidence score** — Tesseract trả `data.confidence` + `data.words[].confidence` nhưng code chỉ lấy `data.text`, không filter low-confidence.
9. **Không validate format biển VN** — không regex check `\d{2}[A-ZĐ]-\d{4,5}` (xe máy) hoặc `\d{2}[A-Z]-\d{3}\.\d{2}` (xe hơi) → garbage string vẫn được fill.
10. **Không multi-frame** — chỉ chụp 1 frame. Nên chụp 3-5 frame liên tiếp, pick frame sharp nhất (hoặc OCR tất cả, pick best score).

### Requirements mới (chờ approval — ưu tiên theo impact)

#### R-OCR-1: Plate ROI detection (HIGH impact)
- Dùng edge detection (Canny/Sobel) + contour tìm vùng biển số trước khi OCR
- Hoặc dùng lightweight model (YOLO-tiny plate detector) nếu client-side feasible
- Fallback: cho guard drag-select vùng biển số trên preview nếu auto-detect fail
- **MVP fallback:** Thêm nút "Cắt biển số" — guard crop thủ công vùng biển → OCR chỉ chạy trên crop

#### R-OCR-2: Adaptive thresholding (HIGH impact, LOW effort)
- Thay grayscale-only bằng Otsu threshold HOẶC adaptive Gaussian threshold
- Canvas: `ctx.filter = 'contrast(1.4) brightness(1.1)'` trước grayscale
- Hoặc OpenCV.js (heavy) — chỉ nếu R-OCR-1 + R-OCR-2 native không đủ

#### R-OCR-3: Tăng camera resolution (MEDIUM impact, LOW effort)
- `width: { ideal: 1920 }`, `height: { ideal: 1080 }` (line 89-90)
- Cân nhắc `facingMode: { exact: 'environment' }` để ép camera sau (telephoto tốt hơn cho biển số xa)

#### R-OCR-4: Bỏ double compression (MEDIUM impact, LOW effort)
- Capture ở quality 0.95 (hoặc PNG lossless) → compress 1 lần duy nhất cho upload
- OCR chạy trên bản pre-compression (chưa nén) để giữ detail

#### R-OCR-5: Multi-PSM + confidence filter (MEDIUM impact, LOW effort)
- PSM list: `['7', '6', '8', '13', '4']` (line 621)
- Lọc kết quả theo `data.confidence >= 60` — nếu tất cả PSM < 60 → trả "" → guard nhập tay
- Log confidence score ra console để debug

#### R-OCR-6: VN plate format validation (MEDIUM impact, LOW effort)
- Regex validate sau `_normalizePlate`:
  - Xe máy: `^\d{2}[A-ZĐ]{1,2}-\d{4,5}$` (VD: `51F-12345`, `59P1-67890`)
  - Xe hơi: `^\d{2}[A-Z]{1,2}-\d{3}\.\d{2}$` (VD: `51F-123.45`)
  - Điện: `^\d{2}[A-ZĐ]{1,2}-\d{4,5}$` (VD: `51ĐAB-123.45`)
- Nếu không match → hint "Biển số không đúng format VN — kiểm tra lại"
- Không block submit (guard có thể override) nhưng warn

#### R-OCR-7: Multi-frame capture (LOW impact, MEDIUM effort)
- Khi guard bấm "Chụp" → capture 3 frame cách nhau 300ms → OCR từng frame → pick `bestScore`
- Hoặc dùng `imageCapture.takePhoto()` API (Chrome) cho chất lượng cao hơn grabFrame

#### R-OCR-8: Focus + exposure lock (LOW impact, LOW effort)
- `getUserMedia` constraints thêm `advanced: [{ focusMode: 'continuous' }, { exposureMode: 'continuous' }]`
- Hoặc tap-to-focus trên preview

#### R-OCR-9: Server-side OCR fallback (DEFERRED — cần approval)
- Nếu client Tesseract confidence < 40 → upload ảnh → Gateway gọi Google Vision API / AWS Rekognition
- Cost: ~$1.50/1000 images (Vision) — cân nhắc budget
- Chỉ enable cho tenant có `Guard:ServerOcrEnabled=true`

#### R-OCR-10: OCR telemetry logging (LOW effort, cần cho tuning)
- Khi OCR fail hoặc confidence thấp → log `{ plate, confidence, psm, timestamp }` lên Gateway
- Aggregate để identify pattern fail (góc? glare? biển điện?) → tune preprocessing
- Endpoint: `POST /api/guard/ocr-log` (anonymous, fire-and-forget)

---

## CODING PLAN CHI TIẾT (làm theo thứ tự)

### Phase 1: Fix Bug 2 controller + hoàn tất deploy (BUG MỚI — verify 2026-08-18)
- **Trạng thái:** commit `b9594509` (service fallback) ĐÃ deploy nhưng KHÔNG hiệu quả cho `{sc,sid}` payload
- **Fix cần thêm (chờ approval):**
  - `GuardController.Claim` (line 326-334): khi `ExtractTenantIdFromPayload` trả `Guid.Empty`, thử parse `{sc,sid}` → lookup session by `sid` → lấy `session.TenantId` → dùng cho `ClaimAsync`
  - HOẶC bỏ require `tenantId != Empty` khi payload có `sid` (service fallback sẽ lookup)
  - HOẶC fix DD-1 (thêm `QrPayload` vào `SessionDetailResult`) → PrintTicket render đúng `{t,tn}` → không cần `{sc,sid}` → Bug 2 tự mất
- **Sau deploy:** test API claim với `{sc,sid}` payload → expect 200 (trước fix controller: 400 "Could not determine tenant")

### Phase 2: Điều tra + fix Bug 3 (Print ticket session=null)
- **Bước 2.1:** Viết script test `GET /api/guard/sessions/{sessionId}` với guard JWT
  ```powershell
  # Lấy guard JWT
  $loginBody = '{"email":"baove@vanan.vn","password":"2026@vanan"}'
  $loginResp = Invoke-RestMethod -Uri 'https://api2.khachvip.online/api/auth/login' -Method Post -Body $loginBody -ContentType 'application/json'
  $jwt = $loginResp.token
  # Test GetSession
  $sessionId = '3486aa8b-df1f-4fad-a4ae-885c489ba4bd'  # session vừa tạo
  $resp = Invoke-WebRequest -Uri "https://api2.khachvip.online/api/guard/sessions/$sessionId" -Headers @{Authorization="Bearer $jwt"}
  Write-Output $resp.StatusCode
  Write-Output $resp.Content
  ```
- **Bước 2.2:** Query DB kiểm tra session.TenantId vs JWT tenant_id
  ```bash
  docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c \
    'SELECT "Id", "TenantId", "PlateNumber", "Status" FROM "VehicleSessions" WHERE "Id" = '\''3486aa8b-df1f-4fad-a4ae-885c489ba4bd'\'';'
  ```
- **Bước 2.3:** Decode JWT để xem tenant_id claim
  ```powershell
  $jwt = '...'  # token từ bước 2.1
  $payload = $jwt.Split('.')[1]
  $decoded = [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload + '=='))
  Write-Output $decoded
  ```
- **Bước 2.4:** Test hypothesis "fresh circuit auth state chưa hydrate"
  - Mở PrintTicket URL trực tiếp (không qua Scan → forceLoad) → xem có fail không
  - Delay 2s trước khi gọi `GetSessionAsync` trong PrintTicket → xem có work không
  - Log `tenantId` từ `MintUserTokenAsync` ra console để confirm Empty hay không
- **Bước 2.5:** Fix root cause (chọn 1 trong các option ở Bug 3)
- **Bước 2.6:** Build + commit + push + đợi deploy

### Phase 3: RV thật end-to-end
- **Bước 3.1:** Guard tạo QR (nhập tay) → in vé → QR render trên vé
- **Bước 3.2:** Khách quét QR từ vé bằng KhachLink app → claim thành công
- **Bước 3.3:** Guard verify bằng QR scan → match → checkout
- **Bước 3.4:** OCR với ảnh biển số thật (chụp điện thoại)

### Phase 4: OCR tuning (nếu Phase 3.4 fail) — áp theo requirements R-OCR-1 đến R-OCR-10
- **Ưu tiên implement (chờ approval):**
  1. R-OCR-2 (adaptive threshold) — LOW effort, HIGH impact
  2. R-OCR-3 (camera 1920x1080) — LOW effort, MEDIUM impact
  3. R-OCR-4 (bỏ double compression) — LOW effort, MEDIUM impact
  4. R-OCR-5 (multi-PSM + confidence filter) — LOW effort, MEDIUM impact
  5. R-OCR-6 (VN plate regex validation) — LOW effort, MEDIUM impact
  6. R-OCR-10 (OCR telemetry logging) — LOW effort, cần cho tuning
- **Cần approval riêng (heavy/deferred):**
  - R-OCR-1 (plate ROI detection — cần chọn approach: native canvas vs OpenCV.js vs YOLO-tiny)
  - R-OCR-7 (multi-frame capture)
  - R-OCR-9 (server-side OCR — Google Vision API, có cost)
- **Test với 5+ ảnh biển số thật** (góc nghiêng, glare, tối, biển điện, xe máy + xe hơi)

---

## FILES ĐÃ SỬA (commit c56a91dc + b9594509)

| File | Thay đổi | Bug |
|---|---|---|
| `2_Gateway/Controllers/GuardController.cs` | Bỏ validation `CustomerPhotoKey required` | #130 |
| `1_Shared/Domain.cs` | `VehicleSession` constructor accept null customerPhotoKey | #130 |
| `3_CoreHub/Services/GuardService.cs` | 3 chỗ `GetPresignedDownloadUrl` trả null nếu empty + ClaimAsync fallback | #130 + Bug 2 |
| `3_CoreHub/Services/IGuardService.cs` | DTOs `CustomerPhotoKey/Url` → `string?` | #130 |
| `5_WebApps/ShopERP/wwwroot/js/guard-camera.js` | Tesseract `langPath` → `tessdata.projectnaptha.com` | Bug 1 |
| `3_CoreHub/Services/KhachLinkInstanceService.cs` | Skip Modified state on InMemory provider | test fix |

---

## FILES CẦN ĐIỀU TRA TIẾP (Phase 2 + verify mới 2026-08-18)

| File | Lý do |
|---|---|
| `5_WebApps/ShopERP/Services/GuardApiClient.cs` | `GetSessionAsync` line 164-168 — gọi API fail. `MintUserTokenAsync` line 50-79 — tenantId=Empty trên fresh circuit? `PublicGatewayBaseUrl` line 115-117 — DD-2 fallback sai. |
| `5_WebApps/ShopERP/Components/Pages/Guard/PrintTicket.razor` | OnInitializedAsync line 89-109 — catch exception, session=null. QR payload construct line 119 — dùng `{sc,sid}` thay vì payload gốc (DD-1). |
| `5_WebApps/ShopERP/Components/Pages/Guard/Scan.razor.cs` | **MỚI** — `PrintTicketAsync` line 286-294 dùng `forceLoad: true` → Bug 3. `IssueQrAsync` line 125-269 — `qrImageBase64` từ `result.QrPayload` (format `{t,tn}`). `preloadOcrWorker` line 83 fire-and-forget — chưa handle fail. |
| `2_Gateway/Controllers/GuardController.cs` | `GetSession` line 289-306 — tenantId từ JWT. `Claim` line 311-354 — **BUG MỚI**: `ExtractTenantIdFromPayload` line 409-425 chỉ parse `"tn"` → block `{sc,sid}` payload trước service fallback. |
| `3_CoreHub/Services/GuardService.cs` | `GetSessionAsync` line 254-260 — query by sessionId + tenantId. `IssueAsync` line 74 — payload format `{t,tn}` (KHÔNG có `sid`). `TryLookupByAlternativePayloadAsync` line 290-333 — fallback KHÔNG ĐẠT cho `{sc,sid}` do controller block trước. |
| `3_CoreHub/Services/IGuardService.cs` | **MỚI** — `SessionDetailResult` record thiếu `QrPayload` field (DD-1). |
| `3_CoreHub/Repositories/IVehicleSessionRepository.cs` | **MỚI** — `GetByIdAsync(sessionId, tenantId)` — có overload không filter tenantId không? Cần cho Bug 3 fix option 3. |
| `5_WebApps/ShopERP/wwwroot/js/qr-scanner.js` | **MỚI** — QR scanner Verify tab — handle `{sc,sid}` payload không? |
| `5_WebApps/KhachLink/` (QR claim flow) | **MỚI** — KhachLink `/qr/claim` gửi payload format gì? Bị Bug 2 controller block. |

---

## SCRIPTS RV ĐÃ CÓ

| Script | Mục đích |
|---|---|
| `rv-guard-fullflow.js` | Full flow: login → OCR → issue QR → print → verify → today |
| `rv-debug-print.js` | Debug print ticket: dump HTML + test claim API |
| `rv-136.js` | Test KhachLink instance #136 |
| `rv-136-admin.js` | Test admin KhachLink instance #136 |

---

## LESSONS LEARNED (ghi nhớ cho session sau)

1. **RV phải test thật, không synthetic:** Synthetic canvas OCR pass ≠ ảnh thật pass. QR render pass ≠ KhachLink app quét được.
2. **Warnings là errors:** 9 warnings trong RV trước đó thực ra là 3 bug thật. Đừng bỏ qua warnings.
3. **QR payload format phải nhất quán:** IssueAsync và PrintTicket dùng 2 format khác nhau → ClaimAsync phải có fallback (đã fix SERVICE nhưng CONTROLLER vẫn block — Bug 2 chưa hết) HOẶC PrintTicket phải dùng cùng format IssueAsync (DD-1: thêm `QrPayload` vào `SessionDetailResult`).
4. **Print ticket session=null:** Root cause hypothesis mạnh nhất = fresh circuit (forceLoad) → auth state chưa hydrate → JWT tenant_id=Empty → GetSession filter sai → 404. Cần test runtime confirm (Phase 2.4).
5. **CI pass ≠ runtime works:** CI 7/7 pass nhưng 3 bug runtime. RV là bắt buộc sau deploy.
6. **MỚI (2026-08-18): Fix service KHÔNG đủ nếu controller block trước.** Commit b9594509 fix `ClaimAsync` service fallback nhưng `GuardController.Claim` reject `{sc,sid}` payload trước khi gọi service → fix vô dụng. **Luôn trace full flow: Controller → Service → Repository.**
7. **MỚI (2026-08-18): OCR trên entire photo = accuracy killer.** Không có plate ROI detection → background noise → Tesseract nhảm. Phải crop vùng biển số trước khi OCR (R-OCR-1).
8. **MỚI (2026-08-18): Double JPEG compression giảm OCR accuracy.** Capture 0.85 → compress 0.7 → artifact. OCR nên chạy trên bản pre-compression (R-OCR-4).
9. **MỚI (2026-08-18): `forceLoad: true` = fresh Blazor circuit = auth state chưa sẵn sàng.** Tránh `forceLoad` cho page cần auth data ngay OnInitializedAsync. Dùng same-tab navigation hoặc delay + retry.
