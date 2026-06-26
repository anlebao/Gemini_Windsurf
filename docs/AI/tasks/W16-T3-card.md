# TASK CARD: W16-T3 — Fix VoiceCommand.razor: @inject HttpClient → IHttpClientFactory

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thay `@inject HttpClient Http` (direct inject, anti-pattern trong Blazor WebApp) bằng `@inject IHttpClientFactory HttpClientFactory` trong `VoiceCommand.razor`. Fix URL API từ `/api/v1/orders/...` → `/api/orders/...` (Gateway path thật)
- **Nghiệp vụ áp dụng:** VoiceCommand component ghi chú đơn hàng bằng giọng nói. Khi user stop recording, transcript được `PUT` lên API để cập nhật order note — phải gọi đúng API
- **Master plan:** `docs/AI/tasks/KHACHLINK_PRODUCTION_PLAN.md` § W16-T3
- **Depends on:** Wave 15 complete (app routing ổn định)

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** FIX_ONLY (anti-pattern fix, không thêm feature)
- **Execution Mode:** FIX_ONLY — fix 2 dòng, không redesign

## 3. PHÂN TÍCH HIỆN TRẠNG

### VoiceCommand.razor — Vi phạm
```razor
// Line 3 — HIỆN TẠI — SAI
@inject HttpClient Http
```
```csharp
// Line 192 — HIỆN TẠI
await Http.PutAsJsonAsync($"/api/v1/orders/{currentOrderId}/note", updateData);
//                        ^^^^^^^^^^^^ path sai (không có /v1 trong Gateway)
```

### Tại sao `@inject HttpClient Http` là anti-pattern?
- Blazor WebApp (.NET 8) dùng `IHttpClientFactory` để quản lý connection pooling và named clients
- `@inject HttpClient Http` trong component inject singleton `HttpClient` không configured — không có base URL, không có timeout
- Architecture rule: KhachLink → Gateway qua named client `"gateway"` (đã registered trong `Program.cs`)
- Grep kết quả: chỉ **1 file** còn dùng `@inject HttpClient Http` trong KhachLink — `VoiceCommand.razor`

### API Path sai
```
HIỆN TẠI: PUT /api/v1/orders/{orderId}/note
ĐÚNG:     PUT /api/orders/{orderId}/note      ← Gateway không có prefix /v1
```

> **Note:** Cần verify Gateway `OrdersController` có endpoint `PUT /api/orders/{id}/note` không. Nếu chưa có → tạo endpoint đơn giản.

## 4. QUYẾT ĐỊNH

| Item | Quyết định |
|------|-----------|
| `@inject HttpClient Http` | THAY bằng `@inject IHttpClientFactory HttpClientFactory` |
| `Http.PutAsJsonAsync(...)` | THAY bằng `HttpClientFactory.CreateClient("gateway").PutAsJsonAsync(...)` |
| `/api/v1/orders/{id}/note` | ĐỔI thành `/api/orders/{id}/note` |
| Gateway endpoint | VERIFY tồn tại; nếu không → tạo `PUT /api/orders/{id}/note` trong Gateway |

## 5. RELEVANT FILES

**Files được phép sửa:**
- `5_WebApps/KhachLink/Components/VoiceCommand.razor` (**SỬA** — thay inject + URL)
- `2_Gateway/Controllers/OrdersController.cs` (**SỬA nếu cần** — thêm PUT note endpoint)

**Files đọc để verify:**
- `5_WebApps/KhachLink/Program.cs` (xác nhận `"gateway"` named client registered)
- `2_Gateway/Controllers/OrdersController.cs` (kiểm tra PUT note endpoint)

**KHÔNG được sửa:**
- `1_Shared/Domain.cs`
- `3_CoreHub/` bất kỳ

## 6. TARGET STATE

### `VoiceCommand.razor` — Sau fix

```razor
@using VanAn.UI.Platform.Components.Atomic
@using VanAn.UI.Platform.Tokens
@inject IHttpClientFactory HttpClientFactory    ← THAY HttpClient Http
@inject IJSRuntime JSRuntime
@inject ILocalizationService LocalizationService
```

```csharp
private async Task UpdateOrderNote(string note)
{
    try
    {
        if (!string.IsNullOrEmpty(currentOrderId))
        {
            var updateData = new { OrderNote = note };
            var http = HttpClientFactory.CreateClient("gateway");    // ← THÊM dòng này
            await http.PutAsJsonAsync($"api/orders/{currentOrderId}/note", updateData);
            //                         ^^^^ không có leading /
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error updating order note: {ex.Message}");
    }
}
```

> **Note:** Named HttpClient `"gateway"` đã có `BaseAddress` configured → dùng relative URL `"api/orders/..."` (không có leading `/`)

### Gateway — Nếu endpoint `PUT /api/orders/{id}/note` chưa tồn tại

```csharp
// THÊM vào OrdersController.cs trong 2_Gateway
[HttpPut("{id}/note")]
public async Task<IActionResult> UpdateOrderNote(Guid id, [FromBody] UpdateNoteRequest req)
{
    var http = _factory.CreateClient("shoperp");
    var resp  = await http.PutAsJsonAsync($"api/orders/{id}/note", req);
    return resp.IsSuccessStatusCode ? Ok() : StatusCode((int)resp.StatusCode);
}

public record UpdateNoteRequest(string OrderNote);
```

## 7. BƯỚC THỰC HIỆN

```
S1: Verify Gateway OrdersController có PUT /{id}/note chưa
    → Grep 2_Gateway/Controllers/OrdersController.cs cho "note" hoặc "HttpPut"
    → Nếu CÓ: note path chính xác
    → Nếu KHÔNG: chuẩn bị thêm endpoint ở S3

S2: Sửa VoiceCommand.razor
    → Line 3: @inject HttpClient Http → @inject IHttpClientFactory HttpClientFactory
    → UpdateOrderNote(): thêm var http = HttpClientFactory.CreateClient("gateway")
    → Đổi Http.PutAsJsonAsync → http.PutAsJsonAsync
    → Đổi path "/api/v1/orders/..." → "api/orders/..."

S3: (Nếu cần) Thêm PUT endpoint vào Gateway OrdersController
    → Thêm [HttpPut("{id}/note")] + record UpdateNoteRequest

S4: Build
    → dotnet build VanAn.sln → 0 errors

S5: Anti-pattern check
    → Select-String "@inject HttpClient Http" trong 5_WebApps/KhachLink/ → 0 matches
    → Select-String "/api/v1" trong 5_WebApps/KhachLink/ → 0 matches

S6: Commit
    → "[W16-T3] Fix VoiceCommand: replace direct HttpClient with IHttpClientFactory(gateway)"
```

## 8. SUCCESS CRITERIA
- [ ] **SC1:** `VoiceCommand.razor` không còn `@inject HttpClient Http`
- [ ] **SC2:** `VoiceCommand.razor` inject `IHttpClientFactory HttpClientFactory`
- [ ] **SC3:** `UpdateOrderNote()` dùng `HttpClientFactory.CreateClient("gateway")`
- [ ] **SC4:** API path là `api/orders/{id}/note` (không có `/v1`, không có leading `/`)
- [ ] **SC5:** `Select-String "@inject HttpClient Http"` trong KhachLink → 0 kết quả
- [ ] **SC6:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC7:** `guard-check.ps1` → PASS

## 9. VERIFIED FACTS
- Fact 1: `VoiceCommand.razor` line 3: `@inject HttpClient Http` — confirmed
- Fact 2: `VoiceCommand.razor` line 192: `await Http.PutAsJsonAsync($"/api/v1/orders/{currentOrderId}/note", ...)` — confirmed
- Fact 3: Grep toàn KhachLink cho `@inject HttpClient Http` → chỉ 1 match ở VoiceCommand.razor — confirmed
- Fact 4: `KhachLink/Program.cs` register `"gateway"` named HttpClient với BaseAddress — confirmed (Wave 13)

## 10. ASSUMPTION (cần verify ở S1)
- Gateway `OrdersController` có thể chưa có `PUT /{id}/note` endpoint → cần verify trước khi sửa VoiceCommand

## 11. ESTIMATED EFFORT
- Very low effort — 1 file sửa 3 dòng, có thể thêm 1 endpoint nhỏ
- 0.25 session
- **BLOCKER:** Không có — có thể làm song song với W16-T2
