# TASK CARD: Tiered Auth — Phase 6 — E2E Playwright Tests

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo E2E test full luồng tiered auth: (1) Social login → earn → redeem blocked → upgrade OTP → redeem success. (2) Phone OTP login (Verified) → redeem success.
- **Nghiệp vụ áp dụng:** Final validation — toàn bộ tiered auth flow
- **Status:** ⬜ NOT STARTED
- **Branch:** `feature/tiered-auth-phase6-e2e-tests`
- **Dependency:** Phase 0-5 ALL COMPLETE

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT (tests only)
- **Current Phase:** Phase 6 of 7
- **Dependency:** Phase 0 + 1 + 2 + 3 + 4 + 5 COMPLETE

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `6_Testing/e2e-tests/tiered-auth-social-login-flow.spec.ts` — Scenario 1
- `6_Testing/e2e-tests/tiered-auth-otp-login-flow.spec.ts` — Scenario 2
- `6_Testing/e2e-tests/pages/SocialAuthPage.ts` — Page Object
- `6_Testing/e2e-tests/pages/IdentityUpgradePage.ts` — Page Object

### Files READ ONLY
- `6_Testing/e2e-tests/khachlink-full-order-flow.spec.ts` — existing E2E pattern
- `6_Testing/e2e-tests/khachlink-minimal-flow.spec.ts` — existing E2E pattern
- `6_Testing/e2e-tests/pages/ShopSettingsPage.ts` — existing Page Object pattern
- `6_Testing/e2e-tests/pages/` — existing Page Objects

### Boundary Rules
- KHÔNG sửa production code (Phase 0-5 đã xong)
- Chỉ tạo test files + Page Objects
- Follow existing E2E test patterns (Playwright + Page Object Model)
- Dùng `waitForResponse` thay vì fixed wait (per lesson from Wave 4)

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Playwright:** Dùng existing Playwright config trong `6_Testing/e2e-tests/`
- [ ] **Page Object Model:** Follow existing pattern trong `6_Testing/e2e-tests/pages/`
- [ ] **Mocking:** Google/Facebook OAuth mocked (không test real OAuth redirect)
- [ ] **OTP mocking:** Dev mode exposes OTP via `X-Dev-OTP` header (existing pattern)
- [ ] **Zalo ZNS:** Mocked — không gọi real Zalo API
- [ ] **Test data:** Dùng default tenant `00000000-0000-0000-0000-000000000001`

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** Scenario 1 (social login → earn → redeem blocked → upgrade OTP → redeem success) pass
- [ ] **SC2:** Scenario 2 (phone OTP login → redeem success, already Verified) pass
- [ ] **SC3:** Facebook login flow pass (mocked)
- [ ] **SC4:** Zalo ZNS OTP delivery (mocked) pass
- [ ] **SC5:** Không có flaky test
- [ ] **SC6:** E2E coverage: social login, OTP login, earn points, redeem blocked, upgrade OTP, redeem success, Zalo ZNS, Facebook

---

## 6. SKILLS
- `playwright_cost_optimizer` — efficient E2E test design
- `test-system-upgrade` — test quality

---

## 7. AI HEALTH CHECK
- **Assumptions:** 0
- **Verified Facts:** 4 (existing E2E patterns, Page Object pattern, OTP dev header pattern, Playwright config)
- **Open Questions:** 0
- **Gate check:** Assumptions (0) < Verified Facts (4) → OK để proceed

---

## 8. LIVE RUNTIME VERIFICATION (MANDATORY — see Wave 0 lesson)
> Static checks (build + architecture tests + guard-check) KHÔNG đảm bảo runtime works.
> Phải boot app + test HTTP/UI thực tế trước khi mark phase COMPLETE.

**Prerequisites:**
- [ ] Docker Desktop running (PostgreSQL 5432 + NATS 4222)
- [ ] ShopERP started on http://localhost:5003 (watch logs: no startup errors)
- [ ] KhachLink started on http://localhost:5002 (PWA loads)
- [ ] Gateway started on http://localhost:5001
- [ ] Phase 0-5 ALL COMPLETE (full tiered auth operational)
- [ ] Playwright installed: `npx playwright install chromium` trong `6_Testing/e2e-tests/`
- [ ] Dev mode: `X-Dev-OTP` header exposes OTP for testing (existing pattern)

**RV tests (all MUST pass):**
- [ ] **RV1 — Scenario 1 full flow:** `npx playwright test tiered-auth-social-login-flow` → social login (mocked) → earn points → redeem blocked (403) → upgrade OTP → redeem success → test PASS.
- [ ] **RV2 — Scenario 2 full flow:** `npx playwright test tiered-auth-otp-login-flow` → phone OTP login (already Verified) → redeem success → test PASS.
- [ ] **RV3 — Facebook login (mocked):** Facebook OAuth flow (mocked) → customer created with `IdentityLevel = Social` → test PASS.
- [ ] **RV4 — Zalo ZNS OTP (mocked):** Upgrade OTP via Zalo ZNS (mocked) → `IdentityLevel = Verified` → test PASS.
- [ ] **RV5 — No flaky tests:** Run `npx playwright test tiered-auth-*` 3 lần liên tiếp → all PASS mọi lần (không flaky).
- [ ] **RV6 — Page Object coverage:** SocialAuthPage + IdentityUpgradePage Page Objects used trong cả 2 spec files → grep `import` confirm.
- [ ] **RV7 — E2E test count:** `npx playwright test tiered-auth-* --list` → ≥ 4 test cases (2 scenarios + Facebook + Zalo).
- [ ] **RV8 — Build + guard-check:** `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` ALL CHECKS PASSED.
