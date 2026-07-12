# TASK CARD: Tiered Auth — Phase 3 — KhachLink UI: Social Login + Upgrade Modal

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** (1) Thêm nút "Đăng nhập với Google" vào Login.razor. (2) Wire up IdentityUpgradeModal với upgrade OTP flow. (3) Hiển thị IdentityLevel trên Profile. (4) Khi user cố redeem mà chưa Verified, hiển thị upgrade modal.
- **Nghiệp vụ áp dụng:** UX cho tiered auth — social login button + upgrade prompt
- **Status:** ⬜ NOT STARTED
- **Branch:** `feature/tiered-auth-phase3-khachlink-social-ui`
- **Dependency:** Phase 1 (Google OAuth) + Phase 2 (Verification Gate) COMPLETE

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Phase 3 of 7
- **Dependency:** Phase 1 + Phase 2 COMPLETE

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần MODIFY
- `5_WebApps/KhachLink/Pages/Login.razor` — thêm Google login button + callback handler
- `5_WebApps/KhachLink/Components/IdentityUpgradeModal.razor` — wire up OTP upgrade flow
- `5_WebApps/KhachLink/Pages/Profile.razor` — hiển thị IdentityLevel badge
- `5_WebApps/KhachLink/Pages/LoyaltyCard.razor` — hiển thị upgrade modal khi redeem fail 403

### Files cần CREATE
- `5_WebApps/KhachLink/Services/Http/SocialAuthHttpService.cs` — HTTP helper (nếu cần)

### Files cần MODIFY (DI)
- `5_WebApps/KhachLink/Program.cs` — DI registration
- `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` — assertion

### Files READ ONLY
- `5_WebApps/KhachLink/Pages/Login.razor` — existing login flow pattern
- `5_WebApps/KhachLink/Components/IdentityUpgradeModal.razor` — existing modal structure
- `5_WebApps/KhachLink/Pages/Profile.razor` — existing profile page
- `5_WebApps/KhachLink/Pages/LoyaltyCard.razor` — existing loyalty card page
- `5_WebApps/KhachLink/Services/Http/ShopFeatureSettingsHttpService.cs` — HTTP service pattern

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs`
- KHÔNG sửa ShopERP controllers (đã xong trong P1/P2)
- UI Platform components: VanAnButton, VanAnCard, VanAnAlert — KHÔNG custom HTML/CSS
- KhachLink = Blazor WASM — HTTP-only, không inject DbContext

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **UI Platform:** Dùng VanAnButton cho social login buttons, VanAnCard cho containers, VanAnAlert cho errors
- [ ] **Google login flow:** Button click → `Navigation.NavigateTo("/api/auth/google/login")` (via Gateway) → redirect chain → callback URL với token
- [ ] **Token handling:** Parse `token` từ URL query param → store `localStorage` → redirect to `/profile`
- [ ] **Upgrade modal:** 2-step flow — (1) "Nâng cấp ngay" → send OTP, (2) OTP input → verify → success
- [ ] **IdentityLevel badge:** Profile hiển thị "Tài khoản Social" (Social) hoặc "Đã xác thực" (Verified)
- [ ] **Redeem 403 handling:** LoyaltyCard catch 403 → show IdentityUpgradeModal
- [ ] **DI Checklist:** Mỗi service mới vào KhachLink → (1) DI trong Program.cs, (2) assertion trong KhachLinkStartupTests

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** Login page có nút "Đăng nhập với Google" (VanAnButton + Google icon)
- [ ] **SC2:** Google login redirect → callback → token stored trong localStorage
- [ ] **SC3:** IdentityUpgradeModal hiển thị OTP flow khi click "Nâng cấp ngay"
- [ ] **SC4:** Upgrade OTP verify thành công → modal đóng + Profile update
- [ ] **SC5:** Profile hiển thị IdentityLevel badge (Social / Verified)
- [ ] **SC6:** LoyaltyCard hiển thị upgrade modal khi redeem fail (403)
- [ ] **SC7:** KhachLinkStartupTests pass
- [ ] **SC8:** Build: 0 errors
- [ ] **SC9:** guard-check.ps1 ALL CHECKS PASSED

---

## 6. SKILLS
- `accounting-ui-implementation` — UI patterns (reusable for loyalty UI)
- `ui-platform-compliance-review` — ensure UI Platform components used

---

## 7. AI HEALTH CHECK
- **Assumptions:** 1 (Blazor WASM can handle OAuth redirect callback via query param)
- **Verified Facts:** 5 (Login.razor structure, IdentityUpgradeModal structure, Profile.razor structure, LoyaltyCard.razor structure, HTTP service pattern)
- **Open Questions:** 1 (Google OAuth redirect URI — cần configure trong Google Cloud Console + KhachLink URL)
- **Gate check:** Assumptions (1) < Verified Facts (5) → OK để proceed
