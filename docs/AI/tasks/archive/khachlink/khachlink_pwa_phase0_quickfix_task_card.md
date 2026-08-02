# TASK CARD: PWA-OFFLINE - Phase 0 - Quick Fix Tạm Thời (Offline Shell)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Cải thiện UX offline NGAY LẬP TỨC (không cần convert WASM) — user mất mạng thấy trang đẹp + manage expectation thay vì trắng trang.
- **Nghiệp vụ áp dụng:** Khách hàng cài PWA, mở app khi mất mạng → hiện trang "cần internet" đẹp + catalog snapshot read-only.

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `.devin/workflows/newfeaturebuild.md`
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/wwwroot/service-worker.js` (offline HTML fallback line 131-134)
  - `5_WebApps/KhachLink/Components/PWA/PWAInstallPrompt.razor` (install prompt text)
  - `5_WebApps/KhachLink/wwwroot/index.html` hoặc `Components/App.razor` (offline shell template)
- **Boundary Rules:**
  - KHÔNG convert sang WASM (đây là quick fix, Phase 1-6 lo convert).
  - KHÔNG thay đổi Gateway/ShopERP.
  - KHÔNG thêm business logic mới.

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **UI Platform compliance:** Dùng VanAnButton/VanAnCard/VanAnAlert cho offline shell (nếu render qua Blazor). Nếu HTML tĩnh trong service worker → dùng inline CSS (không load Blazor).
- [ ] **No false promises:** Text install prompt phải nói thật "cần internet để đặt hàng".
- [ ] **Cache size:** Offline shell HTML <50KB (không cache catalog snapshot lớn).

## 5. SUCCESS CRITERIA
- [ ] SC1: Service worker `fetch` fallback trả HTML đẹp thay vì text "Vui lòng kết nối internet".
- [ ] SC2: Offline shell hiện logo Vạn An + icon + "App cần internet để đặt hàng" + nút "Thử lại".
- [ ] SC3: PWAInstallPrompt text sửa: "Cài đặt để truy cập nhanh — cần internet để đặt hàng".
- [ ] SC4: `dotnet build VanAn.KhachLink.csproj` PASS.
- [ ] SC5: RV trên VPS: tắt mạng → reload app → thấy offline shell đẹp (không trắng trang).

**Implementation Date:** _TBD_
**Branch:** `feature/khachlink-pwa-phase0-quickfix`

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — verify offline shell dùng UI Platform components
- `pattern-based-fixing` — service worker fallback pattern

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: service-worker.js line 131-134 trả HTML "Offline" đơn giản
  - Fact 2: PWAInstallPrompt.razor line 14 nói "Trải nghiệm tốt hơn" (không warn cần internet)
  - Fact 3: KhachLink là Blazor Server (circuit chết khi offline)
  - Fact 4: Service worker đã cache static assets (CSS/JS/icon)
  - Fact 5: manifest.json có icon 192x192 + 512x512
- **Assumptions:**
  - Offline shell HTML tĩnh (không cần Blazor render) là đủ cho quick fix.
- **Open Questions:**
  - Q1: Có nên cache catalog snapshot (top 10 products) cho offline shell không? → Phase 0 giữ đơn giản, Phase 3 lo API cache.
- **Recommended Action:** Proceed to IMPLEMENT.

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| service-worker.js | Cache version bump → user tải lại SW | skipWaiting + clients.claim đã có |
| PWAInstallPrompt.razor | Text change không break logic | Verify @if conditions không phụ thuộc text |

## 9. TDD & E2E TESTING STRATEGY
- **Manual RV:** tắt mạng Chrome DevTools → reload → verify offline shell.
- **Test boundary:** Không cần unit test (HTML tĩnh + text change).

## 10. JIT PLANNING + PURE EXECUTION
| Session | JIT Planning | Pure Execution |
|---|---|---|
| S1 | Chốt offline shell HTML template | Sửa service-worker.js fallback + PWAInstallPrompt text + build + RV |

## 12. ESTIMATED EFFORT
- 1 session (~30 phút). **BLOCKER:** none.
