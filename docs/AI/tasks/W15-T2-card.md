# TASK CARD: PRODUCTION_HYGIENE - WAVE15 - Fix Program.cs Routing Fallback

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Sau khi `Index.cshtml` bị xóa ở W15-T1, `MapFallbackToPage("/Index")` trong `Program.cs` sẽ throw runtime exception. Task này fix routing để `Home.razor` là canonical entry point cho mọi unmatched route.
- **Nghiệp vụ áp dụng:** Đảm bảo KhachLink khởi động clean và serve đúng page khi user truy cập `/`
- **Master plan:** `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` § 9. WAVE 15 — task W15-T2
- **Depends on:** W15-T1 (xóa `Index.cshtml`) phải hoàn thành trước

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Fix_Errors.md`
- **Execution Mode:** FIX_ONLY

## 3. PHÂN TÍCH HIỆN TRẠNG

### Program.cs hiện tại (lines 169–183)
```csharp
// PROPER RAZOR PAGES ROUTING - ANTI-CHEATING RULE #2
_ = app.UseDefaultFiles();
_ = app.MapRazorPages();
_ = app.MapFallbackToPage("/Index"); // ← BROKEN sau khi xóa Index.cshtml
_ = app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", ... }));
```

### Vấn đề sau W15-T1
- `MapFallbackToPage("/Index")` gọi đến Razor Page `/Index` → không còn tồn tại → runtime exception khi navigate đến unmatched route
- `Home.razor` (`@page "/"` và `@page "/home"`) là Blazor component — không phải Razor Page → **không thể dùng `MapFallbackToPage`**
- `MapRazorPages()` đã có → serve Razor Pages còn lại
- `MapBlazorHub()` (line 159) đã có → Blazor Server circuit hoạt động

### Giải pháp đúng
Xóa `MapFallbackToPage("/Index")` — Blazor Router trong `App.razor` (hoặc `_Host`) tự handle fallback. `Home.razor` có `@page "/"` sẽ match tất cả root requests. Unmatched routes sẽ được Blazor Router xử lý bằng `<NotFound>` block.

> **Lưu ý kiến trúc:** KhachLink dùng Blazor Server (`AddServerSideBlazor`). Fallback routing cho Blazor Server không dùng `MapFallbackToPage` — thay vào đó Blazor Router tự handle sau khi `MapBlazorHub()` registered.

## 4. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Program.cs` (SỬA — xóa/thay `MapFallbackToPage`)
  - `5_WebApps/KhachLink/Components/` (kiểm tra `App.razor` có `<NotFound>` block)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **KHÔNG được sửa:**
  - `5_WebApps/KhachLink/Pages/Home.razor` — không thay đổi
  - `1_Shared/Domain.cs` — Domain Layer Protection
  - Bất kỳ file nào ngoài danh sách trên

## 5. THAY ĐỔI CỤ THỂ TRONG Program.cs

### Trước (broken sau W15-T1)
```csharp
// PROPER RAZOR PAGES ROUTING - ANTI-CHEATING RULE #2
_ = app.UseDefaultFiles();
_ = app.MapRazorPages();
_ = app.MapFallbackToPage("/Index"); // Broken — Index.cshtml đã xóa
_ = app.MapGet("/health", () => Results.Ok(...));
```

### Sau (đúng)
```csharp
// Razor Pages routing
_ = app.UseDefaultFiles();
_ = app.MapRazorPages();
// NOTE: MapFallbackToPage("/Index") removed — Index.cshtml deleted in W15-T1.
// Blazor Router (Home.razor @page "/") handles root + unmatched routes.
_ = app.MapGet("/health", () => Results.Ok(...));
```

**Thay đổi duy nhất:** Xóa dòng `_ = app.MapFallbackToPage("/Index");` và thêm comment giải thích.

## 6. KIỂM TRA App.razor / Blazor Router

Trước khi sửa, đọc `App.razor` (nếu tồn tại) hoặc tìm Blazor Router configuration để xác nhận `<NotFound>` được handle:

```powershell
Get-ChildItem -Recurse -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink" -Filter "App.razor"
```

Nếu không có `App.razor` riêng → Blazor Server sử dụng `_Imports.razor` + Hub route → `MapBlazorHub()` đủ để handle routing. `Home.razor @page "/"` làm default.

## 7. BƯỚC THỰC HIỆN

```
S1: Đọc toàn bộ Program.cs (context đầy đủ)
    → Xác định chính xác dòng MapFallbackToPage

S2: Kiểm tra App.razor tồn tại không
    → Nếu có: đọc để xác nhận <NotFound> block
    → Nếu không có: Blazor Hub route đủ

S3: Sửa Program.cs
    → Xóa dòng: _ = app.MapFallbackToPage("/Index");
    → Thêm comment giải thích

S4: dotnet build VanAn.sln
    → 0 errors bắt buộc

S5: Commit
    → "[W15-T2] Fix KhachLink routing — remove MapFallbackToPage after Index.cshtml deletion"
```

## 8. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `Program.cs` không còn dòng `MapFallbackToPage("/Index")`
- [ ] **SC2:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC3:** KhachLink khởi động không có runtime exception liên quan đến routing
- [ ] **SC4:** `Home.razor` vẫn registered tại `@page "/"` và `@page "/home"` (không thay đổi)
- [ ] **SC5:** `PRODUCTION_HYGIENE_master_plan.md` updated W15-T2 = ✅ DONE

**Implementation Date:** TBD
**Branch:** `feature/wave15-khachlink-page-cleanup`

## 9. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Fix compile errors nếu phát sinh
- `pattern-based-fixing` — Follow existing Program.cs patterns

## 10. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: `Program.cs` line 175: `_ = app.MapFallbackToPage("/Index");` — confirmed
  - Fact 2: `Program.cs` line 159: `_ = app.MapBlazorHub();` — Blazor Hub đã registered
  - Fact 3: `Home.razor` có `@page "/"` và `@page "/home"` — Blazor Router sẽ match
  - Fact 4: `MapFallbackToPage` chỉ dùng cho Razor Pages, không dùng cho Blazor components
- **Assumptions:**
  - Không có Razor Page nào khác phụ thuộc vào `MapFallbackToPage("/Index")`
- **Open Questions:**
  - Q1: `App.razor` có tồn tại không? (kiểm tra trong S2)
- **Recommended Action:** IMPLEMENT — change rõ ràng, risk thấp

## 11. REVERSE IMPACT ANALYSIS
| Thay đổi | Reverse impact | Mitigation |
|---|---|---|
| Xóa `MapFallbackToPage("/Index")` | Unmatched routes → Blazor Router handle thay vì redirect về Razor Page | Home.razor `@page "/"` xử lý root. NotFound block xử lý routes khác |
| Giữ nguyên `MapRazorPages()` | Các Razor Pages còn lại (`Campaign.cshtml`) vẫn hoạt động | None |

## 12. ESTIMATED EFFORT
- Very low effort — 1 dòng thay đổi + verify
- 1 session (< 30 phút)
- **BLOCKER:** W15-T1 phải hoàn thành (Index.cshtml phải đã xóa)
- **UNBLOCKS:** W15-T3 (cần app khởi động clean)
