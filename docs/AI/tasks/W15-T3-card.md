# TASK CARD: PRODUCTION_HYGIENE - WAVE15 - Rewrite VoiceNote.razor

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Rewrite `VoiceNote.razor` để fix 4 vi phạm kiến trúc đồng thời preserve toàn bộ business logic voice recognition — file giữ nghiệp vụ thật, chỉ sai implementation
- **Nghiệp vụ áp dụng:** KDS Voice Note — `functional-requirements.md §1.3`: *"Ghi chú đơn hàng (text + voice)"* — cho phép staff/khách thêm ghi chú giọng nói vào đơn hàng
- **Master plan:** `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` § 9. WAVE 15 — task W15-T3
- **Depends on:** W15-T2 (app phải khởi động clean)
- **E2E contract:** `6_Testing/e2e-tests/voice-command.spec.ts` — TC_Voice_Flow, TC_Voice_TextCommand

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Fix_Errors.md`
- **Execution Mode:** FIX_ONLY (fix kiến trúc, KHÔNG thay đổi business logic)

## 3. PHÂN TÍCH 4 VI PHẠM CẦN FIX

### Vi phạm 1: `<html><head><body>` trong Razor component
```razor
// HIỆN TẠI — SAI: Razor component KHÔNG được có html/head/body tags
@page "/voice-note"
<html lang="vi" class="notranslate" translate="no">
<head>...</head>
<body>
    <div class="voice-note-container">...</div>
    @code { ... }
    <script>...</script>
    <style>...</style>
</body>

// SAU — ĐÚNG: Chỉ có content, layout được inject từ _Imports.razor
@page "/voice-note"
<PageTitle>Voice Note - Vạn An</PageTitle>
<div class="voice-note-container" data-testid="voice-note-container">...</div>
@code { ... }
```

### Vi phạm 2: `@inject HttpClient Http` — direct inject thay vì named client
```csharp
// HIỆN TẠI — SAI: Bypass "gateway" named HttpClient
@inject HttpClient Http

// SAU — ĐÚNG: Dùng IHttpClientFactory với named client "gateway"
@inject IHttpClientFactory HttpClientFactory

// Trong @code:
private HttpClient Http => HttpClientFactory.CreateClient("gateway");
```
**Lý do:** `Program.cs` đã register `"gateway"` named client với `BaseAddress` pointing đến Gateway. Direct `HttpClient` inject sẽ không có `BaseAddress` → request sẽ fail.

### Vi phạm 3: Endpoint sai — `POST /api/orders/voice-note` không tồn tại
```csharp
// HIỆN TẠI — SAI: Endpoint này không có trong Gateway hay ShopERP
await Http.PostAsJsonAsync("/api/orders/voice-note", voiceNote);

// SAU — ĐÚNG: Endpoint đã verified trong voice-command.spec.ts TC_Voice_TextCommand
await Http.PostAsJsonAsync("/api/v1/voicecommand/text-command", new
{
    CommandText = transcriptionText,
    OrderId = OrderId ?? string.Empty,
    Parameters = "voice_note"
});
```
**Source:** `voice-command.spec.ts` line 56: `request.post(\`${config.GATEWAY_URL}/api/v1/voicecommand/text-command\`)`

### Vi phạm 4: `alert()` native JavaScript — vi phạm UI Platform
```csharp
// HIỆN TẠI — SAI: alert() native bypass UI Platform
await JSRuntime.InvokeVoidAsync("alert", "Ghi chú đã được gửi thành công!");
await JSRuntime.InvokeVoidAsync("alert", "Lỗi khi gửi ghi chú...");

// SAU — ĐÚNG: State variable + VanAnAlert component
private string? _successMessage;
private string? _errorMessage;
// Render: <VanAnAlert Variant="AlertVariant.Success" Message="@_successMessage" />
//         <VanAnAlert Variant="AlertVariant.Danger" Message="@_errorMessage" />
```

## 4. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Pages/VoiceNote.razor` (REWRITE)
  - `5_WebApps/KhachLink/Program.cs` (đọc để confirm "gateway" named client tồn tại — line ~93)
  - `5_WebApps/KhachLink/_Imports.razor` (đọc để biết namespaces và @using đã có)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Files đọc để hiểu UI Platform components:**
  - `UI.Platform/Components/Atomic/VanAnAlert.razor` (nếu cần xem interface)
- **KHÔNG được sửa:**
  - Business logic voice recognition (JS `initializeSpeechRecognition`, `startRecording`, `stopRecording`) — giữ nguyên
  - `[JSInvokable] SetTranscriptionText()` — giữ nguyên (E2E mock cần function này)
  - `1_Shared/Domain.cs` — Domain Layer Protection
  - Bất kỳ file nào khác

## 5. TEMPLATE REWRITE (TARGET STATE)

```razor
@page "/voice-note"
@using VanAn.KhachLink.Services
@inject IHttpClientFactory HttpClientFactory
@inject IJSRuntime JSRuntime
@inject NavigationManager NavigationManager

<PageTitle>Voice Note - Vạn An</PageTitle>

@* Alerts thay thế alert() native *@
@if (!string.IsNullOrEmpty(_successMessage))
{
    <VanAnAlert Variant="AlertVariant.Success" Message="@_successMessage" />
}
@if (!string.IsNullOrEmpty(_errorMessage))
{
    <VanAnAlert Variant="AlertVariant.Danger" Message="@_errorMessage" />
}

<div class="voice-note-container" data-testid="voice-note-container">
    <div class="voice-header">
        <h2 data-testid="voice-note-header">🎤 Ghi chú giọng nói</h2>
        <p>Thêm ghi chú giọng nói cho đơn hàng của bạn</p>
    </div>

    <div class="voice-controls">
        <VanAnButton OnClick="ToggleRecording" Disabled="@isRecording">
            @(isRecording ? "⏹️ Dừng ghi âm" : "🎤 Bắt đầu ghi âm")
        </VanAnButton>
        <VanAnButton Variant="ButtonVariant.Outline" OnClick="ClearRecording" Disabled="@isRecording">
            🗑️ Xóa
        </VanAnButton>
    </div>

    <div class="voice-status">
        @if (isRecording)
        {
            <div class="recording-indicator">
                <span class="recording-dot"></span>
                <span>Đang ghi âm...</span>
            </div>
        }

        @if (!string.IsNullOrEmpty(transcriptionText))
        {
            <div class="transcription-result">
                <h4>Văn bản chuyển đổi:</h4>
                <p data-testid="transcription-text">@transcriptionText</p>
            </div>
        }
    </div>

    <div class="voice-actions">
        <VanAnButton OnClick="SubmitVoiceNote" Disabled="@string.IsNullOrEmpty(transcriptionText)">
            📤 Gửi ghi chú
        </VanAnButton>
        <VanAnButton Variant="ButtonVariant.Outline" OnClick="Cancel">
            ❌ Hủy
        </VanAnButton>
    </div>
</div>

@code {
    [Parameter] public string? OrderId { get; set; }

    private bool isRecording = false;
    private string transcriptionText = "";
    private string? _successMessage;
    private string? _errorMessage;
    private DotNetObjectReference<VoiceNote>? dotNetRef;

    private HttpClient Http => HttpClientFactory.CreateClient("gateway");

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
        {
            dotNetRef = DotNetObjectReference.Create(this);
            JSRuntime.InvokeVoidAsync("initializeSpeechRecognition", dotNetRef);
        }
    }

    private async Task ToggleRecording()
    {
        if (!isRecording)
        {
            isRecording = true;
            await JSRuntime.InvokeVoidAsync("startRecording");
        }
        else
        {
            isRecording = false;
            await JSRuntime.InvokeVoidAsync("stopRecording");
        }
        StateHasChanged();
    }

    [JSInvokable]
    public void SetTranscriptionText(string text)
    {
        transcriptionText = text;
        StateHasChanged();
    }

    private void ClearRecording()
    {
        transcriptionText = "";
        _successMessage = null;
        _errorMessage = null;
        StateHasChanged();
    }

    private async Task SubmitVoiceNote()
    {
        if (string.IsNullOrEmpty(transcriptionText)) return;
        try
        {
            var payload = new
            {
                CommandText = transcriptionText,
                OrderId = OrderId ?? string.Empty,
                Parameters = "voice_note"
            };
            var response = await Http.PostAsJsonAsync("/api/v1/voicecommand/text-command", payload);
            if (response.IsSuccessStatusCode)
            {
                _successMessage = "Ghi chú đã được gửi thành công!";
                _errorMessage = null;
                StateHasChanged();
                await Task.Delay(1500);
                NavigationManager.NavigateTo("/");
            }
            else
            {
                _errorMessage = $"Lỗi khi gửi ghi chú (HTTP {(int)response.StatusCode}). Vui lòng thử lại.";
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            _errorMessage = "Lỗi kết nối. Vui lòng thử lại.";
            Console.WriteLine($"VoiceNote submit error: {ex.Message}");
            StateHasChanged();
        }
    }

    private void Cancel() => NavigationManager.NavigateTo("/");

    public void Dispose() => dotNetRef?.Dispose();
}
```

> **JS block (giữ nguyên hoàn toàn):** `initializeSpeechRecognition`, `startRecording`, `stopRecording` — không thay đổi bất kỳ dòng nào.

## 6. BƯỚC THỰC HIỆN

```
S1: Đọc VoiceNote.razor hiện tại + _Imports.razor
    → Xác nhận namespace VanAnAlert, VanAnButton đã có trong _Imports.razor
    → Xác nhận "gateway" named client trong Program.cs

S2: Rewrite VoiceNote.razor theo template ở mục 5
    → Giữ nguyên toàn bộ <script> JS block
    → Giữ nguyên toàn bộ <style> CSS block
    → Chỉ thay đổi: HTML structure, @inject, @code

S3: dotnet build VanAn.sln
    → Fix bất kỳ compile error nào (thường do namespace VanAnAlert/ButtonVariant)

S4: Commit
    → "[W15-T3] Rewrite VoiceNote.razor — fix arch violations (endpoint, HttpClient, html tags, alert)"
```

## 7. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** Không có `<html>`, `<head>`, `<body>` tags trong `VoiceNote.razor`
- [ ] **SC2:** `@inject HttpClient Http` không còn — thay bằng `@inject IHttpClientFactory HttpClientFactory`
- [ ] **SC3:** Endpoint là `/api/v1/voicecommand/text-command` (khớp `voice-command.spec.ts`)
- [ ] **SC4:** Không có `JSRuntime.InvokeVoidAsync("alert", ...)` — thay bằng `VanAnAlert`
- [ ] **SC5:** `[JSInvokable] SetTranscriptionText()` vẫn tồn tại (E2E mock cần)
- [ ] **SC6:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC7:** `PRODUCTION_HYGIENE_master_plan.md` updated W15-T3 = ✅ DONE

**Implementation Date:** TBD
**Branch:** `feature/wave15-khachlink-page-cleanup`

## 8. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Verify VanAnAlert/VanAnButton usage đúng chuẩn
- `build-error-analysis` — Fix namespace/component errors nhanh
- `domain-integrity-validation` — Verify HttpClient sử dụng đúng named client

## 9. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `voice-command.spec.ts` TC_Voice_TextCommand dùng endpoint `POST /api/v1/voicecommand/text-command` — confirmed (line 56)
  - Fact 2: `Program.cs` line ~93: `builder.Services.AddHttpClient("gateway", ...)` — "gateway" named client tồn tại
  - Fact 3: `[JSInvokable] SetTranscriptionText()` được mock trong E2E (voice-command.spec.ts line 178) — phải giữ nguyên
  - Fact 4: `functional-requirements.md §1.3` xác nhận voice note là nghiệp vụ thật (KDS)
  - Fact 5: `_Imports.razor` có `@using VanAn.UI.Platform` (cần verify trước khi dùng VanAnAlert)
- **Assumptions:**
  - `VanAnAlert` có parameter `Variant` và `Message` (cần verify từ UI.Platform source)
  - `VanAnButton` có parameter `Disabled` (cần verify)
- **Open Questions:**
  - Q1: `VanAnAlert` exact API — `Variant` enum values? (đọc `UI.Platform/Components/Atomic/VanAnAlert.razor` trong S1)
  - Q2: `VanAnButton` `Disabled` parameter tên chính xác?
- **Recommended Action:** IMPLEMENT — facts đủ mạnh, assumptions dễ verify trong S1

## 10. REVERSE IMPACT ANALYSIS
| Thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Xóa `@inject HttpClient Http` | Không có component khác phụ thuộc vào inject này | None |
| Đổi endpoint | E2E `TC_Voice_Flow` mock `webkitSpeechRecognition` — không gọi endpoint trực tiếp trong browser test. `TC_Voice_TextCommand` gọi endpoint trực tiếp qua `request.post()` — endpoint đúng | None |
| `VanAnAlert` thay `alert()` | UX thay đổi từ browser dialog → inline alert — phù hợp production | None |
| Xóa `<html><head><body>` | Layout inject từ `_Imports.razor` — cần verify không có `@layout` conflict | Kiểm tra `_Imports.razor` trong S1 |

## 11. ESTIMATED EFFORT
- Medium effort — rewrite cẩn thận để preserve JS block
- 1–2 sessions
- **BLOCKER:** W15-T2 (app phải khởi động clean trước)
- **UNBLOCKS:** W15-T4 (verify toàn bộ wave)
