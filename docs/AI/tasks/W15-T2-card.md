# TASK CARD: W15-T2 — Modernize Program.cs (Blazor Web App Routing)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Sau khi W15-T1 xóa `_Host.cshtml` + `Index.cshtml`, migrate `Program.cs` sang Blazor Web App architecture: `AddRazorComponents()` + `MapRazorComponents<App>()`. Xóa `AddServerSideBlazor()`, `MapBlazorHub()`, `MapFallbackToPage("/Index")`. `App.razor` trở thành host page duy nhất.
- **Nghiệp vụ áp dụng:** Đảm bảo KhachLink khởi động clean và serve đúng page khi user truy cập `/`
- **Master plan:** `docs/AI/tasks/KHACHLINK_PRODUCTION_PLAN.md` § Wave 15 — task W15-T2
- **Depends on:** W15-T1 (xóa `_Host.cshtml`, `Index.cshtml`, `Components/Pages/Home.razor`) phải hoàn thành trước

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Fix_Errors.md`
- **Execution Mode:** FIX_ONLY (migration routing — không có new feature)

## 3. PHÂN TÍCH HIỆN TRẠNG

### Program.cs hiện tại (pattern cũ — Blazor Server)
```csharp
// HIỆN TẠI — Blazor Server pattern (lai tạp)
builder.Services.AddServerSideBlazor();
// ...
app.MapBlazorHub();
app.MapRazorPages();
app.MapFallbackToPage("/Index"); // ← BROKEN sau W15-T1
```

### Vấn đề sau W15-T1
- `MapFallbackToPage("/Index")` trỏ đến Razor Page `/Index` → không còn tồn tại → runtime exception
- `_Host.cshtml` đã bị xóa → Blazor Server không còn host page
- `AddServerSideBlazor()` + `MapBlazorHub()` là pattern cũ — không phù hợp Blazor Web App (.NET 8)
- `App.razor` hiện tại có thể đã dùng Blazor Web App conventions (cần verify)

### Giải pháp — Blazor Web App (.NET 8)
```csharp
// SAU — Blazor Web App pattern (đúng)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// ...
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
// NOTE: MapFallbackToPage, MapBlazorHub, AddServerSideBlazor removed.
// Blazor Router (Home.razor @page "/") handles root + unmatched routes.
```

> **Lý do migrate:** Blazor Web App (.NET 8) dùng `AddRazorComponents` + `MapRazorComponents<App>()` thay vì `AddServerSideBlazor` + `MapBlazorHub`. `App.razor` là host component duy nhất — không cần `_Host.cshtml`.

## 4. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `5_WebApps/KhachLink/Program.cs` (SỬA — migrate sang Blazor Web App)
  - `5_WebApps/KhachLink/Components/App.razor` (đọc — xác nhận `<Router>` + `<NotFound>` block, có thể cần update `blazor.web.js`)
  - `5_WebApps/KhachLink/Components/Routes.razor` (đọc nếu tồn tại — một số Blazor Web App template dùng Routes.razor)
  - `5_WebApps/KhachLink/KhachLink.csproj` (đọc — xác nhận `<Project Sdk="Microsoft.NET.Sdk.Web">`)
- **KHÔNG được sửa:**
  - `5_WebApps/KhachLink/Pages/Home.razor` — canonical page, không thay đổi
  - `5_WebApps/KhachLink/Pages/VoiceNote.razor` → W15-T3 lo
  - `5_WebApps/KhachLink/Pages/Dashboard.razor` (đã tạo ở W15-T1) — không thay đổi
  - `1_Shared/Domain.cs` — Domain Layer Protection
  - Bất kỳ file nào ngoài danh sách trên

## 5. THAY ĐỔI CỤ THỂ TRONG Program.cs

### Trước (Blazor Server — broken sau W15-T1)
```csharp
// Services
builder.Services.AddServerSideBlazor();

// Middleware pipeline
app.MapBlazorHub();
app.UseDefaultFiles();
app.MapRazorPages();
app.MapFallbackToPage("/Index"); // Broken — Index.cshtml đã xóa
```

### Sau (Blazor Web App — đúng)
```csharp
// Services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Middleware pipeline
app.UseDefaultFiles();
app.MapRazorPages();        // Giữ — còn Campaign.cshtml là Razor Page
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
// NOTE: MapFallbackToPage("/Index") removed — Index.cshtml deleted in W15-T1.
// NOTE: MapBlazorHub() removed — Blazor Web App uses MapRazorComponents<App>().
// NOTE: AddServerSideBlazor() removed — use AddRazorComponents() instead.
// Blazor Router (Home.razor @page "/") handles root + unmatched routes.
```

**Lưu ý:** `MapRazorPages()` giữ lại vì `Campaign.cshtml` vẫn là Razor Page trong Wave 16.

## 6. KIỂM TRA App.razor

Trước khi sửa, đọc `App.razor` để xác nhận:

**Nếu App.razor là Blazor Web App template (có `blazor.web.js`):**
```razor
<!DOCTYPE html>
<html>
<head>
    ...
    <HeadOutlet />
</head>
<body>
    <Routes />
    <script src="_framework/blazor.web.js"></script>
</body>
</html>
```
→ Không cần sửa App.razor.

**Nếu App.razor là Blazor Server cũ (có `blazor.server.js`):**
```razor
<script src="_framework/blazor.server.js"></script>
```
→ Đổi sang `blazor.web.js`.

**Nếu App.razor sử dụng `<Router AppAssembly="@typeof(App).Assembly">`:**
→ Giữ nguyên — Router pattern này tương thích với cả hai loại.

## 7. BƯỚC THỰC HIỆN

```
S1: Đọc Program.cs toàn bộ
    → Xác định chính xác vị trí AddServerSideBlazor, MapBlazorHub, MapFallbackToPage
    → Note các services khác đang inject (HttpClient, Auth, v.v.)

S2: Đọc App.razor
    → Xác nhận script tag: blazor.web.js hay blazor.server.js?
    → Xác nhận có <HeadOutlet /> và <Routes /> hay <Router>?

S3: Sửa Program.cs
    → Thay AddServerSideBlazor() → AddRazorComponents().AddInteractiveServerComponents()
    → Xóa app.MapBlazorHub()
    → Xóa app.MapFallbackToPage("/Index")
    → Thêm app.MapRazorComponents<App>().AddInteractiveServerRenderMode()
    → Thêm comments giải thích

S4: Sửa App.razor nếu cần (blazor.server.js → blazor.web.js)

S5: dotnet build VanAn.sln
    → 0 errors bắt buộc
    → Fix ngay nếu có compile error liên quan đến migration

S6: Commit
    → "[W15-T2] Migrate KhachLink to Blazor Web App routing (AddRazorComponents + MapRazorComponents<App>)"
```

## 8. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `Program.cs` không còn `AddServerSideBlazor()`
- [ ] **SC2:** `Program.cs` không còn `MapBlazorHub()`
- [ ] **SC3:** `Program.cs` không còn `MapFallbackToPage("/Index")`
- [ ] **SC4:** `Program.cs` có `AddRazorComponents().AddInteractiveServerComponents()`
- [ ] **SC5:** `Program.cs` có `MapRazorComponents<App>().AddInteractiveServerRenderMode()`
- [ ] **SC6:** `App.razor` dùng `blazor.web.js` (nếu cần thay đổi)
- [ ] **SC7:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC8:** KhachLink khởi động không có runtime exception liên quan đến routing
- [ ] **SC9:** `Home.razor` vẫn registered tại `@page "/"` và `@page "/home"` — không thay đổi
- [ ] **SC10:** `Campaign.cshtml` vẫn hoạt động qua `MapRazorPages()`

**Implementation Date:** TBD
**Branch:** `feature/wave15-khachlink-page-cleanup`

## 9. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Fix compile errors nếu phát sinh sau migration
- `pattern-based-fixing` — Follow Blazor Web App patterns từ ShopERP Program.cs (nếu có)

## 10. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: `Program.cs` có `AddServerSideBlazor()` + `MapBlazorHub()` + `MapFallbackToPage("/Index")`
  - Fact 2: .NET 8 Blazor Web App dùng `AddRazorComponents()` + `MapRazorComponents<App>()`
  - Fact 3: `_Host.cshtml` đã bị xóa ở W15-T1 → không còn host page cho Blazor Server
  - Fact 4: `Campaign.cshtml` vẫn là Razor Page → cần giữ `MapRazorPages()`
- **Assumptions:**
  - `App.razor` tồn tại và có `<Router>` hoặc `<Routes>` block
  - `KhachLink.csproj` đã có `Microsoft.NET.Sdk.Web` SDK (tương thích Blazor Web App)
  - Không có middleware nào phụ thuộc trực tiếp vào `MapBlazorHub()` path `/blazor`
- **Open Questions:**
  - Q1: `App.razor` hiện dùng `blazor.server.js` hay `blazor.web.js`? (verify ở S2)
  - Q2: Có `Routes.razor` không? (một số Blazor Web App template tách Router ra `Routes.razor`)
- **Recommended Action:** IMPLEMENT — nhưng phải đọc kỹ `App.razor` trước (Q1 critical)

## 11. REVERSE IMPACT ANALYSIS
| Thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `AddRazorComponents` thay `AddServerSideBlazor` | DI registration khác nhau; một số services `ICircuitHandler` không còn đăng ký tự động | Verify không có custom `CircuitHandler` inject trong KhachLink |
| Xóa `MapBlazorHub()` | SignalR `/blazor` hub path không còn → `blazor.server.js` clients fail | Dùng `blazor.web.js` thay — compatible với `MapRazorComponents` |
| Xóa `MapFallbackToPage("/Index")` | Unmatched routes → Blazor Router handle | Home.razor `@page "/"` xử lý root; `<NotFound>` block xử lý routes khác |
| Giữ `MapRazorPages()` | `Campaign.cshtml` vẫn hoạt động | None |

## 12. ESTIMATED EFFORT
- Medium effort — ~10 dòng thay đổi + verify
- 1 session (~ 1 giờ kể cả verify)
- **BLOCKER:** W15-T1 phải hoàn thành (_Host.cshtml + Index.cshtml phải đã xóa)
- **UNBLOCKS:** W15-T3 (cần app khởi động clean để test VoiceNote)
