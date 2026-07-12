# TASK CARD: KhachLink Improvements - Wave 1 - PWA Install Fix

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix PWA installation functionality on diemthuong.${VANAN_DOMAIN}
- **Nghiệp vụ áp dụng:** KhachLink customer-facing PWA - enable app installation for better user experience

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (small feature - single wave)
- **Execution Mode:** ANALYZE → IMPLEMENT

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - `5_WebApps/KhachLink/Services/PWA/PWAService.cs`
  - `5_WebApps/KhachLink/Components/PWA/PWAInstallPrompt.razor`
  - `5_WebApps/KhachLink/Components/AppInstallPrompt.razor` (DELETE)
  - `5_WebApps/KhachLink/wwwroot/js/pwa.js`
  - `5_WebApps/KhachLink/wwwroot/manifest.json`
  - `5_WebApps/KhachLink/wwwroot/service-worker.js`
  - `5_WebApps/KhachLink/Components/App.razor`
- **Boundary Rules (Nghiêm cấm):**
  - KHÔNG sửa manifest.json structure (chỉ cập nhật metadata nếu cần)
  - KHÔNG bypass PWAInstallPrompt.razor với custom HTML/CSS
  - KHÔNG disable service worker (cần cho offline capability)
  - KHÔNG sửa PWA service logic trừ khi có bug verified

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **UI Platform Compliance:** PWAInstallPrompt.razor uses UI Platform components (VanAnButton, VanAnCard)
- [ ] **PWA Standards:** manifest.json must be valid PWA manifest (W3C compliant)
- [ ] **Service Worker:** Must be registered and active for PWA installation
- [ ] **Browser Compatibility:** Must work on Chrome Android (install prompt) and Safari iOS (Add to Home Screen)
- [ ] **No Breaking Changes:** Existing PWA features (notifications, offline) must continue working

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** AppInstallPrompt.razor removed (duplicate eliminated)
- [ ] **SC2:** PWAInstallPrompt.razor is active and rendering in App.razor
- [ ] **SC3:** VAPID key configured (if push notifications enabled) OR push logic disabled gracefully
- [ ] **SC4:** Service worker registered successfully on app startup
- [ ] **SC5:** PWA install prompt shows on mobile devices (within 3 seconds)
- [ ] **SC6:** PWA installation completes successfully on Android Chrome
- [ ] **SC7:** PWA installation completes successfully on iOS Safari (Add to Home Screen)
- [ ] **SC8:** App runs in standalone mode after installation
- [ ] **SC9:** No JavaScript console errors related to PWA
- [ ] **SC10:** Build: 0 errors
- [ ] **SC11:** No regression in existing PWA features (notifications, offline indicator)
- [ ] **SC12:** Documentation updated for VAPID key setup (if applicable)

**Implementation Date:** 2026-06-29
**Branch:** feature/khachlink-wave1-pwa-install

## 6. ACTIVE SKILLS (MAX 3)
- `ui-platform-compliance-review` — Ensure PWAInstallPrompt.razor uses UI Platform components correctly
- `build-error-analysis` — Verify build passes after changes

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: PWAService.cs exists with full implementation (read from file)
  - Fact 2: PWAInstallPrompt.razor exists with complete UI (read from file)
  - Fact 3: AppInstallPrompt.razor is duplicate/incomplete (read from file)
  - Fact 4: manifest.json is properly configured (read from file)
  - Fact 5: service-worker.js exists and has caching logic (read from file)
  - Fact 6: pwa.js has VAPID placeholder "YOUR_VAPID_PUBLIC_KEY" (line 114)
  - Fact 7: PWAInstallPrompt.razor implements IAsyncDisposable with CancellationToken
  - Fact 8: AppInstallPrompt.razor calls vananPWA.checkInstallStatus() but PWAInstallPrompt uses PWAService
- **Assumptions:**
  - AppInstallPrompt.razor is legacy code that should be removed
  - PWAInstallPrompt.razor is the newer, more complete implementation
  - Service worker registration may not be initialized in App.razor
  - Push notifications may not be required (VAPID can be disabled)
- **Open Questions:**
  - Q1: Which install prompt component is currently active in App.razor?
  - Q2: Are push notifications required for current use case?
  - Q3: Is service worker properly registered in App.razor?
- **Recommended Action:** Investigate App.razor to see which component is active, then remove duplicate and fix initialization

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| 5_WebApps/KhachLink/Components/AppInstallPrompt.razor | DELETE - removes duplicate component | No impact (PWAInstallPrompt.razor is replacement) |
| 5_WebApps/KhachLink/Components/App.razor | May need to update component reference | Verify PWAInstallPrompt.razor is properly included |
| 5_WebApps/KhachLink/wwwroot/js/pwa.js | VAPID key configuration or disable push | Document VAPID setup or comment out push logic |
| 5_WebApps/KhachLink/Services/PWA/PWAService.cs | No changes expected (verification only) | Read-only to understand current implementation |

## 9. TDD & E2E TESTING STRATEGY
- **Manual Testing on Real Devices:**
  - Test PWA installation on Android Chrome (install prompt should appear)
  - Test PWA installation on iOS Safari (Add to Home Screen)
  - Verify app runs in standalone mode after installation
  - Check browser console for PWA-related errors
- **Test boundary:**
  - Unit tests: Not applicable (UI/infrastructure feature)
  - Integration tests: Not applicable (client-side feature)
  - E2E tests: Manual testing on real devices required

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

This is a small cleanup + config task. Single session is sufficient:
1. Planning Phase: Investigate current state, identify which component is active, plan cleanup
2. Execution Phase: Remove duplicate, fix initialization, test on devices

### Micro-phase breakdown cho Wave 1 (PWA Install Fix)

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Read App.razor to see which install prompt component is active<br>- Verify service worker registration<br>- Decide on VAPID key (configure or disable)<br>- Plan cleanup steps | - Remove AppInstallPrompt.razor<br>- Update App.razor to use PWAInstallPrompt.razor<br>- Configure VAPID or disable push logic<br>- Test PWA installation on Android Chrome<br>- Test PWA installation on iOS Safari |

### Rules
- MUST test on real devices (not just desktop browser)
- MUST verify no console errors related to PWA
- MUST ensure existing PWA features continue working
- MUST document VAPID setup if push notifications are enabled

## 11. ESTIMATED EFFORT
- 1-2 hours total
- 1 session theo JIT Planning
- **BLOCKER:** Access to real Android and iOS devices for testing