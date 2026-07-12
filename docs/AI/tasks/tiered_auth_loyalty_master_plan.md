# MASTER IMPLEMENTATION PLAN — Tiered Authentication for Loyalty Program

> **Status:** NOT STARTED — Phase 0-6 PENDING
> **Created:** 2026-07-12
> **Last Updated:** 2026-07-12
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → feature branches per phase
> **Execution principle:** JIT Planning + Pure Execution + Domain-First
> **Prerequisite:** OTP cost analysis session (Jul 12, 2026) — hybrid strategy approved by user
> **Reference:** Previous session analysis of Social Login + Firebase Phone Auth + Zalo ZNS

---

## 0. EXECUTION RULES

### JIT Planning Strategy
**Nguyên tắc:** Investigate trước, Implement sau. KHÔNG code mò mẫm.

**Bước 1: INVESTIGATE** — Verify existing code structure, service signatures, UI component patterns
**Bước 2: IMPLEMENT** — Theo plan đã chốt, mỗi phase xong chạy `guard-check.ps1` + `dotnet build`

### Session protocol
1. Mỗi session chỉ làm 1 phase
2. Bắt đầu session: Đọc `project_state.md` + task card phase đang làm
3. Sau khi plan chốt: Execution Phase
4. Trước session end: Build + test
5. Sau mỗi phase: Commit `[AUTH P{N}] Task description`

### Branch protocol
```
main
  └── feature/tiered-auth-phase0-domain-identity-level
      └── feature/tiered-auth-phase1-google-oauth
          └── feature/tiered-auth-phase2-verification-gate
              └── feature/tiered-auth-phase3-khachlink-social-ui
                  └── feature/tiered-auth-phase4-facebook-oauth
                      └── feature/tiered-auth-phase5-zalo-zns-otp
                          └── feature/tiered-auth-phase6-e2e-tests
```

### Hard rules
- **Domain layer:** Phase 0 được phép sửa `Domain.cs` (thêm `IdentityLevel` enum + property trên `Customer`) — có user approval
- **AccountingEntry immutable** — không thay đổi
- **UI Platform:** Mọi UI mới PHẢI dùng VanAnButton, VanAnCard, VanAnForm — KHÔNG custom HTML/CSS
- **OTP TTL:** Giữ 5 phút (existing `OtpService`)
- **Customer token:** Dùng existing `ICustomerTokenService` (DataProtection-based, 30-day TTL)
- **KhachLink = Blazor WebAssembly PWA** — HTTP-only, không inject DbContext
- **ShopERP = Blazor Server** — hosts in-process CoreHub services (Option B)
- **Playwright DISABLED** cho đến Phase 6 (E2E tests)
- **Social Login flow:** OAuth redirect → ShopERP callback → verify ID token → issue customer token → redirect KhachLink
- **Redeem gate:** `SubtractPointsAsync` kiểm tra `Customer.IdentityLevel >= Verified` trước khi cho phép
- **Zalo ZNS:** Chỉ implement sau khi OAuth + verification gate hoạt động

### Critical context
- **Architecture:** KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite (business) + PostgreSQL (accounting)
- **Current auth:** Phone OTP only — `CustomerIdentityController` (send/verify OTP, issue token)
- **Current login UI:** `Login.razor` — Phone → OTP → Success → redirect `/profile`
- **Customer token:** `CustomerTokenService` — IDataProtector, 30-day TTL, format `{customerId}:{expiry}`
- **Loyalty:** `LoyaltyRewardsService` — `AddPointsAsync` (earn), `SubtractPointsAsync` (redeem) — NO verification gate
- **Customer entity:** `Domain.cs:608-653` — có `PhoneNumber`, `Email`, `CustomerTier` (Bronze→Platinum), KHÔNG có `IdentityLevel`
- **IdentityUpgradeModal:** `KhachLink/Components/IdentityUpgradeModal.razor` — UI prompt "nâng cấp" nhưng chưa có logic thực
- **Cost target:** Giảm OTP cost ~70-85% (chỉ gửi OTP khi redeem, không phải mỗi login)

---

## 1. CURRENT ISSUES SUMMARY

### Issue 1: Không có Identity Verification Level
**Status:** ❌ MISSING
**Priority:** 0 (Critical — nền tảng cho tiered auth)

`Customer` entity không có field `IdentityLevel`. Không phân biệt khách hàng Social Login vs OTP verified. Tất cả khách hàng đều cùng tier bảo mật.

### Issue 2: Không có Social Login (Google/Facebook OAuth)
**Status:** ❌ MISSING
**Priority:** 1 (High)

`Login.razor` chỉ có Phone OTP flow. Không có nút "Đăng nhập với Google" / "Đăng nhập với Facebook". Mỗi login đều tốn OTP cost.

### Issue 3: SubtractPointsAsync không có verification gate
**Status:** ❌ MISSING
**Priority:** 1 (High — security)

`LoyaltyRewardsService.SubtractPointsAsync` cho phép deduct points mà không kiểm tra identity level. Bất kỳ ai có customer token đều có thể đổi điểm.

### Issue 4: IdentityUpgradeModal chưa có logic thực
**Status:** ⚠️ PARTIAL
**Priority:** 2 (Medium)

`IdentityUpgradeModal.razor` chỉ có UI prompt "Nâng cấp ngay" nhưng `OnUpgrade` callback chưa wire lên API nào. Cần kết nối với OTP upgrade flow.

### Issue 5: Zalo ZNS OTP chưa triển khai
**Status:** ❌ MISSING
**Priority:** 2 (Medium — cost optimization)

Hiện chỉ có eSMS provider (`EsmsNotificationService`). Zalo ZNS (300đ/OTP) rẻ hơn eSMS (1.000-1.200đ/OTP) nhưng chưa implement. User đã có Zalo OA account.

### Issue 6: E2E test tiered auth không tồn tại
**Status:** ❌ MISSING
**Priority:** 3 (Final validation)

Không có test cho social login flow, verification gate, upgrade flow, hay Zalo ZNS OTP.

---

## 2. PHASE 0 — Domain: IdentityLevel + Migration

**Branch:** `feature/tiered-auth-phase0-domain-identity-level`
**Priority:** 0 (Critical — BLOCKING mọi phase sau)
**Task Card:** `docs/AI/tasks/tiered_auth_phase0_domain_task_card.md`

### Mục tiêu
Thêm `IdentityLevel` enum vào Domain + property trên `Customer` + EF migration. Đây là nền tảng để mọi phase sau check identity level.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P0-T1 | Thêm `IdentityLevel` enum vào `Domain.cs` (Guest, Social, Verified, Full) | `1_Shared/Domain.cs` | ⬜ |
| 2 | P0-T2 | Thêm `IdentityLevel` property vào `Customer` entity + method `UpgradeIdentityLevel()` | `1_Shared/Domain.cs` | ⬜ |
| 3 | P0-T3 | EF Core configuration: map `IdentityLevel` column + default `Social` | `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs` | ⬜ |
| 4 | P0-T4 | Tạo EF migration `AddCustomerIdentityLevel` | `5_WebApps/ShopERP/` | ⬜ |
| 5 | P0-T5 | Update `CustomerIdentityController.VerifyOtp`: set `IdentityLevel = Verified` khi OTP verify thành công | `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` | ⬜ |
| 6 | P0-T6 | Update `CustomerIdentityResponse`: thêm `IdentityLevel` field | `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` | ⬜ |
| 7 | P0-T7 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] `IdentityLevel` enum tồn tại trong Domain (Guest, Social, Verified, Full)
- [ ] `Customer.IdentityLevel` property tồn tại, default = `Social`
- [ ] `Customer.UpgradeIdentityLevel(IdentityLevel)` method tồn tại
- [ ] EF migration tạo column `IdentityLevel` với default `Social`
- [ ] OTP verify flow set `IdentityLevel = Verified`
- [ ] `CustomerIdentityResponse` trả về `IdentityLevel`
- [ ] Build: 0 errors

---

## 3. PHASE 1 — Google OAuth (Blazor Server)

**Branch:** `feature/tiered-auth-phase1-google-oauth`
**Priority:** 1 (High)
**Task Card:** `docs/AI/tasks/tiered_auth_phase1_google_oauth_task_card.md`

### Mục tiêu
Implement Google OAuth login flow: KhachLink redirect → Google consent → ShopERP callback → verify Google ID token → find/create Customer → issue customer token → redirect KhachLink với token.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P1-T1 | Cài đặt NuGet `Google.Apis.Auth` (hoặc dùng `Microsoft.AspNetCore.Authentication.Google`) | `Directory.Packages.props` + `5_WebApps/ShopERP/VanAn.ShopERP.csproj` | ⬜ |
| 2 | P1-T2 | Tạo `SocialAuthController` — endpoint `GET /api/auth/google/login` (redirect to Google) + `GET /api/auth/google/callback` | `5_WebApps/ShopERP/Controllers/SocialAuthController.cs` (NEW) | ⬜ |
| 3 | P1-T3 | Tạo `ISocialAuthService` + `GoogleAuthService` — verify Google ID token, extract email + name | `3_CoreHub/Services/ISocialAuthService.cs` (NEW), `3_CoreHub/Services/GoogleAuthService.cs` (NEW) | ⬜ |
| 4 | P1-T4 | Link social account: find Customer by email → nếu không có, tạo mới với `IdentityLevel = Social` | `3_CoreHub/Services/SocialAuthService.cs` | ⬜ |
| 5 | P1-T5 | Issue customer token via `ICustomerTokenService` + redirect to KhachLink với token | `5_WebApps/ShopERP/Controllers/SocialAuthController.cs` | ⬜ |
| 6 | P1-T6 | DI registration: `ISocialAuthService` trong ShopERP `Program.cs` | `5_WebApps/ShopERP/Program.cs` | ⬜ |
| 7 | P1-T7 | Config: `Google:ClientId` + `Google:ClientSecret` trong `appsettings.json` + `appsettings.Production.json` | `5_WebApps/ShopERP/appsettings.json`, `5_WebApps/ShopERP/appsettings.Production.json` | ⬜ |
| 8 | P1-T8 | Gateway YARP: ensure `/api/auth/{**path}` forwards to ShopERP | `2_Gateway/Program.cs` | ⬜ |
| 9 | P1-T9 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] `GET /api/auth/google/login` redirect đến Google consent screen
- [ ] Google callback verify ID token thành công
- [ ] Customer mới tạo với `IdentityLevel = Social`
- [ ] Customer có sẵn (by email) link với social account, giữ nguyên `IdentityLevel`
- [ ] Customer token issued + redirect về KhachLink
- [ ] Config dùng environment variables cho production
- [ ] Build: 0 errors

---

## 4. PHASE 2 — Verification Gate trong SubtractPointsAsync

**Branch:** `feature/tiered-auth-phase2-verification-gate`
**Priority:** 1 (High — security)
**Task Card:** `docs/AI/tasks/tiered_auth_phase2_verification_gate_task_card.md`

### Mục tiêu
Thêm verification gate vào `SubtractPointsAsync`: chỉ cho phép redeem khi `Customer.IdentityLevel >= Verified`. Thêm API endpoint upgrade identity level qua OTP.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P2-T1 | `LoyaltyRewardsService.SubtractPointsAsync`: thêm check `Customer.IdentityLevel >= IdentityLevel.Verified` | `3_CoreHub/Services/LoyaltyRewardsService.cs` | ⬜ |
| 2 | P2-T2 | Tạo custom exception `IdentityLevelNotSufficientException` | `3_CoreHub/Services/` hoặc `1_Shared/` | ⬜ |
| 3 | P2-T3 | API endpoint `POST /api/customer-identity/upgrade/send-otp` — gửi OTP để upgrade level | `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` | ⬜ |
| 4 | P2-T4 | API endpoint `POST /api/customer-identity/upgrade/verify-otp` — verify OTP + update `IdentityLevel = Verified` | `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` | ⬜ |
| 5 | P2-T5 | `LoyaltyController` — catch `IdentityLevelNotSufficientException` → return 403 với upgrade required message | `5_WebApps/ShopERP/Controllers/LoyaltyController.cs` | ⬜ |
| 6 | P2-T6 | Unit test: `SubtractPointsAsync` throws khi `IdentityLevel < Verified` | `6_Tests/VanAn.Unit.Tests/` | ⬜ |
| 7 | P2-T7 | Unit test: `SubtractPointsAsync` succeeds khi `IdentityLevel >= Verified` | `6_Tests/VanAn.Unit.Tests/` | ⬜ |
| 8 | P2-T8 | Verify build: 0 errors + guard-check.ps1 pass + unit tests pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] `SubtractPointsAsync` throw exception khi `IdentityLevel < Verified`
- [ ] `SubtractPointsAsync` thành công khi `IdentityLevel >= Verified`
- [ ] API upgrade send-otp + verify-otp hoạt động
- [ ] `LoyaltyController` trả 403 với message rõ ràng khi insufficient level
- [ ] Unit tests pass (2 test cases minimum)
- [ ] Build: 0 errors

---

## 5. PHASE 3 — KhachLink UI: Social Login + Upgrade Modal

**Branch:** `feature/tiered-auth-phase3-khachlink-social-ui`
**Priority:** 1 (High)
**Task Card:** `docs/AI/tasks/tiered_auth_phase3_khachlink_social_ui_task_card.md`

### Mục tiêu
(1) Thêm nút "Đăng nhập với Google" vào Login.razor. (2) Wire up IdentityUpgradeModal với upgrade OTP flow. (3) Hiển thị identity level trên Profile.razor. (4) Khi user cố redeem mà chưa Verified, hiển thị upgrade modal.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P3-T1 | `Login.razor`: thêm nút "Đăng nhập với Google" (VanAnButton, Google icon) | `5_WebApps/KhachLink/Pages/Login.razor` | ⬜ |
| 2 | P3-T2 | `Login.razor`: Google login flow — redirect to `/api/auth/google/login` via Gateway | `5_WebApps/KhachLink/Pages/Login.razor` | ⬜ |
| 3 | P3-T3 | `Login.razor`: handle callback — parse token from URL query param, store localStorage | `5_WebApps/KhachLink/Pages/Login.razor` | ⬜ |
| 4 | P3-T4 | `IdentityUpgradeModal.razor`: wire `OnUpgrade` → gọi API upgrade send-otp | `5_WebApps/KhachLink/Components/IdentityUpgradeModal.razor` | ⬜ |
| 5 | P3-T5 | `IdentityUpgradeModal.razor`: thêm OTP input step + verify button | `5_WebApps/KhachLink/Components/IdentityUpgradeModal.razor` | ⬜ |
| 6 | P3-T6 | `Profile.razor`: hiển thị IdentityLevel badge (Social / Verified) | `5_WebApps/KhachLink/Pages/Profile.razor` | ⬜ |
| 7 | P3-T7 | `LoyaltyCard.razor`: khi redeem fail với 403, hiển thị IdentityUpgradeModal | `5_WebApps/KhachLink/Pages/LoyaltyCard.razor` | ⬜ |
| 8 | P3-T8 | Tạo `SocialAuthHttpService` cho KhachLink (nếu cần HTTP helper) | `5_WebApps/KhachLink/Services/Http/SocialAuthHttpService.cs` (NEW) | ⬜ |
| 9 | P3-T9 | DI registration + KhachLinkStartupTests assertion | `5_WebApps/KhachLink/Program.cs`, `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` | ⬜ |
| 10 | P3-T10 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] Login page có nút "Đăng nhập với Google"
- [ ] Google login redirect → callback → token stored trong localStorage
- [ ] IdentityUpgradeModal hiển thị OTP flow khi user click "Nâng cấp ngay"
- [ ] Profile hiển thị IdentityLevel badge
- [ ] LoyaltyCard hiển thị upgrade modal khi redeem fail (403)
- [ ] Build: 0 errors

---

## 6. PHASE 4 — Facebook OAuth

**Branch:** `feature/tiered-auth-phase4-facebook-oauth`
**Priority:** 2 (Medium)
**Task Card:** `docs/AI/tasks/tiered_auth_phase4_facebook_oauth_task_card.md`

### Mục tiêu
Thêm Facebook OAuth login flow, tương tự Google OAuth. Reuse `ISocialAuthService` pattern.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P4-T1 | Tạo `FacebookAuthService` — implement `ISocialAuthService` cho Facebook | `3_CoreHub/Services/FacebookAuthService.cs` (NEW) | ⬜ |
| 2 | P4-T2 | `SocialAuthController`: thêm endpoint `GET /api/auth/facebook/login` + `GET /api/auth/facebook/callback` | `5_WebApps/ShopERP/Controllers/SocialAuthController.cs` | ⬜ |
| 3 | P4-T3 | Config: `Facebook:AppId` + `Facebook:AppSecret` trong appsettings | `5_WebApps/ShopERP/appsettings.json`, `5_WebApps/ShopERP/appsettings.Production.json` | ⬜ |
| 4 | P4-T4 | DI registration: `FacebookAuthService` trong ShopERP `Program.cs` | `5_WebApps/ShopERP/Program.cs` | ⬜ |
| 5 | P4-T5 | `Login.razor`: thêm nút "Đăng nhập với Facebook" (VanAnButton, Facebook icon) | `5_WebApps/KhachLink/Pages/Login.razor` | ⬜ |
| 6 | P4-T6 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] `GET /api/auth/facebook/login` redirect đến Facebook consent
- [ ] Facebook callback verify access token thành công
- [ ] Customer mới tạo với `IdentityLevel = Social`
- [ ] Login page có nút "Đăng nhập với Facebook"
- [ ] Build: 0 errors

---

## 7. PHASE 5 — Zalo ZNS OTP (Cost Optimization)

**Branch:** `feature/tiered-auth-phase5-zalo-zns-otp`
**Priority:** 2 (Medium — cost optimization)
**Task Card:** `docs/AI/tasks/tiered_auth_phase5_zalo_zns_task_card.md`

### Mục tiêu
Implement Zalo ZNS OTP provider (300đ/OTP) làm alternative cho eSMS (1.000-1.200đ/OTP). Ưu tiên Zalo ZNS cho OTP upgrade flow, fallback eSMS nếu Zalo không available.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P5-T1 | Tạo `IZnsService` interface + `ZaloZnsService` implementation — gọi Zalo ZNS API | `3_CoreHub/Services/IZnsService.cs` (NEW), `3_CoreHub/Services/ZaloZnsService.cs` (NEW) | ⬜ |
| 2 | P5-T2 | Tạo ZNS OTP template trên Zalo OA Portal (user thực hiện manually) | Zalo Cloud Automation Portal | ⬜ |
| 3 | P5-T3 | Config: `Zalo:AccessToken` + `Zalo:TemplateId` trong appsettings | `5_WebApps/ShopERP/appsettings.json`, `5_WebApps/ShopERP/appsettings.Production.json` | ⬜ |
| 4 | P5-T4 | Tạo `CompositeOtpService` — ưu tiên Zalo ZNS, fallback eSMS | `5_WebApps/ShopERP/Services/CompositeOtpService.cs` (NEW) | ⬜ |
| 5 | P5-T5 | DI registration: thay `EsmsNotificationService` bằng `CompositeOtpService` | `5_WebApps/ShopERP/Program.cs` | ⬜ |
| 6 | P5-T6 | `CustomerIdentityController`: dùng `CompositeOtpService` cho upgrade OTP flow | `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` | ⬜ |
| 7 | P5-T7 | Unit test: `ZaloZnsService.SendOtpAsync` — mock HTTP call | `6_Tests/VanAn.Unit.Tests/` | ⬜ |
| 8 | P5-T8 | Unit test: `CompositeOtpService` — fallback to eSMS khi Zalo fail | `6_Tests/VanAn.Unit.Tests/` | ⬜ |
| 9 | P5-T9 | Verify build: 0 errors + guard-check.ps1 pass + unit tests pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] `ZaloZnsService` gửi OTP qua Zalo ZNS API thành công
- [ ] `CompositeOtpService` ưu tiên Zalo, fallback eSMS
- [ ] Config dùng environment variables cho production
- [ ] Unit tests pass (2 test cases minimum)
- [ ] Build: 0 errors

---

## 8. PHASE 6 — E2E Playwright Tests

**Branch:** `feature/tiered-auth-phase6-e2e-tests`
**Priority:** 3 (Final validation)
**Task Card:** `docs/AI/tasks/tiered_auth_phase6_e2e_tests_task_card.md`

### Mục tiêu
Tạo E2E test full luồng tiered auth: (1) Social login → earn points → redeem (blocked) → upgrade OTP → redeem (success). (2) Phone OTP login → redeem (success, already Verified).

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | P6-T1 | Tạo `tiered-auth-social-login-flow.spec.ts` — Scenario 1: Google login → earn → redeem blocked → upgrade OTP → redeem success | `6_Testing/e2e-tests/tiered-auth-social-login-flow.spec.ts` (NEW) | ⬜ |
| 2 | P6-T2 | Tạo `tiered-auth-otp-login-flow.spec.ts` — Scenario 2: Phone OTP login (already Verified) → redeem success | `6_Testing/e2e-tests/tiered-auth-otp-login-flow.spec.ts` (NEW) | ⬜ |
| 3 | P6-T3 | Page Object Model: SocialAuth page object + IdentityUpgrade page object | `6_Testing/e2e-tests/pages/SocialAuthPage.ts` (NEW), `6_Testing/e2e-tests/pages/IdentityUpgradePage.ts` (NEW) | ⬜ |
| 4 | P6-T4 | Test: Facebook login flow | same spec file hoặc new | ⬜ |
| 5 | P6-T5 | Test: Zalo ZNS OTP delivery (mocked) | same spec file | ⬜ |
| 6 | P6-T6 | Run E2E tests + fix flaky issues | `6_Testing/` | ⬜ |
| 7 | P6-T7 | Verify: all E2E tests pass | `6_Testing/` | ⬜ |

### Exit criteria
- [ ] Scenario 1 (social login → upgrade → redeem) pass
- [ ] Scenario 2 (OTP login → redeem) pass
- [ ] Facebook login flow pass
- [ ] Zalo ZNS OTP delivery (mocked) pass
- [ ] Không có flaky test
- [ ] E2E coverage: social login, OTP login, earn points, redeem blocked, upgrade OTP, redeem success, Zalo ZNS

---

## 9. PHASE DEPENDENCY GRAPH

```
PHASE 0 (Domain: IdentityLevel + Migration) ← BLOCKING
  │
  ├── PHASE 1 (Google OAuth)
  │     │
  │     └── PHASE 2 (Verification Gate)
  │           │
  │           └── PHASE 3 (KhachLink UI: Social Login + Upgrade Modal)
  │                 │
  │                 ├── PHASE 4 (Facebook OAuth)
  │                 │     │
  │                 │     └── PHASE 5 (Zalo ZNS OTP)
  │                 │           │
  │                 │           └── PHASE 6 (E2E Tests)
  │                 │
  │                 └── PHASE 5 (can start after P3)
  │
  └── PHASE 2 (can start after P0, parallel with P1)
```

**Critical path:** P0 → P1 → P2 → P3 → P4 → P5 → P6
**Parallel option:** P2 can run parallel with P1 (both depend on P0 only)

---

## 10. RISK ASSESSMENT

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Google OAuth redirect fail trong PWA | Medium | High | Test trên cả desktop và mobile PWA |
| Customer email conflict (social vs existing phone customer) | Medium | High | Match by email first, fallback to phone, allow manual link |
| EF migration conflict (existing Customer table) | Low | Medium | Add column với default value, không drop existing |
| Zalo ZNS template approval delay | Medium | Low | Start template approval sớm (P5-T2 manual), fallback eSMS |
| KhachLink WASM redirect handling | Medium | Medium | Dùng NavigationManager + query param, test kỹ |
| SubtractPointsAsync gate break existing flows | Low | High | Unit test cả success + fail cases, check all callers |
| Facebook API deprecation risk | Low | Low | Dùng latest Graph API version |

---

## 11. SUCCESS CRITERIA (OVERALL)

- [ ] `IdentityLevel` enum trên `Customer` (Guest, Social, Verified, Full)
- [ ] Google OAuth login hoạt động (redirect → callback → token)
- [ ] Facebook OAuth login hoạt động
- [ ] Social login customer có `IdentityLevel = Social`
- [ ] OTP verify customer có `IdentityLevel = Verified`
- [ ] `SubtractPointsAsync` block redeem khi `IdentityLevel < Verified`
- [ ] Upgrade OTP flow: Social → Verified
- [ ] KhachLink Login page có nút Google + Facebook
- [ ] IdentityUpgradeModal wire up với upgrade API
- [ ] Profile hiển thị IdentityLevel badge
- [ ] Zalo ZNS OTP provider hoạt động (300đ/OTP)
- [ ] CompositeOtpService ưu tiên Zalo, fallback eSMS
- [ ] E2E tests: 2+ scenarios pass
- [ ] Build: 0 errors
- [ ] guard-check.ps1 pass
- [ ] Cost saving: ~70-85% (OTP chỉ gửi khi redeem/upgrade, không phải mỗi login)

---

## 12. COST ANALYSIS

| Scenario | Hiện tại (OTP cho mọi login) | Tiered Auth (Social + OTP cho redeem) |
|---|---|---|
| 1000 khách, 2 login/tháng | 2.000 × 1.000-1.200đ = 2.000.000-2.400.000đ | ~0đ (Social Login free) |
| 200 lượt redeem/tháng | (đã tính trong login) | 200 × 300đ (Zalo) = 60.000đ |
| 100 lượt upgrade/tháng | (đã tính trong login) | 100 × 300đ (Zalo) = 30.000đ |
| **Tổng/tháng** | **2.000.000-2.400.000đ** | **~90.000đ** |
| **Tiết kiệm** | — | **~96%** |

---

## 13. POST-COMPLETION

Sau khi tất cả 7 phases complete:
1. Update `docs/AI/project_state.md` — move objective to history, add completed items
2. Commit final: `[TIERED AUTH] All 7 phases complete — tiered authentication operational`
3. Tag: `tiered-auth-v1.0`
4. Monitor cost: so sánh OTP spend trước/sau 1 tháng
5. Consider: Apple Sign In (Phase 7 — future) nếu user demand
