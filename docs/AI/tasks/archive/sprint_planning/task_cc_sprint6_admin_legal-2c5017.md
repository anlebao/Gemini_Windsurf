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
  - **Nav/Menu entry points (G1-G4 FIX):** ShopERP AdminLayout + NavMenu get Community section (ProductReferralConfigs Sprint 4 debt + AdminPanel + FraudFlags + FraudStats). KhachLink NavMenu gets `_isShopOwner` flag + wallet link for shop owners (Sprint 5 pending-advances confirmation currently unreachable from nav). Profile.razor shows community roles + salesman self-view FraudFlag status.

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Sprint 6 of 7 (FINAL) — v1.2: tăng sessions từ 3 → 4 (+Fraud Review + nav/menu)
- **Dependency:** Sprint 5 COMPLETE (wallet + COD working) + **Sprint 4 COMPLETE (FraudFlag data exists — v1.2 NEW)**

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files cần CREATE
- `2_Gateway/Controllers/CommunityAdminController.cs` — admin endpoints (eligible, activate, deactivate)
- `2_Gateway/Controllers/FraudFlagController.cs` (v1.2 NEW — full impl, preview in S4) — admin FraudFlag endpoints (list, detail, confirm, dismiss, stats)
- `3_CoreHub/Services/ICommunityAdminService.cs` — interface
- `3_CoreHub/Services/CommunityAdminService.cs` — eligible list, activate/deactivate
- `3_CoreHub/Services/IFraudReviewService.cs` (v1.2 NEW) — interface
- `3_CoreHub/Services/FraudReviewService.cs` (v1.2 NEW) — list pending, confirm/dismiss/review, 3-strike ban logic, fraud stats, GetMyFlags
- `5_WebApps/ShopERP/Components/Pages/Community/AdminPanel.razor` — admin UI (ShopERP) @page /admin/community/admin-panel
- `5_WebApps/ShopERP/Components/Pages/Community/FraudFlags.razor` (v1.2 NEW) — Fraud Review UI @page /admin/community/fraud-flags
- `5_WebApps/ShopERP/Components/Pages/Community/FraudStats.razor` (v1.2 NEW) — Fraud Stats dashboard @page /admin/community/fraud-stats
- `docs/legal/community-terms-of-service.md` — điều khoản sử dụng
- `docs/legal/community-privacy-policy.md` — chính sách bảo mật — **v1.2: +device fingerprint consent clause**
- `docs/legal/marketplace-policy.md` — quy chế sàn TMĐT
- `docs/legal/anti-fraud-policy.md` (v1.2 NEW) — 3-strike ban, hold 48h, KYC bank account cho payout
- `6_Tests/VanAn.Core.Tests/Community/CommunityAdminServiceTests.cs` — 8 unit tests
- `6_Tests/VanAn.Core.Tests/Community/FraudReviewServiceTests.cs` (v1.2 NEW) — 6 unit tests
- `6_Testing/e2e-tests/community-admin.spec.ts`
- `6_Testing/e2e-tests/community-fraud-review.spec.ts` (v1.2 NEW)
- `6_Testing/e2e-tests/community-full-regression.spec.ts`

### Files cần MODIFY
- `2_Gateway/Program.cs` — DI for CommunityAdminService + FraudReviewService + auth policy "CommunityAdmin"
- `5_WebApps/ShopERP/Program.cs` — DI if needed
- `5_WebApps/KhachLink/Pages/Profile.razor` — display community roles + salesman self-view FraudFlag status
- `5_WebApps/KhachLink/Services/Http/CommunityHttpService.cs` — add admin HTTP calls (if needed) + GetMyFraudFlagsAsync (salesman self-view)
- `5_WebApps/ShopERP/Components/Pages/Admin/AdminLayout.razor` — **G2 FIX: add Community section to AdminMenuItems** — ProductReferralConfigs (Sprint 4 debt), AdminPanel, FraudFlags, FraudStats links
- `5_WebApps/ShopERP/Components/Layout/NavMenu.razor` — **G2 FIX: add Community section under SystemAdmin AuthorizeView** — same 4 links as AdminLayout
- `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` — **G4 FIX: add `_isShopOwner` flag + show `/community/wallet` link for shop owners** (shop owner confirms pending advances — Sprint 5 feature currently unreachable from nav)

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
- [ ] **v1.2 NEW — Nav entry points (G1-G4 FIX):** Mọi page mới (AdminPanel, FraudFlags, FraudStats) + page Sprint 4 debt (ProductReferralConfigs) MUST có nav link trong ShopERP AdminLayout + NavMenu. KHÔNG có orphan page (page tồn tại nhưng không có link truy cập).
- [ ] **v1.2 NEW — Shop Owner wallet access (G1/G4 FIX):** KhachLink NavMenu thêm `_isShopOwner` flag (query CommunityHttpService.GetRoleAsync — cần thêm IsShopOwner field). Wallet link hiển thị cho cả shipper + shop owner. Shop owner thấy pending advances section (Sprint 5 đã có UI, chỉ thiếu nav entry).
- [ ] **v1.2 NEW — Profile roles detail (G6 FIX):** Profile.razor query `GET /api/customer-identity/me` (modified to include `communityRoles` array) — hiển thị badge list với active/inactive status. Salesman thêm section "Fraud Flag status" via `GET /api/community/my-fraud-flags` (new endpoint, X-Customer-Token auth, returns own flags only).

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** GET `/api/admin/community/eligible` trả list customer đủ điều kiện
- [ ] **SC2:** POST `/api/admin/community/{customerId}/activate-role` tạo CommunityRole + push notification
- [ ] **SC3:** POST `/api/admin/community/{customerId}/deactivate-role` set IsActive=false
- [ ] **SC4:** AdminPanel UI (ShopERP): list eligible + activate/deactivate buttons
- [ ] **SC5:** Profile page (KhachLink): hiển thị community roles
- [ ] **SC6:** Push notification gửi khi activate role
- [ ] **SC7:** 4 legal documents draft hoàn thành (ToS + Privacy + Marketplace + Anti-Fraud)
- [ ] **SC8:** Unit tests ≥14 cases pass (8 CommunityAdmin + 6 FraudReview)
- [ ] **SC9:** `dotnet build` 0 errors + `guard-check.ps1` pass
- [ ] **SC10:** Full E2E regression: tất cả community-*.spec.ts pass
- [ ] **SC11:** Architecture tests pass
- [ ] **SC12:** VPS full regression: RV6-1 to RV6-11 ALL PASS
- [ ] **SC13 (v1.2 NEW — G2):** ShopERP AdminLayout + NavMenu có Community section với 4 links (ProductReferralConfigs, AdminPanel, FraudFlags, FraudStats) — admin truy cập được qua menu, không cần biết URL
- [ ] **SC14 (v1.2 NEW — G1/G4):** KhachLink NavMenu có `_isShopOwner` flag + wallet link — shop owner truy cập `/community/wallet` từ menu để confirm pending advances (Sprint 5 feature)
- [ ] **SC15 (v1.2 NEW — G5):** Profile.razor (KhachLink) hiển thị community roles + salesman self-view FraudFlag status (nếu flagged)
- [ ] **SC16 (v1.2 NEW — G3):** ProductReferralConfigs page (Sprint 4 debt) có nav link trong ShopERP admin menu — không còn orphan page

**Branch:** `feature/community-sprint6-admin-legal`

---

## 6. ACTIVE SKILLS (MAX 3)
- `domain-integrity-validation` — CommunityRole activation/deactivation
- `accounting-ui-implementation` — AdminPanel + Profile UI
- `build-error-analysis` — Final regression errors

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 8
- **Verified Facts:**
  - Fact 1: `TenantOnboardingController.cs` — [Authorize(Policy="SystemAdmin", AuthenticationSchemes=JwtBearerDefaults)]
  - Fact 2: `ShopInstancesController.cs` — admin CRUD pattern with SystemAdmin
  - Fact 3: `CommunityRole` entity (Sprint 0) — has Deactivate() method
  - Fact 4: `Customer.IdentityLevel` + `Customer.LoyaltyPoints` — eligibility criteria fields
  - Fact 5: PushSubscription + VAPID infrastructure exists (PWA push notifications)
  - Fact 6: `ShopERP/Components/Pages/Admin/AdminLayout.razor` — has AdminMenuItems list (verified, no Community section yet — G2 gap)
  - Fact 7: `ShopERP/Components/Layout/NavMenu.razor` — has SystemAdmin AuthorizeView section (verified, no Community links — G2 gap)
  - Fact 8: `KhachLink/Pages/Profile.razor` exists (verified) + `KhachLink/Components/Layout/NavMenu.razor` has _isShipper/_isSalesman but NO _isShopOwner (verified — G4 gap)
- **Assumptions:**
  - AdminPanel in ShopERP (not Gateway) — ShopERP has admin UI
  - Push notification to KhachLink PWA works (VAPID already configured)
- **Open Questions:**
  - Q1: Profile page exists in KhachLink? Need to check. — **RESOLVED: Yes, `5_WebApps/KhachLink/Pages/Profile.razor` exists (verified).**
  - Q2: Push notification content — who writes the template?
  - Q3 (v1.2 NEW): CommunityRole has IsShopOwner or equivalent? Need to check if GetRoleAsync returns shop owner flag — if not, need to add.
- **Recommended Action:** PROCEED — Assumptions (2) < Verified Facts (8), Open Questions (2) < 3
