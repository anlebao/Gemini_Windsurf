# Task P6: OCR Quality Upgrade — Cloud Assist Fallback + On-Device Engine Spike

> **SRS:** `docs/requirements/van_an_self_serve_onboarding_srs_v1.md` — FR-17, FR-18, [G13], [G14], Q6-Q7
> **Master plan:** `docs/AI/plans/self-serve-onboarding-master-plan.md` §4 P6
> **Mode:** IMPLEMENT (sau P0+P1; song song được với P4/P5 — không block go-live)
> **Layer:** 2_Gateway + 3_CoreHub + 5_WebApps/KhachLink + ShopERP wizard JS
> **User decisions (2026-11-09):** đồng ý cloud assist + on-device upgrade; lý do — Tesseract kém chính xác ngoài trời; chấp nhận Gemini free-tier trade-off (data có thể train) với `Provider` config đổi được sang `vision`.

## 1. OBJECTIVE

1. **FR-17 — `POST /api/v1/ocr-assist`**: khi OCR client-side (Tesseract) confidence thấp → fallback sang cloud OCR (Gemini Flash structured extraction hoặc Cloud Vision text→heuristic) → trả kết quả merge vào form. Privacy: consent checkbox + provider config + rate-limit + metering.
2. **FR-18 — On-device spike**: đánh giá engine OCR on-device tốt hơn Tesseract cho ảnh ngoài trời (biển hiệu/menu). ML Kit chỉ native → ứng viên chính **PaddleOCR.js/ONNX Runtime Web** (WASM browser). Pass criteria → implement engine thứ 2; fail → ghi kết quả, giữ Tesseract + cloud assist.

## 2. FILES

### Mới
| File | Nội dung |
|---|---|
| `3_CoreHub/Services/Ocr/IOcrAssistService.cs` + `OcrAssistService.cs` | Provider switch `gemini`/`vision`; gọi HTTP provider; parse response về DTO thống nhất |
| `3_CoreHub/Services/Ocr/GeminiOcrProvider.cs` + `VisionOcrProvider.cs` | Implement từng provider (HttpClient typed, timeout, retry 1 lần) |
| `3_CoreHub/Services/Ocr/OcrAssistDtos.cs` | `OcrAssistRequest{Kind, ImageBase64?, ImageUrl?, Hints?}`, `OcrAssistResponse{Lines?, Extraction?, Provider, RawConfidence}` |
| `2_Gateway/Controllers/OcrAssistController.cs` | `POST /api/v1/ocr-assist` — rate-limit `ocr-assist` 20/h/IP anonymous; SystemAdmin token bypass quota |
| `6_Tests/VanAn.Core.Tests/Services/Ocr/OcrAssistServiceTests.cs` | Provider parse, switch config, error mapping, no-key graceful |
| `docs/AI/tasks/self_serve_onboarding/ocr-spike-results.md` | Kết quả benchmark on-device (ảnh test + accuracy bảng) |

### Sửa
| File | Change |
|---|---|
| `2_Gateway/Program.cs` | DI `IOcrAssistService` + providers + rate-limit `ocr-assist` + config `OcrAssist:*` |
| `5_WebApps/ShopERP/appsettings*.json` + `docker-compose.gateway.yml` | `OcrAssist__Provider`, `OcrAssist__ConfidenceThreshold=60`, `OcrAssist__GeminiApiKey=${GEMINI_API_KEY:-}`, `OcrAssist__VisionApiKey=${GOOGLE_VISION_KEY:-}` (env, không commit secret) |
| `5_WebApps/KhachLink/wwwroot/js/onboarding-ocr.js` | Thêm `assistCloud(kind, imageBlob|url)` gọi Blazor → endpoint; engine slot cho on-device V2 |
| `5_WebApps/KhachLink/Pages/Onboard.razor` | Nút "Nhận diện nâng cao" + consent checkbox + merge kết quả + confidence badge |
| `5_WebApps/ShopERP/Components/Pages/Admin/OcrOnboardingWizard.razor` | Cùng nút assist (Track A cũng hưởng) |
| `5_WebApps/ShopERP/wwwroot/js/onboarding-ocr.js` + `onboarding-wizard.js` | Port assist call tương tự |

## 3. CHANGES CHI TIẾT

### 3.1 `IOcrAssistService`

```csharp
Task<OcrAssistResponse> AssistAsync(OcrAssistRequest req, CancellationToken ct);
```

- `OcrAssist:Provider` = `gemini` (default) | `vision`.
- `gemini`: POST Generative Language API `models/gemini-*-flash:generateContent` — inline image base64 → prompt theo `kind`:
  - `gpdk`: "Trích xuất JSON {mst, name, address, legalForm, owner} từ giấy phép kinh doanh Việt Nam. Chỉ trả JSON."
  - `sign`: "Đọc tên hiệu trên biển hiệu — trả {signName, confidence}."
  - `menu`: "Trích xuất danh sách món ăn/giá từ menu — trả JSON [{name, price}]"
  - Parse JSON từ response (strip markdown fences) → map DTO; parse fail → fallback `vision` nếu configured else 502.
- `vision`: POST `vision.googleapis.com/v1/images:annotate` `DOCUMENT_TEXT_DETECTION` → trả `lines[{text}]` → client heuristic (giữ extraction logic JS hiện có — response chỉ cần lines, client tự parse).
- Cả 2: timeout 30s, không retry storm (1 retry backoff 2s), lỗi provider → `OcrAssistResponse{Provider, Error}` 502 với message thân thiện.
- API key thiếu config → endpoint trả `503 { error: "OCR assist chưa cấu hình" }` — client ẩn nút.

### 3.2 Endpoint + security

- `[AllowAnonymous]` + `[EnableRateLimiting("ocr-assist")]` — policy 20/h/IP partition IP (anonymous không session → IP only; chấp nhận NAT share — assist chỉ là convenience, abuse cost nhỏ vì provider free/cheap + rate-limit).
- SystemAdmin JWT → skip rate-limit (Track A sysadmin dùng nhiều).
- Request size limit: ảnh base64 ≤ ~8MB (tương đương ~6MB binary — ảnh đã preprocess 2400px thường <2MB).
- **Không log** imageBase64/extraction PII — chỉ kind/provider/status/tenantId(nếu có).
- Metering `feature.ocr` fire-and-forget metadata `{"assist":"cloud","provider":"X","kind":"Y"}`.

### 3.3 Client (KhachLink + ShopERP wizard)

- `onboarding-ocr.js`: thêm `requestCloudAssist(kind, blob|dataUrl)` → `DotNetObjectReference` → Blazor gọi HTTP → trả response → merge.
- UI: sau OCR local, nếu `confidence < OcrAssistThreshold` (60) trên field bắt buộc → hiện banner "Độ chính xác thấp — [Nhận diện nâng cao]" + checkbox consent (persist localStorage `ocr-cloud-consent`); click → gọi assist → merge + badge provider.
- Nút luôn hiện (không chỉ khi low-conf) — user chủ động dùng khi muốn.
- Consent=false → không gọi; helper text "ảnh chỉ gửi tới Google để đọc chữ, không lưu trữ" (theo provider — vision nói "không dùng train").

### 3.4 On-device spike (FR-18 — làm TRƯỚC phần implement engine)

1. **Thu thập ảnh test**: xin user 10-15 ảnh biển hiệu/menu thật ngoài trời (nắng, xuyên sáng, chữ cách điệu) — nếu không có → chụp tại quán gần. Lưu `6_Testing/ocr-spike/` (gitignore nếu cần — ảnh thật có thể chứa PII, dùng ảnh tự chụp/synthetic).
2. **Candidates:**
   - `onnxruntime-web` + PaddleOCR detection+recognition ONNX (tiếng Việt — verify model list; nếu không có `vietnamese` → test multilingual/latin model).
   - `paddlejs`/PaddleOCR.js wrapper nếu có sẵn.
   - ML Kit: ghi nhận chỉ native — không khả thi cho PWA (note trong spike doc).
3. **Benchmark script**: chạy từng engine trên ảnh test → đo accuracy tên biển hiệu + số dòng menu đúng + thời gian mobile-emulated (CPU throttle). So Tesseract baseline.
4. **Quyết định**: pass → implement `_getOnDeviceEngineV2()` trong `onboarding-ocr.js` (lazy-load ONNX assets ~10-15MB, PWA cache) + auto-fallback khi Tesseract conf thấp; fail → `ocr-spike-results.md` ghi số liệu + close.

## 4. ACCEPTANCE CRITERIA

- [ ] `POST /api/v1/ocr-assist` kind=gpdk + ảnh GPKD mẫu → trả extraction đúng format (provider=gemini khi config) hoặc lines (vision)
- [ ] Provider switch config đổi không cần redeploy code (chỉ env)
- [ ] Không API key → 503 graceful; client ẩn nút assist
- [ ] Rate-limit: >20/h/IP → 429; SystemAdmin không bị limit
- [ ] Consent=false → client không gọi endpoint (verify bằng network log)
- [ ] Response merge vào form + confidence badge hoạt động cả 2 wizard
- [ ] Metering event `feature.ocr` với metadata assist fired
- [ ] Spike doc `ocr-spike-results.md` có bảng accuracy; nếu implement V2 → e2e extractSignName/extractMenu qua engine mới pass trên ảnh test
- [ ] `dotnet build` + guard + Core.Tests PASS

## 5. VERIFICATION

```powershell
dotnet build VanAn.sln
dotnet test 6_Tests/VanAn.Core.Tests --filter "FullyQualifiedName~OcrAssist"
# Manual: curl POST /api/v1/ocr-assist với ảnh test + GEMINI_API_KEY dev
```

## 6. RISKS / NOTES

- **Gemini free-tier data training**: user đã chấp nhận; `vision` provider = escape hatch không train (paid ~$1.50/1k ảnh — volume thấp ≈ vài USD/tháng).
- **Gemini API key**: cần user tạo Google AI Studio key → set `GEMINI_API_KEY` env trên VPS (Secrets — không commit, theo rule dev-token-secret tương tự).
- **Model name thay đổi nhanh**: pin model cụ thể trong config (`OcrAssist:GeminiModel`) thay vì "flash-latest".
- **ONNX model tiếng Việt**: nếu PaddleOCR không có `vietnamese` recognition model → phương án multilingual/latin — spike phải verify trước khi hứa implement.
- **Context**: P6 độc lập P4/P5 — nếu go-live gấp, P6 ship sau; UI assist degrade gracefully (nút ẩn khi endpoint 404/503).
