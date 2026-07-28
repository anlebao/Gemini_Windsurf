# TASK CARD: Community Commerce — Sprint 6 — Admin + Fraud Review + Polish + Legal (v1.2)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Admin API + UI để kích hoạt/hủy role cộng tác viên + push notification + Profile roles + **Fraud Review UI (v1.2 NEW)** + legal documents + full E2E regression.
- **Nghiệp vụ áp dụng:** UC-02 (Admin activate role) + **Fraud Review workflow (v1.2 NEW — admin review FraudFlag từ Sprint 4)** + legal checklist từ requirements spec v1.2.
- **Status:** NOT STARTED
- **Branch:** `feature/community-sprint6-admin-legal`
- **v1.2 changes:**
  - **Fraud Review UI:** `/admin/fraud-flags` page — list pending FraudFlag sort by RiskScore, detail modal với risk factors + related entities, action buttons Confirm/Dismiss/MarkReviewed.
  - **Fraud Stats dashboard:** `/admin/fraud-stats` — pending count, confirmed count (with $ loss prevented), dismissed count, top 5 flagged customers.
  - **FraudFlagController:** Full endpoints (preview in Sprint 4, full impl ở đây) — GET list, GET detail, POST confirm/dismiss/review, GET stats.
  - **3-strike ban logic:** Confirm 3 FraudFlags cho same Customer → auto-ban customer (IsActive=false on Customer hoặc flag).
  - **Legal docs:** Thêm device fingerprint consent clause + anti-fraud policy document.

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Sprint 6 of 7 (FINAL) — v1.2: tăng sessions từ 3 → 4 (+Fraud Review)
- **Dependency:** Sprint 5 COMPLETE (wallet + COD working) + **Sprint 4 COMPLETE (FraudFlag data exists — v1.2 NEW)**

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `2_Gateway/Controllers/CommunityAdminController.cs` — admin endpoints
- `2_Gateway/Controllers/FraudFlagController.cs` (v1.2 NEW — full impl, preview in S4) — admin FraudFlag endpoints
- `3_CoreHub/Services/ICommunityAdminService.cs` — interface
- `3_CoreHub/Services/CommunityAdminService.cs` — eligible list, activate/deactivate
- `3_CoreHub/Services/IFraudReviewService.cs` (v1.2 NEW) — interface
- `3_CoreHub/Services/FraudReviewService.cs` (v1.2 NEW) — list pending, confirm/dismiss/review, 3-strike ban logic, fraud stats
- `5_WebApps/ShopERP/Components/Pages/Community/AdminPanel.razor` — admin UI (ShopERP)
- `5_WebApps/ShopERP/Components/Pages/Community/FraudFlags.razor` (v1.2 NEW) — Fraud Review UI
- `5_WebApps/ShopERP/Components/Pages/Community/FraudStats.razor` (v1.2 NEW) — Fraud Stats dashboard
- `5_WebApps/KhachLink/Pages/Profile.razor` — modify to show community roles (or create if not exists)
- `docs/legal/community-terms-of-service.md` — điều khoản sử dụng
- `docs/legal/community-privacy-policy.md` — chính sách bảo mật — **v1.2: +device fingerprint consent clause**
- `docs/legal/marketplace-policy.md` — quy chế sàn TMĐT
- `docs/legal/anti-fraud-policy.md` (v1.2 NEW) — 3-strike ban, hold 48h, KYC bank account cho payout
- `6_Testing/e2e-tests/community-admin.spec.ts`
- `6_Testing/e2e-tests/community-fraud-review.spec.ts` (v1.2 NEW)
- `6_Testing/e2e-tests/community-full-regression.spec.ts`

### Files cần MODIFY
- `2_Gateway/Program.cs` — DI for CommunityAdminService + auth policy "CommunityAdmin"
- `5_WebApps/ShopERP/Program.cs` — DI if needed
- `5_WebApps/KhachLink/Pages/Profile.razor` — display community roles
- `5_WebApps/KhachLink/Services/Http/CommunityHttpService.cs` — add admin HTTP calls (if needed)

### Files READ ONLY
- `2_Gateway/Controllers/TenantOnboardingController.cs` — SystemAdmin controller pattern
- `2_Gateway/Controllers/ShopInstancesController.cs` — admin CRUD pattern
- `2_Gateway/Program.cs` — "SystemAdmin" policy pattern
- `5_WebApps/ShopERP/Controllers/CustomerIdentityController.cs` — customer profile endpoint

### Boundary Rules
- Admin API: SystemAdmin JWT auth (existing policy) — NOT X-Customer-Token
- Push notification: reuse existing PushSubscription + push service
- Legal documents: draft only — not legal advice, require lawyer review before launch
- Full E2E regression: tất cả community-*.spec.ts phải pass
- KHÔNG sửa Domain.cs

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS
- [ ] **Admin auth:** SystemAdmin JWT policy (existing) — cross-tenant operations
- [ ] **Eligible criteria:** IdentityLevel >= Verified OR IdentityLevel >= DeviceVerified (v1.2 NEW) AND LoyaltyPoints >= 1000
- [ ] **Push notification:** Reuse existing VAPID + PushSubscription infrastructure
- [ ] **Profile roles:** Query CommunityRole WHERE CustomerId → display in KhachLink Profile
- [ ] **Legal:** Draft documents based on Nghị định 13/2023 (data protection) + Thông tư 39/TT-BCT (e-commerce) + **v1.2: device fingerprint consent + anti-fraud policy**
- [ ] **Full regression:** Tất cả E2E specs từ S1-S5 phải pass trên VPS
- [ ] **v1.2 NEW — Fraud Review auth:** SystemAdmin JWT policy (same as admin endpoints).
- [ ] **v1.2 NEW — 3-strike ban:** Confirm 3 FraudFlags cho same CustomerId → auto-ban (Customer.IsActive=false hoặc flag). Verify before ban.
- [ ] **v1.2 NEW — Fraud Review UI:** List pending FraudFlag sort by RiskScore desc. Detail modal show risk factors (JSON pretty) + related entities (DeviceRegistration, Customer, Order/SalesReferral/AppInstallAttribution).
- [ ] **v1.2 NEW — Confirm action side effects:** Update related entity status (SalesReferral.CommissionStatus=Rejected, AppInstallAttribution.AttributionStatus=Rejected). Create Reversal Wallet transaction if commission/bonus đã pay.
- [ ] **v1.2 NEW — Dismiss action side effects:** Whitelist entity (DeviceRegistration.IsVerified=true, RiskScore giảm). KHÔNG tính strike.

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** GET `/api/admin/community/eligible` trả list customer đủ điều kiện
- [ ] **SC2:** POST `/api/admin/community/{customerId}/activate-role` tạo CommunityRole + push notification
- [ ] **SC3:** POST `/api/admin/community/{customerId}/deactivate-role` set IsActive=false
- [ ] **SC4:** AdminPanel UI (ShopERP): list eligible + activate/deactivate buttons
- [ ] **SC5:** Profile page (KhachLink): hiển thị community roles
- [ ] **SC6:** Push notification gửi khi activate role
- [ ] **SC7:** 3 legal documents draft hoàn thành
- [ ] **SC8:** Unit tests ≥6 cases pass
- [ ] **SC9:** `dotnet build` 0 errors + `guard-check.ps1` pass
- [ ] **SC10:** Full E2E regression: tất cả community-*.spec.ts pass
- [ ] **SC11:** Architecture tests pass
- [ ] **SC12:** VPS full regression: RV6-1 to RV6-6 ALL PASS

**Branch:** `feature/community-sprint6-admin-legal`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — CommunityRole activation/deactivation
- `accounting-ui-implementation` — AdminPanel + Profile UI
- `build-error-analysis` — Final regression errors

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `TenantOnboardingController.cs` — [Authorize(Policy="SystemAdmin", AuthenticationSchemes=JwtBearerDefaults)]
  - Fact 2: `ShopInstancesController.cs` — admin CRUD pattern with SystemAdmin
  - Fact 3: `CommunityRole` entity (Sprint 0) — has Deactivate() method
  - Fact 4: `Customer.IdentityLevel` + `Customer.LoyaltyPoints` — eligibility criteria fields
  - Fact 5: PushSubscription + VAPID infrastructure exists (PWA push notifications)
- **Assumptions:**
  - AdminPanel in ShopERP (not Gateway) — ShopERP has admin UI
  - Push notification to KhachLink PWA works (VAPID already configured)
- **Open Questions:**
  - Q1: Profile page exists in KhachLink? Need to check.
  - Q2: Push notification content — who writes the template?
- **Recommended Action:** PROCEED — Assumptions (2) < Facts (5), Open Questions (2) < 3
