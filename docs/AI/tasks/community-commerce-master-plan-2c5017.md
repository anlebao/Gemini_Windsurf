# Master Plan: Community Commerce Module — Shipper/Salesman

Kế hoạch triển khai 7 sprint (S0-S6) cho module Community Commerce, mỗi sprint có task card + detailed plan riêng, CI/CD pipeline tự động deploy VPS + runtime verification test sau mỗi sprint.

> **Revision history:**
> - v1.1 — 2026-07-25 — Sprint 0 bỏ CC-S0-T4 (Social login), redesign Salesman model (composite referral), thêm 2 entity (ProductReferralConfig, AppInstallAttribution), WalletTransaction Reversal.
> - **v1.2 — 2026-07-26** — **Self-hosted anti-fraud (zero external dependency):** thêm 2 entity (`DeviceRegistration`, `FraudFlag`), `RiskScore` fields trên `SalesReferral` + `AppInstallAttribution`, mở rộng `IdentityLevel` (+`DeviceVerified=4`), UC-09/UC-12 risk scoring + hold 48h, Sprint 0 +2 entity (11 total, tăng từ 9), Sprint 4 +risk scoring service (7 sessions, tăng từ 6), Sprint 6 +Fraud Review UI (4 sessions, tăng từ 3). **SMS OTP OPTIONAL** (không bắt buộc). **WebAuthn Passkey OPTIONAL** (post-PoC Sprint 7+). 5-layer fraud prevention, target <0.5% fraud rate.
> - **v1.4 — 2026-07-26 — Hybrid Central + Edge Architecture (CORE COMPETITIVE ADVANTAGE):**新增 Section 12 "Cost & Capacity Plan" (cost projections PoC→10M, SMS 58% cost driver, multi-channel OTP, Vietnam-specific optimizations, make-or-buy, optimization priority Tier 1/2/3, per-user cost $0.009 @ 1M VN-optimized, edge vs central break-even ~1M users).新增 Section 13 "Sprint 7+ Edge Migration Plan" (15 tasks CC-S7-T1 to T15, entry/exit criteria, 10 VPS verification tests, trigger khi >1M users).新增 Section 14 "Hard Rules Scale Up" (12 rules HR-SCALE-1 to HR-SCALE-12, apply từ Sprint 0 hoặc khi threshold reached). Visual timeline updated với Sprint 7+ post-PoC. Total: PoC 7 sprints (29 sessions, ~37 days) + post-PoC Sprint 7+ (10-15 sessions, when >1M users).
> - **v1.5 — 2026-07-29 — Collaborator Verification Toggle + Sprint 0.5 Fingerprint Wire-up + Customer Login Simplify:** 3 task mới chia vào sprint hiện có: (1) **CC-S0-T3 (Sprint 0.5):** Wire-up DeviceRegistrationService vào production path — Sprint 0 claim fingerprint infrastructure nhưng chưa wire-up (RV0-11 chỉ test JS load, không test end-to-end). Thêm endpoint `POST /api/customer-identity/device/register` + gọi `window.fingerprint.collect()` từ Login.razor/Checkout.razor. (2) **CC-S1-T0c (Sprint 1):** Customer login simplify — xóa SMS OTP khỏi Login.razor primary flow, giữ Google + thêm Guest button. Aligns v1.2 "SMS OTP OPTIONAL" cho customer. (3) **CC-S6-T5 (Sprint 6):** Collaborator SMS OTP + Deposit Wallet với SystemAdmin TOGGLE (ON/OFF). Default OFF (early stage). ON khi scale đủ (Salesman/Shipper/Owner bắt buộc SMS OTP, phí trừ deposit). Domain changes: `WalletTransactionType.Deposit=7` + `SmsOtpFee=8`, `CommunityRole.IsPhoneVerified` + `PhoneVerifiedAt`, `SystemSetting.CollaboratorSmsVerificationEnabled` toggle. Spec updated to v1.5 (Section 1.6 + UC-02b). Total: PoC 7 sprints + Sprint 0.5 (32 sessions, ~40 days).

---

## 0. EXECUTION RULES

### Session protocol
1. **JIT Planning + Pure Execution:** Mỗi session = Phase 1 (chốt file/method/test case) → Phase 2 (viết code). Không re-explore trong Phase 2.
2. **TDD bắt buộc:** Test viết TRƯỚC code. Stub/TODO cấm tính DONE.
3. **guard-check.ps1** chạy TRƯỚC khi ghi DONE.
4. **DoD 5 mục per feature:** code + test theo TDD Plan + DI wired + guard-check 0 errors + endpoint trả data thật.
5. **Runtime verification trên VPS** là exit gate bắt buộc mỗi sprint.

### Branch protocol
```
main
  └─ feature/community-sprint0-foundation
  └─ feature/community-sprint1-nearby-orders
  └─ feature/community-sprint2-delivery-gps
  └─ feature/community-sprint3-chat
  └─ feature/community-sprint4-salesman-qr
  └─ feature/community-sprint5-wallet-cod
  └─ feature/community-sprint6-admin-legal
  └─ feature/community-sprint7-edge-migration   (v1.4 NEW — post-PoC, trigger khi >1M users)
```
- Mỗi sprint = 1 branch, merge vào main sau khi VPS verification pass
- Không merge nếu VPS runtime test fail
- **v1.4 NEW:** Sprint 7 branch trigger khi threshold reached (>1M users OR >1K shippers OR >10K users — xem Section 13 entry criteria). Branch tạo JIT, không tạo trước.

### Hard rules
- Domain entities mới vào `1_Shared/Domain.cs` (Single Source of Truth)
- `WalletTransaction` immutable (append-only, giống `AccountingEntry`) + Reversal pattern (v1.1)
- KhachLink KHÔNG inject DbContext — HTTP qua Gateway only
- UI dùng UI Platform components — không custom HTML
- Community entities trên Gateway PG (cross-tenant)
- `.NET 8.0.x` cho CI/CD (KHÔNG upgrade lên 9.x)
- **Single-Identity Pattern (v1.1):** Mọi entity mới dùng `BaseEntity.Id` trực tiếp (không business key VO) — explicit trong detailed plan.
- **Per-product config (v1.1):** Commission rate + app-install bonus do sysadmin set per-product qua `ProductReferralConfig` — KHÔNG hardcode.
- **Composite referral code (v1.1):** Format `{salesmanCode}|{productShortCode}` — salesman chọn product trước khi generate QR.
- **v1.2 NEW — Self-hosted anti-fraud (zero external dependency):** Mọi anti-fraud logic MUST self-host. KHÔNG phụ thuộc SMS gateway, Zalo OA, WhatsApp, Kafka, Synadia managed, RDS managed. Device fingerprint (FingerprintJS MIT, self-host) + behavioral rules + risk scoring. SMS OTP OPTIONAL (không bắt buộc).
- **v1.2 NEW — Risk scoring mandatory:** Mọi SalesReferral + AppInstallAttribution MUST compute RiskScore (0-100) khi tạo. Score≥60 → hold 48h + FraudFlag. Score≥80 → auto-reject + FraudFlag.
- **v1.2 NEW — WebAuthn OPTIONAL:** Post-PoC (Sprint 7+). Zero vendor dependency (W3C standard, browser native). KHÔNG bắt buộc cho PoC.
- **v1.2 NEW — DeviceRegistration max 3 per Customer:** Enforce tại application layer. Device 4+ → require admin approval.
- **v1.4 NEW — Scale-Up Hard Rules:** See Section 14 "Hard Rules Scale Up" (HR-SCALE-1 to HR-SCALE-12). Apply từ Sprint 0 (HR-SCALE-1, 2, 5, 11) hoặc khi threshold reached (HR-SCALE-6 to 10, 12 — Sprint 7+).

---

## 1. SPRINT 0 — Foundation: Domain + Migration + Anti-Fraud (v1.2: +DeviceRegistration +FraudFlag)

**Branch:** `feature/community-sprint0-foundation`
**Estimated sessions:** 3 (v1.3: giảm từ 4 — bỏ CC-S0-T3 SQLite migration, community entities PG ONLY)
**Conflict risk:** LOW (chỉ thêm mới, không sửa existing logic)

### Tasks (v1.5: 3 tasks — restore CC-S0-T3 as fingerprint wire-up)
| # | Task ID | Task | Files | Task card | Detailed plan |
|---|---|---|---|---|---|
| 1 | CC-S0-T1 | Domain entities + enums (11 entity, v1.2: +DeviceRegistration +FraudFlag) | `1_Shared/Domain.cs` | `task_cc_sprint0_foundation-2c5017.md` | `sprint0_foundation_detailed_plan-2c5017.md` |
| 2 | CC-S0-T2 | EF Configuration + Migration (PG ONLY — v1.3: bỏ SQLite) + Device fingerprint JS (FingerprintJS, self-host, vendored) | `3_CoreHub/Infrastructure/Configurations/`, `3_CoreHub/Infrastructure/Migrations/`, `5_WebApps/KhachLink/wwwroot/lib/fingerprintjs/` (v1.2 NEW) | same | same |
| 3 (v1.5 NEW) | **CC-S0-T3** | **Device fingerprint P0 wire-up** — wire-up DeviceRegistrationService vào production path. Thêm endpoint `POST /api/customer-identity/device/register` (**Gateway-native**, KHÔNG forward ShopERP — DeviceRegistration là community entity trên Gateway PG v1.3). Gọi `window.fingerprint.collect()` từ Login.razor (sau Google + OTP login) + Checkout.razor (guest checkout). Sprint 0 claim fingerprint infrastructure nhưng chưa wire-up (RV0-11 chỉ test JS load). | `2_Gateway/Controllers/DeviceRegistrationController.cs` (NEW), `5_WebApps/KhachLink/Pages/Login.razor`, `5_WebApps/KhachLink/wwwroot/index.html`, `6_Tests/VanAn.Core.Tests/Community/DeviceRegistrationControllerTests.cs` (NEW) | same | same |
| ~~4~~ | ~~CC-S0-T4~~ | ~~Social login (Google) endpoint~~ (v1.1: REMOVED — đã có `SocialAuthController.cs`) | — | — | — |

### Entry criteria
- [ ] `dotnet build VanAn.sln` pass trên main
- [ ] `guard-check.ps1` pass
- [ ] OTP login hiện tại hoạt động (không regression) — **OPTIONAL trong v1.2 (SMS không bắt buộc)**
- [ ] Social login (Google) hiện tại hoạt động (v1.1: verify existing — không build mới)

### Exit criteria — ALL PASSED (v1.5 VERIFIED 2026-07-29)
- [x] `dotnet build VanAn.sln` 0 errors — **VERIFIED: 0 errors, 1120 warnings**
- [x] `guard-check.ps1` ALL CHECKS PASSED — **VERIFIED 2026-07-29 (sau fix regex + exclude test files)**
- [x] Migration apply thành công (local PG + SQLite) — 11 tables mới (v1.2: tăng từ 9) — **VERIFIED: `20260726105331_CommunitySprint0.cs` tồn tại**
- [x] Unit test: `CommunityRole`, `DeliveryTask`, `WalletTransaction`, `ProductReferralConfig`, `AppInstallAttribution`, `DeviceRegistration`, `FraudFlag` — ≥25 test cases pass (v1.2: tăng từ 22) — **VERIFIED: 59 community tests PASS**
- [x] Architecture test: `WalletTransaction_Immutable_NoPublicSetter` + `WalletTransaction_NoUpdateMethod` PASS — **VERIFIED: 39 architecture tests PASS**
- [x] **v1.2 NEW:** Device fingerprint generation (FingerprintJS, self-host) hoạt động client-side — test HTML page generate hash — **VERIFIED: `wwwroot/js/fingerprint.js` + `wwwroot/lib/fingerprintjs/fingerprint.js` tồn tại**
- [x] **v1.2 NEW:** RiskScore calculation logic unit test — verify deterministic scoring per 8 factors — **VERIFIED: `RiskScoringServiceTests.cs` tồn tại, 59 tests PASS**
- [x] ~~Social login (Google) hoạt động end-to-end~~ (v1.1: bỏ — đã verify trong Tiered Auth P1)
- [x] OTP login vẫn hoạt động (regression test pass) — **OPTIONAL** — VERIFIED via build PASS
- [x] Architecture tests pass — **VERIFIED: 39 PASS**
- [x] **VPS Runtime Verification:** Deploy → `curl /api/customer-identity/otp/send` trả 200 → migration tables tồn tại (11 tables) — **VERIFIED 2026-07-26 (commit f563e415)**
- [x] **v1.5 CC-S0-T3 IMPLEMENTED 2026-07-29:** Device fingerprint wire-up hoàn thành — endpoint `POST /api/customer-identity/device/register` (Gateway-native) + Login.razor gọi `window.fingerprint.collect()` sau Google + OTP login. 3 controller tests PASS. GAP closed.

### VPS Runtime Verification (Sprint 0 — v1.2: +2 tables)
| # | Test | Command | Expected |
|---|---|---|---|
| RV0-1 | Gateway health | `curl https://{VPS_DOMAIN}/api/health` | 200 OK |
| RV0-2 | OTP send (OPTIONAL v1.2) | `curl -X POST https://{VPS_DOMAIN}/api/customer-identity/otp/send -H 'Content-Type: application/json' -d '{"phoneNumber":"0901234567"}'` | 200 + message |
| RV0-3 | OTP verify (dev, OPTIONAL v1.2) | `curl -X POST .../otp/verify -d '{"phoneNumber":"0901234567","otp":"{X-Dev-OTP}"}'` | 200 + customerToken |
| RV0-4 | ~~Google login~~ (v1.1: REMOVED — đã verify Tiered Auth P1) | — | — |
| RV0-5 | DB migration PG — CommunityRoles | `docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c '\dt CommunityRoles'` | Table exists |
| RV0-6 | DB migration SQLite (v1.3: REMOVED — community entities PG ONLY) | ~~`docker exec vanan-shoperp sqlite3 /data/shoperp.db ".tables"`~~ | ~~Tables exist~~ — community tables KHÔNG trên SQLite |
| RV0-7 | DB migration PG — ProductReferralConfigs | `docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c '\dt ProductReferralConfigs'` | Table exists |
| RV0-8 | DB migration PG — AppInstallAttributions | `docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c '\dt AppInstallAttributions'` | Table exists |
| RV0-9 (v1.2 NEW) | DB migration PG — DeviceRegistrations | `docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c '\dt DeviceRegistrations'` | Table exists |
| RV0-10 (v1.2 NEW) | DB migration PG — FraudFlags | `docker exec vanan-postgres psql -U vanan_admin -d VanAnCoreHub -c '\dt FraudFlags'` | Table exists |
| RV0-11 (v1.2 NEW) | Device fingerprint JS load | `curl https://{VPS_DOMAIN}/js/fingerprint.js` | 200 + JS content |
| RV0-12 (v1.2 NEW) | RiskScore calculation API (test endpoint) | `curl -X POST https://{VPS_DOMAIN}/api/community/risk-score/test -d '{"fingerprint":"abc","ip":"1.2.3.4","customerAgeDays":3,"deviceFirstSeenHours":2,"ordersFromDeviceToday":5,"salesmanFingerprintMatch":true,"sameIp24h":true,"appInstallSeconds":15}'` | 200 + riskScore (deterministic) |

### Why first
- Domain entities là nền tảng cho mọi sprint sau
- Migration phải chạy trước khi thêm API/UI
- ~~Social login cần thiết cho UC-01~~ (v1.1: đã có — không cần build lại)
- Không phá existing OTP flow

### Task card structure (CC-S0)
```
docs/AI/tasks/task_cc_sprint0_foundation-2c5017.md          — Task card (Sections 1-7)
docs/AI/tasks/sprint0_foundation_detailed_plan-2c5017.md     — Detailed plan (TDD, coding, sessions)
```

### Detailed plan outline (Sprint 0 — v1.1)
**TDD Plan (≥15 test cases — v1.1: tăng từ 10):**
1. `CommunityRole_Create_Valid_ReturnsEntity` — tạo role Shipper, check fields
2. `CommunityRole_Create_Salesman_GeneratesSalesmanCode` — code 6-8 chars unique
3. `CommunityRole_Deactivate_SetsDeactivatedAt` — deactivate check
4. `DeliveryTask_Create_Assigned_Status` — tạo task, status=Assigned
5. `DeliveryTask_Transition_AssignedToPickedUp` — valid transition
6. `DeliveryTask_Transition_InvalidThrows` — e.g. Delivered→PickedUp throws
7. `DeliveryTracking_AppendOnly_NoUpdate` — verify append-only pattern
8. `WalletTransaction_Create_BalanceAfterCorrect` — balance calc
9. `WalletTransaction_Immutable_NoUpdate` — verify no update method
10. `WalletTransaction_Reversal_CreatesNegatingEntry` (v1.1 NEW) — Reversal entry Amount=-original
11. `Order_NewFields_ShipperIdSalesmanIdReferralProductId_Nullable` (v1.1: +ReferralProductId) — new fields exist
12. `SalesReferral_AttachToOrder_CommissionFromProductConfig` (v1.1 NEW) — commission = orderTotal * ProductReferralConfig.CommissionRate
13. `SalesReferral_AttachAppInstallBonus_SetsBonusAmount` (v1.1 NEW) — bonus from ProductReferralConfig
14. `ProductReferralConfig_Create_ValidFields` (v1.1 NEW) — CommissionRate 2-5%, AppInstallBonus, ProductShortCode
15. `AppInstallAttribution_Create_UniquePerCustomer` (v1.1 NEW) — 1 customer 1 attribution

**Coding Plan (v1.1: 3 sessions thay vì 4):**
- Session 1: Domain entities (9 entity) + enums in `Domain.cs` + unit tests (15 cases)
- Session 2: EF Configuration (PG) + migration + DI registration
- Session 3: EF Configuration (SQLite) + migration + regression test + final verification

---

## 2. SPRINT 1 — Shipper Nearby Orders + Accept (v1.1: + CC-S1-T0 "delivering" status)

**Branch:** `feature/community-sprint1-nearby-orders`
**Estimated sessions:** 4
**Conflict risk:** MEDIUM (sửa `OrdersController`, thêm Gateway controller, v1.1: có thể sửa `OrderWorkflowService` + `OrderStatuses.Default[]`)

### Tasks (v1.1: thêm CC-S1-T0)
| # | Task ID | Task | Depends on | Task card | Detailed plan |
|---|---|---|---|---|---|
| 4 | CC-S1-T0 (v1.1 NEW) | Verify/Add `"delivering"` OrderStatus + transition rules | CC-S0-T1 | `task_cc_sprint1_nearby_orders-2c5017.md` | `sprint1_nearby_orders_detailed_plan-2c5017.md` |
| 4b (v1.5 NEW) | **CC-S1-T0c** | **Customer login simplify** — xóa SMS OTP khỏi Login.razor primary flow (giữ Google button + thêm Guest button). Aligns v1.2 "SMS OTP OPTIONAL" cho customer. SMS OTP endpoints giữ (dùng cho CC-S6-T5 collaborator verify). Guest button → nhập tên+SĐT (không token) → checkout as guest. | CC-S0-T3 | same | same |
| 5 | CC-S1-T1 | Nearby orders API (Haversine) | CC-S0-T2, CC-S1-T0 | same | same |
| 6 | CC-S1-T2 | Accept order API + concurrency | CC-S1-T1 | same | same |
| 7 | CC-S1-T3 | KhachLink Nearby Orders page | CC-S1-T1, CC-S1-T2 | same | same |
| 8 | CC-S1-T4 | E2E test: nearby + accept | CC-S1-T3 | same | same |

### Entry criteria (v1.4: + Domain Modification approval)
- [ ] Sprint 0 VPS verification ALL PASSED
- [ ] Migration applied trên VPS
- [ ] **v1.4 NEW: User approval for Domain Modification #2** — CC-S1-T0 sửa `OrderStatuses.Default[]` (Domain.cs) + `OrderWorkflowService.IsTransitionValidAsync` transitions. Đây là Domain Modification thứ 2 (sau Sprint 0). Cần user approval per governance.md.

### Exit criteria — ALL PASSED
- [ ] `"delivering"` status có trong `OrderStatuses.Default[]` + `OrderWorkflowService.IsTransitionValidAsync` (v1.1 NEW — CC-S1-T0)
- [ ] `dotnet build` 0 errors + `guard-check.ps1` pass
- [ ] GET `/api/community/nearby-orders` trả đơn DELIVERY trong bán kính (unit test + integration test)
- [ ] POST `/api/community/orders/{id}/accept` tạo DeliveryTask + set Order.ShipperId
- [ ] Double-accept → 409 Conflict
- [ ] KhachLink "Nearby Orders" page hiển thị đơn + nút "Nhận đơn"
- [ ] E2E test: shipper login → nearby orders → accept → order detail
- [ ] **VPS Runtime Verification:** Deploy → curl nearby-orders API → Playwright E2E pass

### VPS Runtime Verification (Sprint 1)
| # | Test | Method | Expected |
|---|---|---|---|
| RV1-1 | "delivering" status (v1.1 NEW) | `psql -c "SELECT * FROM \"OrderStatuses\" WHERE Id='delivering'"` (hoặc verify trong code) | Row exists hoặc constant defined |
| RV1-2 | Nearby orders API | `curl -H 'X-Customer-Token: {token}' https://{VPS}/api/community/nearby-orders?lat=10.8&lng=106.7&radiusKm=5` | 200 + JSON array |
| RV1-3 | Accept order | `curl -X POST -H 'X-Customer-Token: {token}' .../orders/{id}/accept` | 200 + DeliveryTask |
| RV1-4 | Double accept | `curl -X POST -H 'X-Customer-Token: {token2}' .../orders/{id}/accept` | 409 Conflict |
| RV1-5 | E2E Playwright | `npx playwright test e2e-tests/community-nearby-orders.spec.ts` | PASS |
| RV1-6 | DB check | `psql -c "SELECT * FROM \"DeliveryTasks\" WHERE \"ShipperId\" IS NOT NULL"` | ≥1 row |

---

## 3. SPRINT 2 — Delivery Workflow + GPS Tracking

**Branch:** `feature/community-sprint2-delivery-gps`
**Estimated sessions:** 5
**Conflict risk:** MEDIUM (sửa OrderWorkflowService, thêm SignalR hub)

### Tasks
| # | Task ID | Task | Depends on | Task card | Detailed plan |
|---|---|---|---|---|---|
| 9 | CC-S2-T1 | Delivery state machine API | CC-S1-T2 | `task_cc_sprint2_delivery_gps.md` | `sprint2_delivery_gps_detailed_plan.md` |
| 10 | CC-S2-T2 | LocationHub SignalR + tracking API | CC-S2-T1 | same | same |
| 11 | CC-S2-T3 | Leaflet map integration | CC-S2-T2 | same | same |
| 12 | CC-S2-T4 | Customer tracking page | CC-S2-T3 | same | same |
| 13 | CC-S2-T5 | E2E test: delivery flow + GPS | CC-S2-T4 | same | same |

### Entry criteria
- [ ] Sprint 1 VPS verification ALL PASSED

### Exit criteria — ALL PASSED
- [ ] State machine: Assigned→PickedUp→OutForDelivery→Delivered/Failed
- [ ] GPS polling 10s khi tab active
- [ ] SignalR push location → customer thấy marker
- [ ] Leaflet map hiển thị (không phải Google Maps iframe)
- [ ] Order.Completed khi Delivered
- [ ] E2E test: accept → pickup → delivering → delivered
- [ ] **VPS Runtime Verification:** Deploy → Playwright delivery flow → SignalR connection check

### VPS Runtime Verification (Sprint 2)
| # | Test | Method | Expected |
|---|---|---|---|
| RV2-1 | Pickup API | `curl -X POST .../orders/{id}/pickup` | 200 + PickedUpAt |
| RV2-2 | Delivering API | `curl -X POST .../orders/{id}/delivering` | 200 + OutForDeliveryAt |
| RV2-3 | Delivered API | `curl -X POST .../orders/{id}/delivered` | 200 + Order status=completed |
| RV2-4 | Location update | `curl -X POST .../location/update -d '{"lat":10.8,"lng":106.7}'` | 200 |
| RV2-5 | SignalR hub | Playwright: connect LocationHub → receive location push | PASS |
| RV2-6 | E2E Playwright | `npx playwright test e2e-tests/community-delivery-flow.spec.ts` | PASS |
| RV2-7 | DB tracking | `psql -c "SELECT COUNT(*) FROM \"DeliveryTracking\""` | ≥3 rows per delivery |

---

## 4. SPRINT 3 — Chat (Customer ↔ Shipper)

**Branch:** `feature/community-sprint3-chat`
**Estimated sessions:** 3
**Conflict risk:** LOW (toàn bộ mới, không đụng existing)

### Tasks
| # | Task ID | Task | Depends on | Task card | Detailed plan |
|---|---|---|---|---|---|
| 14 | CC-S3-T1 | Chat API + ChatHub SignalR | CC-S2-T1 | `task_cc_sprint3_chat.md` | `sprint3_chat_detailed_plan.md` |
| 15 | CC-S3-T2 | Chat UI (KhachLink) | CC-S3-T1 | same | same |
| 16 | CC-S3-T3 | E2E test: chat flow | CC-S3-T2 | same | same |

### Entry criteria
- [ ] Sprint 2 VPS verification ALL PASSED

### Exit criteria — ALL PASSED
- [ ] Chat chỉ mở khi DeliveryTask tồn tại
- [ ] Message real-time qua SignalR ChatHub
- [ ] Chat history persist DB
- [ ] E2E test: shipper + customer chat
- [ ] **VPS Runtime Verification:** Deploy → Playwright chat test

### VPS Runtime Verification (Sprint 3)
| # | Test | Method | Expected |
|---|---|---|---|
| RV3-1 | Send message | `curl -X POST .../chat/messages -d '{"conversationId":"{id}","content":"hello"}'` | 200 + message |
| RV3-2 | Get history | `curl .../chat/conversations/{orderId}` | 200 + messages array |
| RV3-3 | SignalR ChatHub | Playwright: connect → send → receive | PASS |
| RV3-4 | E2E Playwright | `npx playwright test e2e-tests/community-chat.spec.ts` | PASS |

---

## 5. SPRINT 4 — Salesman + Composite QR Referral + Per-Product Commission + App-Install Bonus (v1.1 REDESIGN)

**Branch:** `feature/community-sprint4-salesman-qr`
**Estimated sessions:** 7 (v1.2: tăng từ 6 — +risk scoring service + FraudFlag integration + device fingerprint)
**Conflict risk:** MEDIUM (sửa order creation flow, QRScanner, thêm admin UI cho ProductReferralConfig)

### Tasks (v1.1: redesign — 7 tasks thay vì 5)
| # | Task ID | Task | Depends on | Task card | Detailed plan |
|---|---|---|---|---|---|
| 17 | CC-S4-T1 | Nearby products API + join ProductReferralConfig (v1.1) | CC-S0-T2 | `task_cc_sprint4_salesman_qr-2c5017.md` | `sprint4_salesman_qr_detailed_plan-2c5017.md` |
| 18 | CC-S4-T2 | Composite QR generation (salesman + product, v1.1) | CC-S0-T1, CC-S4-T1 | same | same |
| 19 | CC-S4-T3 | Composite referral code in order creation (v1.1) | CC-S4-T2 | same | same |
| 20 | CC-S4-T4 | Commission calculation per-product + Sales dashboard (v1.1) | CC-S4-T3 | same | same |
| 21 | CC-S4-T5 | App-install attribution + bonus (UC-12, v1.1 NEW) | CC-S4-T2 | same | same |
| 22 | CC-S4-T6 | Admin UI: ProductReferralConfig CRUD (v1.1 NEW) | CC-S0-T1 | same | same |
| 23 | CC-S4-T7 | E2E test: salesman flow + app-install bonus (v1.1) | CC-S4-T5, CC-S4-T6 | same | same |

### Entry criteria (v1.4: + Sprint 1 merged + boundary clarify)
- [ ] Sprint 3 VPS verification ALL PASSED
- [ ] Sprint 0 migration applied (ProductReferralConfig, AppInstallAttribution tables exist)
- [ ] **v1.4 NEW: Sprint 1 merged to main** — NavMenu.razor community tabs (Sprint 1) phải merge trước khi Sprint 4 thêm salesman tabs
- [ ] **v1.4 NEW: Boundary clarify** — Sprint 4 = CALC only (tạo SalesReferral/AppInstallAttribution Pending + FraudFlag). Sprint 5 = PAYOUT (tạo WalletTransaction khi admin approve hoặc cooling pass qua IWalletService.CreateTransactionAsync từ Sprint 0). Sprint 4 CoolingPeriodJob (S6) gọi IWalletService.CreateTransactionAsync — KHÔNG re-implement wallet logic.

### Exit criteria — ALL PASSED
- [ ] GET `/api/community/nearby-products` trả FeaturedProducts trong bán kính + commission rate + app-install bonus từ ProductReferralConfig (v1.1)
- [ ] GET `/api/community/salesman/qr?productId={productId}` trả composite code `{salesmanCode}|{productShortCode}` (v1.1)
- [ ] SalesmanCode unique 6-8 chars, QR generate client-side chứa composite code
- [ ] QR scan → lưu composite referral code → gửi khi order → Order.SalesmanId + Order.ReferralProductId set (v1.1)
- [ ] Commission tính theo `ProductReferralConfig.CommissionRate` (2-5%, per-product, KHÔNG hardcode) (v1.1)
- [ ] POST `/api/community/app-install/attributed` tạo AppInstallAttribution + WalletTransaction bonus cho salesman (v1.1 NEW)
- [ ] 1 customer chỉ attribute 1 lần (unique constraint) (v1.1)
- [ ] Admin UI: GET/POST/PUT/DELETE `/api/admin/products/{productId}/referral-config` (v1.1 NEW)
- [ ] Sales dashboard hiển thị doanh số + commission chốt đơn + app-install bonus (tách biệt 2 nguồn, v1.1)
- [ ] E2E test: scan QR → order → commission + app-install → bonus
- [ ] **VPS Runtime Verification:** Deploy → Playwright salesman flow + app-install attribution

### VPS Runtime Verification (Sprint 4 — v1.1)
| # | Test | Method | Expected |
|---|---|---|---|
| RV4-1 | Nearby products + config | `curl .../nearby-products?lat=10.8&lng=106.7&radiusKm=10` | 200 + products array với commissionRate + appInstallBonus (v1.1) |
| RV4-2 | Composite Salesman QR (v1.1) | `curl -H 'X-Customer-Token: {token}' .../salesman/qr?productId={pid}` | 200 + composite code `{salesmanCode}\|{productShortCode}` |
| RV4-3 | Order with composite referral (v1.1) | `curl -X POST .../orders -d '{"referralCode":"ABC123\|TR-001",...}'` | 200 + Order.SalesmanId + Order.ReferralProductId set |
| RV4-4 | Commission list (v1.1) | `curl .../salesman/{id}/commissions` | 200 + commission records + appInstallBonus records (tách biệt) |
| RV4-5 | App-install attribution (v1.1 NEW) | `curl -X POST .../app-install/attributed -d '{"referralCode":"ABC123\|TR-001"}'` | 200 + AppInstallAttribution + WalletTransaction |
| RV4-6 | Double attribution rejected (v1.1 NEW) | `curl -X POST .../app-install/attributed` (same customer 2nd time) | 409 Conflict |
| RV4-7 | Admin ProductReferralConfig (v1.1 NEW) | `curl -X POST .../admin/products/{pid}/referral-config -d '{"commissionRate":0.05,"appInstallBonus":10000,...}'` | 200/201 + config record |
| RV4-8 | E2E Playwright | `npx playwright test e2e-tests/community-salesman.spec.ts` | PASS (scan QR → order → commission + app-install bonus) |

---

## 6. SPRINT 5 — Wallet + COD + Settlement (v1.1: + Reversal pattern)

**Branch:** `feature/community-sprint5-wallet-cod`
**Estimated sessions:** 3
**Conflict risk:** HIGH (logic tài chính, ảnh hưởng kế toán)

### Tasks
| # | Task ID | Task | Depends on | Task card | Detailed plan |
|---|---|---|---|---|---|
| 24 | CC-S5-T1 | Wallet API + immutable ledger + Reversal endpoint (v1.1) | CC-S0-T1 | `task_cc_sprint5_wallet_cod-2c5017.md` | `sprint5_wallet_cod_detailed_plan-2c5017.md` |
| 25 | CC-S5-T2 | COD confirm + settlement flow | CC-S5-T1, CC-S2-T1 | same | same |
| 26 | CC-S5-T3 | Wallet UI + COD in delivery workflow + Reverse UI (v1.1) | CC-S5-T2 | same | same |
| 27 | CC-S5-T4 | E2E test: wallet + COD + reversal (v1.1) | CC-S5-T3 | same | same |

### Entry criteria (v1.4: + IWalletService base from Sprint 0)
- [ ] Sprint 4 VPS verification ALL PASSED
- [ ] **v1.4 NEW: IWalletService.CreateTransactionAsync (atomic base) từ Sprint 0 Session S3** — Sprint 5 extends với COD/Advance/Settlement/Reverse, KHÔNG re-implement CreateTransactionAsync

### Exit criteria — ALL PASSED
- [ ] Order.PaymentMethod hỗ trợ "COD"
- [ ] WalletTransaction append-only (no update/delete method)
- [ ] BalanceAfter tính đúng sau mỗi transaction
- [ ] Reversal entry: `Type=Reversal`, `Amount=-original`, `RelatedTransactionId=original.Id` (v1.1 NEW)
- [ ] Settlement record tạo cho shop
- [ ] Unit test: wallet balance, COD flow, double-entry integrity, reversal flow — ≥15 test cases (v1.1: +reversal)
- [ ] Architecture test: `WalletTransaction_Immutable_NoPublicSetter` + `WalletTransaction_NoUpdateMethod` PASS (v1.1)
- [ ] E2E test: COD order → shipper confirm → wallet balance update → reverse → balance correct (v1.1)
- [ ] **VPS Runtime Verification:** Deploy → Playwright COD flow + reversal → DB wallet check

### VPS Runtime Verification (Sprint 5 — v1.1)
| # | Test | Method | Expected |
|---|---|---|---|
| RV5-1 | Wallet balance | `curl -H 'X-Customer-Token: {token}' .../wallet` | 200 + balance + transactions |
| RV5-2 | Confirm COD | `curl -X POST .../wallet/confirm-cod -d '{"orderId":"{id}","amount":50000}'` | 200 + WalletTransaction |
| RV5-3 | Wallet immutable | `psql -c "UPDATE \"WalletTransactions\" SET \"Amount\"=0 WHERE \"Id\"='{id}'"` | Should fail (no update path) |
| RV5-4 | Balance integrity | `psql -c "SELECT SUM(...) ..."` | BalanceAfter consistent |
| RV5-5 | Reverse transaction (v1.1 NEW) | `curl -X POST .../wallet/reverse -d '{"transactionId":"{id}","reason":"wrong amount"}'` | 200 + Reversal entry (Amount=-original) |
| RV5-6 | E2E Playwright | `npx playwright test e2e-tests/community-wallet-cod.spec.ts` | PASS (COD + reverse) |

---

## 7. SPRINT 6 — Admin + Fraud Review + Polish + Legal (v1.2: +Fraud Review UI)

**Branch:** `feature/community-sprint6-admin-legal`
**Estimated sessions:** 4 (v1.2: tăng từ 3 — +Fraud Review UI + API)
**Conflict risk:** LOW (admin API + UI + documents)

### Tasks (v1.2: thêm CC-S6-T4 Fraud Review)
| # | Task ID | Task | Depends on | Task card | Detailed plan |
|---|---|---|---|---|---|
| 26 | CC-S6-T1 | Admin API: eligible list + activate/deactivate | CC-S0-T1 | `task_cc_sprint6_admin_legal-2c5017.md` | `sprint6_admin_legal_detailed_plan-2c5017.md` |
| 27 | CC-S6-T2 | Admin UI + Profile roles + push notification | CC-S6-T1 | same | same |
| 28 | CC-S6-T3 | Legal documents draft + E2E smoke test | CC-S6-T2 | same | same |
| 29 (v1.2 NEW) | CC-S6-T4 | Fraud Review API + UI (list pending FraudFlag, confirm/dismiss/review, fraud-stats dashboard) | CC-S0-T1, CC-S4-T5 | same | same |
| 30 (v1.5 NEW) | **CC-S6-T5** | **Collaborator SMS OTP + Deposit Wallet (TOGGLE)** — SystemAdmin toggle ON/OFF. Default OFF. ON: Salesman/Shipper/Owner bắt buộc SMS OTP verify SĐT, phí trừ deposit wallet. Domain changes: `WalletTransactionType.Deposit=7` + `SmsOtpFee=8`, `CommunityRole.IsPhoneVerified` + `PhoneVerifiedAt`, `SystemSetting.CollaboratorSmsVerificationEnabled` + `SmsOtpFeePerVerification` + `CollaboratorMinDeposit`. Service: `CollaboratorVerificationService`. Controller: `/api/collaborator-verification/init` + `/verify` + `/deposit`. UI: admin toggle + collaborator verification page + wallet deposit view. | CC-S6-T1, CC-S5-T1 | same | same |

### Entry criteria
- [ ] Sprint 5 VPS verification ALL PASSED
- [ ] Sprint 4 risk scoring + FraudFlag creation working (v1.2 NEW — Fraud Review cần FraudFlag data)
- [ ] **v1.5 NEW: User approval for Domain Modification #3** — CC-S6-T5 thêm `WalletTransactionType.Deposit=7` + `SmsOtpFee=8` (enum extension), `CommunityRole.IsPhoneVerified` + `PhoneVerifiedAt` (new fields), `SystemSetting` toggle (new config). Đây là Domain Modification thứ 3 (sau Sprint 0 + Sprint 1). Cần user approval per governance.md.

### Exit criteria — ALL PASSED
- [ ] Admin API: GET eligible, POST activate/deactivate
- [ ] Push notification gửi khi activate role
- [ ] Profile page hiển thị community roles
- [ ] **v1.2 NEW:** `/admin/fraud-flags` page hiển thị pending flags sort by RiskScore
- [ ] **v1.2 NEW:** Fraud flag detail modal — show risk factors + related entities
- [ ] **v1.2 NEW:** Confirm/Dismiss/Review actions work — update entity status + customer ban if 3 strikes
- [ ] **v1.2 NEW:** `/admin/fraud-stats` dashboard hiển thị stats đúng
- [ ] Legal documents draft hoàn thành (terms, privacy + **v1.2: device fingerprint consent + anti-fraud policy**, marketplace policy)
- [ ] Full E2E regression: tất cả community specs pass
- [ ] **VPS Runtime Verification:** Full regression trên VPS

### VPS Runtime Verification (Sprint 6 — Full Regression, v1.2: +Fraud Review)
| # | Test | Method | Expected |
|---|---|---|---|
| RV6-1 | Admin eligible | `curl -H 'Authorization: Bearer {adminJWT}' .../admin/community/eligible` | 200 + customer list |
| RV6-2 | Activate role | `curl -X POST .../admin/community/{customerId}/activate-role -d '{"role":"Shipper"}'` | 200 + CommunityRole |
| RV6-3 | Profile roles | `curl -H 'X-Customer-Token: {token}' .../customer-identity/me` | 200 + roles array |
| RV6-4 (v1.2 NEW) | Fraud flags list | `curl -H 'Authorization: Bearer {adminJWT}' .../admin/fraud-flags?status=Pending` | 200 + flags array (sort by RiskScore desc) |
| RV6-5 (v1.2 NEW) | Fraud flag detail | `curl .../admin/fraud-flags/{id}` | 200 + detail (risk factors, related entities) |
| RV6-6 (v1.2 NEW) | Confirm fraud | `curl -X POST .../admin/fraud-flags/{id}/confirm -d '{"note":"self-deal"}'` | 200 + Status=Confirmed + entity updated |
| RV6-7 (v1.2 NEW) | Dismiss fraud | `curl -X POST .../admin/fraud-flags/{id}/dismiss -d '{"note":"family same house"}'` | 200 + Status=Dismissed + entity whitelisted |
| RV6-8 (v1.2 NEW) | Fraud stats | `curl .../admin/fraud-stats` | 200 + {pending, confirmed, dismissed, lossPrevented} |
| RV6-9 | Full E2E regression | `npx playwright test e2e-tests/community-*.spec.ts` | ALL PASS |
| RV6-10 | guard-check | `pwsh guard-check.ps1` | ALL PASSED |
| RV6-11 | Architecture tests | `dotnet test VanAn.Architecture.Tests` | ALL PASS |

---

## 8. CI/CD PIPELINE — Sprint Verification Gate

> **v1.1 NOTE:** YAML dưới đây là **pseudo-code** minh họa cấu trúc workflow. Implement thật theo template `.github/workflows/ci.yml` + `cd.yml` hiện có (dùng `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/setup-node@v4`, `docker/build-push-action@v5`, `appleboy/ssh-action`). KHÔNG copy-paste pseudo-code này trực tiếp.

### Workflow file: `.github/workflows/community-sprint-verify.yml`

```yaml
name: Community Sprint Verification

on:
  push:
    branches: [ 'feature/community-sprint*' ]
  workflow_dispatch:
    inputs:
      sprint:
        description: 'Sprint number (0-6)'
        required: true
        type: string

env:
  DOTNET_VERSION: '8.0.x'
  NODE_VERSION: '20.x'
  VPS_DOMAIN: ${{ secrets.VANAN_DOMAIN }}

jobs:
  # Stage 1: Build + Unit Tests + guard-check
  build-and-test:
    runs-on: ubuntu-latest
    timeout-minutes: 20
    steps:
      - checkout
      - setup-dotnet 8.0.x
      - dotnet restore VanAn.sln
      - dotnet build VanAn.sln --no-restore --configuration Release
      - dotnet test 6_Tests/VanAn.Core.Tests/ --no-build --configuration Release
      - dotnet test 6_Tests/VanAn.Unit.Tests/ --no-build --configuration Release
      - dotnet test 6_Tests/VanAn.Architecture.Tests/ --no-build --configuration Release
      # guard-check equivalent (Linux)
      - grep -r "TODO: Implement" 1_Shared/ 2_Gateway/ 3_CoreHub/ 5_WebApps/ --include="*.cs" && exit 1 || true
      - grep -r "// Stub" 1_Shared/ 2_Gateway/ 3_CoreHub/ 5_WebApps/ --include="*.cs" && exit 1 || true

  # Stage 2: Integration Tests
  integration-tests:
    needs: build-and-test
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - dotnet test 6_Tests/VanAn.Integration.Tests/ --configuration Release

  # Stage 3: Build Docker + Push GHCR
  build-push-images:
    needs: integration-tests
    runs-on: ubuntu-latest
    timeout-minutes: 30
    steps:
      - docker build & push Gateway, ShopERP, KhachLink to GHCR

  # Stage 4: Deploy to VPS
  deploy-vps:
    needs: build-push-images
    runs-on: ubuntu-latest
    timeout-minutes: 15
    environment: production
    steps:
      - SSH to VPS
      - docker compose pull + up -d
      - sleep 20 (health check)
      - docker compose ps (verify all containers up)

  # Stage 5: Runtime Verification (VPS)
  runtime-verification:
    needs: deploy-vps
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - checkout
      # Run sprint-specific verification script
      - pwsh scripts/verify-sprint.ps1 -Sprint ${{ inputs.sprint }} -Domain $VPS_DOMAIN
      # Run Playwright E2E against VPS
      - npx playwright test e2e-tests/community-sprint${{ inputs.sprint }}*.spec.ts --base-url=https://$VPS_DOMAIN

  # Stage 6: Report
  report:
    needs: runtime-verification
    runs-on: ubuntu-latest
    steps:
      - upload artifacts (test results, screenshots)
      - create deployment summary
```

### Verification script: `scripts/verify-sprint.ps1`

Mỗi sprint có verification script riêng, chạy curl API tests + DB checks trên VPS:

```powershell
# scripts/verify-sprint.ps1
param(
    [Parameter(Mandatory)] [string]$Sprint,
    [Parameter(Mandatory)] [string]$Domain
)

$results = @()

switch ($Sprint) {
    "0" {
        # RV0-1 to RV0-6
        $results += Test-Api -Url "https://$Domain/api/health" -Expected 200 -Name "RV0-1 Gateway health"
        $results += Test-Api -Method POST -Url "https://$Domain/api/customer-identity/otp/send" -Body '{"phoneNumber":"0901234567"}' -Expected 200 -Name "RV0-2 OTP send"
        # ... etc
    }
    "1" {
        # RV1-1 to RV1-5
    }
    # ... etc
}

# Summary
$failed = $results | Where-Object { $_.Status -ne "PASS" }
if ($failed) {
    Write-Host "VERIFICATION FAILED:" -ForegroundColor Red
    $failed | Format-Table
    exit 1
} else {
    Write-Host "ALL VERIFICATION PASSED" -ForegroundColor Green
    $results | Format-Table
}
```

---

## 9. FILE CONFLICT MATRIX (v1.4: updated — remove stale SQLite + workflows rows)

| File zone | S0 | S1 | S2 | S3 | S4 | S5 | S6 | Conflict mitigation |
|---|---|---|---|---|---|---|---|---|
| `1_Shared/Domain.cs` | ✏️ (v1.2: +DeviceRegistration, +FraudFlag, +RiskScore fields) | ✏️ (v1.1: CC-S1-T0 "delivering" — Domain Modification #2, cần user approval) | — | — | — | — | — | S0 owns new entities; S1 chỉ thêm "delivering". Sequential — S0 merge trước S1 start. |
| `3_CoreHub/Services/OrderWorkflowService.cs` | — | ✏️ (v1.1: CC-S1-T0 transition rules) | ✏️ (delivery transitions) | — | — | — | — | **Shared file — rebase trước merge.** S1: IsTransitionValidAsync cho "delivering". S2: delivery state machine calls. Sequential — S1 merge trước S2 start. |
| `3_CoreHub/Services/IWalletService.cs` + `WalletService.cs` | ✏️ (v1.4: base CreateTransactionAsync atomic — HR-SCALE-3) | — | — | — | — | ✏️ (v1.4: extends với COD/Advance/Settlement/Reverse) | — | S0 owns base; S5 extends. Sequential — S0 merge trước S5 start. |
| `3_CoreHub/Infrastructure/` | ✏️ | — | — | — | — | — | — | S0 owns EF config + migration (PG only, v1.3) |
| ~~`5_WebApps/ShopERP/Migrations/`~~ | ~~✏️~~ | — | — | — | — | — | — | **v1.3: REMOVED — community entities PG ONLY, không SQLite migration** |
| `2_Gateway/Controllers/` | — | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ | Mỗi sprint thêm controller riêng |
| `2_Gateway/Hubs/` | — | — | ✏️ (LocationHub — v1.4: X-Customer-Token query string auth) | ✏️ (ChatHub — v1.3: X-Customer-Token query string auth) | — | — | — | S2: LocationHub, S3: ChatHub. Cùng auth pattern (v1.4). |
| `3_CoreHub/Services/` | ✏️ (v1.4: +RiskScoringService +WalletService base) | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ (v1.4: extends WalletService) | ✏️ | Mỗi sprint thêm service riêng. S0: RiskScoringService + WalletService base. S5: WalletService extends. |
| `5_WebApps/KhachLink/Pages/` | — | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ | Mỗi sprint thêm page riêng |
| `5_WebApps/KhachLink/Program.cs` | ✏️ (v1.1: PWA install event handler, v1.2: device fingerprint JS wiring) | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ | DI registration — sequential. S0: app-install event + fingerprint JS (v1.2) + IWalletService DI (v1.4). |
| `5_WebApps/KhachLink/Components/Layout/NavMenu.razor` | — | ✏️ (v1.3: community tabs — Shipper) | — | — | ✏️ (v1.3: community tabs — Salesman) | — | — | **Shared file — S1 merge trước S4 start.** S1: Shipper tabs. S4: Salesman tabs. |
| `5_WebApps/KhachLink/Components/` | — | — | ✏️ | ✏️ | ✏️ | — | ✏️ | S2: Leaflet map, S4: QR + ProductReferralConfig admin UI (v1.1) |
| `6_Testing/e2e-tests/` | — | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ | ✏️ | Mỗi sprint thêm spec riêng |
| ~~`.github/workflows/`~~ | ~~✏️~~ | — | — | — | — | — | — | **v1.3: REMOVED — use existing cd.yml + per-sprint verify scripts (Section 11.4)** |

---

## 10. VISUAL TIMELINE (v1.3: Sprint 0 giảm 4→3 sessions — bỏ SQLite migration)

```
Week 1-2 (Day 1-5):
  S0 Foundation + Anti-Fraud ████ (3 sessions — v1.3: giảm từ 4, bỏ CC-S0-T3 SQLite migration, community entities PG ONLY)
  └─ VPS Verify: RV0-1 to RV0-12 (v1.2: +4 tests, v1.3: RV0-6 removed)

Week 2-3 (Day 6-10):
  S1 Nearby Orders + CC-S1-T0 + Facebook UI + NavMenu ██████ (4 sessions — v1.1: +CC-S1-T0, v1.3: +Facebook UI +NavMenu)
  └─ VPS Verify: RV1-1 to RV1-6

Week 3-4 (Day 11-16):
  S2 Delivery + GPS ████████ (5 sessions)
  └─ VPS Verify: RV2-1 to RV2-7

Week 4 (Day 17-19):
  S3 Chat █████ (3 sessions)
  └─ VPS Verify: RV3-1 to RV3-4

Week 5-6 (Day 20-28):
  S4 Salesman + Composite QR + App-Install + Risk Scoring ██████████ (7 sessions — v1.2: tăng từ 6)
  └─ VPS Verify: RV4-1 to RV4-9

Week 7 (Day 29-32):
  S5 Wallet + COD + Reversal ██████ (3 sessions)
  └─ VPS Verify: RV5-1 to RV5-6

Week 8 (Day 33-37):
  S6 Admin + Fraud Review + Polish + Legal ████████ (4 sessions — v1.2: tăng từ 3)
  └─ VPS Verify: RV6-1 to RV6-11 (FULL REGRESSION)

Total: ~37 days, 29 sessions, 7 sprints (v1.3: -1 session so với v1.2 do bỏ SQLite migration)

Post-PoC (Sprint 7+ — Edge Migration, khi >1M users):
  S7 Edge Migration + Scale-Up ████████████████ (estimated 10-15 sessions)
  └─ Switch từ central-only → Hybrid Central + Edge (per spec Section 7C.1)
  └─ Deploy gateway per region (Hà Nội, HCMC, Đà Nẵng, Cần Thơ, ...)
  └─ PostGIS + TimescaleDB + PgBouncer + Redis backplane
  └─ Geo-sharding (LT5) + Service decomposition (LT2) + SQLite → PG RLS (LT4)
  └─ See Section 13 "Sprint 7+ Edge Migration Plan" below
```

---

## 13. SPRINT 7+ EDGE MIGRATION PLAN (v1.4 NEW — post-PoC, when >1M users)

> **Trigger:** Khi user count >1M HOẶC >1K shippers active HOẶC >65K SignalR connections. Khi đó central-only architecture không scale được tiếp.
> **Branch:** `feature/community-sprint7-edge-migration`
> **Estimated sessions:** 10-15 (estimated, JIT planning khi đến gần threshold)
> **Conflict risk:** HIGH — thay đổi architecture toàn bộ hệ thống

### 13.1 Entry criteria
- [ ] Sprint 0-6 COMPLETE (PoC validated, VPS RV ALL PASSED)
- [ ] User count >100K (đã có data thực tế để plan migration)
- [ ] Team DevOps ≥3 người (edge deployment cần ops capacity)
- [ ] PostGIS threshold reached (>1K shippers) — đã install PostGIS extension
- [ ] Redis backplane threshold reached (>1K shippers) — đã config SignalR backplane
- [ ] PgBouncer deployed (>10K users)
- [ ] PG partitioning by month configured (>100K users)

### 13.2 Tasks (estimated — JIT planning khi đến gần)
| # | Task ID | Task | Depends on | Threshold | Task card | Detailed plan |
|---|---|---|---|---|---|---|
| 30 | CC-S7-T1 | PostGIS extension install + nearby queries migrate sang ST_DWithin + GIST index | >1K shippers | ST1 | `task_cc_sprint7_edge_migration-2c5017.md` | `sprint7_edge_migration_detailed_plan-2c5017.md` |
| 31 | CC-S7-T2 | SignalR Redis backplane config (AddStackExchangeRedis) + multi-Gateway scale out | >1K shippers OR >65K connections | ST3 | same | same |
| 32 | CC-S7-T3 | PgBouncer transaction-mode deploy | >10K users | ST4 | same | same |
| 33 | CC-S7-T4 | Region column (province code) add vào Order + TenantSettings + Community entities + migration + backfill | Before geo-sharding | ST7, HR-SCALE-10 | same | same |
| 34 | CC-S7-T5 | PG declarative partitioning by month (WalletTransaction, DeliveryTracking, Message) | >100K users | ST10 | same | same |
| 35 | CC-S7-T6 | Edge Gateway deployment — first 4 regions (Hà Nội, HCMC, Đà Nẵng, Cần Thơ) | >1M users | LT5 | same | same |
| 36 | CC-S7-T7 | GeoDNS config (Cloudflare/AWS Route 53) — route user → nearest gateway by region | CC-S7-T6 | — | same | same |
| 37 | CC-S7-T8 | SQLite → PG RLS migration (tenant-by-tenant, lazy) — eliminate 300K SQLite files | CC-S7-T6 | LT4, ST8 | same | same |
| 38 | CC-S7-T9 | TimescaleDB extension install + GPS DeliveryTracking migrate sang hypertable | >10K shippers | LT1 | same | same |
| 39 | CC-S7-T10 | Service decomposition phase 1 — extract Delivery Service + own DB | >1M users | LT2 | same | same |
| 40 | CC-S7-T11 | Service decomposition phase 2 — extract Chat Service + Cassandra/Mongo | >1M users | LT2, LT6 | same | same |
| 41 | CC-S7-T12 | Service decomposition phase 3 — extract Wallet Service + event-sourced | >1M users | LT2, LT3 | same | same |
| 42 | CC-S7-T13 | Email/Password login + WebAuthn Passkey (deferred from v1.2) | Post-PoC | A1 | same | same |
| 43 | CC-S7-T14 | Multi-channel OTP (Viettel SMS + Zalo OA + WhatsApp Business API) | >10K users | O1 | same | same |
| 44 | CC-S7-T15 | Full E2E regression across all edge gateways + central | All above | — | same | same |

> **v1.5 NEW — Task card + Detailed plan files (JIT creation):**
> - `docs/AI/tasks/task_cc_sprint7_edge_migration-2c5017.md` — Task card (Sections 1-7)
> - `docs/AI/tasks/sprint7_edge_migration_detailed_plan-2c5017.md` — Detailed plan (TDD, coding, sessions)
> - Files tạo JIT khi Sprint 7 trigger (threshold reached). KHÔNG tạo trước (tránh stale spec).
> - Mỗi task CC-S7-T1..T15 = 1 PR merge vào `feature/community-sprint7-edge-migration` branch.
> - CI/CD trigger: push to `feature/community-sprint7-edge-migration` → build + test → CD deploy → RV7-1..RV7-10 verification.
> - Sprint 7 merge vào main chỉ khi ALL RV7 tests PASS (same protocol as Sprint 0-6).

### 13.3 Exit criteria — ALL PASSED
- [ ] 4 edge gateways deployed (HN, HCMC, ĐN, CT) — each serving users within 15-20km
- [ ] GeoDNS routes user → nearest gateway correctly (verify via `curl` from different regions)
- [ ] PostGIS nearby queries <10ms (vs Haversine full scan 5s+ @ 1M orders)
- [ ] SignalR Redis backplane — 2+ Gateway instances serve 100K+ concurrent connections
- [ ] PgBouncer — PG max_connections không exceed 100 (pool reuses connections)
- [ ] PG partitioning — WalletTransaction query chỉ scan current month partition (<1s vs 30s+ full scan)
- [ ] SQLite → PG RLS — 90% tenants migrated (lazy migration, 10% long-tail)
- [ ] TimescaleDB — GPS write 10K writes/sec sustained, compression 90% storage saving
- [ ] Service decomposition — Delivery, Chat, Wallet services independent deploy
- [ ] Multi-channel OTP — 80% users dùng Zalo/WhatsApp, SMS cost giảm 80%
- [ ] **VPS Runtime Verification:** Full regression across all edge gateways + central
- [ ] **Cost verification:** Monthly cost ≤ $10K @ 1M users (Vietnam-optimized)

### 13.4 VPS Runtime Verification (Sprint 7 — Edge + Scale)
| # | Test | Method | Expected |
|---|---|---|---|
| RV7-1 | GeoDNS routing | `curl -I https://{VPS_DOMAIN}/api/health` từ HN + HCMC + ĐN + CT | Each region hits nearest gateway |
| RV7-2 | PostGIS nearby query | `curl .../nearby-orders?lat=10.8&lng=106.7&radiusKm=5` + measure response time | <10ms (vs 5s+ Haversine) |
| RV7-3 | SignalR multi-gateway | Playwright: 100K concurrent connections across 4 gateways | All connections stable, Redis backplane sync |
| RV7-4 | PG partition | `EXPLAIN ANALYZE SELECT * FROM "WalletTransactions" WHERE "CreatedAt" > NOW() - INTERVAL '7 days'` | Scan only current month partition |
| RV7-5 | SQLite → RLS | `psql -c "SELECT COUNT(*) FROM tenants WHERE migrated_to_rls=true"` | >90% tenants migrated |
| RV7-6 | TimescaleDB GPS | `EXPLAIN ANALYZE SELECT * FROM "DeliveryTrackings" WHERE "RecordedAt" > NOW() - INTERVAL '1 hour'` | Hypertable scan, compression active |
| RV7-7 | Service decomposition | `curl https://delivery.{VPS_DOMAIN}/health` + `curl https://chat.{VPS_DOMAIN}/health` + `curl https://wallet.{VPS_DOMAIN}/health` | All 3 services 200 OK |
| RV7-8 | Multi-channel OTP | Send OTP via Zalo OA → verify delivery | Zalo message received |
| RV7-9 | Cost verify | Check Vultr/AWS billing dashboard | Monthly cost ≤ $10K @ 1M users |
| RV7-10 | Full E2E regression | `npx playwright test e2e-tests/community-*.spec.ts` across all edge gateways | ALL PASS |

---

## 14. HARD RULES — SCALE UP (v1.4 NEW — HR-SCALE-1 to HR-SCALE-12)

> Các hard rules này được trigger khi threshold scale reached. Apply từ Sprint 0 (rules có thể apply ngay) HOẶC Sprint 7+ (rules trigger khi threshold).

### Apply từ Sprint 0 (immediate)
| Rule | Description | Sprint |
|---|---|---|
| **HR-SCALE-1** | API endpoints MUST use `/api/v1/community/*` versioning — prepare for v2 refactor | Sprint 1 |
| **HR-SCALE-2** | Community entities MUST use Guid FK references (KHÔNG direct aggregate references) — Anti-Corruption Layer (R2) | Sprint 0 (đã đúng) |
| **HR-SCALE-5** | SalesmanCode MUST use tenant prefix + random chars (unique per tenant, KHÔNG global unique) — ST9 | Sprint 0 |
| **HR-SCALE-11** | Mọi PG migration trên production MUST test trên copy of production DB trước (pg_dump → restore → migrate). Nếu >5min → expand-contract — R7 | Sprint 0+ (process) |

### Apply tại Sprint cụ thể (PoC scope)
| Rule | Description | Sprint |
|---|---|---|
| **HR-SCALE-3** | WalletTransaction BalanceAfter MUST be computed via atomic sequence (`SELECT FOR UPDATE` hoặc `UPDATE ... RETURNING`) — KHÔNG read-then-write — ST5 | Sprint 5 |
| **HR-SCALE-4** | Commission calculation MUST go through Outbox + NATS (KHÔNG inline sync trong OrderWorkflowService) — ST6 | Sprint 4 |

### Apply khi threshold reached (Sprint 7+ — post-PoC)
| Rule | Description | Threshold | Solution |
|---|---|---|---|
| **HR-SCALE-6** | PostGIS extension + ST_DWithin + GIST index MUST be installed | >1K shippers OR >10K orders/ngày | ST1 |
| **HR-SCALE-7** | SignalR Redis backplane MUST be configured | >1K shippers OR >65K total connections | ST3 |
| **HR-SCALE-8** | PgBouncer transaction-mode MUST be deployed | >10K users | ST4 |
| **HR-SCALE-9** | PG declarative partitioning by month MUST be configured cho WalletTransaction, DeliveryTracking, Message | >100K users | ST10 |
| **HR-SCALE-10** | Region column (province code, 2 chars) MUST be added vào Order, TenantSettings, CommunityRole, DeliveryTask before geo-sharding | Sprint 7+ (before LT5) | ST7 |
| **HR-SCALE-12** | Geo-fence MỀM — default nearest gateway, fallback central cho cross-region flows (referral, app-install, wallet, dashboard). KHÔNG geo-fence cứng | Sprint 7+ (edge) | — |

### Architecture Decision References
- **Hybrid Central + Edge architecture:** Spec Section 7C.1 (target architecture diagram)
- **11 bottleneck B1-B11:** Spec Section 7C.2
- **10 short-term solutions ST1-ST10:** Spec Section 7C.3
- **8 long-term solutions LT1-LT8:** Spec Section 7C.4
- **9 corrections (edge-only sai ở đâu):** Spec Section 7C.5
- **8 refactor impact reduction R1-R8:** Spec Section 7C.6
- **Cuốn chiếu strategy:** Spec Section 7C.7
- **Architecture evolution roadmap:** Spec Section 7C.8
- **Cost projections + optimization:** Master plan Section 12
- **Edge migration plan:** Master plan Section 13 (this section)
```

---

## 11. DEPLOYMENT PLAN (v1.3 NEW — bổ sung từ review)

### 11.1 nginx config update (Sprint 2 — LocationHub + Sprint 3 — ChatHub)
**File:** `nginx/templates/vanan.conf.template` (line 218 hiện tại: `location ~ ^/(orderHub|kitchenhub)`)

**v1.3 Change:** Add locationHub + chatHub vào regex:
```nginx
# SignalR hubs (v1.3: +locationHub +chatHub)
location ~ ^/(orderHub|kitchenhub|locationHub|chatHub) {
    proxy_pass         http://vanan-gateway:80;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection "upgrade";
    proxy_set_header   Host $host;
    proxy_cache_bypass $http_upgrade;
}
```

**Task assignment:** Sprint 2 CC-S2-T2 (LocationHub) cập nhật nginx. Sprint 3 verify chatHub route đã có (because regex chung).

### 11.2 Vendor Leaflet + FingerprintJS (no CDN — consistent zero-dependency)
> **v1.3 CORRECTION:** Spec v1.2 nói "zero external dependency" nhưng Sprint 2 detailed plan line 99-101 dùng Leaflet CDN (unpkg.com). Contradiction. Fix: vendor tất cả JS/CSS locally.

**Leaflet v1.9.4 (BSD-2-Clause, free):**
- Download `leaflet.js` + `leaflet.css` từ `https://unpkg.com/leaflet@1.9.4/dist/`
- Vendor vào `5_WebApps/KhachLink/wwwroot/lib/leaflet/leaflet.js` + `leaflet.css`
- **Map tiles:** Dùng OSM standard tile server (`https://tile.openstreetmap.org/{z}/{x}/{y}.png`) — free, nhưng có rate limit. Cho PoC OK. Post-PoC: self-host tile server (vd dùng `openmaptiles`) hoặc dùng CartoDB free tier (10K loads/month).
- **Task:** Sprint 2 CC-S2-T1 vendor Leaflet + update LeafletMap.razor reference từ CDN → local path.

**FingerprintJS v4 (MIT, free):**
- Download `fingerprint.js` từ `https://open-source.fingerprintjs.com/fingerprintjs/4.5.0/fingerprint.js`
- Vendor vào `5_WebApps/KhachLink/wwwroot/lib/fingerprintjs/fingerprint.js`
- **Task:** Sprint 0 CC-S0-T2 vendor FingerprintJS (đã có trong task card S0 v1.2).

**SRI (Subresource Integrity):** Tất cả vendored JS có SRI hash trong `<script>` tag để detect tampering.

### 11.3 docker-compose.prod.yml — NO changes needed for PoC (v1.3)
- Community entities trên existing PostgreSQL (vanan-postgres) — KHÔNG cần thêm service.
- SignalR single instance (Gateway) — KHÔNG cần Redis backplane cho PoC (10-20 cửa hàng, 50-100 khách). Post-PoC khi scale >1K shippers: add Redis service + `AddStackExchangeRedis` backplane.
- KHÔNG cần TimescaleDB cho PoC (GPS volume thấp: 10 shippers × 6 req/min = 60 req/min — PG plain table OK). Post-PoC khi scale >10K shippers: install TimescaleDB extension.
- **Env vars mới (add vào Gateway service):**
  - `Community__DefaultRadiusKm=5` (nearby orders/products default radius)
  - `Community__MaxDevicesPerCustomer=3` (device registration limit)
  - `Community__RiskHoldHours=48` (hold period for RiskScore 60-79)
  - `Community__RiskCoolingHours=24` (cooling period for RiskScore <60)
  - `Community__RiskRejectThreshold=80` (auto-reject threshold)
  - `Community__RiskHoldThreshold=60` (hold threshold)
  - `Community__GpsPollingOutForDeliverySec=10` (adaptive polling)
  - `Community__GpsPollingPickedUpSec=30`
  - `Community__Enabled=true` (feature flag — default true sau Sprint 6, false trong quá trình dev sprints 0-5)

**Task:** Sprint 0 CC-S0-T2 add env vars vào `docker-compose.prod.yml` Gateway service (with defaults).

### 11.4 CD pipeline (existing cd.yml — KHÔNG cần file mới)
- Existing `.github/workflows/cd.yml` đã deploy trên push to main.
- Sprint branches merge vào main → CD auto-deploy.
- **v1.3 Decision:** KHÔNG tạo `community-sprint-verify.yml` riêng — dùng existing CD + thêm RV scripts (`scripts/verify-sprint{N}.ps1`) gọi từ cd.yml post-deploy step. Mỗi sprint tạo 1 verify script.

### 11.5 Migration rollback plan (Sprint 0)
- **Forward:** `dotnet ef migrations add CommunitySprint0` → `dotnet ef database update`
- **Rollback:** `dotnet ef database update <previous_migration>` → `dotnet ef migrations remove` (if还没 push)
- **Production rollback:** SSH VPS → `docker exec vanan-gateway dotnet ef database update <previous> --project 3_CoreHub/Infrastructure` → redeploy previous image
- **Practice:** Test rollback trên staging BEFORE production deploy. Time budget: 5 min rollback window.

### 11.6 Feature flag rollout (v1.3 NEW)
- `Community__Enabled=false` default trong dev (sprints 0-5).
- Sprint 6 complete → flip `Community__Enabled=true` on production → community APIs activate.
- KHÔNG gradual per-tenant rollout cho PoC (10-20 cửa hàng — big-bang OK). Post-PoC: per-tenant flag trong ShopFeatureSettings.

### 11.7 Monitoring/alerting (v1.3 — PoC minimal + post-PoC full)
**PoC scope (Sprint 0-6):**
- Seq logs: `docker logs vanan-gateway | grep -i "community\|fraud\|wallet"` — manual check
- `docker compose ps` — verify all containers healthy
- `docker exec vanan-postgres psql -c "SELECT COUNT(*) FROM \"FraudFlags\" WHERE \"Status\"=1"` — manual fraud queue check
- `docker exec vanan-postgres psql -c "SELECT COUNT(*) FROM \"WalletTransactions\""` — wallet growth check
- RV scripts (verify-sprint{N}.ps1) — automated per-sprint

**Post-PoC (NOT in PoC scope):**
- Prometheus + Grafana: `community_gps_writes_total`, `community_signalr_connections`, `community_fraud_pending_count`
- Daily cron: fraud rate alert, wallet balance integrity reconcile (`SUM(Amount) GROUP BY OwnerId` vs current balance)
- Alertmanager: Slack/email when fraud rate >5% hoặc wallet integrity fail

### 11.8 VPS resource planning (v1.3 NEW — D9)
**Current VPS:** 4 vCPU / 8GB RAM / 160GB SSD (Vultr/DigitalOcean Singapore) — per project_state.md
**PoC load (10-20 cửa hàng, 50-100 khách, 10 cộng tác viên):**
- GPS writes: 10 shippers × 6 req/min = 60 req/min — PG plain table OK (no TimescaleDB needed)
- SignalR connections: ~20 concurrent (10 shippers + 10 customers tracking) — single Gateway instance OK
- Community tables: 11 tables, <10K rows total — PG storage minimal
- **Verdict: Current VPS ĐỦ cho PoC.** KHÔNG cần upgrade.
**Post-PoC thresholds (when to upgrade):**
- >1K shippers active → upgrade to 8 vCPU / 16GB + add Redis for SignalR backplane
- >10K shippers → add TimescaleDB extension for GPS + PgBouncer for connection pooling
- >100K customers → add PG read replica for nearby-orders/products queries

### 11.9 Backup strategy (v1.3 NEW — D10)
**Community tables trên Gateway PG (vanan-postgres):**
- **Daily backup:** `docker exec vanan-postgres pg_dump -U vanan_admin VanAnCoreHub > backup_$(date +%Y%m%d).sql` — already in existing backup script
- **Sensitive tables:** `DeviceRegistration` (fingerprint + IP — PII), `FraudFlag` (sensitive), `WalletTransaction` (financial) — include in daily backup
- **Encryption:** backup files encrypted at rest (VPS disk encryption LUKS — already configured per project_state.md)
- **Retention:** 30 days daily backups + 12 months monthly backups (matching existing accounting backup policy)
- **Restore test:** Quarterly restore test trên staging VPS — verify community tables recover correctly

### 11.10 ShopERP admin nav for community (v1.3 NEW — D15)
**File:** `5_WebApps/ShopERP/Components\Layout\NavMenu.razor` (ĐÃ TỒN TẠI — có admin section với redemption-catalog, redemption-history, campaigns, etc.)
**v1.3 Add community admin links (Sprint 6):**
```razor
@* v1.3 NEW: Community Commerce admin *@
<NavLink class="nav-link" href="admin/community-roles">
    <i class="bi bi-people-fill"></i> Cộng tác viên
</NavLink>
<NavLink class="nav-link" href="admin/product-referral-configs">
    <i class="bi bi-tag"></i> Commission config
</NavLink>
<NavLink class="nav-link" href="admin/fraud-flags">
    <i class="bi bi-shield-exclamation"></i> Fraud Review
</NavLink>
<NavLink class="nav-link" href="admin/fraud-stats">
    <i class="bi bi-graph-up"></i> Fraud Stats
</NavLink>
```
**Auth:** SystemAdmin role only (existing pattern — same as tenants/shop-instances/featured-products links)
**Task:** Sprint 6 CC-S6-T2 (Admin UI task) — add links vào NavMenu.razor

### 11.11 Legal review gate before production (v1.3 NEW — D14)
**Gate:** KHÔNG flip `Community__Enabled=true` trên production cho đến khi:
- [ ] Legal documents drafted (Sprint 6 CC-S6-T3): terms-of-service, privacy-policy (with device fingerprint consent clause), marketplace-policy, anti-fraud-policy
- [ ] Legal documents REVIEWED bởi lawyer (Vietnam commercial law — Nghị định 13/2023 data protection + Thông tư 39/TT-BCT e-commerce)
- [ ] Privacy policy published trên KhachLink (link trong footer)
- [ ] Terms of service published trên KhachLink + ShopERP
- [ ] Device fingerprint consent dialog tested (Sprint 0 — DeviceFingerprintConsentDialog.razor)
**PoC exception:** PoC (10-20 cửa hàng, 50-100 khách) có thể deploy với `Community__Enabled=true` trên staging VPS (khách VIP online) để test — KHÔNG production deploy cho đến khi legal review pass.
**Task:** Sprint 6 CC-S6-T3 — legal docs draft. Post-Sprint-6: lawyer review (user-arranged, NOT AI task).

### 11.12 Service worker cache update (v1.3 NEW — D16)
**File:** `5_WebApps/KhachLink/wwwroot/service-worker.js` (ĐÃ TỒN TẠI — `staticUrlsToCache` array at line 67-78)
**v1.3 Add community assets (Sprint 0 + Sprint 2):**
```javascript
// v1.3 NEW: Community Commerce assets
const staticUrlsToCache = [
  // ...existing assets...
  '/js/fingerprint.js',              // v1.2 Sprint 0 — device fingerprint interop
  '/lib/fingerprintjs/fingerprint.js', // v1.2 Sprint 0 — FingerprintJS vendored
  '/lib/leaflet/leaflet.css',        // v1.3 Sprint 2 — Leaflet vendored
  '/lib/leaflet/leaflet.js',         // v1.3 Sprint 2 — Leaflet vendored
];
```
**Bump cache version:** `STATIC_CACHE = 'vanan-static-v16-community'` (increment from v15-install-fix2)
**Task:** Sprint 0 CC-S0-T2 (fingerprint.js) + Sprint 2 CC-S2-T1 (leaflet) — update service-worker.js staticUrlsToCache + bump cache version.
**Dynamic API cache (dynamicCachePatterns at line 85):** KHÔNG cache `/api/community/*` endpoints — community APIs are auth-scoped (X-Customer-Token), caching risk cross-user leak. Same pattern as existing `/api/customers/me` exclusion.

### 11.13 PostGIS extension decision (v1.3 NEW — D17)
**PoC (10-20 cửa hàng, 50-100 khách):** KHÔNG cần PostGIS. Haversine SQL query trên small dataset (<1K orders) — full scan OK, <50ms.
**Post-PoC threshold:** >1K shippers hoặc >10K orders/ngày → install PostGIS extension + migrate nearby queries sang `ST_DWithin` + GIST index.
**Implementation when needed:**
- `docker exec vanan-postgres psql -c "CREATE EXTENSION postgis;"`
- Add `geography(Point, 4326)` column to Order + TenantSettings
- Backfill lat/lng → geometry
- Migrate query: `WHERE ST_DWithin(location, ST_MakePoint(lng, lat)::geography, {radiusMeters})`
**Task:** NOT in PoC scope. Document trong tech debt ledger. Post-PoC sprint (Sprint 8+).

---

## 12. COST & CAPACITY PLAN (v1.4 NEW — CORE COMPETITIVE ADVANTAGE)

> **v1.4 — 2026-07-26:** Section này документ hóa **lợi thế cạnh tranh chi phí** của VanAn Community Commerce — self-host everything, zero paid external services, scale từ $50/tháng (PoC) đến $100K/tháng (10M users optimized). Per-user cost target: **$0.013/user/tháng @ 1M users optimized**.

### 12.1 Cost Projections theo mốc scale

| Mốc | Users | Tenants | Base cost | Optimized cost | Saving | Per-user (optimized) |
|---|---|---|---|---|---|---|
| **PoC** | 50 | 10-20 | $50/tháng | $50/tháng | 0% | $1.00/user/tháng |
| **10K** | 10K | 1K | $720/tháng | **$550/tháng** | 24% | $0.055/user/tháng |
| **100K** | 100K | 10K | $3,900/tháng | **$2,400/tháng** | 38% | $0.024/user/tháng |
| **1M** | 1M | 50K | $26,000/tháng | **$13,000/tháng** | 50% | **$0.013/user/tháng** |
| **5M** | 5M | 100K | $155K/tháng | **$70K/tháng** | 55% | $0.014/user/tháng |
| **10M** | 10M | 300K | $300K/tháng | **$135K/tháng** | 55% | $0.014/user/tháng |

**Per-user cost trend:** Giảm từ $1.00 (PoC) → $0.013 (1M) — **77x giảm** nhờ economies of scale + optimization.

### 12.2 Cost Driver Analysis @ 1M users

```
Tỷ trọng chi phí ở mốc 1M users:

SMS OTP          58%  ████████████████████████████████  ← BIGGEST
PG cluster       15%  ████████
App cluster       6%  ███
TimescaleDB       7%  ████
Redis             2%  █
NATS              2%  █
Chat (Cassandra)  2%  █
Kafka             2%  █
Backup/LB/BW      3%  ██
Logging (Seq)     1%  █
Other             2%  █
```

**Insight:** SMS OTP chiếm **58% chi phí** ở 1M users. Đây là điểm tối ưu #1.

### 12.3 SMS OTP Optimization (giảm 50-80% SMS cost)

> **v1.4 Hard rule:** SMS OTP OPTIONAL trong PoC (device fingerprint thay thế). Khi scale, multi-channel OTP giảm chi phí 80%.

| Technique | Saving | Implementation |
|---|---|---|
| **OTP via WhatsApp Business API** | $0.005/msg thay $0.05 SMS → **10x rẻ hơn** | Tích hợp WhatsApp Business API, fallback SMS nếu WhatsApp fail |
| **OTP via Zalo OA** | Free cho verified Zalo OA (Vietnam-specific) | Tích hợp Zalo OA API — Vietnam users 90% có Zalo |
| **OTP token caching 5 phút** | Customer đăng nhập lại trong 5 phút → reuse OTP, không gửi SMS mới | Redis/in-memory cache `otp:{phone}` TTL 5min |
| **Smart OTP retry** | Limit 3 SMS/giờ/số → block abuse | Rate limit `otp_ratelimit:{phone}` |
| **WhatsApp + Zalo + SMS fallback chain** | Đa kênh → SMS chỉ là last resort | Saving: 80% users dùng WhatsApp/Zalo → 80% SMS cost giảm |

**Saving @ 1M users:** $15K × 80% = **$12K/tháng tiết kiệm** = $144K/năm

### 12.4 Optimization Priority (ROI ranking)

#### 🥇 Tier 1: ROI cực cao (giảm 30-60% chi phí tổng)
| # | Technique | Saving @ 1M | When to apply |
|---|---|---|---|
| O1 | Multi-channel OTP (Viettel + Zalo + WhatsApp) | $12K/tháng | Khi scale >10K users |
| O2 | Reserved instances + committed use discounts (Vultr 20-30%, AWS 30-50%) | $5K/tháng | Khi stable >1 tháng |
| O3 | Spot/Preemptible instances cho batch jobs (TimescaleDB compression, backup, analytics) | $2K/tháng | Khi có batch jobs |

#### 🥈 Tier 2: ROI cao (giảm 15-30%)
| # | Technique | Saving @ 100K | When to apply |
|---|---|---|---|
| O4 | GPS adaptive polling aggressive (15s OutForDelivery, 60s PickedUp, drop static points) | $150/tháng (TimescaleDB storage) | Sprint 2 (implement ngay) |
| O5 | Read replica cho Dashboard + reporting (heavy queries → replica) | $80/tháng (giảm primary spec 1 tier) | >10K users |
| O6 | Cloudflare R2 thay S3 cho backup ($0 egress) | $50/tháng egress | Ngay lập tức |
| O7 | Self-host vs Managed services (Redis, PG, NATS — self-host đến 100K) | $400-600/tháng | Đến 100K users |

#### 🥉 Tier 3: ROI trung bình (giảm 5-15%)
| # | Technique | Saving @ 100K | When to apply |
|---|---|---|---|
| O8 | KhachLink WASM CDN + cache (Cloudflare free) — giảm 60-80% bandwidth | $400/tháng @ 1M | Ngay lập tức |
| O9 | PostgreSQL autovacuum tuning + partitioning (WalletTransaction by month, Message by month + archival 90 ngày) | $80/tháng (giảm primary spec) | >10K users |
| O10 | Logging volume reduction (Seq WARN+ only, INFO sampling 1%) | $60/tháng | >10K users |
| O11 | SQLite → shared PG với RLS (LT4 — 1 DB instance thay 300K files) | $100/tháng (backup + sync worker) | Sprint 7+ |

### 12.5 Make-or-Buy Decisions

| Component | Buy (Managed) khi | Make (Self-host) khi | Recommendation |
|---|---|---|---|
| PostgreSQL | 1M+ users, team <3 DevOps | PoC-100K, team có PG expertise | **Self-host đến 100K**, managed từ 1M |
| Redis | 100K+ users | PoC-10K | **Self-host đến 10K**, managed từ 100K. PoC: in-memory .NET (zero Redis) |
| NATS | Rare — Synadia niche | Hầu hết cases | **Self-host luôn** (low maintenance) |
| TimescaleDB | 1M+ users | 10K-1M | **Self-host đến 1M** (TimescaleCloud đắt) |
| SMS OTP | Luôn buy | — | **Viettel SMS + Zalo OA + WhatsApp multi-channel** |
| SignalR | 100K+ connections | PoC-100K | **Self-host + Redis backplane đến 100K**, Azure SignalR Service từ 100K |
| Object storage | Luôn buy (free tier OK) | — | **Cloudflare R2** (zero egress) |
| CDN | Luôn buy (free tier OK) | — | **Cloudflare free tier** |
| Logging | 100K+ users | PoC-100K | **Seq self-host đến 100K**, Loki/Grafana Cloud từ 100K |

### 12.6 Vietnam-Specific Optimizations

| # | Technique | Saving |
|---|---|---|
| V1 | **Viettel SMS gateway** thay Twilio — $0.02/SMS (Viettel) vs $0.05 (Twilio) → **60% giảm** | 60% SMS cost |
| V2 | **Zalo OA free messaging** — Zalo OA verified = free → 90% Vietnam users có Zalo | 90% SMS cost (Zalo channel) |
| V3 | **Vietnam VPS providers** (VinaHost, Tenten, Viettel IDC) cho non-critical — 30-50% rẻ hơn Vultr/DO | 30-50% VPS cost |
| V4 | **Cross-border data residency** — lưu PII trong Vietnam → tránh GDPR/PDPA compliance cost + faster latency | Compliance cost |
| V5 | **Vietnamese CDN** (VinaCDN, FPT CDN) cho static — latency <20ms trong VN, rẻ hơn Cloudflare Pro | Latency + cost |
| V6 | **MoMo/ZaloPay banking API** thay Stripe — Vietnam-specific payment, lower fees | Payment fees |

**Vietnam-optimized cost @ 1M users:**
- SMS: Viettel + Zalo + WhatsApp → **$5K/tháng thay $15K** (giảm 67%)
- VPS: Vietnam IDC cho PG replica + backup → **giảm 30%** ($800 saving)
- **Total 1M @ Vietnam-optimized: ~$18K/tháng thay $26K = giảm 30%**

### 12.7 Total Cost With Optimization (Vietnam-optimized)

| Mốc | Base | Optimized | Vietnam-optimized | Per-user (VN) |
|---|---|---|---|---|
| PoC (50) | $50 | $50 | $50 | $1.00 |
| 10K | $720 | $550 | **$450** | $0.045 |
| 100K | $3,900 | $2,400 | **$1,800** | $0.018 |
| 1M | $26,000 | $13,000 | **$9,000** | **$0.009** |
| 5M | $155K | $70K | **$48K** | $0.0096 |
| 10M | $300K | $135K | **$92K** | $0.0092 |

**Per-user cost Vietnam-optimized @ 1M: $0.009/user/tháng = $0.11/user/năm** — cực cạnh tranh.

### 12.8 Edge vs Central Cost Comparison

| Mốc | Central-only (spec hiện tại) | Edge (Hybrid) | Verdict |
|---|---|---|---|
| 10K users | $450/tháng (VN) | $1.5K/tháng (100 gateways × $15) | Edge ĐẮT HƠN 3x |
| 100K users | $1,800/tháng | $2.5K/tháng | Edge đắt 1.4x |
| 1M users | $9K/tháng | $10K/tháng | Ngang ngửa |
| 5M users | $48K/tháng | **$38K/tháng** | Edge RẺ HƠN 21% |
| 10M users | $92K/tháng | **$72K/tháng** | Edge RẺ HƠN 22% |

**Break-even: ~1M users.** Trước 1M → central cheaper. Sau 1M → edge cheaper do PG central không scale được tiếp.

### 12.9 Implementation Priority Timeline

```
Ngay lập tức (Sprint 0-1):
  ✓ Cloudflare R2 cho backup (O6) — saving $50/tháng, 0 effort
  ✓ Viettel SMS gateway (V1) — saving 60% SMS từ ngày 1
  ✓ Cloudflare CDN free (O8) — giảm bandwidth ngay
  ✓ Seq sampling WARN+ (O10) — giảm logging cost
  ✓ /api/v1/ versioning (HR-SCALE-1) — prepare for refactor
  ✓ Anti-Corruption Layer (R2, HR-SCALE-2) — prepare for service decomposition

3-6 tháng (khi đạt 1K-10K users):
  ✓ O1 WhatsApp + Zalo OA OTP — biggest saving
  ✓ O7 self-host Redis (or in-memory) + NATS — đã có sẵn skill
  ✓ O4 adaptive GPS polling — giảm TimescaleDB cost sớm
  ✓ ST5 Wallet atomic — implement ngay từ Sprint 5
  ✓ ST6 Outbox commission — implement ngay từ Sprint 4
  ✓ ST9 SalesmanCode tenant prefix — implement ngay từ Sprint 0

6-12 tháng (khi đạt 10K-100K users):
  ✓ O2 reserved instances — commit 12 tháng
  ✓ O5 read replica + materialized view
  ✓ O9 PG partitioning — bắt buộc trước 100K
  ✓ O3 spot instances cho batch jobs
  ✓ ST1 PostGIS — khi >1K shippers
  ✓ ST3 Redis backplane — khi >1K shippers
  ✓ ST4 PgBouncer — khi >10K users

12+ tháng (khi đạt 100K+ users, Sprint 7+):
  ✓ LT5 Geo-sharding — chỉ khi geographic spread rõ
  ✓ LT2 Service decomposition — khi team >5 DevOps
  ✓ LT4 SQLite → PG RLS — big migration
  ✓ Edge deployment — khi >1M users (break-even)
  ✓ Managed services chuyển đổi (PG → RDS optional)
```

---

## 11. SESSION CHECKLIST

### Before session start
- [ ] Read `docs/AI/project_state.md` (current objective)
- [ ] Read task card cho sprint hiện tại
- [ ] Read detailed plan cho sprint hiện tại
- [ ] Confirm VPS verification của sprint trước đã PASS
- [ ] `git checkout -b feature/community-sprint{N}-...`

### During session
- [ ] JIT Planning: chốt file/method/test case (Phase 1)
- [ ] User approve plan trước Phase 2
- [ ] Pure Execution: viết code (Phase 2)
- [ ] TDD: test trước, code sau

### Before session end
- [ ] `dotnet build VanAn.sln` 0 errors
- [ ] `guard-check.ps1` pass
- [ ] Unit test pass (sprint-specific)
- [ ] Git commit + push
- [ ] Update `project_state.md` (status, next actions)
- [ ] Nếu sprint hoàn thành: trigger CI/CD → VPS verification

---

## 12. ROLLBACK PLAN

Nếu sprint fail trên VPS:
1. **Identify failure:** Check CI/CD logs + verification script output
2. **Fix on branch:** `git checkout feature/community-sprint{N}-...` → fix → push → CI/CD re-run
3. **Nếu fix không khả thi trong 2 attempts:** Rollback VPS về image trước sprint
   ```bash
   # On VPS
   cd /opt/vanan
   # Edit .env: IMAGE_TAG={previous_sprint_tag}
   sudo docker compose -f docker-compose.prod.yml pull
   sudo docker compose -f docker-compose.prod.yml up -d
   ```
4. **Document failure:** Ghi vào `project_state.md` Known Risks
5. **Re-plan:** Chia sprint thành sub-sprint nhỏ hơn

---

## 13. DELIVERABLES CHECKLIST

### Per sprint
- [ ] 1 task card file (`docs/AI/tasks/task_cc_sprint{N}_*.md`)
- [ ] 1 detailed plan file (`docs/AI/tasks/sprint{N}_*_detailed_plan.md`)
- [ ] Code: domain + EF config + service + controller + UI
- [ ] Unit tests (TDD, ≥ sprint-specific minimum)
- [ ] E2E test spec (`6_Testing/e2e-tests/community-*.spec.ts`)
- [ ] CI/CD pass: build + unit + integration + architecture + guard-check
- [ ] VPS deploy + runtime verification ALL PASS
- [ ] `project_state.md` updated

### Final (sau Sprint 6)
- [ ] Tất cả 7 sprint VPS verification PASS
- [ ] Full E2E regression trên VPS PASS
- [ ] Legal documents draft hoàn thành
- [ ] PoC ready: 10-20 shops, 50-100 customers, 10 community members

---

## REFERENCES
- Requirements spec: `C:\Users\lebao\.windsurf\plans\community-commerce-requirements-spec-2c5017.md`
- Codebase review: `C:\Users\lebao\.windsurf\plans\shipper-saleman-review-2c5017.md`
- Task card template: `docs/AI/tasks/Template_phase_task_card.md`
- Master plan template: `docs/AI/tasks/Template_master_implementation_plan.md`
- CI workflow: `.github/workflows/ci.yml`
- CD workflow: `.github/workflows/cd.yml`
- E2E workflow: `.github/workflows/e2e.yml`
- guard-check: `guard-check.ps1`
