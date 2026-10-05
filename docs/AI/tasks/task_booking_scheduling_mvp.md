# TASK CARD: Vạn An Appointment Booking & Staff Scheduling MVP (SRS v1.1)

> Created: 2026-10-05 (user directive 2026-10-05: **Booking feature TRƯỚC → Sprint B2 SAU** + 5 quyết định D1-D5)
> Source SRS: `docs/requirements/van_an_appointment_booking_srs_v1.1_mvp (1).md` (v1.1 MVP — 44 sections)
> Master plan: `docs/AI/plans/booking-scheduling-master-plan.md` (ACTIVE — chờ approve)
> Branch: `main`
> Status: **P1 + P2 DONE (2026-10-05, `a8ce5482` + `1a98c7b9` — guard ALL PASSED · build 0 errors · BookingDomainTests 26 PASS + BookingServiceTests 47 PASS) — ⏳ Session P3 QR/Commission/Financial**

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
  - Session 4 (P4): Gateway public API + KhachLink 4 screens + Status polling + E2E spec → guard PASS → commit
  - Session 5 (P5): ShopERP 7 pages + NavMenu + Sitemap + bUnit → guard PASS → commit
  - Session 6 (P6): Test matrix đầy đủ (§34) + 9 E2E (3-4 specs) → guard + build PASS
  - Session 7 (P7): Deploy CD Multi-VPS + migrations PG + RV L1-L5 → cập nhật project_state + master plan status + đóng task card

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

- [ ] **P3.1 `IQRAttributionService`:** resolve qr_token (token active ∧ tenant active ∧ salesman ∈ tenant — §7.3, **không tin tenant từ client** Risk 5) · AttributionSession create/update (rapid-scan guard §26.3, refresh không đổi §26.4) · snapshot vào booking (§7.6)
- [ ] **P3.2 `ICommissionService`:** qualification = COMPLETED + payment qualified + attribution valid (§17.1, Risk 4) → CommissionLedgerEntry (rule snapshot + tax snapshot) · finalize/reverse (refund/cancel → reversal §26.6, AC-Q03-Q05) · ledger hợp nhất (D3) · payout → WalletTransaction (Commission + Reversal — reuse)
- [ ] **P3.3 `TaxWithholdingPolicyAdapter`:** versioned — inputs §17.3 → gross/withheld/net + reason code; **KHÔNG hard-code 10%** (§17.3/§43.2); test fixtures versioned
- [ ] **P3.4 `IBookingFinancialService`:** deposit capture (classification §16.2) → PaymentTransaction + VietQR (reuse VietQrService) · invoice facts → InvoiceIntegrationRecord + outbox events (`DepositPaid`/`PaymentCaptured`/`ServiceCompleted`/`InvoiceRequired`/`InvoiceFailed`/`RefundCompleted` §16.6) — booking = source of facts; Accounting/E-Invoice = source of state (Risk 11)
- [ ] **P3.5 Booking→Order hook (D2, Q1 chốt = COMPLETED):** tại transition **COMPLETED** → `CreateOrderCommand` từ offering/add-on snapshots → path `CreateOrderFromCommandAsync` hiện có (**giữ `ApplyHtxInternalTagAsync` — lesson P6c**) → `Booking.OrderId` + idempotent (không double-create — R7)
- [ ] **P3.6 Tests:** attribution resolution · commission calculation/finalization/reversal · tax adapter fixtures · outbox event generation → Core.Tests PASS

### Phase 4 — Public API + KhachLink customer UI (task `task_booking_phase4_public_api_ui.md`)

- [ ] **P4.1 Gateway public API** (map conventions §20): `GET /api/public/booking/qr/{qrToken}` · `GET .../tenants/{tenantId}/services` · `GET .../availability` · `POST .../bookings` (Idempotency-Key) · `GET .../bookings/{publicBookingToken}` (ETag/Last-Modified §10.3) · `POST .../cancel` + rate-limit (nginx) + opaque token, không trả internal IDs/commission (§25)
- [ ] **P4.2 Gateway tenant API:** `/api/tenant/booking/...` — queue/confirm/reject/assign/change-staff/check-in/start/complete/availability/staff/schedules/financial-status (authorize tenant claim)
- [ ] **P4.3 KhachLink Screen 1 — Offering:** tenant branding (từ QR resolve) + category chips + offering cards (giá/duration) + add-on chips (non-scheduling) + sticky summary (§5.2) — UI Platform 100% (Gate 5)
- [ ] **P4.4 Screen 2 — Time & Staff:** horizontal date picker + time slot large buttons + staff filter (Bất kỳ ai / cụ thể — server-filtered §11.5) — 1 primary staff, KHÔNG N-service (§5.2)
- [ ] **P4.5 Screen 3 — Note & Deposit:** Quick Tags chips (Phòng riêng/Lần đầu đến/KTV nữ/Cần chuẩn bị trước) + 🎙 SpeechRecognition JS interop (text-only + editable + fallback text input — **KHÔNG upload audio** §15.2-15.3) + deposit selector (§16.1)
- [ ] **P4.6 Screen 4 — Confirm + submit:** summary + policy + CTA lớn → submit với idempotency key (client retry §29 — KHÔNG báo thành công trước server persist) → Status page
- [ ] **P4.7 Status page:** polling 5s × 2 phút → 10-15s (§10.2) · dừng ở terminal state · nút Làm mới · lỗi hướng dẫn hành động (§6.5) · lazy-load route (WASM lazy assembly — R5)
- [ ] **P4.8 E2E spec** `booking-customer.spec.ts` (QR→offering→time→quick tag/STT-fallback→booking→status polling confirm — AC-C01/C02/C03/C06) + Sitemap

### Phase 5 — ShopERP tenant UI (task `task_booking_phase5_shoperp_ui.md`)

- [ ] **P5.1 `/booking/queue`:** PENDING_CONFIRMATION list (AC-T01) + detail + confirm/reject (AC-T02) + assign/change staff (AC-T03 — capability+available list trước §13, re-check conflict)
- [ ] **P5.2 `/booking/staff`:** staff CRUD + skill/service eligibility (§11.2)
- [ ] **P5.3 `/booking/schedules`:** working schedule config — weekday + exact override + leave/break (§11.3)
- [ ] **P5.4 `/booking/availability`:** **who-is-available dashboard** (AC-T05 §14 — staff/skill/time/status table)
- [ ] **P5.5 `/booking/calendar`:** staff day/week calendar (AC-T04)
- [ ] **P5.6 `/booking/deposits`:** deposit tracking (§16) + `/booking/qr-channels` (tạo QR tenant/campaign/salesman + revoke §26.1) + `/booking/commission` (rules + ledger + salesman view)
- [ ] **P5.7 NavMenu (IShopErpMenuService) + Sitemap** + bUnit tests → ShopERP.Tests PASS
- [ ] **P5.8** Tenant ops qua Gateway API (`GatewayAdminApiClientBase` pattern + `Guid? tenantId` override — precedent FI SystemAdmin)

### Phase 6 — Hardening + full test matrix (task `task_booking_phase6_hardening.md`)

- [ ] **P6.1 Unit matrix (§34):** working schedule · break/leave exclusion · skill matching · fixed package duration · availability · commission · attribution resolution · state transitions · tax-withholding fixtures
- [ ] **P6.2 Integration matrix (§34):** create/idempotent/confirm/assign · **double-booking** · deposit/payment state · invoice event/outbox · QR attribution persistence · commission finalization/reversal · **tenant isolation 5 negative cases (§18.3):** đọc booking B · assign staff B · QR A tạo booking B · salesman A xem commission B · public token A resolve dữ liệu B
- [ ] **P6.3 E2E 9 tests → gộp 3-4 specs:** `booking-customer.spec.ts` (P4.8) · `booking-commission.spec.ts` (QR salesman→completed→commission + withholding fixture) · `booking-concurrency.spec.ts` (2 customer race — AC-C04) · `booking-isolation.spec.ts` (tenant A/B — AC-Q01; leave → unavailable; deposit → financial status)
- [ ] **P6.4 Rate-limit verify (§25) + audit completeness (§24) + perf sanity** (availability p95 ≤ 500ms reconcile §28 — không tạo SLA riêng)

### Phase 7 — Deploy + RV production (task `task_booking_phase7_rv.md`)

- [ ] **P7.1** `guard-check.ps1` + `dotnet build VanAn.sln` + Core.Tests + ShopERP.Tests ALL PASS → commit từng phase
- [ ] **P7.2** CD Multi-VPS + migration PG `AddBookingScheduling` applied
- [ ] **P7.3 RV 5 lớp:** L1 markers + migrations · L2 health/public routes · L3 E2E production (impersonate tenant — pattern va-iie RV) · L4 UI flow thật (tenant demo booking → confirm → assign → check-in → Order) · L5 manual (user) → cập nhật project_state + master plan + đóng task card

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
