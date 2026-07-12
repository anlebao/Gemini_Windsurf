# TASK CARD: Tiered Auth — Phase 5 — Zalo ZNS OTP (Cost Optimization)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Implement Zalo ZNS OTP provider (300đ/OTP) làm alternative cho eSMS (1.000-1.200đ/OTP). Ưu tiên Zalo ZNS, fallback eSMS.
- **Nghiệp vụ áp dụng:** Cost optimization — giảm OTP cost ~70% so với eSMS
- **Status:** ⬜ NOT STARTED
- **Branch:** `feature/tiered-auth-phase5-zalo-zns-otp`
- **Dependency:** Phase 2 (upgrade OTP flow phải tồn tại) + Phase 3 (UI phải tồn tại)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** ANALYZE → IMPLEMENT
- **Current Phase:** Phase 5 of 7
- **Dependency:** Phase 2 + Phase 3 COMPLETE

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `3_CoreHub/Services/IZnsService.cs` — interface
- `3_CoreHub/Services/ZaloZnsService.cs` — Zalo ZNS API implementation
- `5_WebApps/ShopERP/Services/CompositeOtpService.cs` — ưu tiên Zalo, fallback eSMS
- `6_Tests/VanAn.Unit.Tests/Services/ZaloZnsServiceTests.cs`
- `6_Tests/VanAn.Unit.Tests/Services/CompositeOtpServiceTests.cs`

### Files cần MODIFY
- `5_WebApps/ShopERP/Program.cs` — DI registration (thay EsmsNotificationService bằng CompositeOtpService)
- `5_WebApps/ShopERP/appsettings.json` — Zalo config section
- `5_WebApps/ShopERP/appsettings.Production.json` — Zalo config (env vars)
- `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — dùng CompositeOtpService

### Files READ ONLY
- `3_CoreHub/Services/EsmsNotificationService.cs` — existing SMS provider pattern
- `3_CoreHub/Services/ISmsService.cs` — existing SMS interface
- `5_WebApps/ShopERP/Services/OtpService.cs` — existing OTP generation/storage
- `5_WebApps/ShopERP/appsettings.Production.json:26-30` — eSMS config pattern

### Boundary Rules
- KHÔNG sửa `1_Shared/Domain.cs`
- KHÔNG xóa `EsmsNotificationService` — giữ làm fallback
- KHÔNG sửa KhachLink UI (đã xong trong P3)
- Zalo ZNS template phải được user tạo manually trên Zalo OA Portal

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **Zalo ZNS API:** `POST https://business.openapi.zalo.me/message/template` với Access Token + Template ID
- [ ] **OTP template:** User tạo trên Zalo Cloud Automation Portal (template type: Xác thực — Loại 1)
- [ ] **Config:** `Zalo:AccessToken` + `Zalo:TemplateId` + `Zalo:OaId` — environment variables cho production
- [ ] **CompositeOtpService:** Try Zalo ZNS first → if fail (HTTP error, timeout, invalid response) → fallback eSMS
- [ ] **Logging:** Log provider used (Zalo vs eSMS) cho cost tracking
- [ ] **Unit tests:** Mock HttpClient cho Zalo API + fallback scenario
- [ ] **Cost tracking:** Log mỗi OTP gửi kèm provider name + estimated cost

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** `ZaloZnsService.SendOtpAsync` gửi OTP qua Zalo ZNS API thành công
- [ ] **SC2:** `CompositeOtpService` ưu tiên Zalo ZNS, fallback eSMS khi Zalo fail
- [ ] **SC3:** Config dùng environment variables cho production
- [ ] **SC4:** Logging hiển thị provider used (Zalo vs eSMS)
- [ ] **SC5:** Unit tests: 2 test cases pass (Zalo success + eSMS fallback)
- [ ] **SC6:** Build: 0 errors
- [ ] **SC7:** guard-check.ps1 ALL CHECKS PASSED

---

## 6. SKILLS
- `einvoice-integration` — similar third-party API integration pattern
- `test-system-upgrade` — TDD for Zalo + fallback

---

## 7. AI HEALTH CHECK
- **Assumptions:** 2 (Zalo ZNS API endpoint correct, Zalo OA template approved)
- **Verified Facts:** 4 (EsmsNotificationService pattern, ISmsService interface, OtpService pattern, appsettings env var pattern)
- **Open Questions:** 2 (Zalo ZNS template ID — user phải tạo, Zalo Access Token — user phải provide)
- **Gate check:** Assumptions (2) < Verified Facts (4) → OK, nhưng cần user provide Zalo credentials

---

## 8. USER ACTION REQUIRED
User cần thực hiện các bước sau trên Zalo Cloud Automation Portal:
1. Đăng nhập Zalo OA → Zalo Cloud Automation (ZCA) Portal
2. Developers → tạo application → lấy Access Token
3. Tạo OTP template (Loại 1 — Xác thực) → lấy Template ID
4. Cung cấp `AccessToken` + `TemplateId` + `OaId` cho developer
