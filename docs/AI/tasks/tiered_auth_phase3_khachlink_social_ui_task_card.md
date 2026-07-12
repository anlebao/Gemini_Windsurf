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

---

## 8. LIVE RUNTIME VERIFICATION (MANDATORY — see Wave 0 lesson)
> Static checks (build + architecture tests + guard-check) KHÔNG đảm bảo runtime works.
> Phải boot app + test HTTP/UI thực tế trước khi mark phase COMPLETE.

**Prerequisites:**
- [ ] Docker Desktop running (PostgreSQL 5432 + NATS 4222)
- [ ] ShopERP started on http://localhost:5003 (watch logs: no startup errors)
- [ ] KhachLink started on http://localhost:5002 (PWA loads)
- [ ] Gateway started on http://localhost:5001
- [ ] Phase 1 COMPLETE (Google OAuth endpoints hoạt động)
- [ ] Phase 2 COMPLETE (Verification gate + upgrade API hoạt động)

**RV tests (all MUST pass):**
- [ ] **RV1 — Google login button visible:** Mở `http://localhost:5002/login` → nút "Đăng nhập với Google" hiển thị (VanAnButton + Google icon) — inspect DOM `data-testid="btn-google-login"`.
- [ ] **RV2 — Google login redirect:** Click nút Google → redirect đến `GET /api/auth/google/login` (via Gateway 5001) → redirect đến Google consent screen.
- [ ] **RV3 — Token callback handling:** Sau Google OAuth → redirect `http://localhost:5002/login?token={token}&provider=google` → KhachLink parse token từ URL → store `localStorage` → redirect `/profile`.
- [ ] **RV4 — Profile shows IdentityLevel badge:** Mở `http://localhost:5002/profile` → hiển thị badge "Tài khoản Social" (cho Social) hoặc "Đã xác thực" (cho Verified) — inspect DOM.
- [ ] **RV5 — Upgrade modal OTP flow:** Mở IdentityUpgradeModal → click "Nâng cấp ngay" → gọi `POST /api/customer-identity/upgrade/send-otp` → 200 → modal hiển thị OTP input → nhập OTP → click verify → `POST /api/customer-identity/upgrade/verify-otp` → 200 → modal đóng.
- [ ] **RV6 — Redeem 403 shows upgrade modal:** LoyaltyCard → redeem với `IdentityLevel = Social` → API trả 403 → IdentityUpgradeModal tự động hiển thị.
- [ ] **RV7 — Upgrade persists:** Sau upgrade OTP verify thành công → refresh page → Profile badge đổi từ "Social" → "Đã xác thực" → redeem thành công (không 403).
- [ ] **RV8 — KhachLinkStartupTests pass:** `dotnet test --filter "KhachLinkStartupTests"` → all PASS (DI assertions).
- [ ] **RV9 — UI Platform compliance:** Login + Profile + LoyaltyCard + IdentityUpgradeModal dùng VanAnButton/VanAnCard/VanAnAlert — grep HTML source, KHÔNG custom button/card.
- [ ] **RV10 — Build + guard-check:** `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` ALL CHECKS PASSED.
