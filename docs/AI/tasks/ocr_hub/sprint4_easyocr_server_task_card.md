# SPRINT 4: EasyOCR Server-side (Menu Input) — Task Card

> **Sprint:** 4 — EasyOCR Server-side for Menu Input
> **Status:** ⏳ DEFERRED — not enough user demand + RAM risk on Gateway VPS (e2-small 2GB, EasyOCR needs ~1GB active)
> **Branch:** `feature/ocr-hub-easyocr` (not created)
> **Deferral rationale:** Use case (menu OCR by photo) chưa có tenant F&B yêu cầu. Tesseract.NET fallback đã có sẵn. Khi nào làm: upgrade VPS lên 4GB RAM (~$13/tháng) hoặc VPS riêng + có tenant demand.
> **Master plan:** `docs/AI/tasks/ocr_hub/master_plan.md`
> **Depends on:** Sprint 2 (OCR config infrastructure)
> **Can run in parallel with:** Sprint 3 (no conflict — server vs client)
> **Estimated files changed:** 8 (7 new + 1 edit)
> **Estimated effort:** ~2-3 hours (microservice + C# adapter)

## Objective

EasyOCR Python microservice chạy server-side — cho menu input (F&B), accuracy 95%+, latency 1-3s acceptable. C# adapter gọi microservice via HTTP.

## Tasks

| # | Task | Files | Status |
|---|---|---|---|
| 1 | Python microservice (FastAPI + EasyOCR) | `7_Services/ocr-microservice/main.py` (NEW), `requirements.txt` (NEW), `Dockerfile` (NEW), `docker-compose.yml` (NEW) | ⏳ |
| 2 | `IOcrEngine` C# interface + DTOs | `3_CoreHub/Services/IOcrEngine.cs` (NEW) | ⏳ |
| 3 | `EasyOcrAdapter` — HTTP call to Python microservice | `3_CoreHub/Services/EasyOcrAdapter.cs` (NEW) | ⏳ |
| 4 | `TesseractServerAdapter` — Tesseract.NET (C# fallback) | `3_CoreHub/Services/TesseractServerAdapter.cs` (NEW) | ⏳ |
| 5 | `OcrEngineFactory` — select adapter based on config | `3_CoreHub/Services/OcrEngineFactory.cs` (NEW) | ⏳ |
| 6 | `OcrController` — `POST /api/ocr/recognize` (for menu input) | `2_Gateway/Controllers/OcrController.cs` (NEW) | ⏳ |
| 7 | Enable Menu OCR option in `OcrSettings.razor` | `5_WebApps/ShopERP/Components/Pages/Admin/OcrSettings.razor` (EDIT) | ⏳ |
| 8 | Build + guard-check + commit + push + PR + merge + CD + RV | — | ⏳ |

## Task Details

### Task 1: Python Microservice

```
7_Services/ocr-microservice/
├── main.py              # FastAPI: POST /ocr → { text, confidence }
├── requirements.txt     # easyocr, fastapi, uvicorn, python-multipart
├── Dockerfile           # Python 3.11 + EasyOCR + ONNX Runtime
├── docker-compose.yml   # Port 5005, volume for model cache
└── README.md            # Setup + deploy instructions
```

**`main.py`:**
```python
from fastapi import FastAPI, UploadFile, File
from pydantic import BaseModel
import easyocr
import io
from PIL import Image

app = FastAPI(title="VanAn OCR Microservice")

# Lazy load — first request downloads models (~100MB)
reader_vi = None
reader_en = None

def get_reader(lang: str):
    global reader_vi, reader_en
    if lang == 'vi':
        if reader_vi is None:
            reader_vi = easyocr.Reader(['vi', 'en'], gpu=False)
        return reader_vi
    else:
        if reader_en is None:
            reader_en = easyocr.Reader(['en'], gpu=False)
        return reader_en

class OcrResult(BaseModel):
    text: str
    confidence: float
    lang: str

@app.post("/ocr", response_model=OcrResult)
async def recognize(file: UploadFile = File(...), lang: str = "vi"):
    image_bytes = await file.read()
    image = Image.open(io.BytesIO(image_bytes))
    reader = get_reader(lang)
    results = reader.readtext(np.array(image))
    
    if not results:
        return OcrResult(text="", confidence=0.0, lang=lang)
    
    # Combine all detected text
    full_text = ' '.join([text for (_, text, _) in results])
    avg_conf = sum(conf for (_, _, conf) in results) / len(results)
    
    return OcrResult(text=full_text, confidence=avg_conf, lang=lang)

@app.get("/health")
async def health():
    return {"status": "ok"}
```

**`Dockerfile`:**
```dockerfile
FROM python:3.11-slim

WORKDIR /app

# Install system deps for OpenCV (EasyOCR dependency)
RUN apt-get update && apt-get install -y \
    libgl1-mesa-glx libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY main.py .

# Pre-download models at build time (optional — reduces first request latency)
# RUN python -c "import easyocr; easyocr.Reader(['vi','en'], gpu=False)"

EXPOSE 5005
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "5005"]
```

**`docker-compose.yml`:**
```yaml
version: '3.8'
services:
  ocr-microservice:
    build: .
    ports:
      - "5005:5005"
    volumes:
      - ocr-models:/root/.EasyOCR  # Cache models
    restart: unless-stopped
    # Review fix: Resource limits — prevent EasyOCR from starving Gateway/ShopERP
    mem_limit: 1.5g              # Max 1.5GB RAM (EasyOCR model ~1GB)
    mem_reservation: 512m        # Soft limit 512MB
    cpus: 1.0                    # Max 1 CPU core
    cpu_quota: 100000            # 1 CPU = 100000 microseconds per 100ms period
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5005/health"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  ocr-models:
```

**Review fix:** `mem_limit: 1.5g` + `cpus: 1.0` — tránh EasyOCR ngốn RAM/CPU Gateway VPS làm sập ShopERP/Gateway APIs.

### Task 2: IOcrEngine C# interface

```csharp
namespace VanAn.CoreHub.Services;

public interface IOcrEngine
{
    Task<OcrResult> RecognizeAsync(byte[] imageBytes, string lang = "vi", CancellationToken ct = default);
}

public record OcrResult
{
    public string Text { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public string Lang { get; init; } = "vi";
}
```

### Task 3: EasyOcrAdapter

```csharp
public class EasyOcrAdapter : IOcrEngine
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EasyOcrAdapter> _logger;

    public EasyOcrAdapter(HttpClient httpClient, ILogger<EasyOcrAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OcrResult> RecognizeAsync(byte[] imageBytes, string lang = "vi", CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(imageBytes), "file", "image.jpg");
        content.Add(new StringContent(lang), "lang");

        var response = await _httpClient.PostAsync("/ocr", content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<OcrResult>(json);
        return result ?? new OcrResult();
    }
}
```

**DI registration:**
```csharp
services.AddHttpClient<EasyOcrAdapter>(client =>
{
    client.BaseAddress = new Uri(config["Ocr:EasyOcrUrl"] ?? "http://ocr-microservice:5005");
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

### Task 4: TesseractServerAdapter (fallback)

```csharp
public class TesseractServerAdapter : IOcrEngine
{
    // Uses Tesseract.NET (C# wrapper) — no external microservice needed
    // Install: Tesseract NuGet package
    // Traineddata: download to wwwroot/tessdata/
    
    public async Task<OcrResult> RecognizeAsync(byte[] imageBytes, string lang = "vi", CancellationToken ct = default)
    {
        using var engine = new Tesseract.TesseractEngine("tessdata", "eng+vie");
        using var img = Tesseract.Pix.LoadFromMemory(imageBytes);
        using var page = engine.Process(img);
        return new OcrResult
        {
            Text = page.GetText().Trim(),
            Confidence = page.GetMeanConfidence()
        };
    }
}
```

### Task 5: OcrEngineFactory

```csharp
public class OcrEngineFactory
{
    private readonly IServiceProvider _provider;
    private readonly IOcrConfigService _configService;

    public async Task<IOcrEngine> GetMenuEngineAsync(Guid tenantId, CancellationToken ct = default)
    {
        var config = await _configService.GetConfigAsync(tenantId, ct);
        return config.MenuEngine switch
        {
            "EasyOCR" => _provider.GetRequiredService<EasyOcrAdapter>(),
            "Tesseract" => _provider.GetRequiredService<TesseractServerAdapter>(),
            _ => _provider.GetRequiredService<TesseractServerAdapter>() // default
        };
    }
}
```

### Task 6: OcrController

```csharp
[ApiController]
[Route("api/ocr")]
[Authorize]
public class OcrController : ControllerBase
{
    [HttpPost("recognize")]
    public async Task<ActionResult<OcrResult>> Recognize(IFormFile file, [FromQuery] string? lang = null)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var engine = await _factory.GetMenuEngineAsync(tenantId);
        var result = await engine.RecognizeAsync(ms.ToArray(), lang ?? "vi");
        return Ok(result);
    }
}
```

### Task 7: Enable Menu OCR in Admin UI

```razor
<select @bind="_config.MenuEngine">
    <option value="EasyOCR">EasyOCR (server) — accuracy 95%+</option>
    <option value="Tesseract">Tesseract.NET (server) — lighter</option>
</select>
```

## Entry Criteria

- [ ] Sprint 2 merged + deployed
- [ ] OCR config infrastructure (`IOcrConfigService`) available
- [ ] VPS for Python microservice provisioned (or Docker container on existing VPS)

## Exit Criteria — ALL PASSED

- [ ] `dotnet build VanAn.sln` — 0 errors
- [ ] `guard-check.ps1` — ALL PASSED
- [ ] Python microservice health check PASS
- [ ] CI pre-push — ALL PASSED
- [ ] CD Multi-VPS — SUCCESS (including OCR microservice container)
- [ ] RV: `POST /api/ocr/recognize` with EasyOCR → text result
- [ ] RV: fallback to Tesseract.NET if EasyOCR microservice down
- [ ] PR merged to `main`

## RV Plan (6 tests)

| # | Test | Expected | Layer |
|---|---|---|---|
| 1 | API health (Gateway + OCR microservice) | 200 | L1 |
| 2 | `POST /api/ocr/recognize` with menu image | Text result + confidence | L2 |
| 3 | EasyOCR accuracy on Vietnamese menu | 95%+ | L3 |
| 4 | EasyOCR microservice down → fallback Tesseract | Tesseract result + warning log | L3 |
| 5 | Admin select EasyOCR → save config | 200 OK | L2 |
| 6 | Admin select Tesseract → save config | 200 OK | L2 |

## Deployment Notes

### Option A: Separate VPS for OCR microservice
- Provision VPS with Docker
- Deploy `docker-compose.yml`
- Configure `Ocr:EasyOcrUrl` in Gateway appsettings

### Option B: Docker container on existing Gateway VPS
- Add OCR microservice to `docker-compose.gateway.yml`
- Internal network: `http://ocr-microservice:5005`
- No new VPS needed

**Recommended:** Option B (simpler, no new VPS)

### Resource requirements
- RAM: ~1GB (EasyOCR model in memory)
- CPU: 1 core (inference)
- Disk: ~200MB (models + Docker image)
- First request latency: ~10-30s (model warm-up)
- Subsequent: 1-3s per image

## Notes

- Sprint 4 chỉ cho menu input (F&B) — không dùng cho plate scanning (latency too high)
- EasyOCR model ~100MB first download — cached in Docker volume
- Fallback to Tesseract.NET guaranteed if microservice down
- Sprint 3 + 4 can run in parallel (no file conflict)
