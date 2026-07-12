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
