# TASK CARD: KhachLink Full Flow — Wave 3 — Voice Note Redesign (STT only + TTS) + QR Table Number

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** (1) Voice note toggle ON/OFF. (2) STT only (không audio storage). (3) TTS ở nhà bếp khi bếp trưởng nhấn "Nhận đơn" — toggle độc lập. (4) QR payload thêm TableNumber (toggle).
- **Nghiệp vụ áp dụng:** Section 4 (Giai đoạn 1, mục 3) + Section 3 (Module Toggles) của `Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` v1.2
- **Status:** ⬜ NOT STARTED
- **Branch:** `feature/khachlink-flow-wave3-voice-note-tts`
- **Dependency:** Wave 2 COMPLETE (completion + loyalty bypass)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Wave 3 of 5
- **Dependency:** Wave 0 (toggle infrastructure) + Wave 1 (kitchen UI buttons)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/khachlink_full_flow_master_plan.md` (READ — master plan)
- `docs/MVP_Product/Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` (READ — requirements v1.2)

### Files cần MODIFY (KhachLink client)
- `5_WebApps/KhachLink/Pages/VoiceNote.razor` — toggle ON/OFF, cleanup audio logic (chỉ giữ STT)
- `5_WebApps/KhachLink/Pages/Scan.razor` — hiển thị số bàn khi có trong payload
- `5_WebApps/KhachLink/Components/CartDrawer.razor` hoặc `Pages/Cart.razor` — hiển thị số bàn (nếu có)

### Files cần MODIFY (ShopERP server)
- `5_WebApps/ShopERP/Components/Pages/Orders/Detail.razor` — thêm nút "Đọc ghi chú" (TTS) khi bếp trưởng nhận đơn
- `5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor` — thêm sub-toggle cho TTS (nếu cần độc lập)

### Files cần MODIFY (Shared/CoreHub)
- `1_Shared/DTOs/QRCodePayload.cs` — thêm field `TableNumber` (DTO, không phải Domain entity)
- `1_Shared/Domain.cs` — mark `VoiceNoteAudioBlob` + `ItemNoteAudioBlob` as obsolete (không xóa field, chỉ thêm `[Obsolete]` attribute)

### Files cần CREATE
- `5_WebApps/ShopERP/wwwroot/js/tts-reader.js` — JS interop cho Text-to-Speech (Web Speech API `speechSynthesis`)

### Files READ ONLY (investigate patterns)
- `5_WebApps/KhachLink/Pages/VoiceNote.razor` — existing STT implementation (lines 154-229)
- `5_WebApps/KhachLink/Components/VoiceCommand.razor` — alternative voice component
- `1_Shared/Domain.cs` lines 730-731, 864-865 — `ItemNoteAudioBlob`, `VoiceNoteAudioBlob` fields
- `5_WebApps/ShopERP/Components/Pages/Orders/Detail.razor` — kitchen buttons (from Wave 1)
- `5_WebApps/ShopERP/wwwroot/js/` — existing JS files pattern

### Boundary Rules
- KHÔNG xóa `VoiceNoteAudioBlob` / `ItemNoteAudioBlob` — chỉ mark obsolete (backward compat)
- KHÔNG sửa Domain logic — chỉ thêm `[Obsolete]` attribute (cosmetic, không break)
- `QRCodePayload` là DTO (1_Shared/DTOs/), KHÔNG phải Domain entity — có thể thêm field
- TTS dùng Web Speech API (`speechSynthesis`) — JS interop trong Blazor Server
- TTS toggle: sub-toggle của `Voice_Note_Enabled` — khi voice OFF, TTS cũng OFF

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Domain Protection:** Chỉ thêm `[Obsolete]` attribute — KHÔNG xóa field, KHÔNG sửa logic
- [ ] **UI Platform:** Mọi UI mới MUST dùng VanAnButton, VanAnCard
- [ ] **KhachLink HTTP-only:** Toggle check qua `ShopFeatureSettingsHttpService`
- [ ] **STT only:** KHÔNG capture audio blob, KHÔNG upload file, KHÔNG nén audio
- [ ] **TTS:** Dùng `speechSynthesis` API (browser native) — không library ngoài
- [ ] **TTS toggle:** Khi `Voice_Note_Enabled` = OFF, TTS cũng OFF (sub-toggle)
- [ ] **QR TableNumber:** `QRCodePayload` thêm field `TableNumber` (string?, optional)
- [ ] **QR toggle:** Khi `QR_TableNumber_Enabled` = OFF, QR generation không include table number

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** Voice note toggle ON/OFF hoạt động — nút "Ghi chú" ẩn khi OFF
- [ ] **SC2:** STT chỉ lưu text (không audio blob capture/upload)
- [ ] **SC3:** TTS đọc ghi chú ở nhà bếp khi bếp trưởng nhấn "Nhận đơn" (status=confirmed)
- [ ] **SC4:** TTS toggle: khi `Voice_Note_Enabled` = OFF, TTS cũng OFF
- [ ] **SC5:** `QRCodePayload` có field `TableNumber` (string?, optional)
- [ ] **SC6:** KhachLink Scan page hiển thị số bàn khi có trong payload
- [ ] **SC7:** `VoiceNoteAudioBlob` + `ItemNoteAudioBlob` mark `[Obsolete]`
- [ ] **SC8:** Build: 0 errors
- [ ] **SC9:** guard-check.ps1 pass
- [ ] **SC10:** Architecture Tests pass
- [ ] **SC11:** Live Runtime Verification PASS (RV1-RV12 trong §9 Post-IMPLEMENT) — boot ShopERP+KhachLink+Docker, test STT/TTS/QR thực tế

---

## 6. DETAILED IMPLEMENTATION

### 6.1. ANALYZE Phase (trước khi code)

**Cần investigate:**
1. **VoiceNote.razor:** Đọc full file (410 lines) — hiểu STT implementation, xác định audio-related code cần cleanup, xác định chỗ thêm toggle check.
2. **Scan.razor:** Đọc lines 75-124 — hiểu QR payload parsing, xác định chỗ hiển thị table number.
3. **QRCodePayload.cs:** Đọc full file — hiểu structure, xác định chỗ thêm `TableNumber`.
4. **Domain.cs:** Đọc lines 730-731, 864-865 — confirm `VoiceNoteAudioBlob` + `ItemNoteAudioBlob` field declarations.
5. **Detail.razor:** Đọc kitchen buttons section (from Wave 1) — xác định chỗ thêm nút "Đọc ghi chú".
6. **ShopERP JS interop pattern:** Check `5_WebApps/ShopERP/wwwroot/js/` — existing JS files, Blazor Server JS interop pattern.
7. **QR generation:** Tìm QR generation logic (admin page hoặc service) — xác định chỗ thêm table number.

### 6.2. Voice Note Toggle + STT Cleanup (W3-T1, W3-T2)

**File:** `5_WebApps/KhachLink/Pages/VoiceNote.razor`

**W3-T1: Toggle check:**
```razor
@inject ShopFeatureSettingsHttpService FeatureSettings

// In OnInitializedAsync:
var settings = await FeatureSettings.GetSettingsAsync(tenantId);
voiceNoteEnabled = settings?.Voice_Note_Enabled ?? false;

// In render:
@if (voiceNoteEnabled)
{
    <!-- Show "Ghi chú" button + STT UI -->
}
else
{
    <!-- Hide voice note, show text-only note input -->
}
```

**W3-T2: STT cleanup:**
- Remove any audio blob capture logic (if exists)
- Keep only STT (Web Speech API) → text transcription
- Text saved via `api/orders/{id}/note` (existing)
- KHÔNG populate `VoiceNoteAudioBlob` / `ItemNoteAudioBlob` fields

### 6.3. Domain Obsolete Mark (W3-T3)

**File:** `1_Shared/Domain.cs`

```csharp
// Line 730-731:
[Obsolete("Audio storage removed per requirements v1.2 — STT only. TTS reads text at kitchen.")]
public string? ItemNoteAudioBlob { get; private set; }

// Line 864-865:
[Obsolete("Audio storage removed per requirements v1.2 — STT only. TTS reads text at kitchen.")]
public string? VoiceNoteAudioBlob { get; private set; }
```

**Note:** `[Obsolete]` attribute trên property private set — build sẽ warning nhưng không error. KHÔNG xóa field.

### 6.4. TTS at Kitchen (W3-T4, W3-T5, W3-T6)

**W3-T4: Nút "Đọc ghi chú" trong Detail.razor**

**File:** `5_WebApps/ShopERP/Components/Pages/Orders/Detail.razor`

```razor
@if (order.Status?.Value == "confirmed" && !string.IsNullOrEmpty(order.VoiceNoteText) && voiceNoteEnabled)
{
    <VanAButton Variant="secondary" OnClick="ReadVoiceNote" data-testid="btn-read-note">
        🔊 Đọc ghi chú
    </VanAButton>
}
```

**W3-T5: TTS implementation via JS interop**

**File:** `5_WebApps/ShopERP/wwwroot/js/tts-reader.js` (NEW)

```javascript
window.ttsReader = {
    speak: function (text, lang = 'vi-VN') {
        if ('speechSynthesis' in window) {
            const utterance = new SpeechSynthesisUtterance(text);
            utterance.lang = lang;
            utterance.rate = 1.0;
            window.speechSynthesis.speak(utterance);
            return true;
        }
        return false;
    },
    cancel: function () {
        if ('speechSynthesis' in window) {
            window.speechSynthesis.cancel();
        }
    }
};
```

**Blazor interop in Detail.razor:**
```csharp
[Inject] IJSRuntime JS { get; set; }

private async Task ReadVoiceNote()
{
    await JS.InvokeVoidAsync("ttsReader.speak", order.VoiceNoteText, "vi-VN");
}
```

**W3-T6: TTS sub-toggle**

TTS là sub-toggle của `Voice_Note_Enabled` — khi voice OFF, TTS cũng OFF. Không cần toggle riêng trong Shop Settings (đơn giản hóa). Logic: `voiceNoteEnabled = settings?.Voice_Note_Enabled ?? false` — dùng cho cả STT (client) và TTS (kitchen).

### 6.5. QR Table Number (W3-T7, W3-T8, W3-T9)

**W3-T7: QRCodePayload DTO**

**File:** `1_Shared/DTOs/QRCodePayload.cs`

```csharp
public class QRCodePayload
{
    public Guid ProductId { get; set; }
    public Guid ShopId { get; set; }
    public DateTime Timestamp { get; set; }
    public string? TableNumber { get; set; } // Optional — only when QR_TableNumber_Enabled = ON
}
```

**W3-T8: QR generation**

Tìm QR generation logic (admin page hoặc service). Khi `QR_TableNumber_Enabled` = ON, include `TableNumber` trong payload JSON. Khi OFF, không include (null).

**W3-T9: KhachLink Scan page**

**File:** `5_WebApps/KhachLink/Pages/Scan.razor`

```razor
@if (!string.IsNullOrEmpty(payload.TableNumber))
{
    <VanAAlert Variant="info" Message="@($"Bàn số: {payload.TableNumber}")" data-testid="table-number-display" />
}
```

---

## 7. AI HEALTH CHECK MATRIX

### Pre-ANALYZE
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: `VoiceNote.razor` (410 lines) có STT bằng Web Speech API (lines 154-229), text lưu qua `api/orders/{id}/note` (line 185-200) (subagent A)
  - Fact 2: `QRCodePayload` (`1_Shared/DTOs/QRCodePayload.cs` lines 7-36) chỉ có `ProductId, ShopId, Timestamp` — không có `TableNumber` (subagent A)
  - Fact 3: Domain có `OrderItem.ItemNoteAudioBlob` + `Order.VoiceNoteAudioBlob` (`1_Shared/Domain.cs` lines 730-731, 864-865) (subagent B)
  - Fact 4: `Order.VoiceNoteText` tồn tại trong Domain (line 864) — dùng cho TTS
- **Assumptions:**
  - Assumption 1: `speechSynthesis` API available trong Blazor Server JS interop (browser native — high confidence)
  - Assumption 2: QR generation logic tồn tại ở admin page (Cần verify location)
- **Open Questions:**
  - Q1: QR generation logic ở đâu? (admin page? service? controller?)
  - Q2: `VoiceNote.razor` có audio blob capture logic không? (subagent nói "no evidence" nhưng cần verify full file)
- **Gate check:** Assumptions (2) < Verified Facts (4) → ✅ OK để proceed IMPLEMENT sau khi verify Q1-Q2

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `VoiceNote.razor` (toggle + cleanup) | Low — conditional render + remove unused code | None |
| `Scan.razor` (table number display) | Low — additive display | None |
| `QRCodePayload.cs` (add field) | Low — optional field, backward compat | None |
| `Domain.cs` (Obsolete attribute) | Low — warning only, no error | None |
| `Detail.razor` (TTS button) | Low — additive button | None |
| `tts-reader.js` (new) | No impact — new file | None |
| `ShopFeatures.razor` (sub-toggle if needed) | Low — additive | None |

---

## 9. EXECUTION CHECKLIST

### ANALYZE Phase
- [ ] Read `VoiceNote.razor` full (410 lines) — STT + audio logic
- [ ] Read `Scan.razor` lines 75-124 — QR parsing
- [ ] Read `QRCodePayload.cs` full
- [ ] Read `Domain.cs` lines 730-731, 864-865
- [ ] Read `Detail.razor` — kitchen buttons (from Wave 1)
- [ ] Find QR generation logic (admin page? service?)
- [ ] Check `5_WebApps/ShopERP/wwwroot/js/` — existing JS pattern
- [ ] Update Health Check Matrix

### IMPLEMENT Phase
- [ ] W3-T1: Voice note toggle ON/OFF
- [ ] W3-T2: STT cleanup (remove audio logic if any)
- [ ] W3-T3: Domain Obsolete mark
- [ ] W3-T4: Detail.razor "Đọc ghi chú" button
- [ ] W3-T5: tts-reader.js + JS interop
- [ ] W3-T6: TTS sub-toggle (voice OFF → TTS OFF)
- [ ] W3-T7: QRCodePayload TableNumber field
- [ ] W3-T8: QR generation include table number when toggle ON
- [ ] W3-T9: Scan.razor display table number
- [ ] W3-T10: Build + guard-check.ps1 + Architecture Tests

### Post-IMPLEMENT
- [ ] Commit: `[KL WAVE 3] Voice note STT-only + TTS kitchen + QR table number`
- [ ] Update `project_state.md` (if user requests)

### Live Runtime Verification (MANDATORY — see Wave 0 lesson)
> Static checks (build + architecture tests + guard-check) KHÔNG đảm bảo runtime works.
> Phải boot app + test HTTP/UI thực tế trước khi mark wave COMPLETE.

**Prerequisites:**
- [ ] Docker Desktop running (PostgreSQL 5432 + NATS 4222)
- [ ] ShopERP started on http://localhost:5003
- [ ] KhachLink started on http://localhost:5002
- [ ] DevLogin admin trên ShopERP + customer login trên KhachLink
- [ ] Browser có Web Speech API support (Chrome/Edge — `window.speechSynthesis` + `window.SpeechRecognition`)
- [ ] Order có VoiceNote từ customer (seed hoặc tạo qua UI)

**RV tests (all MUST pass):**
- [ ] **RV1 — Voice note toggle OFF:** Set `Voice_Note_Enabled=false` → KhachLink Checkout/OrderNote → VoiceNote component KHÔNG hiển thị → chỉ text input
- [ ] **RV2 — Voice note toggle ON (STT):** Set `Voice_Note_Enabled=true` → VoiceNote component hiển thị → click mic → SpeechRecognition transcribe → text hiển thị trong textarea
- [ ] **RV3 — No audio storage:** Sau STT → inspect Network tab → KHÔNG có upload audio file (chỉ text transcript gửi qua API) → verify request body chỉ chứa `noteText` string
- [ ] **RV4 — TTS kitchen (toggle ON):** ShopERP Order Detail (order có voice note) → nút "Đọc ghi chú" hiển thị → click → `window.speechSynthesis.speak()` called → verify JS console log `TTS speaking: <text>`
- [ ] **RV5 — TTS sub-toggle (voice OFF):** Set `Voice_Note_Enabled=false` → ShopERP Order Detail → nút "Đọc ghi chú" KHÔNG hiển thị (TTS phụ thuộc voice toggle)
- [ ] **RV6 — QR table number (toggle ON):** Set `QR_TableNumber_Enabled=true` → generate QR code → scan → KhachLink Scan.razor hiển thị "Bàn số: X" (table number extracted từ QR payload)
- [ ] **RV7 — QR table number (toggle OFF):** Set `QR_TableNumber_Enabled=false` → generate QR → scan → KHÔNG hiển thị table number (QR payload không có field)
- [ ] **RV8 — Domain Obsolete:** `VoiceNote` entity (nếu có audio field) marked `[Obsolete("Audio storage removed per D5 — STT-only + TTS")]` — build warning xuất hiện nhưng không error
- [ ] **RV9 — JS interop:** `tts-reader.js` loaded trong ShopERP wwwroot → browser console không error `Cannot find module tts-reader`
- [ ] **RV10 — EF Migration:** Nếu có entity change (QRCodePayload) → migration applied (no `no such table` error)
- [ ] **RV11 — LINQ translation:** Mọi query mới dùng direct comparison — no `InvalidOperationException`
- [ ] **RV12 — UI Platform:** VoiceNote.razor + Scan.razor dùng VanA components (no custom HTML)
