# MASTER PLAN: Vạn An Appointment Booking & Staff Scheduling MVP (SRS v1.1)

> Created: 2026-10-05 (user review SRS + chốt 5 quyết định 2026-10-05)
> Source SRS: `docs/requirements/van_an_appointment_booking_srs_v1.1_mvp (1).md` (44 sections, 7 phases §36)
> Status: **ACTIVE — P1 DONE (`a8ce5482`, 2026-10-05) → Session P2 Services core**
> Branch: `main`
> Priority: **ƯU TIÊN TRƯỚC Sprint B2 (HR-Payroll)** — quyết định user 2026-10-05 (D5)

---

## 1. MỤC TIÊU

Xây **Appointment Booking Infrastructure** dùng chung cho tenant mô hình dịch vụ theo lịch hẹn (Spa / Hair Salon / Massage / Karaoke / Clinic), đúng SRS v1.1 MVP:

`QR Code → Booking Form (4 màn hình tap-first) → Chọn Offering/Combo → Chọn giờ → Chọn staff / "Bất kỳ ai" → Quick Tags / Speech-to-Text → Đặt cọc (nếu có) → Xác nhận → Tenant confirm → Gán staff → Theo dõi trạng thái (HTTP polling) → Hoàn tất → Chuyển Order → Payment/Invoice → Tính hoa hồng (qualified-only)`

**MVP hard requirements (SRS §35 DoD):** DB persistence thật · server-side validation · **double-booking protection thật** (AC-C04) · tenant isolation có automated tests (§18.3) · QR attribution persist thật · commission ledger có historical snapshot · status polling với terminal-state stopping · payment/invoice integration state persist thật · tax rule snapshot khi phát sinh khấu trừ · E2E happy + critical negative path.

**MVP non-goals (§3):** KHÔNG SignalR phụ thuộc · KHÔNG audio storage · KHÔNG N-service solver · KHÔNG load-balancing staff · KHÔNG xây HĐĐT/kế toán engine (chỉ facts + outbox).

---

## 2. HIỆN TRẠNG BASE CODE (khảo sát 2026-10-05)

| Thành phần SRS cần | Hiện trạng | Kết luận |
|---|---|---|
| Multi-tenant isolation (§18) | `Tenant`/`TenantId` VO + `IMustHaveTenant` + global filter + PG (Gateway) / SQLite (ShopERP) | ✅ Đầy đủ — tái dùng |
| Customer identity (§4.1) | `Customer` + `CustomerDeviceId` (anonymous zero-friction) | ✅ Đầy đủ |
| Salesman + QR (§7, §17) | `CommunityRole` (SalesmanCode 6 ký tự) · `SalesReferral` (commission snapshot + Pending/Paid/Rejected/Held) · `ProductReferralConfig` · `AppInstallAttribution` · `WalletTransaction` (immutable + Reversal) · `SalesmanQR.razor` (QR canvas) | 🟡 ~60% — nhánh product-order; cần nhánh booking |
| E-invoice boundary (§16.4-16.6) | `ElectronicInvoice`/`InvoiceAggregate`/`OutboxEvent` (RoutingKey + CorrelationId)/`SubmitAttempt`/`BatchInvoiceProcessor` | ✅ Đầy đủ — booking chỉ emit facts |
| Event/audit (§23-24) | `OutboxEvent` + `AuditTrailService`/`AuditLogQueue` | ✅ Đầy đủ |
| Notification (§10.4) | `IAlertNotifier` Telegram/Zalo stub + `AlertNotifierDispatcher` fail-safe (VA-IIE Sprint B) | ✅ Đầy đủ |
| Voice STT text-only (§15) | `Order.VoiceNoteText` (audio đã xóa) · browser SpeechRecognition | ✅ Đầy đủ |
| Public anonymous API | `PublicOrdersController` + Gateway YARP + nginx rate-limit | ✅ Đầy đủ |
| Order creation path | `OrdersController.CreateOrder` (Gateway, PG) → `CreateOrderFromCommandAsync` (+ `ApplyHtxInternalTagAsync` chung 2 path — lesson P6c) → NATS → ShopERP | ✅ Tái dùng cho D2 |
| ShopERP → Gateway API | `GatewayAdminApiClientBase` + `FinancialIntelligenceHttpService` (Guid? tenantId override — FI SystemAdmin precedent) | ✅ Tái dùng cho tenant ops |
| Staff identity | `DemoUser` (UserRole.Staff) + `Shift.StaffUserId → Users.Id` (VA-IIE) | 🟡 Cần Staff profile mới (skill/schedule) |
| **Staff scheduling / availability / conflict** | KHÔNG có — domain mới hoàn toàn | ❌ MỚI |
| **AppointmentOffering / Package / AddOn** | KHÔNG có — Product là F&B item (không duration/skill) | ❌ MỚI |
| **Booking aggregate + 9-state machine** | `Order` state machine là reference pattern (OrderStatusId string VO) | ❌ MỚI (theo pattern Order) |
| **QRChannel / AttributionSession** | Chưa có campaign/attribution-session/expiry/immutable snapshot | ❌ MỚI |
| **Deposit classification / PaymentTransaction** | `Order.PaymentStatus` string — không có SECURITY vs PREPAYMENT | ❌ MỚI |
| **TaxWithholdingPolicy** (NĐ 253/2026 — 5tr/lần) | Chưa có — `SalesReferral` chỉ snapshot rate | ❌ MỚI |
| UI Platform | `VanAButton`/`VanACard`/`VanAForm`/`VanAInput`/`VanALayout`/`VanAMetricsCard`/`VanAAlert`/Modal/Table + NavMenu động (`IShopErpMenuService`) | ✅ Đầy đủ |
| Test hạ tầng | Core.Tests 1977 · ShopERP.Tests 136 · Architecture 41 · Playwright + guard-check | ✅ Đầy đủ |

---

## 3. QUYẾT ĐỊNH ĐÃ CHỐT (user 2026-10-05)

| # | Quyết định | Chi tiết |
|---|---|---|
| D1 | **Booking sống ở Gateway PG — source of truth (Option A)** | Giống Orders (Option C): toàn bộ Booking/Staff/Offering/QR/Attribution/Commission entities lưu **PG (Gateway DbContext)**. Public booking API trên Gateway. Availability + conflict check authoritative tại PG (transaction + row lock). **KHÔNG sync replica sang ShopERP SQLite cho MVP** — tenant ops qua Gateway API (pattern `GatewayAdminApiClientBase`/`FinancialIntelligenceHttpService`). NATS chỉ emit event (notification/audit), không phải source của tenant display. |
| D2 | **Completed booking → chuyển thành Order** | Tại transition **COMPLETED** (chốt Q1 2026-10-05 — đơn giản, đúng doanh thu; karaoke/spa dùng Order tạo ở check-in thủ công nếu cần phục vụ sớm) → tạo Order qua path `CreateOrderFromCommandAsync` hiện có (đảm bảo `ApplyHtxInternalTagAsync` — lesson P6c) → NATS → ShopERP. Booking giữ link `OrderId`. |
| D3 | **Hợp nhất commission ledger** | Một ledger chung cho cả booking lẫn order (Q&A chốt cơ chế: entity mới `CommissionLedgerEntry` generic vs extend `SalesReferral`). `SalesReferral` legacy **giữ nguyên** (không đụng — tránh regress 1000+ tests). Payout qua `WalletTransaction` (Type=Commission + Reversal) như hiện có. |
| D4 | **Customer booking pages thêm vào KhachLink** | Thêm pages mới vào KhachLink WASM (không tạo app/public flow riêng): 4 màn hình (Offering → Time & Staff → Note & Deposit → Confirm) + Status page (HTTP polling). |
| D5 | **Ưu tiên feature này TRƯỚC Sprint B2 (HR-Payroll)** | Sprint B2 defer sau booking. `Order.StaffId` vẫn để B2 (không đụng Order lần này ngoài D2 order creation). |

---

## 4. SCOPE (phases + task cards)

| Phase | Nội dung | Layer | Task card | Trạng thái |
|---|---|---|---|---|
| **1** | **Domain + EF + migration PG + seed** — Staff/StaffService/StaffWorkingSchedule/StaffScheduleOverride/ServiceCategory/AppointmentOffering/AppointmentOfferingItem/AddOn/QRChannel/AttributionSession/Booking/BookingItem/BookingStaffAssignment/PaymentTransaction/InvoiceIntegrationRecord/BookingEvent/CommissionRule/CommissionLedgerEntry/BookingTenantConfig (19 entity + 6 enum) | 1_Shared + 2_Gateway (migrations) | `task_booking_phase1_domain.md` | ✅ `a8ce5482` (26 tests PASS) |
| **2** | **Services core** — IStaffService · IOfferingService · IAvailabilityService (skill match + schedule + conflict) · IBookingService (state machine + idempotency + **double-booking prevention** + audit events + outbox) | 3_CoreHub | `task_booking_phase2_services.md` | ⏳ pending |
| **3** | **Services QR/Commission/Financial + Order hook** — IQRAttributionService (first-qualified-wins + expiry) · ICommissionService (qualification = COMPLETED + paid + attribution valid; reversal; ledger hợp nhất D3) · TaxWithholdingPolicy adapter (NĐ 253/2026 — 5tr/lần, KHÔNG hard-code 10%) · IBookingFinancialService (deposit classification SECURITY/PREPAYMENT/FINAL + InvoiceIntegrationRecord + outbox facts) · **hook Booking→Order (D2)** | 3_CoreHub | `task_booking_phase3_services2.md` | ⏳ pending |
| **4** | **Public API (Gateway) + KhachLink customer UI** — 6 public endpoints (QR resolve, offerings, availability, create + Idempotency-Key, status polling + ETag, cancel) · 4 screens tap-first + Status page (5s/10-15s polling, terminal-stop) · Quick Tags + SpeechRecognition JS interop (text-only, fallback input) · lazy-load route | 2_Gateway + 5_WebApps/KhachLink | `task_booking_phase4_public_api_ui.md` | ⏳ pending |
| **5** | **Tenant backend UI (ShopERP)** — booking queue PENDING_CONFIRMATION · detail + confirm/reject · assign/change staff (re-check conflict server-side) · staff management (CRUD + skill) · working schedule config (weekday + override + leave/break) · **who-is-available dashboard** · staff day/week calendar · deposit tracking · QR channel admin (tạo/revoke) · commission admin + salesman view | 5_WebApps/ShopERP (qua Gateway API) | `task_booking_phase5_shoperp_ui.md` | ⏳ pending |
| **6** | **Hardening + full test matrix (SRS §34)** — concurrency tests (AC-C04 race) · tenant isolation negative tests (§18.3 — 5 cases) · rate limiting verify · audit completeness · perf (availability p95 ≤ 500ms — reconcile SLO §28) · **9 E2E specs** (gộp 3-4 spec files) | 6_Tests + 6_Testing | `task_booking_phase6_hardening.md` | ⏳ pending |
| **7** | **Deploy + RV production** — CD Multi-VPS + migrations PG applied + L1-L5 (markers → health → E2E production → UI flow thật → manual) | production | `task_booking_phase7_rv.md` | ⏳ pending |

**Dependency chain:** 1 → 2 → 3 → 4 → 5 → 6 → 7 (tuần tự; P4 UI có thể song song chuẩn bị sau khi P1 API contract chốt — nhưng MVP giữ tuần tự session-per-phase như VA-IIE).

---

## 5. THIẾT KẾ CHI TIẾT

### 5.1 Domain/Data (Phase 1) — `1_Shared/Domain.cs`, Single-Identity Pattern 100%

**Staff & Scheduling:**
- `Staff` (tenant-scoped, PK = Id): `StaffId` VO Ignore · DisplayName · Role · Active · AvatarUrl? · `StaffUserId` (Guid?, FK → Users.Id — precedent `Shift.StaffUserId`, để B2 payroll reuse)
- `StaffService` (Staff ↔ Offering/Service capability): StaffId · ServiceId · (eligibility mapping — SRS §11.2)
- `StaffWorkingSchedule`: StaffId · Weekday · StartTime · EndTime · BreakStart? · BreakEnd? (SRS §11.3 weekday recurring)
- `StaffScheduleOverride`: StaffId · Date (exact) · Type = `WORKING | LEAVE | UNAVAILABLE | BREAK` · StartTime/EndTime (SRS §11.3 override + leave + unavailable)

**Catalog:**
- `ServiceCategory` (name, display order)
- `AppointmentOffering`: offering_type = `SERVICE | PACKAGE` · display_name · **duration_minutes_snapshot** · **price_snapshot** · required_staff_skill? · active · category (SRS §8.2)
- `AppointmentOfferingItem` (package con: OfferingId · ChildServiceName · Quantity — snapshot)
- `AddOn` (chỉ non-scheduling: name · price · active — nước uống/sản phẩm/phụ thu cố định, SRS §8.3)

**Booking aggregate:**
- `Booking`: public_booking_code (opaque, non-sequential — SRS §25) · TenantId · CustomerId? (CustomerDeviceId anonymous fallback) · start_at/end_at · status (9 state — §9.1) + internal sub-states (deposit_pending/deposit_paid/… §9.2) · offering_id + snapshots (name/duration/price — §8.4) · staff_id? · deposit_required/deposit_type · payment_status (7 state §16.3) · invoice_status/invoice_trigger (§16.4) · attribution_id? · **version** (optimistic concurrency — §19.1) · OrderId? (D2 link)
- `BookingTenantConfig` (feature-flag per tenant — Q5 chốt 2026-10-05): `IsEnabled` (default false, get-or-create pattern `VaIIeTenantConfig`) · deposit policy (no_deposit/fixed/percentage §16.1) · cancel/reschedule policy text · e-invoice applicability profile ref (§16.5) — 1 row/tenant, unique TenantId index. **Chỉ tenant được enable mới resolve QR/bookings.**
- `BookingItem` (offering line + add-ons snapshots)
- `BookingStaffAssignment` (0..1 primary staff; immutable history entries — reassign tạo entry mới + audit)
- `BookingEvent` (audit event store: aggregate_type/aggregate_id/event_type/actor/actor_id/timestamp/metadata — SRS §23)

**QR / Attribution / Commission:**
- `QRChannel`: qr_token (opaque, unguessable — hash lưu DB, §7.2/§25) · TenantId · CampaignId? · SalesmanId? · Active · CreatedAt · RevokedAt (SRS §17.2)
- `AttributionSession`: tenant_id · qr_id · salesman_id? · campaign_id? · first_seen_at · last_seen_at · anonymous_session_id · attribution_expiry_at (§7.4) — **first-qualified-wins** (§7.5)
- `CommissionRule`: tenant_id · offering_id? · salesman_id? · commission_type · commission_value · qualification_status (§17.2)
- `CommissionLedgerEntry` (D3 — ledger hợp nhất): source_type = `BOOKING | ORDER` · booking_id?/order_id? · salesman_id · qr_id · rule snapshot (JSON) · base_amount · gross · **tax_withheld_amount** · net · currency · state = `PENDING | EARNED | VOIDED | PAID | REVERSED` (§17.4) · tax_rule_version · created/finalized/paid_at (§17.2) — **immutable sau finalized, reversal tạo entry mới** (§17.5)
- `TaxWithholdingPolicy` (versioned adapter — inputs: payee_type/residency/contract_type/amount/rule_version → gross → withheld → net; mốc **5tr/lần** NĐ 253/2026, KHÔNG hard-code 10% — §17.3/§43.2): policy entity + adapter interface

**Financial:**
- `PaymentTransaction`: booking_id · type = `SECURITY_DEPOSIT | PREPAYMENT_FOR_SERVICE | FINAL_PAYMENT` (§16.2) · amount · method (CASH/VIETQR — reuse VietQrService) · status (NOT_REQUIRED/PENDING/PAID/FAILED/PARTIALLY_REFUNDED/REFUNDED/FORFEITED §16.3) · provider_ref?
- `InvoiceIntegrationRecord`: booking_id · invoice_status (NOT_REQUIRED/PENDING/ISSUED/FAILED/CANCELLED §16.4) · invoice_trigger (ON_PAYMENT/ON_COMPLETION/EXTERNAL_RULE/NOT_APPLICABLE — **snapshot tax policy, không cho frontend chọn**) · provider · reference · error_code · issued_at

**EF/migrations:** configs (Ignore business key VO + precision) + **1 migration PG** `AddBookingScheduling` (Gateway DbContext) — pattern Sprint B PG-empty. Seed: service categories mẫu cho tenant demo (KHÔNG bắt buộc — get-or-create defaults).

### 5.2 Services core (Phase 2) — `3_CoreHub/Services/Booking/` (namespace `VanAn.CoreHub.Services.Booking`)

- `IStaffService`: CRUD staff + skills + schedule + override (tenant-scoped, mọi query filter TenantId — lesson a21f97f2)
- `IOfferingService`: CRUD ServiceCategory/AppointmentOffering/Package/AddOn
- `IAvailabilityService`: **`GetAvailableSlotsAsync(tenantId, offeringId, date, staffId?)`** — chỉ trả staff/slot còn khả dụng (SRS §11.5, Risk 3): active ∧ skill match ∧ working interval ∧ không break/leave/unavailable ∧ không booking conflict. Trả lý do unavailable tối thiểu cho tenant admin (§11.5).
- `IBookingService`: create (idempotency key — §21.1) → state machine transitions (validate allowed transitions §9.3, reject invalid — backend authoritative §37.2) · confirm/reject idempotent (§21.2) · assign/change staff (idempotent §21.3 + re-check conflict) · check-in/start/complete · cancel (policy) · status polling DTO (public token) · **audit events mọi transition** (§24) · emit BookingEvent + outbox
- **Double-booking prevention (§12, AC-C04):** server re-check availability + **atomic conflict check trong transaction PG** (SELECT … FOR UPDATE trên staff schedule / booking rows — precedent `WalletService` HR-SCALE-3) → tối đa 1 request thành công, request còn lại nhận business conflict (409 → message tiếng Việt thân thiện §6.5). KHÔNG dựa UI lock.

### 5.3 Services QR/Commission/Financial + Order hook (Phase 3)

- `IQRAttributionService`: resolve qr_token → tenant/campaign/salesman/policy (token active ∧ tenant active ∧ salesman ∈ tenant — §7.3, **không suy diễn tenant từ client**) · tạo/update AttributionSession (rate-limit rapid scans §26.3, refresh không đổi attribution §26.4) · gắn immutable snapshot vào booking (§7.6)
- `ICommissionService`: qualification check (**COMPLETED + payment qualified + attribution valid** — §17.1, Risk 4) → tạo CommissionLedgerEntry (snapshot rule + tax) · finalize/reverse (refund/cancel → reversal nếu rule yêu cầu §26.6) · ledger hợp nhất (D3) · payout → `WalletTransaction` (Commission + Reversal)
- `TaxWithholdingPolicyAdapter`: versioned — inputs §17.3 → snapshot gross/withheld/net + reason code; test fixtures versioned (§34 unit)
- `IBookingFinancialService`: deposit capture (classification) → PaymentTransaction + VietQR (reuse `VietQrService`) · invoice facts → InvoiceIntegrationRecord + outbox events (`DepositPaid`/`PaymentCaptured`/`ServiceCompleted`/`InvoiceRequired`… §16.6) — booking là source of facts, Accounting/E-Invoice là source of state (§16.6, Risk 11)
- **Booking→Order hook (D2):** tại transition **COMPLETED** (Q1 chốt) → build `CreateOrderCommand` (items = offerings/add-ons snapshots) → gọi path hiện có (giữ `ApplyHtxInternalTagAsync` — lesson P6c) → lưu `Booking.OrderId` → NATS delivery tự động (không cần code mới bên ShopERP cho MVP)

### 5.4 Public API (Phase 4) — Gateway (map conventions hiện có, §20)

```http
GET  /api/public/booking/qr/{qrToken}                          # resolve → tenant branding + offerings
GET  /api/public/booking/tenants/{tenantId}/services           # categories + offerings + add-ons
GET  /api/public/booking/availability?tenantId=&offeringId=&date=&staffId=   # chỉ trả available
POST /api/public/booking/bookings                              # Idempotency-Key header (§21.1)
GET  /api/public/booking/bookings/{publicBookingToken}         # status polling + ETag/Last-Modified (§10.3)
POST /api/public/booking/bookings/{publicBookingToken}/cancel  # theo tenant policy
```
+ rate-limit public endpoints (nginx đã có) + opaque token, không trả internal IDs/commission (§25). Tenant ops endpoints (queue/confirm/reject/assign/check-in/start/complete/availability/staff/schedules/financial-status) đặt theo convention `/api/tenant/booking/...` (authorize tenant claim).

### 5.5 KhachLink customer UI (Phase 4) — UI Platform 100%, tap-first, ≤4 màn hình (§5.1)

- `/booking/{qrToken}` — Screen 1 **Offering**: tenant logo/name + category chips + offering cards (giá + duration) + add-on chips (non-scheduling) + sticky bottom summary (§5.2)
- Screen 2 **Time & Staff**: horizontal date picker + time slot large buttons + staff filter (Bất kỳ ai / staff cụ thể — server-filtered §11.5) — 1 primary staff, KHÔNG multi-resource (§5.2)
- Screen 3 **Note & Deposit**: Quick Tags chips (Phòng riêng/Lần đầu đến/KTV nữ/Cần chuẩn bị trước) + 🎙 SpeechRecognition JS interop (text-only, editable, fallback text input — KHÔNG upload audio §15.2/§15.3) + deposit selector (không cọc / số cấu hình §16.1)
- Screen 4 **Confirm**: summary + policy hủy/reschedule + CTA lớn → submit (idempotency key giữ client state, retry — §29, KHÔNG báo thành công trước server persist)
- **Status page**: HTTP polling 5s × 2 phút → 10-15s (§10.2), dừng ở terminal state, nút Làm mới + lỗi hướng dẫn hành động (§6.5)
- Lazy-load route (Blazor WASM lazy assembly) — mitigation initial load vs §28 ≤3s (R5)

### 5.6 ShopERP tenant UI (Phase 5) — qua Gateway API (pattern GatewayAdminApiClientBase/FI)

- `/booking/queue` — PENDING_CONFIRMATION list (AC-T01) + detail: confirm/reject (AC-T02) + assign/change staff (AC-T03 — hiển thị capability + available trước §13, re-check conflict server-side)
- `/booking/staff` — staff CRUD + skill/service eligibility (AC-T03/T05)
- `/booking/schedules` — working schedule config: weekday recurring + exact override + leave/break (§11.3)
- `/booking/availability` — **who-is-available dashboard** (AC-T05, §14): staff/skill/time/status table
- `/booking/calendar` — staff day/week calendar (AC-T04)
- `/booking/deposits` — deposit tracking (§16)
- `/booking/qr-channels` — QR admin: tạo QR (tenant/campaign/salesman) + revoke (§26.1) · `/booking/commission` — rules + ledger + salesman view
- NavMenu (IShopErpMenuService) + Sitemap

### 5.7 Hardening + tests (Phase 6) — SRS §34 matrix

- **Unit:** working schedule · break/leave exclusion · skill matching · fixed package duration · availability calculation · commission calculation · attribution resolution · state transition validation · tax-withholding adapter (versioned fixtures)
- **Integration:** create booking · idempotent create · confirm · assign staff · **double-booking prevention** · deposit/payment state · invoice event/outbox · QR attribution persistence · commission finalization/reversal · **tenant isolation (5 negative cases §18.3)**
- **E2E (9 SRS §34 → gộp 3-4 spec files, precedent va-iie-shift/forecast):** `booking-customer.spec.ts` (QR→offering→time→tag/STT-fallback→booking→status polling confirm) · `booking-commission.spec.ts` (QR salesman→completed→commission) · `booking-concurrency.spec.ts` (2 customer race same staff/time — AC-C04) · `booking-isolation.spec.ts` (tenant A/B: đọc/assign/QR/commission/token — AC-Q01)
- Audit completeness (§24) + perf sanity (availability p95) + rate-limit verify

---

## 6. EXECUTION STRATEGY (session-per-phase — precedent VA-IIE Sprint B)

| Session | Phase | Deliverable | Validation |
|---|---|---|---|
| S1 (2 ngày nếu cần) | P1 | Domain 18 entities + EF + migration PG | guard + build + Architecture tests |
| S2 | P2 | 4 services core + double-booking + tests | Core.Tests + integration tests |
| S3 | P3 | QR/Commission/Financial services + Tax adapter + Order hook | Core.Tests |
| S4 (2 ngày) | P4 | Public API + KhachLink 4 screens + polling | guard + ShopERP/KhachLink build |
| S5 (2 ngày) | P5 | ShopERP 7 pages + NavMenu + Sitemap | ShopERP.Tests (bUnit) |
| S6 (2 ngày) | P6 | Test matrix đầy đủ + E2E specs | guard + build + E2E (sau IMPLEMENT) |
| S7 (2 ngày) | P7 | CD Multi-VPS + migrations + RV L1-L5 | RV protocol 5 lớp |

**Tổng ước lượng: ~7-9 session ≈ 4-6 sprint** (feature lớn nhất repo — scope gấp 3-4× sprint VA-IIE điển hình).

**Test delta ước lượng:** Core.Tests ~1977 → ~2150-2200 · ShopERP.Tests ~136 → ~180-200 · E2E +9 tests (3-4 specs).

---

## 7. RỦI RO & MITIGATION

| # | Rủi ro | Mitigation |
|---|---|---|
| R1 | **Double-booking concurrency (AC-C04)** — bài toán khó nhất | Conflict check atomic trên PG (SELECT FOR UPDATE precedent WalletService) + integration test race 2 requests; SQLite KHÔNG tham gia conflict (D1 — booking chỉ ở PG) |
| R2 | Scope creep kế toán/thuế | Giữ ranh giới §16: booking emit facts + outbox, KHÔNG định khoản; TaxWithholdingPolicy versioned adapter (không hard-code 10%) |
| R3 | Khối lượng E2E/test §34 nặng | Test matrix gộp phase 6 riêng (2 session); E2E 9 tests gộp 3-4 specs; Playwright governance giữ |
| R4 | Trùng khái niệm commission (SalesReferral legacy) | D3: ledger hợp nhất 1 bảng mới; legacy giữ nguyên — không đụng 1000+ tests |
| R5 | WASM initial load vs §28 ≤3s | Lazy-load booking route (lazy assembly); target áp cho booking route |
| R6 | ShopERP tenant ops phụ thuộc Gateway API (network) | Pattern đã chứng minh (FI SystemAdmin selector) — errors hiển thị rõ + retry |
| R7 | Booking→Order kép accounting (trùng doanh thu) | Order tạo ở **COMPLETED** với payment liên kết `Booking.OrderId`; test chặn double-create (idempotent) |
| R8 | QR token bảo mật | Token unguessable + hash lưu DB + opaque (không nhúng dữ liệu nhạy cảm §7.2) + rate-limit + revoked check |

---

## 8. OUT OF SCOPE / DEFERRED (ghi nợ rõ ràng)

- **Sprint B2 HR-Payroll** — defer SAU booking (D5); `Order.StaffId` không đụng (B2 sẽ thêm)
- **SignalR booking realtime** — MVP dùng polling (§10.2); infrastructure hiện có giữ nguyên
- **Audio storage / object storage** — cấm (§15.2)
- **Multi-resource scheduling / CSP solver / room/chair booking** — extension point data model thôi (§22)
- **Full HĐĐT/tax engine trong Booking** — chỉ facts + outbox (§16.6)
- **Load-balancing staff / dynamic pricing / AI recommend** — ngoài MVP (§3)
- **Device fingerprint / anti-self-referral / velocity detection** — Phase 2 (§26 future)
- **Push/SMS/Zalo notification thật** — extension point Phase 2 (§10.4); reuse IAlertNotifier stub nếu cần
- **Booking replica sang ShopERP SQLite / NATS delivery booking** — KHÔNG làm MVP (D1 — tenant ops qua Gateway API)

---

## 9. SUCCESS CRITERIA (SRS §35 DoD + §34 matrix)

- [ ] DB persistence + server-side validation thật (không stub availability/commission)
- [ ] Double-booking protection thật — E2E race test PASS (AC-C04)
- [ ] Tenant isolation — 5 negative tests PASS (§18.3)
- [ ] QR attribution persist thật — first-qualified-wins + revoked check (AC-Q01-Q06)
- [ ] Commission ledger snapshot immutable + reversal entry (AC-Q03-Q05)
- [ ] Status polling hoạt động + terminal-state stopping (AC-C06)
- [ ] Payment/Invoice integration state persist thật + outbox events (invoice_trigger snapshot)
- [ ] Tax rule snapshot khi phát sinh khấu trừ (fixtures versioned)
- [ ] Booking→Order (D2): completed booking tạo Order đúng path (HTX tag giữ — lesson P6c)
- [ ] KhachLink 4 screens + ShopERP 7 pages UI Platform 100% (Gate 5) + E2E (Gate 4)
- [ ] `guard-check.ps1` + `dotnet build VanAn.sln` PASS mọi batch · Domain pure · Single-Identity 100%
- [ ] Deploy + RV production PASS (CD Multi-VPS + L1-L5) → đóng master plan
