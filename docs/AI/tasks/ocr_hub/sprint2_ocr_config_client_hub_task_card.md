# SPRINT 2: OCR Config Infrastructure + Client OCR Hub — Task Card

> **Sprint:** 2 — OCR Config Infrastructure + Client OCR Hub
> **Status:** ✅ COMPLETE + MERGED + DEPLOYED + RV PASS (PR #149)
> **Branch:** `feature/ocr-hub-client` → merged to `main`
> **Merged via:** PR #149 (squash) — S1+S2 combined
> **Master plan:** `docs/AI/tasks/ocr_hub/master_plan.md`
> **Depends on:** Sprint 1 (guard-camera.js refactored)
> **Estimated files changed:** 6 (4 new + 2 edit)
> **Estimated effort:** ~70 minutes

## Objective

1. **OCR Config Infrastructure:** System admin chọn OCR engine per use case (plate vs menu), stored in `SystemSetting` table
2. **Client OCR Hub:** `ocr-hub.js` abstraction layer — `guard-camera.js` gọi qua hub thay vì Tesseract trực tiếp, có thể swap engine

## Tasks

| # | Task | Files | Status |
|---|---|---|---|
| 1 | `IOcrConfigService` + `OcrConfigService` — read/write OCR engine config from SystemSetting | `3_CoreHub/Services/IOcrConfigService.cs` (NEW), `OcrConfigService.cs` (NEW) | ⏳ |
| 2 | `OcrConfigController` — GET/PUT `/api/ocr/config` (admin only) | `2_Gateway/Controllers/OcrConfigController.cs` (NEW) | ⏳ |
| 3 | `OcrSettings.razor` — Admin UI dropdown chọn engine | `5_WebApps/ShopERP/Components/Pages/Admin/OcrSettings.razor` (NEW) | ⏳ |
| 4 | `ocr-hub.js` — OCR Hub + Tesseract adapter (wrap existing Tesseract worker) | `5_WebApps/ShopERP/wwwroot/js/ocr-hub.js` (NEW) | ⏳ |
| 5 | `guard-camera.js` — refactor `_ocrRoi` → gọi `vananOcrHub.recognize()` | `5_WebApps/ShopERP/wwwroot/js/guard-camera.js` (EDIT) | ⏳ |
| 6 | `Scan.razor.cs` — preload via `vananOcrHub.preload()` | `5_WebApps/ShopERP/Components/Pages/Guard/Scan.razor.cs` (EDIT) | ⏳ |
| 7 | Unit tests — OcrConfigService | `4_Testing/Core.Tests/Services/OcrConfigServiceTests.cs` (NEW) | ⏳ |
| 8 | Build + guard-check + commit + push + PR + merge + CD + RV | — | ⏳ |

## Task Details

### Task 1: IOcrConfigService + OcrConfigService

**Pattern:** Copy from `FeatureFlagService` (SystemSetting key-value, IMemoryCache 60s)

```csharp
public interface IOcrConfigService
{
    Task<OcrEngineConfig> GetConfigAsync(Guid tenantId, CancellationToken ct = default);
    Task UpdateConfigAsync(Guid tenantId, OcrEngineConfig config, string updatedBy, CancellationToken ct = default);
}

public record OcrEngineConfig
{
    public string PlateEngine { get; init; } = "Tesseract"; // "Tesseract" | "PaddleOCR"
    public string MenuEngine { get; init; } = "EasyOCR";    // "EasyOCR" | "Tesseract"
}
```

**SystemSetting keys:**
- `Ocr:PlateEngine` = `Tesseract` | `PaddleOCR`
- `Ocr:MenuEngine` = `EasyOCR` | `Tesseract`

**Default:** `Tesseract` (backward compat)

### Task 2: OcrConfigController

```csharp
[ApiController]
[Route("api/ocr")]
[Authorize(Roles = "SystemAdmin,Owner")]
public class OcrConfigController : ControllerBase
{
    [HttpGet("config")]
    public async Task<ActionResult<OcrEngineConfig>> Get() { ... }

    [HttpPut("config")]
    public async Task<IActionResult> Update([FromBody] OcrEngineConfig config) { ... }
}
```

**Note:** Uses `HttpContextTenantProvider` for tenantId (per-tenant config).

### Task 3: OcrSettings.razor — Admin UI

```
┌─────────────────────────────────────┐
│  Cài đặt OCR                         │
│                                      │
│  Engine quét biển số:                │
│  ┌─────────────────────────────┐    │
│  │ Tesseract.js v5      ▼      │    │
│  │ PaddleOCR (ONNX)            │    │
│  └─────────────────────────────┘    │
│  Recommended: PaddleOCR (nhanh hơn) │
│                                      │
│  Engine nhập menu (F&B):            │
│  ┌─────────────────────────────┐    │
│  │ EasyOCR (server)     ▼      │    │
│  │ Tesseract.js                │    │
│  └─────────────────────────────┘    │
│  Recommended: EasyOCR (accuracy cao)│
│                                      │
│  [Lưu]                               │
└─────────────────────────────────────┘
```

**Note:** PaddleOCR option chỉ hiện khi Sprint 3 complete — disable + "Coming soon" cho giờ.

### Task 4: ocr-hub.js — Client OCR Hub

```javascript
window.vananOcrHub = {
    _engine: null,
    _adapter: null,
    _configPromise: null,

    async _loadConfig() {
        if (this._engine) return this._engine;
        try {
            const resp = await fetch('/api/ocr/config', { credentials: 'include' });
            const cfg = await resp.json();
            this._engine = cfg.plateEngine || 'Tesseract';
        } catch { this._engine = 'Tesseract'; }
        return this._engine;
    },

    async getAdapter() {
        if (this._adapter) return this._adapter;
        const engine = await this._loadConfig();
        if (engine === 'PaddleOCR') {
            this._adapter = await this._loadPaddleAdapter(); // Sprint 3
        } else {
            this._adapter = await this._loadTesseractAdapter();
        }
        return this._adapter;
    },

    async recognize(canvas) {
        const adapter = await this.getAdapter();
        return adapter.recognize(canvas);
    },

    async preload() {
        await this.getAdapter();
    },

    async _loadTesseractAdapter() {
        // Wrap existing Tesseract worker from guard-camera.js
        // Delegate to vananGuardCamera.preloadOcrWorker()
        const worker = await vananGuardCamera.preloadOcrWorker();
        return {
            async recognize(canvas) {
                const { data } = await worker.recognize(canvas);
                return { text: data.text, confidence: data.confidence };
            }
        };
    },

    async _loadPaddleAdapter() {
        // Sprint 3 — stub for now
        throw new Error('PaddleOCR not yet available — falling back to Tesseract');
    },
};
```

### Task 5: guard-camera.js — refactor _ocrRoi

**Current:** `const worker = await this.preloadOcrWorker(); const { data } = await worker.recognize(canvas);`
**New:** `const result = await window.vananOcrHub.recognize(ocrCanvas);`

Keep `_normalizeVnPlate`, `_preprocessRoiForOcr`, `_ocrTwoRows` (from Sprint 1) — only change the OCR call.

### Task 6: Scan.razor.cs — preload

**Current:** `_ = JS.InvokeVoidAsync("vananGuardCamera.preloadOcrWorker");`
**New:** `_ = JS.InvokeVoidAsync("vananOcrHub.preload");`

### Task 7: Unit tests

```csharp
public class OcrConfigServiceTests
{
    [Fact] GetConfigAsync_ReturnsDefault_WhenNoSetting() { ... }
    [Fact] GetConfigAsync_ReturnsConfig_WhenSettingExists() { ... }
    [Fact] UpdateConfigAsync_CreatesNew_WhenNotExists() { ... }
    [Fact] UpdateConfigAsync_UpdatesExisting() { ... }
    [Fact] GetConfigAsync_CachesResult_60s() { ... }
}
```

## Entry Criteria

- [ ] Sprint 1 merged + deployed
- [ ] `guard-camera.js` has `_ocrTwoRows` + `_preprocessRoiForOcr` (from Sprint 1)

## Exit Criteria — ALL PASSED

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — ALL PASSED
- [ ] Unit tests — OcrConfigService 5 tests PASS
- [ ] CI pre-push — ALL PASSED
- [ ] CD Multi-VPS — SUCCESS
- [ ] RV: admin set `Ocr:PlateEngine = Tesseract` → guard scan works (backward compat)
- [ ] RV: admin set `Ocr:PlateEngine = PaddleOCR` → fallback to Tesseract (Sprint 3 not ready)
- [ ] PR merged to `main`

## RV Plan (6 tests)

| # | Test | Expected | Layer |
|---|---|---|---|
| 1 | API health | 200 | L1 |
| 2 | `GET /api/ocr/config` (admin) | Returns config JSON | L2 |
| 3 | `PUT /api/ocr/config` (admin) | 200 OK | L2 |
| 4 | `ocr-hub.js` deployed | File exists, has `vananOcrHub` | L2 |
| 5 | Guard scan with Tesseract engine | OCR works (backward compat) | L3 |
| 6 | Guard scan with PaddleOCR engine | Fallback to Tesseract + console warning | L3 |

## Notes

- Sprint 2 chỉ implement Tesseract adapter — PaddleOCR adapter là stub (Sprint 3)
- `OcrSettings.razor` PaddleOCR option disabled + "Coming soon" cho giờ
- OCR config per-tenant — mỗi tenant có thể chọn engine khác nhau
- Default = Tesseract → backward compat guaranteed
