# EXECUTION STRATEGY: OCR Hub — Dual-PR, Safe, Fast, 2 RV Passes

> **Created:** 2026-08-19
> **Last Updated:** 2026-08-19 (revised per technical review — Dual-PR strategy)
> **Master plan:** `docs/AI/tasks/ocr_hub/master_plan.md`
> **Goal:** Ship S1+S2+S3 fast (frontend/C#), isolate S4 (Python microservice) separately

## Strategy: Dual-PR — Client Phase + Server Phase

```
main
 │
 ├── PR 1: feature/ocr-hub-client (S1 + S2 + S3)
 │     ├── commit: S1 — QR Wallet Merge (4 files)
 │     ├── commit: S1 — OCR Plate Improvements (1 file)
 │     ├── commit: S2 — OCR Config Infrastructure (4 new files)
 │     ├── commit: S2 — Client OCR Hub (2 files)
 │     ├── commit: S3 — PaddleOCR adapter + CDN assets (JS only)
 │     ├── commit: S3 — Enable PaddleOCR in Admin UI
 │     ├── [1 PR] → [1 merge] → [1 CD] → [1 RV — 16 tests]
 │     └── Ship to production — QR Wallet + OCR improvements LIVE
 │
 ├── PR 2: feature/ocr-hub-easyocr (S4 only)
 │     ├── commit: S4 — Python microservice (main.py, Dockerfile)
 │     ├── commit: S4 — C# adapters (IOcrEngine, EasyOcrAdapter, TesseractServerAdapter)
 │     ├── commit: S4 — OcrController + Factory + docker-compose
 │     ├── commit: S4 — Enable EasyOCR in Admin UI
 │     ├── [1 PR] → [1 merge] → [1 CD + OCR container] → [1 RV — 4 tests]
 │     └── Ship to production — EasyOCR for menu input LIVE
```

## Why Dual-PR (revised from Single-PR per technical review)

| Approach | S4 blocks S1-S3? | Build time | RV count | Risk isolation |
|---|---|---|---|---|
| Single-PR (original) | **YES** — Docker build fail → all 4 sprints stuck | 5-10x longer (Python + ONNX + C#) | 1 | Poor — 1 bug rolls back all |
| **Dual-PR (revised)** ✅ | **NO** — S4 isolated, S1-S3 ship independently | Phase 1 fast (C#/JS only), Phase 2 separate | 2 | Excellent — S4 bug doesn't affect S1-S3 |

### Key insight from review
S4 (Python microservice + Docker) có failure mode hoàn toàn khác (network timeout tải PyTorch, RAM thiếu, Docker build fail). Gộp với S1-S3 (C#/JS thuần) = 1 bug S4 chết chìm toàn bộ UX fix khẩn cấp cho bảo vệ.

## Conflict Analysis (Dual-PR — zero conflicts guaranteed)

### PR 1: feature/ocr-hub-client (S1 + S2 + S3)

| File | S1 | S2 | S3 | Conflict |
|---|---|---|---|---|
| `Wallet.razor` | REWRITE | — | — | None |
| `Wallet.razor.cs` | REWRITE | — | — | None |
| `NavMenu.razor` | EDIT | — | — | None |
| `Claim.razor` | REWRITE | — | — | None |
| `guard-camera.js` | EDIT (OCR improvements) | EDIT (refactor to hub) | — | Sequential OK |
| `Scan.razor.cs` | — | EDIT (preload via hub) | — | None |
| `ocr-hub.js` | — | NEW | EDIT (PaddleAdapter) | Sequential OK |
| `OcrSettings.razor` | — | NEW (Paddle disabled) | EDIT (enable Paddle) | Sequential OK |
| `IOcrConfigService.cs` | — | NEW | — | None |
| `OcrConfigService.cs` | — | NEW | — | None |
| `OcrConfigController.cs` | — | NEW | — | None |
| ONNX models | — | — | CDN/script (not in Git) | None |

### PR 2: feature/ocr-hub-easyocr (S4 only)

| File | Conflict with PR 1? |
|---|---|
| `IOcrEngine.cs` (NEW) | None — new file |
| `EasyOcrAdapter.cs` (NEW) | None |
| `TesseractServerAdapter.cs` (NEW) | None |
| `OcrEngineFactory.cs` (NEW) | None |
| `OcrController.cs` (NEW) | None |
| `OcrSettings.razor` (EDIT — enable EasyOCR) | **Possible** — PR 1 also edits this. **Merge PR 2 after PR 1** → resolve trivially |
| `docker-compose.gateway.yml` (EDIT — add container) | None |
| Python microservice (NEW) | None |

**Zero conflicts** — PR 2 merges after PR 1, only `OcrSettings.razor` needs trivial rebase.

## Execution Phases

### Phase 1: PR 1 — S1 + S2 + S3 (Client, ~3-4 hours)

#### Step 1.1: S1 — QR Wallet + OCR Improvements (~50 min)

```
[1] Wallet.razor          — rewrite 2 tabs, bỏ login gate
[2] Wallet.razor.cs       — rewrite logic, DoClaimAsync xử lý cả 2 TH
[3] NavMenu.razor         — change link /qr/claim → /qr/wallet
[4] Claim.razor           — redirect (backward compat)
[5] guard-camera.js       — OCR improvements:
    - Tách 2 hàng ROI (NHƯNG check aspect ratio + tilt, không crop cứng 50%)
    - PSM 7 từng hàng
    - Whitelist chặt (bỏ Q,J,U,W,V)
    - Regex validate từng hàng
    - Downscale 300px
    - Fallback full-ROI nếu 2 hàng fail
[6] Build + guard-check
[7] Commit "S1: QR wallet merge + OCR plate improvements"
```

**Review fix applied:** Crop 2 hàng không cứng 50% — check aspect ratio + tilt angle. Nếu ROI lệch góc > 15° → fallback full-ROI OCR (không crop).

#### Step 1.2: S2 — OCR Config + Client Hub (~70 min)

```
[8]  IOcrConfigService.cs    — NEW
[9]  OcrConfigService.cs     — NEW (SystemSetting key-value, cache 60s)
[10] OcrConfigController.cs  — NEW (GET/PUT /api/ocr/config, admin only)
[11] OcrSettings.razor       — NEW (PaddleOCR disabled, EasyOCR disabled)
[12] ocr-hub.js              — NEW (Tesseract adapter only, PaddleAdapter stub)
[13] guard-camera.js         — EDIT (_ocrRoi → vananOcrHub.recognize)
[14] Scan.razor.cs           — EDIT (preload via vananOcrHub)
[15] OcrConfigServiceTests.cs — NEW (5 unit tests)
[16] Build + guard-check + unit tests
[17] Commit "S2: OCR config infrastructure + client OCR hub"
```

#### Step 1.3: S3 — PaddleOCR Client-side (~2 hours)

```
[18] Download + convert PaddleOCR ONNX models (INT8 quantized)
[19] Upload ONNX models to CDN (KHÔNG commit vào Git — tránh phình .git)
[20] CI/CD script: pull ONNX from CDN → wwwroot/js/lib/ocr/paddle/ during build
[21] ocr-hub.js — EDIT (add PaddleAdapter with timeout fallback 3s)
[22] OcrSettings.razor — EDIT (enable PaddleOCR option)
[23] ServiceWorker caching for ONNX models (cache-first strategy)
[24] Build + guard-check
[25] Commit "S3: PaddleOCR integration (client-side ONNX, CDN-hosted)"
```

**Review fixes applied:**
- ONNX models trên CDN, không commit vào Git
- ServiceWorker cache-first cho ONNX (tránh re-download 4G)
- PaddleOCR init timeout 3s → downgrade Tesseract cho session đó
- Memory check: nếu `navigator.deviceMemory < 4` → skip PaddleOCR, dùng Tesseract

#### Step 1.4: PR 1 Merge + CD + RV

```
[26] Final build + guard-check
[27] Push feature/ocr-hub-client
[28] Create PR 1: "OCR Hub Client — QR Wallet + OCR Config + PaddleOCR (S1-S3)"
[29] CI runs (C#/JS only — fast, no Python/Docker)
[30] Merge PR 1 → main
[31] CD Multi-VPS (Gateway + ShopERP + KhachLink — no new container)
[32] RV 1 — 16 tests
```

### Phase 2: PR 2 — S4 EasyOCR Server (~2-3 hours, after PR 1 merged)

#### Step 2.1: S4 — Python Microservice + C# Adapters

```
[33] Python microservice (main.py, requirements.txt, Dockerfile, docker-compose.yml)
[34] IOcrEngine.cs — NEW
[35] EasyOcrAdapter.cs — NEW (HttpClient → Python microservice)
[36] TesseractServerAdapter.cs — NEW (Tesseract.NET fallback)
[37] OcrEngineFactory.cs — NEW
[38] OcrController.cs — NEW (POST /api/ocr/recognize)
[39] OcrSettings.razor — EDIT (enable EasyOCR option) — rebase after PR 1
[40] docker-compose.gateway.yml — add ocr-microservice container with mem_limit: 1.5g
[41] Build + guard-check
[42] Commit "S4: EasyOCR server-side microservice + C# adapters"
```

**Review fix applied:** `mem_limit: 1.5g` cho ocr-microservice — tránh EasyOCR ngốn RAM Gateway VPS.

#### Step 2.2: PR 2 Merge + CD + RV

```
[43] Push feature/ocr-hub-easyocr
[44] Create PR 2: "OCR Hub Server — EasyOCR Microservice (S4)"
[45] CI runs (includes Docker build for Python microservice)
[46] Merge PR 2 → main
[47] CD Multi-VPS (includes new OCR container deploy)
[48] RV 2 — 4 tests (EasyOCR specific)
```

## RV Plan

### RV 1: PR 1 (16 tests — Client Phase)

#### Layer 1: API Health (2 tests)
| # | Test | Expected |
|---|---|---|
| 1 | Gateway /health | 200 |
| 2 | ShopERP /health | 200 |

#### Layer 2: Static Assets + Config (5 tests)
| # | Test | Expected |
|---|---|---|
| 3 | `/qr/wallet` page load (no login) | 2 tabs visible |
| 4 | `/qr/claim` → redirect `/qr/wallet` | 301/302 |
| 5 | `ocr-hub.js` deployed | File exists, has `vananOcrHub` |
| 6 | ONNX models on CDN | `det.onnx` + `rec.onnx` accessible via CDN URL |
| 7 | `GET /api/ocr/config` (admin) | Returns config JSON |

#### Layer 3: QR Wallet Flow (4 tests)
| # | Test | Expected |
|---|---|---|
| 8 | Quét QR vé xe (no login) → save wallet | Success, tab switch |
| 9 | Tab "Vé của tôi" → tap vé → fullscreen QR | QR canvas renders |
| 10 | Short code input → save wallet | Success |
| 11 | NavMenu "QR gửi xe" → `/qr/wallet` | Navigate |

#### Layer 4: OCR Plate Scanning (5 tests)
| # | Test | Expected |
|---|---|---|
| 12 | Guard scan with Tesseract engine | OCR works (backward compat) |
| 13 | Guard scan with PaddleOCR engine (strong device) | Accuracy 90%+, speed < 200ms |
| 14 | PaddleOCR init timeout 3s → fallback Tesseract | Console warning + Tesseract works |
| 15 | PaddleOCR on low-memory device (< 4GB RAM) → skip to Tesseract | Tesseract works, no OOM |
| 16 | OCR biển số 2 hàng + tilt → fallback full-ROI | No midY crop error |

### RV 2: PR 2 (4 tests — Server Phase)

#### Layer 5: OCR Menu Input + Microservice (4 tests)
| # | Test | Expected |
|---|---|---|
| 17 | OCR microservice /health | 200 |
| 18 | `POST /api/ocr/recognize` with EasyOCR | Text result + confidence |
| 19 | EasyOCR microservice down → fallback Tesseract.NET | Tesseract result + warning log |
| 20 | `mem_limit: 1.5g` enforced | Container OOM-killed if exceeds, not VPS |

## Review Fixes Applied (from technical review)

### Fix 1: Dual-PR Strategy (was Single-PR)
- **Issue:** S4 Docker build fail → block S1-S3 UX fix khẩn cấp
- **Fix:** Split into 2 PRs — S1+S2+S3 (client) ship first, S4 (server) separate
- **Benefit:** S1 QR Wallet fix LIVE trong ~4 giờ, không chờ Python microservice

### Fix 2: S1 — Tách 2 hàng ROI không crop cứng 50%
- **Issue:** Tilt/perspective distortion → crop midY 50% cắt ngang dãy số
- **Fix:** Check aspect ratio + tilt angle trước khi crop. Nếu lệch > 15° → fallback full-ROI OCR
- **Implementation:** Dùng bounding box analysis hoặc simple horizontal projection profile

### Fix 3: S3 — ONNX models không commit vào Git
- **Issue:** 9MB ONNX files phình .git directory
- **Fix:** Upload to CDN, CI/CD script pull assets during build
- **Alternative:** Git LFS nếu không có CDN

### Fix 4: S3 — PaddleOCR timeout + memory check
- **Issue:** 12MB WASM trên mobile yếu → OOM crash, 4G lag
- **Fix:**
  - `navigator.deviceMemory < 4` → skip PaddleOCR, dùng Tesseract
  - Init timeout 3s → downgrade Tesseract cho session đó
  - ServiceWorker cache-first cho ONNX (tránh re-download)

### Fix 5: S4 — mem_limit 1.5g cho OCR microservice
- **Issue:** EasyOCR ngốn RAM → sập Gateway/ShopERP trên cùng VPS
- **Fix:** `mem_limit: 1.5g` trong docker-compose.gateway.yml
- **Additional:** `cpu_quota: 100000` (1 CPU core) để không starve Gateway

### Fix 6: S1 — localStorage vé xe backup mechanism
- **Issue:** Khách xóa cache = mất vé → tranh chấp bồi thường
- **Fix:** Thêm "Lưu ảnh QR" button trong wallet — cho phép khách screenshot/save QR image
- **Future:** SMS/Zalo deep link backup (requires backend — deferred)
- **UI:** Warning toast khi save wallet: "Vui lòng chụp màn hình lưu QR để phòng mất"

## Time Estimate

| Phase | Duration | Cumulative | Ship |
|---|---|---|---|
| Phase 1: S1 | ~50 min | 50 min | — |
| Phase 1: S2 | ~70 min | 2 hours | — |
| Phase 1: S3 | ~2 hours | 4 hours | — |
| Phase 1: PR + CD + RV 1 | ~30 min | 4.5 hours | **S1-S3 LIVE** |
| Phase 2: S4 | ~2-3 hours | 7 hours | — |
| Phase 2: PR + CD + RV 2 | ~30 min | 7.5 hours | **S4 LIVE** |

**Phase 1 ships in ~4.5 hours** — QR Wallet fix + OCR improvements + PaddleOCR LIVE
**Phase 2 ships in ~3 hours** — EasyOCR for menu input LIVE

## Safety Guarantees

1. **Zero merge conflicts** — sequential commits within each PR
2. **S4 cannot block S1-S3** — separate PRs, separate deploys
3. **2 CI runs** — Phase 1 fast (C#/JS), Phase 2 includes Docker
4. **2 CD deploys** — Phase 1 no new container, Phase 2 adds OCR container
5. **2 RV passes** — 16 tests (client) + 4 tests (server)
6. **Rollback granular** — revert PR 1 or PR 2 independently

## Fallback Plan

### If RV 1 (Phase 1) fails:
- Bug in S1 → split S1 to hotfix PR, merge immediately
- Bug in S3 (PaddleOCR) → disable PaddleOCR option in OcrSettings, merge S1+S2
- Branch not deleted → easy re-push fix

### If RV 2 (Phase 2) fails:
- Bug in S4 → EasyOCR option stays disabled, Tesseract.NET fallback works
- Docker build fail → fix Dockerfile, re-push
- Microservice OOM → increase mem_limit or optimize model

### If S3 model prep takes too long:
- Ship S1+S2 first (PR 1a), S3 later (PR 1b)
- S1+S2 still has value (QR Wallet + OCR config + Tesseract improvements)
