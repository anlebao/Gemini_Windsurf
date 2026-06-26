# TASK CARD: W15-T1 — Xóa Dead/Demo Pages + Convert Dashboard

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa 6 files dead/demo/orphan khỏi KhachLink và convert `Dashboard.cshtml` → `Dashboard.razor` để dứt điểm kiến trúc lai tạp, unblock Blazor Web App migration ở W15-T2
- **Nghiệp vụ áp dụng:** Production hygiene — loại bỏ code không được test, gây route conflict, vi phạm `VA-KHACHLINK-004` (KhachLink không được access DB trực tiếp)
- **Master plan:** `docs/AI/tasks/KHACHLINK_PRODUCTION_PLAN.md` § Wave 15 — task W15-T1
- **Nguồn phân tích:**
  - `docs/AI/e2e-gap-backlog.md` §Route #9: `Home.razor @page "/"` là canonical entry point
  - `docs/AI/tasks/TD-001_KhachLink_ArchitecturalViolation.md`: `VA-KHACHLINK-004` — KhachLink KHÔNG truy cập DB trực tiếp
  - `Index.cshtml.cs` line 8: TECH DEBT comment tự khai vi phạm `VA-KHACHLINK-004`
  - `6_Testing/e2e-tests/order-flow.spec.ts`: E2E chỉ test `Home.razor`
  - Quyết định kiến trúc 2026-06-26: `_Host.cshtml` xóa để dứt điểm lai tạp; `Dashboard.cshtml` convert sang `.razor`

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Fix_Errors.md` (xóa file + tạo page wrapper — không có new feature)
- **Execution Mode:** FIX_ONLY

## 3. FILES CẦN XÓA (6 files)

| File | Lý do xóa | Risk |
|------|-----------|------|
| `5_WebApps/KhachLink/Pages/Index.razor` | Route `@page "/"` conflict với `Home.razor`. `SampleProducts` hardcode với `Guid.NewGuid()`. `Checkout()` fake `Task.Delay(2000)` — không gọi API. Dead code. | LOW — shadow bởi `Home.razor` |
| `5_WebApps/KhachLink/Pages/IndexModern.cshtml` | Route `/modern`. Stats bịa: `1000+ quán`, `50K+/ngày`. Không có code-behind, không handler. Demo prototype. | ZERO — không có consumer |
| `5_WebApps/KhachLink/Pages/Index.cshtml` | Duplicate landing. `FeaturedProducts` hardcode `Guid.NewGuid()`. Social proof JS fake dùng `setInterval`. | LOW — fallback routing xử lý ở W15-T2 |
| `5_WebApps/KhachLink/Pages/Index.cshtml.cs` | Code-behind của `Index.cshtml`. Vi phạm `VA-KHACHLINK-004`: inject `IShopConfigService` từ CoreHub (bypasses Gateway). | LOW — cùng lúc với `Index.cshtml` |
| `5_WebApps/KhachLink/Pages/_Host.cshtml` | Orphan HTML shell. Không còn phù hợp với Blazor Web App routing. Nội dung marketing tĩnh không giá trị. Xóa để W15-T2 migration sạch. | LOW — không được route đến |
| `5_WebApps/KhachLink/Components/Pages/Home.razor` | Scaffold "Hello world" 5 dòng. `@page "/"` conflict trực tiếp với `Pages/Home.razor` đang live. Non-deterministic routing. | LOW — shadow bởi `Pages/Home.razor` |

## 4. FILE CẦN CONVERT

**`Pages/Dashboard.cshtml` → `Pages/Dashboard.razor`**

`Dashboard.cshtml` hiện tại là Razor Page wrapper nhúng `<RealTimeDashboard />`. Cần convert sang Blazor component để consistent với Blazor Web App architecture sau W15-T2.

### Target state — `Pages/Dashboard.razor`
```razor
@page "/dashboard"
@using VanAn.KhachLink.Components.Dashboard
@attribute [Authorize]

<PageTitle>Dashboard - Vạn An</PageTitle>
<RealTimeDashboard />
```

> **Note:** `RealTimeDashboard.razor` vẫn dùng `"demo-shop"` hardcode — sẽ được fix ở W16-T2. Task này chỉ tạo page wrapper đúng kiến trúc.

## 5. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc (context):**
  - `5_WebApps/KhachLink/Pages/Dashboard.cshtml` (đọc để hiểu content trước khi xóa)
  - `5_WebApps/KhachLink/Components/Dashboard/RealTimeDashboard.razor` (xác nhận namespace cho `@using`)
  - `5_WebApps/KhachLink/Pages/_Imports.razor` (verify không có reference đến files bị xóa)
  - `5_WebApps/KhachLink/Components/App.razor` (xác nhận `<AuthorizeRouteView>` tồn tại)
- **Files được phép xóa:** 6 files liệt kê ở mục 3
- **Files được phép tạo mới:** `5_WebApps/KhachLink/Pages/Dashboard.razor`
- **KHÔNG được sửa trong task này:**
  - `5_WebApps/KhachLink/Program.cs` → W15-T2 lo
  - `5_WebApps/KhachLink/Pages/Home.razor` → canonical page, KHÔNG CHẠM
  - `5_WebApps/KhachLink/Pages/VoiceNote.razor` → W15-T3 lo
  - `5_WebApps/KhachLink/Components/Dashboard/RealTimeDashboard.razor` → W16-T2 lo
  - `1_Shared/Domain.cs` → Domain Layer Protection

## 6. PRE-DELETE CHECKLIST (THỰC HIỆN TRƯỚC KHI XÓA)

```powershell
# Tìm references đến Index.razor
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*" -Pattern "Index\.razor|Pages\.Index" -Recurse

# Tìm references đến IndexModern
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*" -Pattern "IndexModern|\/modern" -Recurse

# Tìm references đến _Host.cshtml
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*" -Pattern "_Host|host\.cshtml" -Recurse

# Tìm references đến Index.cshtml / IndexModel
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*" -Pattern "IndexModel|Index\.cshtml" -Recurse

# Tìm references đến Components/Pages/Home.razor (scaffold)
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*" -Pattern "Components\.Pages\.Home|Components/Pages/Home" -Recurse

# Tìm references đến Dashboard.cshtml
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*" -Pattern "Dashboard\.cshtml" -Recurse
```

Nếu tìm thấy reference ngoài dự kiến → **STOP, báo cáo trước khi xóa.**

## 7. BƯỚC THỰC HIỆN (TUẦN TỰ)

```
S1: Pre-delete scan
    → Chạy grep commands ở mục 6
    → Xác nhận 0 external references (ngoài chính file đó)

S2: Đọc Dashboard.cshtml để capture namespace RealTimeDashboard
    → Xác nhận @using cần thiết cho Dashboard.razor

S3: Xóa 6 files
    → Xóa Index.razor
    → Xóa IndexModern.cshtml
    → Xóa _Host.cshtml
    → Xóa Index.cshtml
    → Xóa Index.cshtml.cs
    → Xóa Components/Pages/Home.razor
    → Xóa Dashboard.cshtml

S4: Tạo Pages/Dashboard.razor
    → Nội dung theo template ở mục 4

S5: Build check
    → dotnet build VanAn.sln
    → Nếu error liên quan đến files vừa xóa: fix ngay trong task này
    → Nếu error > 5 không liên quan: STOP, ghi report

S6: Commit
    → git add -A
    → "[W15-T1] Remove 6 dead/demo KhachLink pages, convert Dashboard.cshtml to Dashboard.razor"
```

## 8. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `Index.razor` không còn trong repository
- [ ] **SC2:** `IndexModern.cshtml` không còn trong repository
- [ ] **SC3:** `_Host.cshtml` không còn trong repository
- [ ] **SC4:** `Index.cshtml` và `Index.cshtml.cs` không còn trong repository
- [ ] **SC5:** `Components/Pages/Home.razor` không còn trong repository
- [ ] **SC6:** `Dashboard.cshtml` không còn trong repository
- [ ] **SC7:** `Pages/Dashboard.razor` tồn tại với `@page "/dashboard"` và `<RealTimeDashboard />`
- [ ] **SC8:** `dotnet build VanAn.sln` → 0 errors sau khi xóa + tạo mới
- [ ] **SC9:** `grep -r "IndexModel\|IndexModern\|_Host.cshtml\|Components/Pages/Home" KhachLink/` → 0 results

**Implementation Date:** TBD
**Branch:** `feature/wave15-khachlink-page-cleanup`

## 9. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Fix compile errors nếu phát sinh sau khi xóa
- `domain-integrity-validation` — Verify không ảnh hưởng Domain layer

## 10. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 6
- **Verified Facts:**
  - Fact 1: `e2e-gap-backlog.md` §Route #9 xác nhận `Home.razor` là canonical `@page "/"`
  - Fact 2: `Index.cshtml.cs` có TECH DEBT comment: *"KhachLink must not access DB directly (VA-KHACHLINK-004)"*
  - Fact 3: `Program.cs` có `MapFallbackToPage("/Index")` — W15-T2 sẽ replace toàn bộ routing
  - Fact 4: `order-flow.spec.ts` không có test nào navigate đến `/modern`, `_Host`, `Index.cshtml`
  - Fact 5: `Components/Pages/Home.razor` là scaffold 5 dòng, không phải `Pages/Home.razor` production
  - Fact 6: `Dashboard.cshtml` là Razor Page wrapper — cần convert sang Blazor để phù hợp Blazor Web App
- **Assumptions:**
  - Không có component nào `@inject` hoặc `@using` trực tiếp từ `IndexModel`
  - `Components/Dashboard/RealTimeDashboard.razor` tồn tại và import đúng namespace
- **Open Questions:**
  - Q1: `_Imports.razor` có `@layout` nào reference `_Host.cshtml` không? (kiểm tra ở Pre-delete scan)
  - Q2: Namespace chính xác của `RealTimeDashboard`? (verify ở S2)
- **Recommended Action:** IMPLEMENT — evidence rõ ràng, assumptions dễ verify

## 11. REVERSE IMPACT ANALYSIS
| File xóa/convert | Reverse impact | Mitigation |
|---|---|---|
| `Index.razor` (`@page "/"`) | Route `/` chỉ còn `Home.razor` — kết quả mong muốn | None |
| `IndexModern.cshtml` (`@page "/modern"`) | Route `/modern` → 404 — không có consumer | None |
| `_Host.cshtml` | Không được route đến → 0 impact trực tiếp; W15-T2 hoàn tất migration | W15-T2 |
| `Index.cshtml` + `.cshtml.cs` | `MapFallbackToPage("/Index")` broken → W15-T2 replace toàn bộ routing | W15-T2 |
| `Components/Pages/Home.razor` | Xóa route conflict `@page "/"` — chỉ còn `Pages/Home.razor` | None |
| `Dashboard.cshtml` → `Dashboard.razor` | Route `/dashboard` chuyển từ Razor Page sang Blazor component | Dashboard.razor tạo ngay trong task này |

## 12. ESTIMATED EFFORT
- Low effort — xóa 6 files + tạo 1 file nhỏ + grep verify
- 1 session
- **BLOCKER:** Không có — task độc lập
- **UNBLOCKS:** W15-T2 (cần biết `_Host.cshtml` + `Index.cshtml` đã xóa để migrate routing)
