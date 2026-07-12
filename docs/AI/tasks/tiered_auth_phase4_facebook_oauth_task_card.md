# TASK CARD: Tiered Auth — Phase 4 — Facebook OAuth

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thêm Facebook OAuth login flow, tương tự Google OAuth (Phase 1). Reuse `ISocialAuthService` pattern.
- **Nghiệp vụ áp dụng:** Tier 1 — Social Login mở rộng thêm Facebook
- **Status:** ⬜ NOT STARTED
- **Branch:** `feature/tiered-auth-phase4-facebook-oauth`
- **Dependency:** Phase 1 (Google OAuth — reuse pattern) + Phase 3 (KhachLink UI — thêm button)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Phase 4 of 7
- **Dependency:** Phase 1 + Phase 3 COMPLETE

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `3_CoreHub/Services/FacebookAuthService.cs` — Facebook access token verification + customer link

### Files cần MODIFY
- `5_WebApps/ShopERP/Controllers/SocialAuthController.cs` — thêm Facebook endpoints
- `5_WebApps/ShopERP/Program.cs` — DI registration
- `5_WebApps/ShopERP/appsettings.json` — Facebook config section
- `5_WebApps/ShopERP/appsettings.Production.json` — Facebook config (env vars)
- `5_WebApps/KhachLink/Pages/Login.razor` — thêm Facebook button
- `Directory.Packages.props` — add Facebook SDK (nếu cần)

### Files READ ONLY
- `3_CoreHub/Services/GoogleAuthService.cs` — reuse pattern (Phase 1)
- `5_WebApps/ShopERP/Controllers/SocialAuthController.cs` — existing OAuth controller (Phase 1)

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs`
- KHÔNG tạo new controller — extend `SocialAuthController` (Phase 1)
- UI Platform: VanAnButton cho Facebook button
- Facebook customer luôn có `IdentityLevel = Social`

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **OAuth flow:** Server-side flow (redirect-based)
- [ ] **Token verification:** Facebook Graph API `/me` endpoint với access token
- [ ] **Customer matching:** Find by Email first → if not found, create new with `IdentityLevel = Social`
- [ ] **Config:** `Facebook:AppId` + `Facebook:AppSecret` — environment variables cho production
- [ ] **Redirect:** Callback redirect về KhachLink URL: `https://localhost:5002/login?token={token}&provider=facebook`
- [ ] **Reuse:** Dùng cùng `ICustomerTokenService.CreateToken` pattern

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** `GET /api/auth/facebook/login` redirect đến Facebook consent
- [ ] **SC2:** `GET /api/auth/facebook/callback` verify Facebook access token thành công
- [ ] **SC3:** Customer mới tạo với `IdentityLevel = Social`
- [ ] **SC4:** Login page có nút "Đăng nhập với Facebook" (VanAnButton + Facebook icon)
- [ ] **SC5:** Config dùng environment variables cho production
- [ ] **SC6:** Build: 0 errors
- [ ] **SC7:** guard-check.ps1 ALL CHECKS PASSED

---

## 6. SKILLS
- `domain-integrity-validation` — verify Customer creation consistency

---

## 7. AI HEALTH CHECK
- **Assumptions:** 1 (Facebook Graph API compatible with .NET 8 HttpClient)
- **Verified Facts:** 4 (GoogleAuthService pattern from P1, SocialAuthController structure from P1, Login.razor structure from P3, appsettings pattern)
- **Open Questions:** 1 (Facebook App review — cần configure trong Facebook Developer Console)
- **Gate check:** Assumptions (1) < Verified Facts (4) → OK để proceed

---

## 8. LIVE RUNTIME VERIFICATION (MANDATORY — see Wave 0 lesson)
> Static checks (build + architecture tests + guard-check) KHÔNG đảm bảo runtime works.
> Phải boot app + test HTTP/UI thực tế trước khi mark phase COMPLETE.

**Prerequisites:**
- [ ] Docker Desktop running (PostgreSQL 5432 + NATS 4222)
- [ ] ShopERP started on http://localhost:5003 (watch logs: no startup errors)
- [ ] KhachLink started on http://localhost:5002 (PWA loads)
- [ ] Gateway started on http://localhost:5001
- [ ] Facebook Developer Console: App configured với redirect URI `https://localhost:5003/api/auth/facebook/callback`
- [ ] `Facebook:AppId` + `Facebook:AppSecret` trong appsettings.json (dev) hoặc env vars (prod)

**RV tests (all MUST pass):**
- [ ] **RV1 — Facebook login redirect:** `GET /api/auth/facebook/login` (via Gateway 5001) → HTTP 302 redirect đến `facebook.com/v18.0/dialog/oauth` với đúng `client_id` + `redirect_uri` + `scope`.
- [ ] **RV2 — Facebook callback (new customer):** Complete OAuth flow → `GET /api/auth/facebook/callback?code=...` → verify access token thành công → Customer mới tạo với `IdentityLevel = Social` → query DB confirm.
- [ ] **RV3 — Facebook callback (existing customer):** OAuth với email đã tồn tại → Customer giữ nguyên `IdentityLevel` → issue token.
- [ ] **RV4 — Token redirect to KhachLink:** Callback redirect đến `https://localhost:5002/login?token={token}&provider=facebook` → KhachLink Login page nhận token.
- [ ] **RV5 — Facebook button visible:** Mở `http://localhost:5002/login` → nút "Đăng nhập với Facebook" hiển thị (VanAnButton + Facebook icon) — inspect DOM `data-testid="btn-facebook-login"`.
- [ ] **RV6 — Gateway forwarding:** `GET /api/auth/facebook/login` qua Gateway 5001 → forward đến ShopERP 5003 → 302 redirect (không 404/500).
- [ ] **RV7 — Error handling:** OAuth với invalid code → redirect KhachLink với error param (không crash server).
- [ ] **RV8 — LINQ translation:** Mọi query mới dùng direct property comparison — verify không `InvalidOperationException`.
- [ ] **RV9 — Build + guard-check:** `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` ALL CHECKS PASSED.
