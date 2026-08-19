# SPRINT 3: PaddleOCR Integration (Client-side ONNX) — Task Card

> **Sprint:** 3 — PaddleOCR Integration
> **Status:** ✅ COMPLETE + MERGED + DEPLOYED + RV PASS (PR #151 + S3-fix `7a38fcb8`)
> **Branch:** `feature/ocr-paddle-s3` → merged to `main`
> **Merged via:** PR #151 (squash) + S3-fix commit (`.onnx` MIME type via StaticFileOptions)
> **Models deployed:** `wwwroot/js/lib/ocr/paddle/` — det.onnx (4.5MB), rec.onnx (10.4MB), dict.txt (6623 chars)
> **Master plan:** `docs/AI/tasks/ocr_hub/master_plan.md`
> **Depends on:** Sprint 2 (OCR config + client hub)
> **Estimated files changed:** 5 (4 new + 1 edit)
> **Estimated effort:** ~2-4 hours (model prep + adapter)

## Objective

PaddleOCR plate detection + recognition model chạy trong browser via ONNX/WASM — accuracy 90-95%, speed 100-200ms/frame (3-5x faster than Tesseract).

## Tasks

| # | Task | Files | Status |
|---|---|---|---|
| 1 | Download + convert PaddleOCR plate models to ONNX INT8 | Offline (model prep) | ⏳ |
| 2 | Upload ONNX models to CDN (KHÔNG commit vào Git) | CDN (Cloudflare R2 or similar) | ⏳ |
| 3 | CI/CD script: pull ONNX from CDN → wwwroot during build | `.github/workflows/ci.yml` or build script (EDIT) | ⏳ |
| 4 | Copy `onnxruntime-web` library to `wwwroot/js/lib/ocr/` | `5_WebApps/ShopERP/wwwroot/js/lib/ocr/ort.min.js` (NEW) | ⏳ |
| 5 | Implement `PaddleAdapter` in `ocr-hub.js` with timeout + memory check | `5_WebApps/ShopERP/wwwroot/js/ocr-hub.js` (EDIT) | ⏳ |
| 6 | ServiceWorker cache-first for ONNX models | `5_WebApps/ShopERP/wwwroot/sw.js` (EDIT) or service-worker registration | ⏳ |
| 7 | Enable PaddleOCR option in `OcrSettings.razor` | `5_WebApps/ShopERP/Components/Pages/Admin/OcrSettings.razor` (EDIT) | ⏳ |
| 8 | Build + guard-check + commit + push + PR + merge + CD + RV | — | ⏳ |

## Task Details

### Task 1: Model preparation (offline, one-time)

**Models needed:**

| Model | Source | Size (INT8) | Purpose |
|---|---|---|---|
| `ch_PP-OCRv4_det` | PaddleOCR repo | ~3MB | Plate detection (find text region) |
| `ch_PP-OCRv4_rec` | PaddleOCR repo | ~6MB | Plate recognition (CRNN + CTC) |

**Conversion steps:**
1. Download from PaddleOCR GitHub releases
2. Convert PaddlePaddle → ONNX: `paddle2onnx` CLI tool
3. Quantize INT8: `onnxruntime-tools quantize` (reduces size 4x)
4. Optimize for web: `onnxruntime-tools optimize`
5. Verify output matches original

**Note:** "Đ" character handling — PaddleOCR `ch_PP-OCRv4_rec` trained on Chinese + English, may not recognize "Đ" well. Workaround:
- Char whitelist includes "Đ"
- Post-process: if OCR returns "D" in letter position + context suggests electric vehicle, map "D" → "Đ"
- Or: use Vietnamese plate recognition model from community (if available)

### Task 2-3: CDN-hosted models + CI pull script (review fix — không commit ONNX vào Git)

**Review fix:** ONNX files (~9MB) không commit vào Git — tránh phình .git directory. Upload to CDN, CI/CD pull during build.

```
CDN (Cloudflare R2 or GitHub Releases):
└── ocr-hub/
    ├── det.onnx              (~3MB — plate detection)
    └── rec.onnx              (~6MB — plate recognition)

CI/CD build script:
└── pulls CDN assets → 5_WebApps/ShopERP/wwwroot/js/lib/ocr/paddle/
    ├── det.onnx              (gitignored — pulled at build time)
    └── rec.onnx              (gitignored — pulled at build time)

5_WebApps/ShopERP/wwwroot/js/lib/ocr/
├── tesseract.min.js          (existing — in Git)
├── worker.min.js             (existing — in Git)
├── ort.min.js                (NEW — onnxruntime-web, ~3MB, in Git — it's a library)
└── paddle/                   (gitignored — CDN-pulled at build)
    ├── det.onnx
    └── rec.onnx
```

**`.gitignore` addition:**
```
# OCR Hub — ONNX models pulled from CDN during CI/CD build
5_WebApps/ShopERP/wwwroot/js/lib/ocr/paddle/*.onnx
```

**CI/CD pull script (in build step):**
```yaml
- name: Pull OCR ONNX models from CDN
  run: |
    mkdir -p 5_WebApps/ShopERP/wwwroot/js/lib/ocr/paddle
    curl -L ${{ secrets.OCR_CDN_URL }}/det.onnx -o 5_WebApps/ShopERP/wwwroot/js/lib/ocr/paddle/det.onnx
    curl -L ${{ secrets.OCR_CDN_URL }}/rec.onnx -o 5_WebApps/ShopERP/wwwroot/js/lib/ocr/paddle/rec.onnx
```

**Note:** Total ~9MB — lazy load only when PaddleOCR engine selected. Tesseract users don't download. ServiceWorker caches ONNX after first load (cache-first strategy).

### Task 5: PaddleAdapter in ocr-hub.js (with timeout + memory check — review fixes)

```javascript
async _loadPaddleAdapter() {
    // Review fix 1: Memory check — skip PaddleOCR on low-end devices
    if (navigator.deviceMemory && navigator.deviceMemory < 4) {
        console.warn('[OCR Hub] Device memory < 4GB — skipping PaddleOCR, using Tesseract');
        return await this._loadTesseractAdapter();
    }
    
    // Review fix 2: Init timeout 3s — downgrade to Tesseract if too slow
    const timeoutPromise = new Promise((_, reject) =>
        setTimeout(() => reject(new Error('PaddleOCR init timeout 3s')), 3000)
    );
    
    try {
        await Promise.race([
            this._loadPaddleAdapterInternal(),
            timeoutPromise
        ]);
    } catch (e) {
        console.warn('[OCR Hub] PaddleOCR init failed (' + e.message + ') — downgrading to Tesseract for this session');
        this._engine = 'Tesseract'; // Permanently downgrade for this session
        return await this._loadTesseractAdapter();
    }
}

async _loadPaddleAdapterInternal() {
    await this._loadScript('/js/lib/ocr/ort.min.js');
    const ort = window.ort;
    
    // Configure WASM backend
    await ort.env();
    ort.env.wasm.wasmPaths = '/js/lib/ocr/';
    
    // Load models (lazy — only when PaddleOCR selected)
    const detSession = await ort.InferenceSession.create('/js/lib/ocr/paddle/det.onnx');
    const recSession = await ort.InferenceSession.create('/js/lib/ocr/paddle/rec.onnx');
    
    return new PaddleAdapter(detSession, recSession);
}

class PaddleAdapter {
    constructor(detSession, recSession) {
        this.det = detSession;
        this.rec = recSession;
    }
    
    async recognize(canvas) {
        // 1. Detection: find text regions in canvas
        const detResult = await this._detect(canvas);
        if (!detResult || detResult.boxes.length === 0) {
            return { text: '', confidence: 0 };
        }
        
        // 2. Recognition: for each detected region, recognize text
        let fullText = '';
        let avgConfidence = 0;
        let count = 0;
        
        for (const box of detResult.boxes) {
            const cropped = this._cropRegion(canvas, box);
            const recResult = await this._recognize(cropped);
            if (recResult.text) {
                fullText += recResult.text + ' ';
                avgConfidence += recResult.confidence;
                count++;
            }
        }
        
        return {
            text: fullText.trim(),
            confidence: count > 0 ? avgConfidence / count : 0
        };
    }
    
    async _detect(canvas) {
        // Preprocess: resize to 960x960 (PaddleOCR det input size)
        // Run inference → get probability map → find boxes via DB post-process
        // Return { boxes: [[x1,y1,x2,y2,x3,y3,x4,y4], ...] }
    }
    
    async _recognize(canvas) {
        // Preprocess: resize to 3x48 (PaddleOCR rec input size, height=48)
        // Run inference → CTC decode → text + confidence
        // Return { text: "51F", confidence: 0.92 }
    }
    
    _cropRegion(canvas, box) {
        // Crop quadrilateral region from canvas using perspective transform
    }
}
```

**Key implementation notes:**
- DB (Differentiable Binarization) post-process: threshold probability map → find contours → filter by area
- CTC decode: greedy decode (argmax) or beam search
- Input preprocessing: normalize to [-1, 1], resize to model input size
- Output post-processing: apply char dictionary (0-9, A-Z, Đ, -)

### Task 5: Enable PaddleOCR in Admin UI

**Current:** PaddleOCR option disabled + "Coming soon"
**New:** Enabled + selectable

```razor
<select @bind="_config.PlateEngine">
    <option value="Tesseract">Tesseract.js v5 (default)</option>
    <option value="PaddleOCR">PaddleOCR (ONNX) — faster + more accurate</option>
</select>
```

## Entry Criteria

- [ ] Sprint 2 merged + deployed
- [ ] `ocr-hub.js` has `_loadPaddleAdapter` stub
- [ ] PaddleOCR models converted to ONNX INT8 + verified

## Exit Criteria — ALL PASSED

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — ALL PASSED
- [ ] CI pre-push — ALL PASSED
- [ ] CD Multi-VPS — SUCCESS
- [ ] RV: admin select PaddleOCR → guard scan → accuracy 90%+ on real plates
- [ ] RV: speed 100-200ms/frame (3-5x faster than Tesseract)
- [ ] RV: fallback to Tesseract if PaddleOCR model fails to load
- [ ] PR merged to `main`

## RV Plan (6 tests)

| # | Test | Expected | Layer |
|---|---|---|---|
| 1 | API health | 200 | L1 |
| 2 | ONNX models deployed | `det.onnx` + `rec.onnx` accessible | L2 |
| 3 | `ort.min.js` deployed | File accessible | L2 |
| 4 | Admin select PaddleOCR → save config | 200 OK | L2 |
| 5 | Guard scan with PaddleOCR → plate result | Accuracy 90%+, speed < 200ms | L3 |
| 6 | PaddleOCR model fail → fallback Tesseract | Console warning + Tesseract works | L3 |

## Risk Assessment

| Risk | Mitigation |
|---|---|
| Model too heavy for low-end mobile | INT8 quantize + lazy load + fallback Tesseract |
| "Đ" not recognized | Post-process mapping + char whitelist |
| ONNX runtime WASM issues | Test on Chrome Android + Safari iOS |
| Model accuracy < 90% | Try alternative model or fine-tune |

## Notes

- Sprint 3 chỉ implement client-side PaddleOCR — không cần server
- Models lazy load — chỉ tải khi admin chọn PaddleOCR
- Fallback to Tesseract guaranteed if model load fails
- "Đ" character: cần test thực tế với biển số xe máy điện (51ĐAB-123.45)
