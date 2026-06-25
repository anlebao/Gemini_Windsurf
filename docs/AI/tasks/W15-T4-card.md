# TASK CARD: PRODUCTION_HYGIENE - WAVE15 - Verify Build & E2E Entry Point Contract

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Verification gate toàn bộ Wave 15 — xác nhận build 0 errors, `Home.razor` serve đúng tại `/`, E2E selector contract từ `order-flow.spec.ts` còn nguyên, và không có broken references sau khi xóa files
- **Nghiệp vụ áp dụng:** Quality gate trước khi commit wave hoàn tất — ngăn regression
- **Master plan:** `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` § 9. WAVE 15 — task W15-T4
- **Depends on:** W15-T1 + W15-T2 + W15-T3 đều hoàn thành

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** Verification (không thuộc workflow mode nào — đây là gate check)
- **Execution Mode:** REVIEW_ONLY (chỉ verify, không sửa code mới)
- **Ngoại lệ:** Nếu phát hiện lỗi nhỏ (< 3 errors) có thể fix inline và document lại

## 3. CHECKLIST VERIFICATION ĐẦY ĐỦ

### 3.1 Build Gate
```powershell
cd C:\VibeCoding\Gemini_Windsurf
dotnet build VanAn.sln
```
- [ ] **0 errors**
- [ ] **0 warnings mới** (so với baseline trước Wave 15)

### 3.2 Guard Check
```powershell
.\guard-check.ps1
```
- [ ] **PASS** — không có vi phạm mới

### 3.3 Architecture Tests
```powershell
dotnet test 6_Tests/VanAn.Architecture.Tests/ --no-build
```
- [ ] **7/7 PASS** (hoặc số lượng tests theo baseline hiện tại)

### 3.4 File Deletion Verification
Verify 5 files đã thực sự bị xóa:
```powershell
$deleted = @(
    "5_WebApps\KhachLink\Pages\Index.razor",
    "5_WebApps\KhachLink\Pages\IndexModern.cshtml",
    "5_WebApps\KhachLink\Pages\_Host.cshtml",
    "5_WebApps\KhachLink\Pages\Index.cshtml",
    "5_WebApps\KhachLink\Pages\Index.cshtml.cs"
)
foreach ($f in $deleted) {
    if (Test-Path "C:\VibeCoding\Gemini_Windsurf\$f") {
        Write-Host "FAIL: $f vẫn tồn tại" -ForegroundColor Red
    } else {
        Write-Host "OK: $f đã xóa" -ForegroundColor Green
    }
}
```
- [ ] Tất cả 5 files → "OK: đã xóa"

### 3.5 Broken References Scan
Verify không còn reference đến các files đã xóa trong toàn bộ KhachLink:
```powershell
$patterns = "IndexModel|IndexModern|_Host\.cshtml|Pages\.Index\b"
$results = Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\**\*.*" `
           -Pattern $patterns -Recurse -ErrorAction SilentlyContinue
if ($results) {
    Write-Host "FAIL: Broken references found:" -ForegroundColor Red
    $results | ForEach-Object { Write-Host "  $($_.Filename):$($_.LineNumber) — $($_.Line.Trim())" }
} else {
    Write-Host "OK: No broken references" -ForegroundColor Green
}
```
- [ ] **0 broken references**

### 3.6 Program.cs Routing Verification
```powershell
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\Program.cs" `
              -Pattern "MapFallbackToPage"
```
- [ ] **0 results** — `MapFallbackToPage` đã xóa

### 3.7 E2E Selector Contract — Home.razor
Xác nhận `Home.razor` vẫn có đủ selectors mà `order-flow.spec.ts` và `order-tracking.spec.ts` cần:
```powershell
# Kiểm tra .feature-card class (selector: '.feature-card')
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\Pages\Home.razor" `
              -Pattern "feature-card"

# Kiểm tra button "Đặt ngay" (selector: 'button:has-text("Đặt ngay")')
Select-String -Path "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\Pages\Home.razor" `
              -Pattern "Đặt ngay"
```
- [ ] `feature-card` → ít nhất 1 match trong `Home.razor`
- [ ] `Đặt ngay` → ít nhất 1 match trong `Home.razor`

### 3.8 VoiceNote.razor Architecture Verification
```powershell
$vn = Get-Content "C:\VibeCoding\Gemini_Windsurf\5_WebApps\KhachLink\Pages\VoiceNote.razor" -Raw

# Không còn html/head/body
if ($vn -match "<html|<head>|<body>") { Write-Host "FAIL: html/head/body tags" -ForegroundColor Red }
else { Write-Host "OK: No html/head/body" -ForegroundColor Green }

# Không còn direct HttpClient inject
if ($vn -match "@inject HttpClient Http\b") { Write-Host "FAIL: Direct HttpClient inject" -ForegroundColor Red }
else { Write-Host "OK: No direct HttpClient" -ForegroundColor Green }

# Endpoint đúng
if ($vn -match "voicecommand/text-command") { Write-Host "OK: Correct endpoint" -ForegroundColor Green }
else { Write-Host "FAIL: Wrong or missing endpoint" -ForegroundColor Red }

# Không còn alert() native
if ($vn -match 'InvokeVoidAsync\("alert"') { Write-Host "FAIL: Native alert() still present" -ForegroundColor Red }
else { Write-Host "OK: No native alert()" -ForegroundColor Green }

# JSInvokable preserved
if ($vn -match "\[JSInvokable\]") { Write-Host "OK: JSInvokable present" -ForegroundColor Green }
else { Write-Host "FAIL: JSInvokable missing" -ForegroundColor Red }
```
- [ ] No html/head/body tags
- [ ] No direct HttpClient inject
- [ ] Correct endpoint `/api/v1/voicecommand/text-command`
- [ ] No native `alert()`
- [ ] `[JSInvokable]` present (SetTranscriptionText preserved)

## 4. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Pages/Home.razor` (đọc để verify selectors)
  - `5_WebApps/KhachLink/Pages/VoiceNote.razor` (đọc để verify rewrite)
  - `5_WebApps/KhachLink/Program.cs` (đọc để verify routing)
  - `docs/AI/tasks/PRODUCTION_HYGIENE_master_plan.md` (cập nhật status)
- **Files được phép sửa (chỉ nếu phát hiện lỗi nhỏ < 3 errors):**
  - `5_WebApps/KhachLink/Pages/VoiceNote.razor` — fix compile error nhỏ nếu có
  - `5_WebApps/KhachLink/Program.cs` — fix routing nếu W15-T2 bỏ sót
- **KHÔNG được sửa mới bất cứ thứ gì ngoài scope fixes trên**

## 5. BƯỚC THỰC HIỆN

```
S1: Chạy toàn bộ checklist 3.1 → 3.8 theo thứ tự
    → Ghi kết quả Pass/Fail cho từng mục

S2: Nếu phát hiện lỗi:
    → ≤ 3 compile errors: fix inline, ghi lại trong commit message
    → > 3 errors: STOP — ghi vào investigation_log.md, báo cáo trước khi tiếp tục
    → Broken reference: fix trong file tương ứng

S3: Chạy lại build sau fixes
    → dotnet build VanAn.sln → 0 errors

S4: Commit verification
    → "[W15-T4] Wave 15 verification gate — build OK, selectors OK, arch OK"
```

## 6. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `dotnet build VanAn.sln` → 0 errors
- [ ] **SC2:** `guard-check.ps1` → PASS
- [ ] **SC3:** Architecture tests → 7/7 PASS
- [ ] **SC4:** 5 files xóa không còn tồn tại
- [ ] **SC5:** 0 broken references đến files đã xóa
- [ ] **SC6:** `MapFallbackToPage` không còn trong `Program.cs`
- [ ] **SC7:** `Home.razor` có `.feature-card` và `Đặt ngay` (E2E selector contract)
- [ ] **SC8:** `VoiceNote.razor` pass tất cả 5 arch checks
- [ ] **SC9:** `PRODUCTION_HYGIENE_master_plan.md` updated W15-T4 = ✅ DONE

**Implementation Date:** TBD
**Branch:** `feature/wave15-khachlink-page-cleanup`

## 7. ACTIVE SKILLS (MAX 3)
- `build-error-analysis` — Fix lỗi phát sinh nếu có
- `domain-integrity-validation` — Verify architecture constraints

## 8. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: `order-flow.spec.ts` lines 35–37 dùng selector `.feature-card` và `button:has-text("Đặt ngay")`
  - Fact 2: `order-tracking.spec.ts` dùng `.order-tracking` container (OrderTracking.razor — không bị xóa)
  - Fact 3: `Architecture.Tests` hiện 7/7 PASS (từ `project_state.md`)
- **Assumptions:**
  - Không có test nào đang test `Index.cshtml`, `IndexModern`, `_Host` → xóa an toàn
- **Open Questions:**
  - Q1: `guard-check.ps1` có check gì liên quan đến Razor Pages không? (chạy để xem)
- **Recommended Action:** EXECUTE — verification task, không có risk

## 9. REVERSE IMPACT ANALYSIS
Task này là read-only verification — không có reverse impact nếu không phát sinh lỗi.

## 10. ESTIMATED EFFORT
- Low effort — chạy commands + đọc output
- < 1 session (30–60 phút)
- **BLOCKER:** W15-T1 + W15-T2 + W15-T3 phải done
- **UNBLOCKS:** W15-T5 (update state)
