# TASK CARD: Tiered Auth — Phase 2 — Verification Gate trong SubtractPointsAsync

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thêm verification gate vào `SubtractPointsAsync` — chỉ cho redeem khi `IdentityLevel >= Verified`. Thêm API endpoint upgrade identity level qua OTP.
- **Nghiệp vụ áp dụng:** Tier 2 — Redeem points yêu cầu xác thực SMS OTP
- **Status:** ✅ CODE COMPLETE (online RV pending on khachvip.online)
- **Branch:** `main`
- **Dependency:** Phase 0 (IdentityLevel phải tồn tại)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Phase 2 of 7
- **Dependency:** Phase 0 COMPLETE (can run parallel with Phase 1)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `3_CoreHub/Services/IdentityLevelNotSufficientException.cs` — custom exception

### Files cần MODIFY
- `3_CoreHub/Services/LoyaltyRewardsService.cs` — thêm IdentityLevel check vào `SubtractPointsAsync`
- `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — thêm upgrade endpoints
- `5_WebApps/ShopERP/Controllers/LoyaltyController.cs` — catch exception → 403 response

### Files cần CREATE (tests)
- `6_Tests/VanAn.Unit.Tests/Services/LoyaltyRewardsServiceVerificationGateTests.cs`

### Files READ ONLY
- `3_CoreHub/Services/LoyaltyRewardsService.cs:80-134` — SubtractPointsAsync current implementation
- `5_WebApps/ShopERP/Controllers/LoyaltyController.cs` — existing loyalty endpoints
- `3_CoreHub/Repositories/ILoyaltyRewardsRepository.cs` — repository methods

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs` (Phase 0 đã xong)
- KHÔNG tạo UI (đó là Phase 3)
- KHÔNG implement Social Login (đó là Phase 1)
- Chỉ thêm check vào `SubtractPointsAsync`, KHÔNG sửa `AddPointsAsync` (earn không cần Verified)

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Gate logic:** `if (customer.IdentityLevel < IdentityLevel.Verified) throw IdentityLevelNotSufficientException`
- [ ] **Exception message:** Clear message chỉ ra cần upgrade qua OTP
- [ ] **Upgrade endpoints:** `POST /api/customer-identity/upgrade/send-otp` + `POST /api/customer-identity/upgrade/verify-otp`
- [ ] **Upgrade flow:** Send OTP → verify → `customer.UpgradeIdentityLevel(IdentityLevel.Verified)` → save
- [ ] **LoyaltyController:** Catch `IdentityLevelNotSufficientException` → return 403 với `{ error, requiresUpgrade: true, currentLevel, requiredLevel }`
- [ ] **Unit tests:** TDD — test trước, code sau (per governance rules)

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** `SubtractPointsAsync` throw `IdentityLevelNotSufficientException` khi `IdentityLevel < Verified`
- [ ] **SC2:** `SubtractPointsAsync` thành công khi `IdentityLevel >= Verified`
- [ ] **SC3:** `POST /api/customer-identity/upgrade/send-otp` gửi OTP thành công
- [ ] **SC4:** `POST /api/customer-identity/upgrade/verify-otp` verify OTP + update IdentityLevel = Verified
- [ ] **SC5:** `LoyaltyController` trả 403 với upgrade required message
- [ ] **SC6:** Unit tests: 2 test cases pass (blocked + success)
- [ ] **SC7:** Build: 0 errors
- [ ] **SC8:** guard-check.ps1 ALL CHECKS PASSED

---

## 6. SKILLS
- `domain-integrity-validation` — verify gate logic correctness
- `test-system-upgrade` — TDD approach for verification gate

---

## 7. AI HEALTH CHECK
- **Assumptions:** 0
- **Verified Facts:** 5 (SubtractPointsAsync implementation, LoyaltyController endpoints, CustomerIdentityController pattern, OtpService pattern, ICustomerRepository methods)
- **Open Questions:** 0
- **Gate check:** Assumptions (0) < Verified Facts (5) → OK để proceed

---

## 8. LIVE RUNTIME VERIFICATION (MANDATORY — see Wave 0 lesson)
> Static checks (build + architecture tests + guard-check) KHÔNG đảm bảo runtime works.
> Phải boot app + test HTTP/UI thực tế trước khi mark phase COMPLETE.

**Prerequisites:**
- [ ] Docker Desktop running (PostgreSQL 5432 + NATS 4222)
- [ ] ShopERP started on http://localhost:5003 (watch logs: no startup errors)
- [ ] KhachLink started on http://localhost:5002 (PWA loads)
- [ ] Gateway started on http://localhost:5001
- [ ] Phase 0 COMPLETE (IdentityLevel column exists in DB)

**RV tests (all MUST pass):**
- [ ] **RV1 — Redeem blocked (Social):** Customer với `IdentityLevel = Social` → `POST /api/loyalty/redeem` → HTTP 403 với `{ error, requiresUpgrade: true, currentLevel: "Social", requiredLevel: "Verified" }`.
- [ ] **RV2 — Redeem success (Verified):** Customer với `IdentityLevel = Verified` → `POST /api/loyalty/redeem` → HTTP 200 → points deducted → query DB confirm.
- [ ] **RV3 — Upgrade send OTP:** `POST /api/customer-identity/upgrade/send-otp` với phone number → OTP gửi thành công → `OtpService.VerifyOtp` returns true cho OTP vừa gửi.
- [ ] **RV4 — Upgrade verify OTP:** `POST /api/customer-identity/upgrade/verify-otp` với correct OTP → `IdentityLevel` updated = Verified → query DB confirm `SELECT IdentityLevel FROM Customers WHERE ...` → value = 2.
- [ ] **RV5 — Upgrade wrong OTP:** `POST /api/customer-identity/upgrade/verify-otp` với wrong OTP → HTTP 400 → `IdentityLevel` KHÔNG thay đổi.
- [ ] **RV6 — Earn points unaffected:** `POST /api/loyalty/earn` (AddPointsAsync) → vẫn thành công cho `IdentityLevel = Social` → points added → KHÔNG bị gate block.
- [ ] **RV7 — LINQ translation:** Mọi query mới dùng direct property comparison — verify không `InvalidOperationException`.
- [ ] **RV8 — Unit tests pass:** `dotnet test --filter "LoyaltyRewardsServiceVerificationGate"` → 2/2 PASS (blocked + success).
- [ ] **RV9 — Build + guard-check:** `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` ALL CHECKS PASSED.
