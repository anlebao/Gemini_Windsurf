# TASK CARD: Tiered Auth — Phase 1 — Google OAuth (Blazor Server)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement Google OAuth login flow: redirect → Google consent → ShopERP callback → verify ID token → find/create Customer (IdentityLevel=Social) → issue customer token → redirect KhachLink.
- **Nghiệp vụ áp dụng:** Tier 1 — Social Login miễn phí, thay thế OTP cho login thường xuyên
- **Status:** ⬜ NOT STARTED
- **Branch:** `feature/tiered-auth-phase1-google-oauth`
- **Dependency:** Phase 0 (IdentityLevel phải tồn tại)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Phase 1 of 7
- **Dependency:** Phase 0 COMPLETE

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `5_WebApps/ShopERP/Controllers/SocialAuthController.cs` — OAuth callback handler
- `3_CoreHub/Services/ISocialAuthService.cs` — interface
- `3_CoreHub/Services/GoogleAuthService.cs` — Google ID token verification + customer link

### Files cần MODIFY
- `5_WebApps/ShopERP/Program.cs` — DI registration + Google auth config
- `5_WebApps/ShopERP/appsettings.json` — Google config section
- `5_WebApps/ShopERP/appsettings.Production.json` — Google config (env vars)
- `Directory.Packages.props` — add Google.Apis.Auth (nếu dùng)
- `2_Gateway/Program.cs` — ensure YARP forwards `/api/auth/*`

### Files READ ONLY
- `5_WebApps/ShopERP/Services/CustomerTokenService.cs` — token creation pattern
- `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — existing customer find/create pattern
- `3_CoreHub/Repositories/ICustomerRepository.cs` — customer query methods

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs` (Phase 0 đã xong IdentityLevel)
- KHÔNG tạo KhachLink UI (đó là Phase 3)
- KHÔNG implement verification gate (đó là Phase 2)
- Social login customer luôn có `IdentityLevel = Social`

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **OAuth flow:** Server-side flow (redirect-based, không client-side token)
- [ ] **ID token verification:** Dùng `Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync` hoặc `Microsoft.AspNetCore.Authentication.Google`
- [ ] **Customer matching:** Find by Email first → if not found, create new with `IdentityLevel = Social`
- [ ] **Token issuance:** Reuse `ICustomerTokenService.CreateToken(customerId)`
- [ ] **Redirect:** Callback redirect về KhachLink URL với token query param: `https://localhost:5002/login?token={token}&provider=google`
- [ ] **Config:** `Google:ClientId` + `Google:ClientSecret` — environment variables cho production
- [ ] **Security:** Validate `aud` (audience) claim = ClientId, `iss` = Google issuer

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** `GET /api/auth/google/login` redirect đến Google consent screen
- [ ] **SC2:** `GET /api/auth/google/callback` verify Google ID token thành công
- [ ] **SC3:** Customer mới tạo với `IdentityLevel = Social` khi email chưa tồn tại
- [ ] **SC4:** Customer có sẵn (by email) — giữ nguyên IdentityLevel, issue token
- [ ] **SC5:** Redirect về KhachLink với token query param
- [ ] **SC6:** Config dùng environment variables cho production
- [ ] **SC7:** Build: 0 errors
- [ ] **SC8:** guard-check.ps1 ALL CHECKS PASSED

---

## 6. SKILLS
- `domain-integrity-validation` — verify Customer creation consistency
- `outbox-pattern-implementation` — N/A

---

## 7. AI HEALTH CHECK
- **Assumptions:** 2 (Google.Apis.Auth compatible with .NET 8, YARP forwards /api/auth/* by default)
- **Verified Facts:** 4 (CustomerTokenService pattern, CustomerIdentityController pattern, ICustomerRepository methods, appsettings env var pattern)
- **Open Questions:** 1 (Google OAuth redirect URI — cần configure trong Google Cloud Console)
- **Gate check:** Assumptions (2) < Verified Facts (4) → OK, nhưng cần investigate Google.Apis.Auth compatibility

---

## 8. LIVE RUNTIME VERIFICATION (MANDATORY — see Wave 0 lesson)
> Static checks (build + architecture tests + guard-check) KHÔNG đảm bảo runtime works.
> Phải boot app + test HTTP/UI thực tế trước khi mark phase COMPLETE.

**Prerequisites:**
- [ ] Docker Desktop running (PostgreSQL 5432 + NATS 4222)
- [ ] ShopERP started on http://localhost:5003 (watch logs: no startup errors)
- [ ] KhachLink started on http://localhost:5002 (PWA loads)
- [ ] Gateway started on http://localhost:5001 (YARP forwards `/api/auth/*` → ShopERP 5003)
- [ ] Google Cloud Console: OAuth 2.0 Client ID configured với redirect URI `https://localhost:5003/api/auth/google/callback`
- [ ] `Google:ClientId` + `Google:ClientSecret` trong appsettings.json (dev) hoặc env vars (prod)

**RV tests (all MUST pass):**
- [ ] **RV1 — Google login redirect:** `GET /api/auth/google/login` (via Gateway 5001) → HTTP 302 redirect đến `accounts.google.com/o/oauth2/v2/auth` với đúng `client_id` + `redirect_uri` + `scope`.
- [ ] **RV2 — Google callback (new customer):** Complete OAuth flow → `GET /api/auth/google/callback?code=...` → verify ID token thành công → Customer mới tạo với `IdentityLevel = Social` → query DB confirm.
- [ ] **RV3 — Google callback (existing customer):** OAuth với email đã tồn tại → Customer giữ nguyên `IdentityLevel` (không override) → issue token.
- [ ] **RV4 — Token redirect to KhachLink:** Callback redirect đến `https://localhost:5002/login?token={token}&provider=google` → KhachLink Login page nhận token từ URL query param.
- [ ] **RV5 — Token validation:** Token từ redirect = `ICustomerTokenService.ValidateToken` trả về correct `customerId` → `GET /api/customers/profile` với `Authorization: Bearer {token}` → 200.
- [ ] **RV6 — Gateway forwarding:** `GET /api/auth/google/login` qua Gateway 5001 → forward đến ShopERP 5003 → 302 redirect (không 404/500).
- [ ] **RV7 — Error handling:** OAuth với invalid code → `GET /api/auth/google/callback?code=invalid` → redirect KhachLink với error param (không crash server).
- [ ] **RV8 — LINQ translation:** Mọi query mới dùng direct property comparison — verify không `InvalidOperationException`.
- [ ] **RV9 — Build + guard-check:** `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` ALL CHECKS PASSED.
