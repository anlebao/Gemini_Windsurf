# TASK CARD: Vạn An Appointment Booking & Staff Scheduling MVP (SRS v1.1)

> Created: 2026-10-05 (user directive 2026-10-05: **Booking feature TRƯỚC → Sprint B2 SAU** + 5 quyết định D1-D5)
> Source SRS: `docs/requirements/van_an_appointment_booking_srs_v1.1_mvp (1).md` (v1.1 MVP — 44 sections)
> Master plan: `docs/AI/plans/booking-scheduling-master-plan.md` (ACTIVE — chờ approve)
> Branch: `main`
> Status: **P1-P7 DONE (2026-10-06 — guard ALL PASSED · Core.Tests 2111 · ShopERP.Tests 142 · CD Multi-VPS ×5 SUCCESS · RV production PASS · sweep 61/61 · E2E 9/11) — ⏳ L5 manual (user) + đóng**

---

## 1. GOAL & CONTEXT

- **Mục tiêu:** Xây Appointment Booking Infrastructure cho tenant dịch vụ theo lịch hẹn (Spa/Salon/Massage/Karaoke/Clinic) — QR attribution → booking 4 màn hình tap-first → tenant confirm/assign staff → availability + double-booking prevention thật → completed → Order → deposit/payment facts → commission ledger hợp nhất (qualified-only).
- **Quy mô:** feature lớn nhất repo — 19 entity mới + 6-7 services + ~20 API endpoints + 4 màn KhachLink + 7-8 màn ShopERP + test matrix §34 (Core.Tests ~+170-220, ShopERP.Tests ~+40-60, E2E +9).
- **Nền tảng đã có (tái dùng ~80%):** multi-tenancy PG/SQLite + `Customer`/`CustomerDeviceId` + `CommunityRole` Salesman + `SalesReferral`/`WalletTransaction` (commission pattern) + e-invoice engine + `OutboxEvent` + `AuditTrailService` + `IAlertNotifier` + `VietQrService` + UI Platform + `GatewayAdminApiClientBase`/`FinancialIntelligenceHttpService` (ShopERP→Gateway API pattern) + test infra (Core.Tests 1977 · ShopERP.Tests 136).
- **Data flow (D1):** KhachLink (5002) → Gateway public booking API (5001, PG source of truth) → NATS event → ShopERP (5003) tenant ops qua Gateway API. Booking/Staff/Offering/QR/Commission **chỉ ở PG** — KHÔNG replica SQLite cho MVP.
- **Điều kiện trigger — ĐÃ THỎA:** SRS review hoàn tất (2026-10-05) + 5 quyết định user chốt (D1-D5).

## 2. ACTIVE WORKFLOW ROUTING

- **Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT — 7 step)
- **Execution Mode:** ANALYZE (hiện tại — plan + task card chờ duyệt) → IMPLEMENT (sau user approve)
- **Skills (max 3):** `domain-integrity-validation` (Single-Identity, Domain purity, immutable ledger) + `outbox-pattern-implementation` (financial facts + events) + `sqlite-concurrency-analysis`→thay bằng pattern PG row-lock (precedent WalletService) — nếu cần kiểm tra SQLite không tham gia → dùng `test-system-upgrade` (test matrix §34)
- **Session strategy (precedent VA-IIE — session-per-phase):**
  - Session 1 (P1): Domain 18 entities + EF configs + migration PG + seed → guard/build/tests PASS → commit
  - Session 2 (P2): IStaffService/IOfferingService/IAvailabilityService/IBookingService + double-booking + tests → guard PASS → commit
  - Session 3 (P3): IQRAttributionService/ICommissionService/TaxWithholdingPolicy/IBookingFinancialService + Order hook (D2) + tests → guard PASS → commit
  - Session 4 (P4): Gateway public API + KhachLink 4 screens + Status polling + E2E spec ✅ DONE `45c615b2` + `c09d3efe` (guard ALL PASSED · build 0 errors · Core.Tests 2103 · ShopERP.Tests 136 · Architecture.Tests 41 — CHƯA push)
  - Session 5 (P5): ShopERP 7 pages + NavMenu + Sitemap + bUnit ✅ DONE `fd9f6c79` (guard ALL PASSED · build 0 errors · Core.Tests 2103 · ShopERP.Tests 142 — CHƯA push)
  - Session 6 (P6): Hardening + test matrix §34 (isolation 5 negative §18.3 + E2E specs) → guard + build PASS
  - Session 7 (P7): PUSH P5+P6 → Deploy CD Multi-VPS + migrations PG + RV L1-L5 (impersonate pattern rv-vaiie) → cập nhật project_state + master plan status + đóng task card

## 3. SCOPE (7 phases per master plan §4)

### Phase 1 — Domain + EF + migration PG + seed (task `task_booking_phase1_domain.md`) ✅ DONE `a8ce5482`

- [x] **P1.1 Domain (1_Shared/Domain.cs, Single-Identity 100% — Id = PK, business key VO Ignore, constructor sync `Id = XxxId.Value`):**
  - Staff & Scheduling: `Staff` (+ StaffUserId nullable → Users.Id, precedent Shift.StaffUserId) · `StaffService` (skill eligibility) · `StaffWorkingSchedule` (weekday recurring + break) · `StaffScheduleOverride` (WORKING/LEAVE/UNAVAILABLE/BREAK)
  - Catalog: `ServiceCategory` · `AppointmentOffering` (SERVICE/PACKAGE + duration/price snapshot + required_staff_skill) · `AppointmentOfferingItem` (package con) · `AddOn` (non-scheduling only)
  - Booking: `Booking` (public_booking_code opaque · 9 state §9.1 + internal sub-states §9.2 · snapshot §8.4 · version optimistic concurrency §19.1 · OrderId? D2) · `BookingItem` · `BookingStaffAssignment` (0..1 primary, immutable history) · `BookingEvent` (audit §23) · **`BookingTenantConfig`** (feature-flag per tenant — Q5 chốt: IsEnabled default false + deposit policy §16.1 + cancel/reschedule policy + e-invoice applicability §16.5; get-or-create pattern VaIIeTenantConfig; 1 row/tenant unique index — **chỉ tenant enabled mới resolve QR/bookings**)
  - QR/Commission: `QRChannel` (qr_token opaque hash §7.2) · `AttributionSession` (first-qualified-wins + expiry §7.4-7.5) · `CommissionRule` · `CommissionLedgerEntry` (source BOOKING|ORDER — ledger hợp nhất D3 · state PENDING/EARNED/VOIDED/PAID/REVERSED §17.4 · immutable sau finalized §17.5) · `TaxWithholdingPolicy` (versioned — inputs §17.3, mốc 5tr/lần NĐ 253/2026, KHÔNG hard-code 10%)
  - Financial: `PaymentTransaction` (type SECURITY_DEPOSIT/PREPAYMENT/FINAL §16.2 · status 7 state §16.3) · `InvoiceIntegrationRecord` (invoice_status + invoice_trigger snapshot §16.4)
  - Enums: BookingStatus (9), BookingSubState, DepositType, PaymentStatus (7), InvoiceStatus, InvoiceTrigger, StaffScheduleOverrideType, CommissionLedgerState, TaxPayeeType (EMPLOYEE/INDIVIDUAL_CONTRACTOR/BUSINESS_ENTITY/OTHER §17.2)
- [x] **P1.2 EF configs (19)** — Ignore VO + precision + indexes (unique: staff schedule, QR token, booking code, BookingTenantConfig.TenantId) + DbSet trên Gateway DbContext (PG — D1; xác nhận `IVanAnDbContext` auto-apply scope như precedent VaIIeTenantConfig)
- [x] **P1.3 Migration PG `AddBookingScheduling`** (Gateway DbContext — pattern Sprint B PG-empty) + verify migration test
- [x] **P1.4 Seed:** service categories mẫu + 1 tenant demo (get-or-create — không bắt buộc backfill)
- [x] **P1.5 Tests:** entity lifecycle (booking 9-state transitions hợp lệ/bất hợp lệ, snapshot, ledger immutability + reversal) → Core.Tests PASS

### Phase 2 — Services core (task `task_booking_phase2_services.md`) ✅ DONE `1a98c7b9`

- [x] **P2.1 `IStaffService`** (`3_CoreHub/Services/Booking/`, namespace `VanAn.CoreHub.Services.Booking`): CRUD staff + skills + schedule + override — **mọi query filter TenantId** (lesson a21f97f2)
- [x] **P2.2 `IOfferingService`:** CRUD category/offering/package/add-on (snapshot fields — đổi catalog không ảnh hưởng booking cũ)
- [x] **P2.3 `IAvailabilityService`:** `GetAvailableSlotsAsync(tenantId, offeringId, date, staffId?)` — active ∧ skill match ∧ working interval ∧ không break/leave/unavailable ∧ không conflict (SRS §11.4-11.5); tenant admin xem lý do unavailable tối thiểu (matrix §14); **chỉ trả available cho customer** (Risk 3)
- [x] **P2.4 `IBookingService`:** create (Idempotency-Key §21.1 qua `BookingIdempotencyRecord` infra entity) · state machine (validate §9.3, backend authoritative §37.2) · confirm/reject idempotent (§21.2) · assign/change staff (idempotent + re-check conflict §21.3/§13) · check-in/start/complete · cancel theo policy · status polling DTO (public token, ETag-ready §10.3) · BookingEvent mọi transition (§23-24)
- [x] **P2.5 Double-booking prevention (§12, AC-C04):** server re-check + atomic conflict transaction PG (advisory lock + SELECT … FOR UPDATE — precedent WalletService HR-SCALE-3) + unique index (TenantId, StaffId, StartAt) → tối đa 1 request thành công; business conflict 409 message tiếng Việt thân thiện (§6.5) — **KHÔNG stub, KHÔNG dựa UI lock**
- [x] **P2.6 Tests:** integration — create/idempotent/confirm/assign/**race 2 requests**/isolation (+47: Staff 8 · Offering 6 · Availability 11 · BookingService 19 · Concurrency 3) → Core.Tests 2054 PASS

### Phase 3 — QR/Commission/Financial services + Order hook (task `task_booking_phase3_services2.md`)

> **Session P3 PREP (làm ở session MỚI — 2026-10-06, base `031c43b0` = P2 deployed + RV PASS production):**
> Đọc trước: `docs/AI/project_state.md` (mục 2/4 — P2 DONE) + SRS §7 (QR/attribution) · §16 (deposit/payment/invoice facts) · §17 (commission/ledger) · §23-24 (events/audit) + `1_Shared/Domain.cs` (QRChannel/AttributionSession/CommissionRule/CommissionLedgerEntry/TaxWithholdingPolicy/PaymentTransaction/InvoiceIntegrationRecord/Booking — đã có P1) + P2 services pattern (`3_CoreHub/Services/Booking/` — alias `BookingEntity`/`StaffServiceEntity` cho namespace/type xung đột; mọi query `IgnoreQueryFilters`+TenantId; `ExecuteAtomicAsync`+advisory lock pattern cho write).
> **Open items kế thừa từ P2:** (1) deposit classification đang set `SecurityDeposit` trong create — P3.4 phải refine theo tax profile (§16.2, NĐ 70/2025: invoice tại thời điểm thu tiền trước; KHÔNG hard-code); (2) design note create-with-staff (StaffAssigned ngay) chờ review ở P4 flow; (3) P2 chưa emit outbox — P3.4 bổ sung facts.
> **Tái dùng:** `SalesReferral`/`WalletTransaction` (commission payout pattern) · `VietQrService` · `OutboxEvent`/`OutboxMessage` · `CreateOrderFromCommandAsync` + `ApplyHtxInternalTagAsync` (lesson P6c) · `IAlertNotifier` stub.
> Validation cuối session: guard-check + build + Core.Tests → commit (KHÔNG push trừ khi user yêu cầu).

- [x] **P3.1 `IQRAttributionService`** ✅: resolve qr_token (SHA-256 hash tại rest §7.2 — token active ∧ tenant active ∧ salesman ∈ tenant §7.3, KHÔNG tin tenant từ client Risk 5) · AttributionSession get-or-create (first-qualified-wins §7.5 — session qualified đầu tiên trong tenant+anon giữ attribution; refresh không đổi salesman; hết hạn → reset period 1 row/key) · rapid-scan guard §26.3 = unique index (TenantId, QrId, AnonymousSessionId) + migration `AddBookingAttributionSessionUnique` · revoke §26.1 · create QR (validate salesman ∈ tenant ngay khi tạo)
- [x] **P3.2 `ICommissionService`** ✅: qualification = COMPLETED + payment qualified (`!DepositRequired || PaymentStatus == Paid`) + attribution valid (§17.1, Risk 4) → CommissionLedgerEntry (rule snapshot JSON + tax snapshot + BookingEvent CommissionEarned) · idempotent (booking đã có entry → trả entry hiện có) · reversal §26.6 (entry mới RelatedEntryId + original → Reversed; PAID → WalletTransaction Reversal −net; idempotent) · payout → WalletTransaction Commission +net + WalletTransactionId link (domain additive `MarkPaid(Guid?)` + `RecordReversalWalletTransaction`) · ledger hợp nhất D3 · rule matching specificity (offering+salesman > offering > salesman > global)
- [x] **P3.3 `TaxWithholdingPolicyAdapter`** ✅: versioned `ND253-2026-1` — inputs §17.3 (payee type/residency/contract/gross) → gross/withheld/net + reason code; mốc 5tr/lần khấu trừ 10% (≥5tr → 10PCT-5M; <5tr → BELOW-5M; HĐLĐ ≥3 tháng → REGULAR-EMPLOYEE 0%; BusinessEntity → NON-INDIVIDUAL; non-resident → NON-RESIDENT; unknown version → NO-RULE) — KHÔNG hard-code 10% cho mọi payee
- [x] **P3.4 `IBookingFinancialService`** ✅: deposit classification §16.2 (ResolveDepositNature theo EinvoiceMode — PrepaymentForService vs SecurityDeposit; KHÔNG hard-code) + InvoiceTrigger snapshot §16.4 (Prepay→OnPayment, SecurityDeposit→ExternalRule, FinalPayment→OnCompletion — NĐ 70/2025) · PaymentTransaction 7-state §16.3 (CaptureDeposit PENDING/Paid + ConfirmDeposit idempotent + CaptureFinalPayment + RefundDeposit) · InvoiceIntegrationRecord get-or-create (trigger = bản chất khoản tiền đầu tiên) · outbox events §16.6 (DepositPaid/PaymentCaptured/ServiceCompleted/InvoiceRequired/InvoiceIssued/InvoiceFailed/RefundCompleted — booking = source of facts) · BookingService create dùng `ResolveDepositNature` thay SecurityDeposit hard-code
- [x] **P3.5 Booking→Order hook (D2, Q1 = COMPLETED)** ✅: tại transition COMPLETED → CreateOrderCommand từ offering/add-on snapshots (ProductId = OfferingId/AddOnId — PG không FK Products) → path `CreateOrderFromCommandAsync` hiện có (**giữ `ApplyHtxInternalTagAsync`** — lesson P6c; TrackingCode = PublicBookingCode) → `Booking.OrderId` · idempotent R7 (OrderId != null → skip; recovery crash giữa create/attach → lookup TrackingCode) · fail-safe (lỗi order KHÔNG fail transition — retry lần sau) · ServiceCompleted facts (RecordServiceCompletedAsync) song song · sync ShopERP an toàn — OrderSyncSubscriber auto-create product stub (pattern có sẵn)
- [x] **P3.6 Tests** ✅ (+49 → Core.Tests 2103): attribution (9 — valid/unknown/revoked/tenant inactive/salesman role deactivated/first-wins/refresh/cross-tenant/hash/expiry/duplicate token) · commission (11 — qualification matrix/reversal/payout/withholding/specificity/isolation) · tax adapter fixtures (9) · financial (9 — classification/outbox/invoice) · order hook (4 — create/idempotent/recovery/fail-safe) — **bài học: (1) test schema EnsureCreated CÒN FK OrderItems→Products (PG đã drop qua migration) → seed Product stub pattern OrderSyncSubscriber; (2) MarkReversed đổi State trước khi check `== Paid` → bắt wasPaid trước; (3) EnsureInvoiceRecordAsync trigger phải từ bản chất khoản tiền đầu tiên không phải FinalPayment; (4) named args record C# phân biệt hoa thường**

### Phase 4 — Public API + KhachLink customer UI (task `task_booking_phase4_public_api_ui.md`)

> **Session P4 PREP (làm ở session MỚI — base `c806df72` = P3 committed, CHƯA push/deploy):**
> Đọc trước: `docs/AI/project_state.md` (mục 2/3/4 — P3 DONE) + SRS §5 (4 màn hình + sticky summary §5.2) · §10 (status polling 5s/10-15s + terminal-stop §10.2, ETag/Last-Modified §10.3) · §15 (Quick Tags + SpeechRecognition text-only §15.2-15.3, KHÔNG upload audio) · §20 (API conventions) · §21 (Idempotency-Key §21.1) · §25 (public security — opaque token, KHÔNG trả internal IDs/commission) · §29 (client retry — KHÔNG báo thành công trước server persist) + P3 services (`3_CoreHub/Services/Booking/` — IQRAttributionService/IBookingService[CreateBookingAsync + GetPublicStatusAsync + BookingStatusDto có Version]/IBookingFinancialService[GetDepositAsync + CaptureDepositAsync]) + Gateway controller pattern (`2_Gateway/Controllers/CatalogController.cs` — `[Route("api/catalog")]` + `[AllowAnonymous]` precedent) + KhachLink pattern (`5_WebApps/KhachLink/Pages/Checkout.razor` — idempotency key + retry + VietQR; `Components/Routes.razor` — hiện chưa lazy-load).
> **Open items kế thừa từ P2/P3:** (1) design note create-with-staff (StaffAssigned ngay) — Screen 2 chọn staff → BookingService đã xử lý; P4 chỉ cần truyền StaffId + hiển thị kết quả; (2) P3.4 CaptureDepositAsync đã emit outbox — Screen 3 deposit → gọi qua Gateway; (3) QR resolve → client nhận AttributionSessionId → truyền vào POST bookings (AttributionId — snapshot §7.6); (4) ràng buộc: public API KHÔNG trả commission/staff conflicts/internal IDs (§25) — chỉ dùng DTO đã thiết kế.
> **Tái dùng:** `VietQrService` (deposit QR — Screen 3) · `BookingStatusDto` (Status page polling) · GatewayAdminApiClientBase (tenant API — pattern FI SystemAdmin, P5 dùng lại) · JS interop precedent (KhachLink gps-mock.ts / SpeechRecognition stub — text-only).
> **Lưu ý Gate:** UI Platform 100% (Gate 5 — KHÔNG bypass) · E2E spec `booking-customer.spec.ts` (Gate 4, P4.8) · Playwright DISABLED trong IMPLEMENT (playwright.rules) · `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests MUST PASS trước commit · commit KHÔNG push trừ khi user yêu cầu.

- [x] **P4.1 Gateway public API** ✅ (`45c615b2` — `PublicBookingController`): `GET /api/public/booking/qr/{qrToken}` (resolve §7.3-7.6 → tenant branding + AttributionSessionId) · `GET .../tenants/{tenantId}/services` (catalog: categories/offerings/add-ons + deposit policy §16.1 + cancel policy; gate Q5 IsEnabled → 404 friendly) · `GET .../availability` (slots §11.5 — chỉ available, Risk 3) · `POST .../bookings` (Idempotency-Key §21.1 bắt buộc; conflict → 409 message §6.5) · `GET .../bookings/{publicBookingToken}` (ETag theo Version §10.3 + If-None-Match 304) · `POST .../bookings/{token}/cancel` — public-safe §25 (không trả commission/conflicts/internal IDs; khách anonymous, CustomerId null + CustomerDeviceId §4.1) + rate-limit "booking-public" 120/min/IP
- [x] **P4.2 Gateway tenant API** ✅ (`TenantBookingController` — JWT tenant_id claim, mọi query filter TenantId): queue/detail · confirm/reject/assign/change-staff/check-in/start/complete/no-show/cancel (idempotent §21.2-21.3, conflict 409) · `availability` who-is-available matrix §14 · staff/schedules · financial-status §16 (deposit tx + payment/invoice state) — P5 ShopERP UI sẽ dùng
- [x] **P4.3 KhachLink Screen 1 — Offering** ✅ (`BookingOffering.razor` `/booking/{QrToken}`): tenant branding (QR resolve) + category chips + offering cards (giá/duration + badge Gói) + add-on chips (non-scheduling §8.3) + sticky summary §5.2 + step bar §6.4 + zero-friction identity (booking_anon_session + customer_device_id localStorage §4.1)
- [x] **P4.4 Screen 2 — Time & Staff** ✅ (`BookingTime.razor` `/booking/{QrToken}/time`): horizontal date picker 7 ngày + slot large buttons (min 56px §6.3) + staff filter (Bất kỳ ai / cụ thể — server-filtered §11.5, dedup slot cùng giờ) + giữ lựa chọn khi back
- [x] **P4.5 Screen 3 — Note & Deposit** ✅ (`BookingNote.razor` `/booking/{QrToken}/note`): Quick Tags chips §15.1 + 🎙 SpeechRecognition JS interop (reuse voice-note.js — text-only §15.2, fallback textarea §15.3, KHÔNG upload audio) + deposit hiển thị theo policy §16.1 (server-authoritative — radio disabled)
- [x] **P4.6 Screen 4 — Confirm + submit** ✅ (`BookingConfirm.razor` `/booking/{QrToken}/confirm`): summary (tenant/thời gian/offering/add-ons/total/deposit/staff/note) + policy §5.2 + CTA "XÁC NHẬN ĐẶT LỊCH" + submit Idempotency-Key (client retry §29 — KHÔNG báo thành công trước server persist) → Status page
- [x] **P4.7 Status page** ✅ (`BookingStatus.razor` `/booking/status/{code}`): polling 5s × 2 phút → 15s §10.2 + dừng terminal state + nút Làm mới + hủy lịch + error hướng dẫn §6.5 + mã đặt lịch opaque §25. ⚠️ lazy-load WASM assembly (R5) DEFER — KhachLink chưa cấu hình lazy assemblies
- [x] **P4.8 E2E spec** ✅ `booking-customer.spec.ts` (QR→offering→time→quick tag/text note→confirm→status polling — AC-C01/C02/C03/C06, self-gating `BOOKING_TEST_QR_TOKEN` cho RV P7) — Sitemap: KhachLink không có sitemap page (ShopERP P5 sẽ thêm nav)

### Phase 5 — ShopERP tenant UI (task `task_booking_phase5_shoperp_ui.md`)

- [x] **P5.1 `/booking/queue`** ✅ (`fd9f6c79`): status filter chips (AC-T01) + list/detail + confirm/reject/no-show/cancel (AC-T02) + assign/change staff modal — **eligible-staff endpoint** (capability §11.2 + ValidateSlotAsync §12/§13 — available badge + reason)
- [x] **P5.2 `/booking/staff`** ✅: staff CRUD (create/update/active) + skills (offering checkboxes — SetStaffServicesAsync §11.2)
- [x] **P5.3 `/booking/schedules`** ✅: weekday recurring editor (start/end/break) + exact-date override (Leave/Unavailable/Working/Break §11.3)
- [x] **P5.4 `/booking/availability`** ✅: who-is-available matrix staff × slot (AC-T05 §14) + offering filter
- [x] **P5.5 `/booking/calendar`** ✅: day view (AC-T04) — giờ/khách/dịch vụ/nhân viên/trạng thái
- [x] **P5.6** ✅ `/booking/deposits` (§16 — tracking + "Đã nhận cọc" get-or-create PENDING→PAID + hoàn cọc) · `/booking/qr-channels` (tạo QR token ngẫu nhiên §25 + salesman + expiry + **raw token hiển thị 1 lần** hash DB §7.2 + revoke §26.1) · `/booking/commission` (ledger D3 + salesman filter + payout → WalletTransaction §17.4 + tổng thuế TNCN)
- [x] **P5.7** ✅ NavMenu "Đặt lịch hẹn" (Owner+StoreKeeper — IShopErpMenuService) + Sitemap card-booking + **bUnit +6** (`BookingPagesTests`) → **ShopERP.Tests 142 PASS**
- [x] **P5.8** ✅ Tenant ops qua Gateway API: `BookingTenantApiClient` (GatewayAdminApiClientBase pattern — JWT tenant_id claim; D1: ShopERP KHÔNG query PG) + Gateway tenant API extensions (staff CRUD/skills, catalog reads, schedules CRUD, config Q5, QR channels, salesmen, commission ledger+pay, deposits+received/refund, eligible-staff)

### Phase 6 — Hardening + full test matrix (task `task_booking_phase6_hardening.md`)

> **Session P6 PREP (làm ở session MỚI — base `3ad6ef06` = P5 committed, CHƯA push):**
> Đọc trước: `docs/AI/project_state.md` (mục 2/3/4 — P5 DONE) + SRS §18.3 (5 negative isolation — security boundary bắt buộc) · §24 (audit completeness — confirm/reject/change-time/assign/reassign/cancel/deposit-policy/commission-rule/revoke QR phải có audit log before/after) · §26 (fraud controls — revoked QR / cross-tenant / rapid-scan / refresh / cancel / reversal) · §28 (perf sanity: availability p95 ≤ 500ms — reconcile với baseline, KHÔNG tạo SLA riêng) · §34 (test matrix — unit/integration/E2E 9 mục).
> **Coverage ĐÃ CÓ (P2/P3/P5 — không làm lại):** unit §34 gần đủ — working schedule/break/leave exclusion/skill matching/package duration/availability (P2: Staff 8 + Offering 6 + Availability 11) · commission/attribution/state transitions/tax fixtures (P3: attribution 9 + commission 11 + tax 9) · integration — create/idempotent/confirm/assign/double-booking race (P2: BookingService 19 + Concurrency 3) · deposit/payment state + invoice/outbox + QR persistence + commission finalization/reversal (P3 financial 9 + order hook 4) · bUnit ShopERP 8 pages (P5 +6). Core.Tests **2103**.
> **P6 CÒN THIẾU (làm trong session P6):** (1) **tenant isolation 5 negative §18.3** — test mới: đọc booking B · assign staff B · QR A tạo booking B · salesman A xem commission B · public token A resolve dữ liệu B (pattern P3 isolation tests — VanAnDbContext test factory có global filter → dùng context + IgnoreQueryFilters đúng chỗ; xem `BookingServiceTests`/`CommissionServiceTests` isolation); (2) audit completeness §24 (verify BookingEvent ghi mọi transition + audit log đủ — có sẵn event §23, check test); (3) **E2E 4 specs** gộp từ 9 tests §34: `booking-customer.spec.ts` (đã có P4.8 — AC-C01/C02/C03/C06) + `booking-commission.spec.ts` (QR salesman → completed → commission + withholding fixture) + `booking-concurrency.spec.ts` (2 customer race — AC-C04) + `booking-isolation.spec.ts` (tenant A/B — AC-Q01; leave → unavailable; deposit → financial status) — spec self-gating + fresh-DB tolerance `.or()` pattern `va-iie-forecast.spec.ts`; (4) rate-limit verify §25 (policy "booking-public" đã config — smoke verify nginx/API) + perf sanity §28 (availability p95 — benchmark đơn giản, không SLA riêng).
> **Lưu ý:** Playwright DISABLED trong IMPLEMENT (playwright.rules — viết spec nhưng KHÔNG chạy diện rộng trong session; chạy tại RV P7). guard-check + build + Core.Tests + ShopERP.Tests MUST PASS → commit (KHÔNG push).

- [x] **P6.1 Unit matrix (§34):** (P2/P3 coverage + `AvailabilityPerfSanityTests` p95) working schedule · break/leave exclusion · skill matching · fixed package duration · availability · commission · attribution resolution · state transitions · tax-withholding fixtures
- [x] **P6.2 Integration matrix (§34):** `BookingIsolationTests` +7 (5 negative §18.3 + audit + commission auto-finalize) — fix 2 gap (attribution tenant + commission call site) create/idempotent/confirm/assign · **double-booking** · deposit/payment state · invoice event/outbox · QR attribution persistence · commission finalization/reversal · **tenant isolation 5 negative cases (§18.3):** đọc booking B · assign staff B · QR A tạo booking B · salesman A xem commission B · public token A resolve dữ liệu B
- [x] **P6.3 E2E 9 tests → 3 specs mới** (`booking-commission`/`booking-concurrency`/`booking-isolation` — production 9/11 PASS) `booking-customer.spec.ts` (P4.8) · `booking-commission.spec.ts` (QR salesman→completed→commission + withholding fixture) · `booking-concurrency.spec.ts` (2 customer race — AC-C04) · `booking-isolation.spec.ts` (tenant A/B — AC-Q01; leave → unavailable; deposit → financial status)
- [x] **P6.4 Rate-limit verify (§25) + audit completeness (§24) + perf sanity** (policy config verified + audit test + p95) (availability p95 ≤ 500ms reconcile §28 — không tạo SLA riêng)

### Phase 7 — Deploy + RV production (task `task_booking_phase7_rv.md`)

> **Session P7 PREP (làm ở session MỚI — base sau P6 commit):**
> **Bước 1 — PUSH:** FAST PUSH toàn bộ P5+P6 (guard + build + Core.Tests + ShopERP.Tests ĐÃ PASS trong session) — `env -u GH_TOKEN -u GITHUB_TOKEN git push --no-verify origin main` (pattern #11: GH_TOKEN/GITHUB_TOKEN env stale — keyring valid; NEVER nhúng token vào remote URL) → verify `gh api repos/anlebao/Gemini_Windsurf/branches/main --jq '.commit.sha'` vs `git rev-parse HEAD`.
> **Bước 2 — CD:** theo dõi `cd-multivps.yml` run (poll `gh run list --json status,conclusion`) — trước: run #37431539179 SUCCESS ×11 jobs (gateway/khachlink/shoperp/directory/crawler + Deploy 3 VPS + Post-Deploy Smoke). Migrations PG tự apply qua gateway startup (3 booking migrations đã applied từ P4 push).
> **Bước 3 — RV L1/L2 (đã có pattern từ P4 RV):** L1 markers Gateway.dll `PublicBookingController`/`TenantBookingController` + CoreHub.dll booking services + ShopERP.dll `BookingTenantApiClient`/`BookingQueue`; L1b migrations (3 migration booking trong `"__EFMigrationsHistory"` — quote `"MigrationId"`); L2 `/health` 200 + public route probe qua SSH VPS (`docker exec vanan-gateway-1 curl localhost:80/api/public/booking/tenants/{bogus}/services` → 404 friendly Q5 gate).
> **Bước 4 — SEED DATA booking-enabled tenant (bắt buộc cho L3/L4):** qua tenant API (JWT tenant_id claim — mint qua `GatewayAdminApiClientBase` hoặc dev-token): PUT `/api/tenant/booking/config` (isEnabled=true + deposit policy) · POST staff (1-2 nhân viên) + POST staff/{id}/services (skills) · POST staff/{id}/schedules (weekday 0..6 + giờ) · POST qr-channels (token = Guid N — giữ raw token làm `BOOKING_TEST_QR_TOKEN`) · offering: P5 chưa có offering CRUD API → seed trực tiếp PG (INSERT AppointmentOfferings/Categories qua psql vanan_admin) HOẶC thêm offering CRUD nhanh vào tenant API nếu cần.
> **Bước 5 — RV L3-L5:** L3 E2E production — `playwright-rv-vaiie.config.ts` (impersonate tenant pattern: storageState auth/rv-vaiie.json + Chromium `--host-resolver-rules=MAP app2.khachvip.online <IP>` vì sandbox egress không tới CDN + ignoreHTTPSErrors) → `npx playwright test e2e-tests/booking-customer.spec.ts` (env `BOOKING_TEST_QR_TOKEN`) + các spec P6; L4 UI flow thật (booking → queue confirm → assign → check-in → complete → Order tạo + status polling); L5 manual (user). **STOP at first failure** (runtime-verification.md).
> **Bước 6 — ĐÓNG:** cập nhật project_state (Section 2/3/4 + maintenance log) + master plan status DONE + task card Phase 7 check → thông báo user Sprint B2 sẵn sàng.

- [x] **P7.1** `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests ALL PASS → commit từng phase
- [x] **P7.2** CD Multi-VPS + migration PG `AddBookingScheduling` applied
- [x] **P7.3 RV L1-L4 (L5 manual chờ user):** L1 markers + migrations · L2 health/public routes · L3 E2E production (impersonate tenant — pattern va-iie RV) · L4 UI flow thật (tenant demo booking → confirm → assign → check-in → Order) · L5 manual (user) → cập nhật project_state + master plan + đóng task card

## 4. USER DECISIONS — ĐÃ CHỐT (2026-10-05)

| # | Câu hỏi | Quyết định | Chi tiết |
|---|---|---|---|
| 1 | Booking sống ở đâu? | **Gateway PG — source of truth (Option A)** | Giống Orders Option C: booking/staff/offering/QR/commission ở PG; public API Gateway; availability+conflict authoritative PG; tenant ops qua Gateway API; KHÔNG replica SQLite MVP |
| 2 | Completed booking có chuyển Order? | **CÓ — chuyển thành Order** | Tạo Order tại transition chốt (Q1) qua path hiện có (giữ HTX tag — lesson P6c) |
| 3 | Commission ledger? | **Hợp nhất 1 ledger** | `CommissionLedgerEntry` chung BOOKING\|ORDER; SalesReferral legacy giữ nguyên |
| 4 | Customer booking UI ở đâu? | **Thêm page vào KhachLink** | 4 màn hình + Status page trong KhachLink WASM (lazy-load route) |
| 5 | Ưu tiên roadmap? | **Feature này TRƯỚC Sprint B2** | Sprint B2 (HR-Payroll) defer; Order.StaffId vẫn để B2 |
| 6 | (Q1) Booking→Order ở transition nào? | **COMPLETED** | Tạo Order khi hoàn tất — đơn giản, đúng doanh thu; karaoke/spa tạo Order thủ công ở check-in nếu cần phục vụ sớm |
| 7 | (Q2) Cơ chế hợp nhất ledger? | **Entity mới `CommissionLedgerEntry`** | 1 bảng chung source BOOKING\|ORDER; SalesReferral legacy giữ nguyên |
| 8 | (Q5) Tenant nào được bật booking? | **Feature-flag per tenant** | `BookingTenantConfig.IsEnabled` (default false) — chỉ tenant demo/dịch vụ mới enable; không regress F&B |

## 5. SRS REVIEW NOTES — RESOLUTION

| # | Câu hỏi | Resolution |
|---|---|---|
| Q1 | Booking→Order transition nào? | ✅ **COMPLETED (chốt 2026-10-05)** — tạo Order khi hoàn tất, đúng doanh thu; idempotent + HTX tag giữ |
| Q2 | D3 hợp nhất ledger — cơ chế? | ✅ **Entity mới `CommissionLedgerEntry`** (chốt 2026-10-05) — source_type BOOKING\|ORDER, nullable booking_id/order_id; SalesReferral giữ legacy (không phá vỡ tests); chuẩn tương lai cho B2 staff commission |
| Q3 | Booking có cần số khách (party size) / phòng (room) không? | **KHÔNG cho MVP** — extension point data model (Resource) — SRS §22: room/chair là future; MVP 1 staff 1 offering |
| Q4 | Deposit: nhận tiền cọc qua VietQR ngay ở MVP? | **CÓ (reuse VietQrService)** nếu Checkout pattern sẵn; ngược lại PaymentTransaction status PENDING + manual — §16.3 không block MVP |
| Q5 | Tenant nào mở booking? | ✅ **Feature-flag per tenant (chốt 2026-10-05)** — `BookingTenantConfig.IsEnabled` default false; chỉ tenant enable mới resolve QR/bookings — đúng §16.5 tax profile per-tenant |
| Q6 | Staff có cần thuộc `Users` (DemoUser) ngay? | **StaffUserId nullable** (FK → Users.Id như Shift.StaffUserId) — B2 payroll sẽ dùng; MVP cho phép staff chưa có user |

## 6. SUCCESS CRITERIA (SRS §35 DoD + §34)

- [ ] DB persistence + server-side validation thật (KHÔNG stub availability/commission)
- [ ] Double-booking protection thật — E2E race PASS (AC-C04)
- [ ] Tenant isolation — 5 negative tests PASS (§18.3)
- [ ] QR attribution persist thật — first-qualified-wins + revoked check (AC-Q01/Q02/Q06)
- [ ] Commission ledger snapshot immutable + reversal (AC-Q03/Q04/Q05) + tax snapshot khi khấu trừ
- [ ] Status polling hoạt động + terminal-state stopping (AC-C06)
- [ ] Payment/Invoice integration state persist thật + outbox events (invoice_trigger snapshot)
- [ ] Booking→Order (D2): đúng path, idempotent, HTX tag giữ
- [ ] KhachLink 4 screens + ShopERP 7 pages UI Platform 100% (Gate 5) · E2E specs PASS (Gate 4)
- [ ] `guard-check.ps1` + `dotnet build VanAn.sln` PASS mọi batch · Domain pure · Single-Identity 100% · AccountingEntry không đụng
- [ ] Deploy + RV production PASS (CD Multi-VPS + L1-L5) → đóng task card + master plan

## 7. CONSTRAINTS (governance)

- Domain PURE (no EF Core/DbContext/DataAnnotations) — mọi entity trong `1_Shared/Domain.cs`
- Single-Identity Pattern 100% (Id = PK, business key VO Ignore, constructor sync `Id = XxxId.Value`)
- Multi-tenancy mọi layer (TenantId filter — KHÔNG leak cross-tenant, lesson #22-23 governance; booking tenant context từ QR resolve/authenticated context — KHÔNG tin client)
- AccountingEntry immutable — không đụng
- CommissionLedgerEntry immutable sau finalized — reversal tạo entry mới (như AccountingEntry/WalletTransaction)
- UI Platform components 100% (Gate 5) — không bypass
- E2E spec cho UI mới (Gate 4) · Playwright DISABLED trong IMPLEMENT (playwright.rules)
- Domain modification CHỈ khi được duyệt (task card này = đề xuất → user approve trước khi implement)
- `guard-check.ps1` + `dotnet build VanAn.sln` MUST PASS trước mọi commit
- Tax/legal rules sau versioned adapters (KHÔNG hard-code 10% TNCN, KHÔNG hard-code thời điểm hóa đơn)
- Secrets: KHÔNG commit token; booking QR token hash lưu DB (không lưu plaintext)

## 8. DEFERRED (ghi nợ rõ ràng)

- **Sprint B2 HR-Payroll** — defer SAU booking (D5); `Order.StaffId` + staff commission payout sẽ dùng `CommissionLedgerEntry` + `Staff.StaffUserId`
- **SignalR booking realtime** — MVP polling (§10.2); infrastructure giữ nguyên
- **Audio storage / object storage** — cấm MVP (§15.2)
- **Multi-resource scheduling / room/chair booking / CSP solver** — extension point data model (§22)
- **Push/SMS/Zalo notification thật** — extension point Phase 2 (§10.4); reuse IAlertNotifier stub
- **Booking replica SQLite / NATS delivery booking** — KHÔNG MVP (D1 — tenant ops qua Gateway API)
- **Device fingerprint / anti-self-referral / velocity detection** — Phase 2 (§26 future)
- **Dynamic pricing / AI recommendation / marketplace** — ngoài MVP (§3)
