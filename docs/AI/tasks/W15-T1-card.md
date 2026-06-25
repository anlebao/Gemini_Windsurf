# TASK CARD: PRODUCTION_HYGIENE - WAVE15 - Xóa Dead/Demo Pages

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa 5 files dead/demo/orphan khỏi `5_WebApps/KhachLink/Pages/` — không có production value, gây route conflict, vi phạm architectural constraint
- **Nghiệp vụ áp dụng:** Production hygiene — loại bỏ code không được test, không được route đến, và vi phạm `VA-KHACHLINK-004` (KhachLink không được access DB trực tiếp)
- **Master plan:** `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` § 9. WAVE 15 — task W15-T1
- **Nguồn phân tích:**
  - `docs/AI/e2e-gap-backlog.md` §Route #9: `Home.razor @page "/"` là canonical entry point
  - `docs/AI/tasks/TD-001_KhachLink_ArchitecturalViolation.md`: `VA-KHACHLINK-004` — KhachLink KHÔNG truy cập DB trực tiếp
  - `Index.cshtml.cs` line 8: TECH DEBT comment tự khai vi phạm `VA-KHACHLINK-004`
  - `6_Testing/e2e-tests/order-flow.spec.ts`: E2E chỉ test `Home.razor` — không có file nào trong list xóa được test

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `Fix_Errors.md` (xóa file, không có new feature)
- **Execution Mode:** FIX_ONLY

## 3. FILES CẦN XÓA (DANH SÁCH ĐẦY ĐỦ)

| File | Lý do xóa | Risk |
|------|-----------|------|
| `5_WebApps/KhachLink/Pages/Index.razor` | Route `@page "/"` conflict với `Home.razor`. `SampleProducts` hardcode với `Guid.NewGuid()`. `Checkout()` fake `Task.Delay(2000)` loop — không gọi API nào. Dead code. | LOW — shadow bởi `Home.razor` |
| `5_WebApps/KhachLink/Pages/IndexModern.cshtml` | Route `/modern`. Stats bịa: `1000+ quán`, `50K+/ngày`, `99.9% uptime`, `4.9★`. Không có code-behind, không handler, không test reference. Demo prototype. | ZERO — không có consumer |
| `5_WebApps/KhachLink/Pages/_Host.cshtml` | Không được route đến (`Program.cs MapFallbackToPage("/Index")`). Link `/order-tracking/demo` trỏ route không tồn tại. Nội dung marketing HTML tĩnh. | ZERO — không được route đến |
| `5_WebApps/KhachLink/Pages/Index.cshtml` | Duplicate landing. `FeaturedProducts` hardcode `Guid.NewGuid()` mỗi request. Social proof JS fake dùng `setInterval` + `Math.random()`. | LOW — xử lý fallback ở W15-T2 |
| `5_WebApps/KhachLink/Pages/Index.cshtml.cs` | Code-behind của `Index.cshtml`. Vi phạm `VA-KHACHLINK-004`: inject `IShopConfigService` từ CoreHub trực tiếp (bypasses Gateway). | LOW — cùng lúc với `Index.cshtml` |

## 4. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc (context):**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status W15-T1 sau khi done)
  - `5_WebApps/KhachLink/Pages/_Imports.razor` (verify không có reference đến files bị xóa)
  - `5_WebApps/KhachLink/Components/` (grep references)
- **Files được phép xóa:**
  - 5 files liệt kê ở mục 3
- **KHÔNG được sửa trong task này:**
  - `5_WebApps/KhachLink/Program.cs` → W15-T2 lo
  - `5_WebApps/KhachLink/Pages/Home.razor` → canonical page, KHÔNG CHẠM
  - `5_WebApps/KhachLink/Pages/VoiceNote.razor` → W15-T3 lo
  - `1_Shared/Domain.cs` → Domain Layer Protection

## 5. PRE-DELETE CHECKLIST (THỰC HIỆN TRƯỚC KHI XÓA)

Với mỗi file, chạy grep trong toàn bộ `5_WebApps/KhachLink/` để tìm references:

```powershell
# Tìm references đến Index.razor
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*" -Pattern "Index\.razor|Pages\.Index" -Recurse

# Tìm references đến IndexModern
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*" -Pattern "IndexModern|\/modern" -Recurse

# Tìm references đến _Host.cshtml
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*" -Pattern "_Host|host\.cshtml" -Recurse

# Tìm references đến Index.cshtml / IndexModel
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*" -Pattern "IndexModel|Index\.cshtml" -Recurse
```

Nếu tìm thấy reference ngoài dự kiến → **STOP, báo cáo trước khi xóa.**

## 6. BƯỚC THỰC HIỆN (TUẦN TỰ)

```
S1: Pre-delete scan
    → Chạy grep commands ở mục 5
    → Xác nhận 0 external references

S2: Xóa files
    → Xóa Index.razor
    → Xóa IndexModern.cshtml
    → Xóa _Host.cshtml
    → Xóa Index.cshtml
    → Xóa Index.cshtml.cs

S3: Build check
    → dotnet build VanAn.sln
    → Nếu có error liên quan đến files vừa xóa: fix ngay trong task này
    → Nếu error > 5 không liên quan: STOP, ghi investigation_log.md

S4: Commit
    → git add -A
    → Commit message: "[W15-T1] Remove dead/demo KhachLink pages"
```

## 7. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `Index.razor` không còn trong repository
- [ ] **SC2:** `IndexModern.cshtml` không còn trong repository
- [ ] **SC3:** `_Host.cshtml` không còn trong repository
- [ ] **SC4:** `Index.cshtml` và `Index.cshtml.cs` không còn trong repository
- [ ] **SC5:** `dotnet build VanAn.sln` → 0 errors sau khi xóa
- [ ] **SC6:** `grep -r "IndexModel\|IndexModern\|_Host.cshtml" KhachLink/` → 0 results
- [ ] **SC7:** `PRODUCTION_HYGIENE_master_plan.md` updated W15-T1 = ✅ DONE

**Implementation Date:** TBD
**Branch:** `feature/wave15-khachlink-page-cleanup`

## 8. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Fix compile errors nếu phát sinh sau khi xóa
- `domain-integrity-validation` — Verify không ảnh hưởng Domain layer

## 9. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `e2e-gap-backlog.md` §Route #9 xác nhận `Home.razor` là canonical `@page "/"`
  - Fact 2: `Index.cshtml.cs` có TECH DEBT comment: *"KhachLink must not access DB directly (VA-KHACHLINK-004)"*
  - Fact 3: `Program.cs` line 175 `MapFallbackToPage("/Index")` — sau khi xóa sẽ cần fix ở W15-T2
  - Fact 4: `order-flow.spec.ts` không có test nào navigate đến `/modern`, `_Host`, `Index.cshtml`
  - Fact 5: `_Host.cshtml` không có Blazor Hub wire-up — không phải `_Host` thực của Server Side Blazor
- **Assumptions:**
  - Không có component nào `@inject` hoặc `@using` trực tiếp từ `IndexModel`
- **Open Questions:**
  - Q1: `_Imports.razor` có `@layout` nào reference `_Host.cshtml` không? (kiểm tra ở Pre-delete scan)
- **Recommended Action:** IMPLEMENT — evidence rõ ràng, assumption dễ verify

## 10. REVERSE IMPACT ANALYSIS
| File xóa | Reverse impact | Mitigation |
|---|---|---|
| `Index.razor` (`@page "/"`) | Route `/` sẽ chỉ served bởi `Home.razor` — đây là kết quả mong muốn | None |
| `IndexModern.cshtml` (`@page "/modern"`) | Route `/modern` → 404 — không có consumer | None |
| `_Host.cshtml` | Không được route đến → 0 impact | None |
| `Index.cshtml` (`@page "/"`) | `MapFallbackToPage("/Index")` sẽ broken → **W15-T2 xử lý ngay sau** | W15-T2 |
| `Index.cshtml.cs` | Code-behind của `Index.cshtml` | Cùng lúc với `Index.cshtml` |

## 11. ESTIMATED EFFORT
- Low effort — chỉ xóa files + grep verify
- 1 session
- **BLOCKER:** Không có — task độc lập
- **UNBLOCKS:** W15-T2 (cần biết `Index.cshtml` đã xóa để fix routing)
