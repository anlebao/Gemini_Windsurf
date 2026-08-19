# MASTER PLAN: OCR Hub + QR Wallet Merge

> **Created:** 2026-08-19
> **Last Updated:** 2026-08-19 (S1+S2+S3 COMPLETE + DEPLOYED + RV PASS; #150 fix applied)
> **Source:** Issue #147 (Guard QR & OCR problems) + user request to simplify QR claim flow + OCR engine selection
> **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT, 7 steps)
> **Domain modification:** NO — uses existing `SystemSetting` key-value table
> **Architecture change:** YES — new OCR Hub abstraction layer (client + server), QR wallet flow simplified
> **Approval:** Pending user review of this plan

## Release Status

| Release | Sprints | Branch | Status | RV |
|---|---|---|---|---|
| **R1** — Client Phase (QR Wallet + OCR Config + PaddleOCR) | 1+2+3 | `feature/ocr-hub-client` → merged to `main` | ✅ COMPLETE + DEPLOYED + RV PASS | 16 tests (L1+L2 PASS, L3 manual browser pending) |
| **R2** — Server Phase (EasyOCR Microservice) | 4 | `feature/ocr-hub-easyocr` | ⏳ DEFERRED — not enough user demand, RAM risk on Gateway VPS (e2-small 2GB) | 4 tests |

**R1 deployment history:**
- PR #149 (S1+S2) — squash-merged, CD Multi-VPS deployed, RV L1+L2 PASS
- PR #151 (S3 PaddleOCR) — squash-merged, CD Multi-VPS deployed, RV L1+L2 PASS
- S3-fix commit `7a38fcb8` — `.onnx` MIME type fix (StaticFileOptions), CD deployed, RV L1+L2 PASS
- #150 fix commit `6c67f594` — QR wallet "Vé không hợp lệ" JSON case-insensitive fix, CD deployed, RV L1+L2 PASS

**R2 (S4) deferral rationale:**
- Use case (menu OCR by photo) chưa có user demand thực tế
- EasyOCR model ~1GB RAM khi active → OOM risk trên Gateway VPS e2-small (2GB)
- Tesseract.NET fallback đã có sẵn — đủ cho menu input quy mô nhỏ
- Khi nào làm: khi có tenant F&B yêu cầu + upgrade VPS lên 4GB RAM (hoặc VPS riêng ~$13/tháng)

**Dual-PR strategy** (revised per technical review): S1+S2+S3 ship first (frontend/C#, fast), S4 isolated (Python Docker, independent). See `execution_strategy.md`.

---

## Problem Statement

### Problem 1: QR vé xe flow phức tạp (Issue #147)
- Khách quét QR vé xe nhưng phải đăng nhập → "Vui lòng đăng nhập" → khách bỏ cuộc
- Quét QR sản phẩm (order) thì smooth vì không cần login → khách nhầm tưởng QR vé xe bị hỏng
- 2 trang riêng biệt `/qr/claim` (nhận QR) + `/qr/wallet` (xem vé) → khách không biết vào đâu để show lại QR cho guard

### Problem 2: OCR biển số chậm + không chính xác (Issue #147)
- Tesseract.js v5 chạy trên ROI full resolution → ~500-1500ms/frame trên mobile
- Biển số VN 2 hàng dọc nhưng PSM 7 (single line) ép đọc theo 1 hàng → mất cấu trúc
- Không có khả năng swap thư viện OCR — hardcoded Tesseract

### Problem 3: Không có OCR Hub cho use cases khác nhau
- Quét biển số: cần real-time (< 500ms), client-side, accuracy 90%+
- Nhập menu F&B (future): cần accuracy cao (95%+), server-side OK, latency 1-3s acceptable
- 1 thư viện không phục vụ tốt cả 2 use case

## Solution

### QR Wallet Merge (Sprint 1)
- Gộp `/qr/claim` + `/qr/wallet` thành 1 trang `/qr/wallet` với 2 tab:
  - **Tab "Vé của tôi"**: list vé từ localStorage, tap → fullscreen QR cho guard quét
  - **Tab "Nhận QR mới"**: QRScanner + short code input
- Bỏ login requirement — lưu QR vào localStorage giống add-to-cart
- `/qr/claim` redirect → `/qr/wallet` (backward compat cho QR in trên vé cũ)

### OCR Plate Improvements (Sprint 1)
- Tách 2 hàng ROI trước OCR (crop hàng trên + hàng dưới riêng)
- PSM 7 cho từng hàng (single line — chính xác nhất)
- Char whitelist chặt hơn (bỏ Q, J, U, W, V — không có trong biển VN)
- Regex validate từng hàng: hàng trên `^\d{2}[A-ZĐ]{1,2}$`, hàng dưới `^\d{3,5}(\.\d{2})?$`
- ROI downscale 300px (biển 2 hàng cần ít pixel hơn 1 hàng dài)

### OCR Hub (Sprint 2-4)
- **Sprint 2:** SystemSetting config (`Ocr:PlateEngine`, `Ocr:MenuEngine`) + Admin UI + client-side `ocr-hub.js` adapter
- **Sprint 3:** PaddleOCR plate model (ONNX/WASM) — accuracy 90-95%, speed 100-200ms
- **Sprint 4:** EasyOCR server-side (Python microservice) — cho menu input, accuracy 95%+

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  ShopERP Admin → System Settings → OCR Engine Config        │
│  ┌─────────────────┬─────────────────────────────────────┐  │
│  │ Plate OCR:      │ Menu OCR (future):                  │  │
│  │ ○ Tesseract.js  │ ○ EasyOCR (server)                  │  │
│  │ ○ PaddleOCR     │ ○ Tesseract.js                      │  │
│  └─────────────────┴─────────────────────────────────────┘  │
│  Stored in: SystemSetting table (per-tenant, key-value)     │
└─────────────────────────────────────────────────────────────┘
          │                              │
          ▼ (fetch config)               ▼ (fetch config)
┌─────────────────────┐     ┌─────────────────────────────┐
│ CLIENT-SIDE (JS)    │     │ SERVER-SIDE (C#/Python)     │
│ ocr-hub.js          │     │ IOcrEngine (C# interface)   │
│ ┌─────────────────┐ │     │ ┌─────────────────────────┐ │
│ │ TesseractAdapter│ │     │ │ EasyOcrAdapter          │ │
│ │ (WASM, current) │ │     │ │ → Python microservice   │ │
│ ├─────────────────┤ │     │ ├─────────────────────────┤ │
│ │ PaddleAdapter   │ │     │ │ TesseractAdapter        │ │
│ │ (ONNX/WASM)     │ │     │ │ → Tesseract.NET         │ │
│ └─────────────────┘ │     │ └─────────────────────────┘ │
│ Used by:            │     │ Used by:                    │
│ guard-camera.js     │     │ Menu input (future)         │
│ (plate scanning)    │     │                             │
└─────────────────────┘     └─────────────────────────────┘
```

### Why Hybrid (client + server)

| Use case | Latency yêu cầu | Best location | Lý do |
|---|---|---|---|
| **Plate scanning** (guard) | < 500ms/frame | Client (browser) | Real-time camera, không network roundtrip |
| **Menu input** (F&B) | 1-3s OK | Server | One-time setup, cần accuracy cao, model nặng |

### Data flow — Plate scanning

1. Admin chọn OCR engine trong System Settings → lưu `Ocr:PlateEngine = Tesseract|PaddleOCR`
2. Guard mở Scan page → `ocr-hub.js` fetch `/api/ocr/config` → biết engine nào
3. Camera live → crop ROI → `ocr-hub.js.recognize(canvas)` → delegate to adapter
4. Adapter (Tesseract/Paddle) return `{ text, confidence }`
5. `_normalizeVnPlate()` post-process → validate format → fill plate input

### Data flow — QR wallet (simplified)

1. Khách mở `/qr/wallet` → tab "Vé của tôi" load từ localStorage (không cần login)
2. Khách切换 tab "Nhận QR mới" → quét QR hoặc nhập short code
3. `DoClaimAsync`:
   - Chưa login: lưu `{qrPayload, shortCode, tenantId, claimedAt}` vào localStorage → switch tab "Vé của tôi"
   - Đã login: gọi API `POST /api/guard/claim` (optional) → lưu wallet → switch tab
4. Lúc lấy xe: khách mở `/qr/wallet` → tab "Vé của tôi" → tap vé → fullscreen QR → guard quét

## Scope

### In scope
- **Sprint 1:** QR wallet merge (4 files) + OCR plate improvements (1 file)
- **Sprint 2:** OCR config infrastructure (4 new files) + client OCR Hub (2 files)
- **Sprint 3:** PaddleOCR ONNX model + PaddleAdapter (3 new files + 1 edit)
- **Sprint 4:** EasyOCR Python microservice + C# adapter (6 new files)

### Out of scope (deferred)
- Google Vision API fallback (Phase 3 — cost-based decision)
- Redis cache for OCR config (in-memory sufficient)
- OCR training custom model cho biển số VN (PaddleOCR pre-trained đủ)
- OCR cho hóa đơn/bill (separate initiative)

## Sprint Dependency Graph

```
PR 1: feature/ocr-hub-client (R1 — Client Phase)
  S1 (QR Wallet + OCR Improvements)
      │
      ├── S2 (OCR Config + Client Hub)
      │       │
      │       └── S3 (PaddleOCR — client-side ONNX)
      │
      └── [PR 1 merge → CD → RV 1 (16 tests)]

PR 2: feature/ocr-hub-easyocr (R2 — Server Phase, after PR 1)
  S4 (EasyOCR Python microservice + C# adapters)
      └── [PR 2 merge → CD + OCR container → RV 2 (4 tests)]
```

- **PR 1 (S1+S2+S3) ships first** — frontend/C# only, fast build, no Docker
- **PR 2 (S4) ships after PR 1** — Python microservice isolated, Docker build separate
- **S4 cannot block S1-S3** — separate PRs, separate deploys (key review fix)
- **S3 + S4 no longer parallel** — S4 waits for PR 1 merge (trivial rebase on OcrSettings.razor)

## Success Criteria

### Sprint 1
1. ✅ Khách quét QR vé xe không cần đăng nhập → lưu vào wallet → show lại QR cho guard
2. ✅ `/qr/wallet` có 2 tab, 1 link trong NavMenu
3. ✅ `/qr/claim` redirect → `/qr/wallet` (backward compat)
4. ✅ OCR biển số tách 2 hàng, PSM 7 từng hàng, accuracy cải thiện
5. ✅ Build + guard-check + CI + CD + RV PASS

### Sprint 2
1. ✅ Admin chọn OCR engine (Tesseract/PaddleOCR) trong System Settings
2. ✅ `ocr-hub.js` fetch config + delegate to correct adapter
3. ✅ guard-camera.js gọi `vananOcrHub.recognize()` thay vì Tesseract trực tiếp
4. ✅ Default = Tesseract (backward compat)

### Sprint 3
1. ✅ PaddleOCR plate model chạy trong browser via ONNX/WASM
2. ✅ Accuracy 90%+ trên biển số VN thật
3. ✅ Speed 100-200ms/frame (3-5x faster than Tesseract)
4. ✅ Admin switch Tesseract → PaddleOCR → work immediately

### Sprint 4
1. ✅ EasyOCR Python microservice deploy trên VPS riêng
2. ✅ C# adapter gọi microservice via HTTP
3. ✅ Menu input (future) dùng EasyOCR → accuracy 95%+

## RV Plan (per sprint)

### Sprint 1 RV
| # | Test | Expected |
|---|---|---|
| 1 | API health (Gateway/ShopERP/KhachLink) | 200 |
| 2 | `/qr/wallet` load không login | Hiện 2 tab |
| 3 | Quét QR vé xe (chưa login) → lưu wallet | Success, switch tab |
| 4 | Tab "Vé của tôi" → tap vé → fullscreen QR | QR hiện |
| 5 | `/qr/claim` → redirect `/qr/wallet` | 302 |
| 6 | NavMenu link "QR gửi xe" → `/qr/wallet` | Navigate |
| 7 | OCR biển số 2 hàng → result | Accuracy cải thiện |
| 8 | Short code input → lưu wallet | Success |

### Sprint 2-4 RV
(Defined in respective sprint task cards)

## Risk Assessment

| Risk | Impact | Mitigation |
|---|---|---|
| PaddleOCR model too heavy for mobile | Slow load | Quantize INT8, lazy load, fallback Tesseract |
| EasyOCR microservice down | Menu input fail | Fallback to Tesseract server-side |
| OCR config not found | Default Tesseract | Hardcoded fallback in ocr-hub.js |
| QR wallet localStorage full | Save fail | Limit 20 sessions, FIFO eviction |
| `/qr/claim` backward compat break | Old QR tickets fail | Redirect handler tested in RV |
