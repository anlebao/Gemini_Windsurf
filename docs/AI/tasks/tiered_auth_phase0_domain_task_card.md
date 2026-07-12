# TASK CARD: Tiered Auth — Phase 0 — Domain: IdentityLevel + Migration

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Thêm `IdentityLevel` enum + property vào `Customer` entity + EF migration. Nền tảng BLOCKING cho mọi phase sau (P1-P6).
- **Nghiệp vụ áp dụng:** Tiered authentication — Tier 1 (Social) earn points, Tier 2+ (Verified) redeem points
- **Status:** ⬜ NOT STARTED
- **Branch:** `feature/tiered-auth-phase0-domain-identity-level`
- **Tech Debt:** N/A — new feature

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Phase 0 of 7
- **Dependency:** None (first phase — BLOCKING cho P1-P6)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/tiered_auth_loyalty_master_plan.md` (READ — master plan)

### Files cần MODIFY
- `1_Shared/Domain.cs` — thêm `IdentityLevel` enum + property trên `Customer` + `UpgradeIdentityLevel()` method
- `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs` — map `IdentityLevel` column
- `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — set `IdentityLevel = Verified` khi OTP verify + thêm field vào response
- `5_WebApps/ShopERP/Infrastructure/ShopERPDbContext.cs` — (nếu cần) ensure migration context

### Files READ ONLY (investigate patterns)
- `3_CoreHub/Infrastructure/Configurations/CustomerConfiguration.cs` — check existing mapping patterns
- `5_WebApps/ShopERP/Migrations/` — check migration patterns
- `1_Shared/Domain.cs:608-653` — Customer entity structure

### Boundary Rules
- ĐƯỢC PHÉP sửa `1_Shared/Domain.cs` (user approved — thêm `IdentityLevel` là part of approved feature plan)
- KHÔNG sửa `AccountingEntry` hoặc bất kỳ entity nào khác
- KHÔNG tạo UI hay controller mới (đó là P1-P3)
- KHÔNG implement Social Login (đó là P1)

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Domain Protection:** Sửa `Customer` entity chỉ thêm property + method, KHÔNG thay đổi existing properties
- [ ] **IdentityLevel enum:** `Guest = 0, Social = 1, Verified = 2, Full = 3` — int-backed cho EF mapping
- [ ] **Default value:** `IdentityLevel.Social` (khách hàng mới qua social login)
- [ ] **Migration:** Add column với default value, KHÔNG drop existing columns
- [ ] **OTP verify:** Khi OTP verify thành công → `IdentityLevel = Verified` (upgrade từ Social)
- [ ] **CustomerIdentityResponse:** Thêm `IdentityLevel` field (string) để KhachLink consume

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** `IdentityLevel` enum tồn tại trong `Domain.cs` (Guest, Social, Verified, Full)
- [ ] **SC2:** `Customer.IdentityLevel` property tồn tại, default = `Social`
- [ ] **SC3:** `Customer.UpgradeIdentityLevel(IdentityLevel)` method tồn tại, chỉ cho phép upgrade (không downgrade)
- [ ] **SC4:** EF migration tạo column `IdentityLevel` với default `Social` (int = 1)
- [ ] **SC5:** `CustomerIdentityController.VerifyOtp` set `IdentityLevel = Verified` khi OTP verify
- [ ] **SC6:** `CustomerIdentityResponse` trả về `IdentityLevel` field
- [ ] **SC7:** Build: 0 errors
- [ ] **SC8:** guard-check.ps1 ALL CHECKS PASSED

---

## 6. SKILLS
- `domain-integrity-validation` — verify Customer entity consistency
- `system-refactor-safety` — safe addition to existing entity

---

## 7. AI HEALTH CHECK
- **Assumptions:** 0
- **Verified Facts:** 5 (Customer entity structure, CustomerConfiguration exists, CustomerIdentityController flow, Migration patterns, CustomerIdentityResponse structure)
- **Open Questions:** 0
- **Gate check:** Assumptions (0) < Verified Facts (5) → OK để proceed

---

## 8. LIVE RUNTIME VERIFICATION (MANDATORY — see Wave 0 lesson)
> Static checks (build + architecture tests + guard-check) KHÔNG đảm bảo runtime works.
> Phải boot app + test HTTP/UI thực tế trước khi mark phase COMPLETE.

**Prerequisites:**
- [ ] Docker Desktop running (PostgreSQL 5432 + NATS 4222)
- [ ] ShopERP started on http://localhost:5003 (watch logs: migration applied + seed OK)
- [ ] KhachLink started on http://localhost:5002 (PWA loads)
- [ ] Gateway started on http://localhost:5001

**RV tests (all MUST pass):**
- [ ] **RV1 — EF Migration applied:** `dotnet ef database update` trong ShopERP → log hiển thị `AddCustomerIdentityLevel` migration applied. Không `no such table` error.
- [ ] **RV2 — Column exists:** Query SQLite `PRAGMA table_info(Customers)` → column `IdentityLevel` tồn tại, default = 1 (Social).
- [ ] **RV3 — OTP verify sets Verified:** `POST /api/customers/otp/send` + `POST /api/customers/otp/verify` thành công → query `SELECT IdentityLevel FROM Customers WHERE ...` → value = 2 (Verified).
- [ ] **RV4 — CustomerIdentityResponse trả IdentityLevel:** `POST /api/customers/otp/verify` → response JSON chứa `"identityLevel": "Verified"` (hoặc `"Social"` cho customer có sẵn chưa verify).
- [ ] **RV5 — LINQ translation:** Mọi query mới dùng direct property comparison (KHÔNG `EF.Property<Guid>` cho TenantId) — verify không `InvalidOperationException: LINQ expression could not be translated`.
- [ ] **RV6 — Existing customers unaffected:** Query existing customers → `IdentityLevel` = 1 (Social, default) — không break existing data.
- [ ] **RV7 — Build + guard-check:** `dotnet build VanAn.sln` 0 errors + `guard-check.ps1` ALL CHECKS PASSED.
