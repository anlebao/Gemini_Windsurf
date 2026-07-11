# MASTER IMPLEMENTATION PLAN — KhachLink Full Business Flow Completion

> **Status:** PLANNING — awaiting user approval to start Wave 0
> **Created:** 2026-07-11
> **Last Updated:** 2026-07-11
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → feature branches per wave
> **Execution principle:** JIT Planning + Pure Execution + Toggle-First
> **Prerequisite:** Tài liệu yêu cầu nghiệp vụ v1.2 đã approved (`docs/MVP_Product/Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md`)
> **Reference:** Verify codebase results Section 6 của tài liệu yêu cầu

---

## 0. EXECUTION RULES

### JIT Planning Strategy
**Nguyên tắc:** Investigate trước, Implement sau. KHÔNG code mò mẫm.

**Bước 1: INVESTIGATE** — Verify existing code structure, service signatures, UI component patterns
**Bước 2: IMPLEMENT** — Theo plan đã chốt, mỗi wave xong chạy `guard-check.ps1` + `dotnet build`

### Session protocol
1. Mỗi session chỉ làm 1 wave
2. Bắt đầu session: Đọc `project_state.md` + task card wave đang làm
3. Sau khi plan chốt: Execution Phase
4. Trước session end: Build + test
5. Sau mỗi wave: Commit `[KL WAVE X] Task description`

### Branch protocol
```
main
  └── feature/khachlink-flow-wave0-toggle-infrastructure
      └── feature/khachlink-flow-wave1-payment-kitchen-ui
          └── feature/khachlink-flow-wave2-completion-loyalty
              └── feature/khachlink-flow-wave3-voice-note-tts
                  └── feature/khachlink-flow-wave4-e2e-tests
```

### Hard rules
- **Domain layer:** Chỉ sửa khi có Domain Modeling Defect được approval — thêm field `TableNumber` vào `QRCodePayload` (DTO, không phải Domain entity) OK
- **AccountingEntry immutable** — không thay đổi
- **UI Platform:** Mọi UI mới PHẢI dùng VanAnButton, VanAnCard, VanAnForm, VanATable — KHÔNG custom HTML/CSS
- **Toggle-First:** Wave 0 phải hoàn thành trước mọi wave khác — toggle infrastructure là nền tảng
- **Playwright DISABLED** cho đến Wave 4 (E2E tests)
- **Polling 3s:** OrderTracking polling interval đổi từ 5-10s → 3s (Wave 1)
- **No audio storage:** Voice note chỉ STT (client) + TTS (kitchen). KHÔNG lưu audio file.
- **OTP TTL:** Giữ 5 phút (quyết định user D2)
- **EInvoice auto-trigger:** DEFERRED — Tech Debt TD-KL-01, không implement trong plan này

### Critical context
- **Architecture:** KhachLink (5002) → Gateway (5001) → ShopERP (5003) → SQLite (business) + PostgreSQL (accounting)
- **KhachLink = Blazor WebAssembly PWA** — HTTP-only, không inject DbContext, không inject CoreHub services có repository dependencies
- **ShopERP = Blazor Server** — hosts in-process CoreHub services (Option B)
- **Real-time:** KhachLink dùng HTTP polling 3s (không SignalR client)
- **DI Checklist:** Mỗi service mới thêm vào KhachLink MUST: (1) đăng ký DI trong `Program.cs`, (2) thêm assertion vào `KhachLinkStartupTests.cs`

---

## 1. CURRENT ISSUES SUMMARY

Nguồn: Section 6 + 9 của `Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` v1.2

### Issue 1: Không có Module Toggle Infrastructure
**Status:** ❌ MISSING
**Priority:** 0 (Critical — nền tảng cho mọi feature)
**Tech Debt:** TD-KL-12

6 toggles cần thiết: `QR_TableNumber_Enabled`, `Kitchen_Workflow_Enabled`, `Voice_Note_Enabled`, `Loyalty_Program_Enabled`, `Accounting_Sync_Enabled`, `EInvoice_Auto_Export_Enabled`

### Issue 2: Payment flow thiếu
**Status:** ❌ MISSING
**Priority:** 1 (High)
**Tech Debt:** TD-KL-04, TD-KL-05

Không có UI chọn Tiền mặt/Chuyển khoản, không có cash Processing Bar, không có dual status bars cho transfer.

### Issue 3: Kitchen flow UI thiếu nút Order-level
**Status:** ⚠️ PARTIAL
**Priority:** 1 (High)
**Tech Debt:** TD-KL-08, TD-KL-09

ShopERP `Orders/Detail.razor` chỉ có nút "Xác nhận" (pending→confirmed). Thiếu nút "Bắt đầu làm" (→preparing) và "Sẵn sàng" (→ready). KhachLink mismatch status name (`processing` vs `preparing`).

### Issue 4: Completion flow thiếu
**Status:** ⚠️ PARTIAL
**Priority:** 1 (High)
**Tech Debt:** TD-KL-06, TD-KL-07

Thiếu nút "Hoàn tất" trên ShopERP UI. Thiếu nút "Xác nhận đã nhận hàng" trên KhachLink.

### Issue 5: Voice note redesign (STT only + TTS)
**Status:** ⚠️ PARTIAL
**Priority:** 2 (Medium)
**Tech Debt:** TD-KL-02

`VoiceNote.razor` có STT nhưng thiếu toggle + TTS ở nhà bếp. Bỏ audio storage (quyết định user D5).

### Issue 6: Polling interval 3s
**Status:** ⚠️ PARTIAL
**Priority:** 2 (Medium)
**Tech Debt:** TD-KL-13

`OrderTracking.razor` hiện 5-10s, cần đổi 3s.

### Issue 7: E2E test full luồng không tồn tại
**Status:** ❌ MISSING
**Priority:** 3 (Final validation)
**Tech Debt:** N/A — tạo mới

8 test files tồn tại nhưng tất cả PARTIAL, không có test full flow.

---

## 2. WAVE 0 — Module Toggle Infrastructure

**Branch:** `feature/khachlink-flow-wave0-toggle-infrastructure`
**Priority:** 0 (Critical — BLOCKING mọi wave sau)
**Task Card:** `docs/AI/tasks/khachlink_flow_wave0_toggle_infrastructure_task_card.md`

### Mục tiêu
Tạo Shop Settings page + toggle storage + logic bypass cho 6 toggles. Đây là nền tảng để mọi wave sau có thể check toggle state.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W0-T1 | Tạo `ShopFeatureSettings` entity (Domain) hoặc config model (Infrastructure) | `1_Shared/Domain.cs` hoặc `3_CoreHub/Infrastructure/` | ⬜ |
| 2 | W0-T2 | Tạo `IShopFeatureSettingsService` + implementation (read/write toggles per shop/tenant) | `3_CoreHub/Services/` | ⬜ |
| 3 | W0-T3 | Đăng ký DI trong ShopERP `Program.cs` + KhachLink `Program.cs` (HTTP service) | `5_WebApps/ShopERP/Program.cs`, `5_WebApps/KhachLink/Program.cs` | ⬜ |
| 4 | W0-T4 | Tạo Shop Settings page (ShopERP) — UI Platform components, 6 toggle switches | `5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor` | ⬜ |
| 5 | W0-T5 | Tạo API endpoint `GET/PUT /api/shop/settings/features` | `5_WebApps/ShopERP/Controllers/ShopSettingsController.cs` | ⬜ |
| 6 | W0-T6 | Tạo `ShopFeatureSettingsHttpService` cho KhachLink (fetch toggles via Gateway) | `5_WebApps/KhachLink/Services/Http/ShopFeatureSettingsHttpService.cs` | ⬜ |
| 7 | W0-T7 | KhachLinkStartupTests assertion | `6_Tests/VanAn.Integration.Tests/KhachLinkStartupTests.cs` | ⬜ |
| 8 | W0-T8 | Seed default toggles (kitchen=ON, loyalty=ON, accounting=ON, QR_table=OFF, voice=OFF, einvoice=OFF) | `5_WebApps/ShopERP/Program.cs` (seed block) | ⬜ |
| 9 | W0-T9 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] 6 toggles lưu được trong DB per shop/tenant
- [ ] ShopERP Settings page hiển thị + chỉnh sửa toggles (UI Platform)
- [ ] KhachLink fetch được toggles qua HTTP
- [ ] Default seed: kitchen=ON, loyalty=ON, accounting=ON, QR_table=OFF, voice=OFF, einvoice=OFF
- [ ] Build: 0 errors
- [ ] KhachLinkStartupTests pass

### Why first
- Mọi wave sau cần check toggle state để quyết định có chạy luồng đó không
- Không có toggle infrastructure → không thể implement bypass logic

---

## 3. WAVE 1 — Payment Flow + Kitchen UI + Polling 3s

**Branch:** `feature/khachlink-flow-wave1-payment-kitchen-ui`
**Priority:** 1 (High)
**Task Card:** `docs/AI/tasks/khachlink_flow_wave1_payment_kitchen_ui_task_card.md`

### Mục tiêu
(1) UI chọn payment method (Cash/Transfer) + Processing Bar cho cash + dual status bars cho transfer. (2) ShopERP kitchen transition buttons ở Order Detail. (3) KhachLink status name fix + polling 3s. (4) Kitchen flow bypass khi toggle OFF.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W1-T1 | Checkout.razor: thêm UI chọn Tiền mặt/Chuyển khoản (VanAnForm radio buttons) | `5_WebApps/KhachLink/Pages/Checkout.razor` | ⬜ |
| 2 | W1-T2 | Cash flow: Processing Bar component + gửi request ShopERP | `5_WebApps/KhachLink/Components/ProcessingBar.razor` (NEW), `Checkout.razor` | ⬜ |
| 3 | W1-T3 | Transfer flow: dual status bars (Xử lý đơn + Chờ thanh toán) | `5_WebApps/KhachLink/Components/QrPaymentModal.razor` | ⬜ |
| 4 | W1-T4 | ShopERP Order Detail: thêm nút "Bắt đầu làm" (→preparing) + "Sẵn sàng" (→ready) | `5_WebApps/ShopERP/Components/Pages/Orders/Detail.razor` | ⬜ |
| 5 | W1-T5 | ShopERP Order Detail: thêm nút "Hoàn tất" (→completed) | `5_WebApps/ShopERP/Components/Pages/Orders/Detail.razor` | ⬜ |
| 6 | W1-T6 | Kitchen flow bypass: khi `Kitchen_Workflow_Enabled` = OFF, skip preparing/ready, đi thẳng confirmed→completed | `3_CoreHub/Services/OrderWorkflowService.cs` | ⬜ |
| 7 | W1-T7 | KhachLink OrderTracking: fix status name `processing` → `preparing` | `5_WebApps/KhachLink/Pages/OrderTracking.razor` line 270 | ⬜ |
| 8 | W1-T8 | KhachLink OrderTracking: polling interval → 3s | `5_WebApps/KhachLink/Pages/OrderTracking.razor` lines 377-388 | ⬜ |
| 9 | W1-T9 | KhachLink OrderTracking: ẩn kitchen statuses khi toggle OFF | `5_WebApps/KhachLink/Pages/OrderTracking.razor` | ⬜ |
| 10 | W1-T10 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] Checkout hiển thị 2 option Tiền mặt/Chuyển khoản
- [ ] Cash flow có Processing Bar
- [ ] Transfer flow có 2 status bars
- [ ] ShopERP Order Detail có nút "Bắt đầu làm", "Sẵn sàng", "Hoàn tất"
- [ ] Kitchen flow bypass khi toggle OFF
- [ ] KhachLink status name sync với domain (`preparing`)
- [ ] Polling interval 3s
- [ ] Build: 0 errors

---

## 4. WAVE 2 — Completion + Loyalty + Customer Confirm

**Branch:** `feature/khachlink-flow-wave2-completion-loyalty`
**Priority:** 1 (High)
**Task Card:** `docs/AI/tasks/khachlink_flow_wave2_completion_loyalty_task_card.md`

### Mục tiêu
(1) KhachLink nút "Xác nhận đã nhận hàng". (2) Loyalty flow bypass khi toggle OFF. (3) PWA disable cho logged-in users. (4) Accounting sync bypass khi toggle OFF.

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W2-T1 | KhachLink OrderTracking: thêm nút "Xác nhận đã nhận hàng" khi status=ready/delivered | `5_WebApps/KhachLink/Pages/OrderTracking.razor` | ⬜ |
| 2 | W2-T2 | API endpoint `POST /api/orders/{id}/confirm-received` (customer confirm) | `5_WebApps/ShopERP/Controllers/OrdersController.cs` hoặc `OrderWorkflowController.cs` | ⬜ |
| 3 | W2-T3 | Order domain: thêm method `ConfirmReceivedByCustomer()` hoặc dùng status transition → delivered | `1_Shared/Domain.cs` (READ — check existing) + `3_CoreHub/Services/OrderWorkflowService.cs` | ⬜ |
| 4 | W2-T4 | Loyalty bypass: khi `Loyalty_Program_Enabled` = OFF, ẩn IdentityUpgradeModal + OTP + PWA prompt, show "Cảm ơn" | `5_WebApps/KhachLink/Pages/OrderTracking.razor`, `Components/IdentityUpgradeModal.razor` | ⬜ |
| 5 | W2-T5 | PWA disable: khi user đã đăng nhập, không show PWAInstallPrompt | `5_WebApps/KhachLink/Components/PWA/PWAInstallPrompt.razor` | ⬜ |
| 6 | W2-T6 | Accounting sync bypass: khi `Accounting_Sync_Enabled` = OFF, `ConfirmPaymentAsync` skip `GenerateAccountingEntriesAsync` | `3_CoreHub/Services/OrderService.cs` line 607 | ⬜ |
| 7 | W2-T7 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] KhachLink có nút "Xác nhận đã nhận hàng"
- [ ] API confirm-received hoạt động
- [ ] Loyalty flow bypass khi toggle OFF (show "Cảm ơn")
- [ ] PWA prompt không hiện cho logged-in users
- [ ] Accounting sync bypass khi toggle OFF
- [ ] Build: 0 errors

---

## 5. WAVE 3 — Voice Note Redesign (STT only + TTS)

**Branch:** `feature/khachlink-flow-wave3-voice-note-tts`
**Priority:** 2 (Medium)
**Task Card:** `docs/AI/tasks/khachlink_flow_wave3_voice_note_tts_task_card.md`

### Mục tiêu
(1) Voice note toggle ON/OFF. (2) STT only (không audio storage). (3) TTS ở nhà bếp khi bếp trưởng nhấn "Nhận đơn" — toggle độc lập. (4) QR payload thêm TableNumber (toggle).

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W3-T1 | Voice note toggle: ẩn/hiện nút "Ghi chú" dựa trên `Voice_Note_Enabled` | `5_WebApps/KhachLink/Pages/VoiceNote.razor` hoặc cart/checkout page | ⬜ |
| 2 | W3-T2 | STT cleanup: xác nhận chỉ lưu text, bỏ logic audio blob (nếu có dư) | `5_WebApps/KhachLink/Pages/VoiceNote.razor` | ⬜ |
| 3 | W3-T3 | Domain: mark `VoiceNoteAudioBlob` + `ItemNoteAudioBlob` as obsolete (không xóa field) | `1_Shared/Domain.cs` lines 730-731, 864-865 | ⬜ |
| 4 | W3-T4 | TTS ở nhà bếp: thêm nút "Đọc ghi chú" (hoặc auto-play) khi bếp trưởng nhấn "Nhận đơn" | `5_WebApps/ShopERP/Components/Pages/Orders/Detail.razor` hoặc Kitchen page | ⬜ |
| 5 | W3-T5 | TTS implementation: dùng Web Speech API (`speechSynthesis`) ở ShopERP (Blazor Server) | JS interop hoặc component | ⬜ |
| 6 | W3-T6 | TTS toggle độc lập trong Shop Settings (sub-toggle của `Voice_Note_Enabled`) | `5_WebApps/ShopERP/Components/Pages/Settings/ShopFeatures.razor` | ⬜ |
| 7 | W3-T7 | QR payload: thêm field `TableNumber` vào `QRCodePayload` DTO | `1_Shared/DTOs/QRCodePayload.cs` | ⬜ |
| 8 | W3-T8 | QR generation: thêm số bàn khi `QR_TableNumber_Enabled` = ON | QR generation logic (admin page) | ⬜ |
| 9 | W3-T9 | KhachLink Scan page: hiển thị số bàn khi có trong payload | `5_WebApps/KhachLink/Pages/Scan.razor` | ⬜ |
| 10 | W3-T10 | Verify build: 0 errors + guard-check.ps1 pass | Solution-wide | ⬜ |

### Exit criteria
- [ ] Voice note toggle ON/OFF hoạt động
- [ ] STT chỉ lưu text (không audio)
- [ ] TTS đọc ghi chú ở nhà bếp khi bếp trưởng nhận đơn
- [ ] TTS toggle độc lập
- [ ] QR payload có field TableNumber
- [ ] QR generation + display số bàn khi toggle ON
- [ ] Build: 0 errors

---

## 6. WAVE 4 — E2E Playwright Tests

**Branch:** `feature/khachlink-flow-wave4-e2e-tests`
**Priority:** 3 (Final validation)
**Task Card:** `docs/AI/tasks/khachlink_flow_wave4_e2e_tests_task_card.md`

### Mục tiêu
Tạo E2E test full luồng với 2 scenarios: (1) Full flow (tất cả toggle ON), (2) Minimal flow (kitchen + loyalty + accounting OFF).

### Tasks
| # | Task ID | Task | Files | Status |
|---|---------|------|-------|--------|
| 1 | W4-T1 | Tạo `khachlink-full-order-flow.spec.ts` — Scenario 1: Full flow (tất cả ON) | `6_Testing/e2e-tests/khachlink-full-order-flow.spec.ts` (NEW) | ⬜ |
| 2 | W4-T2 | Scenario 1: QR scan (có số bàn) → cart → voice note (mocked) → payment (cash + transfer) → real-time sync 3s → kitchen (nhận đơn + TTS + chế biến + sẵn sàng) → admin confirm + hoàn tất → customer confirm → OTP → loyalty → PWA | same file | ⬜ |
| 3 | W4-T3 | Tạo `khachlink-minimal-flow.spec.ts` — Scenario 2: Minimal flow (kitchen + loyalty + accounting OFF) | `6_Testing/e2e-tests/khachlink-minimal-flow.spec.ts` (NEW) | ⬜ |
| 4 | W4-T4 | Scenario 2: QR scan (không số bàn) → cart → payment (cash only) → confirmed→completed (bypass kitchen) → "Cảm ơn" (no OTP/loyalty/PWA) → no accounting sync | same file | ⬜ |
| 5 | W4-T5 | Page Object Model: cập nhật/create mới cho toggle settings, voice note, TTS, customer confirm | `6_Testing/e2e-tests/pages/` | ⬜ |
| 6 | W4-T6 | Run E2E tests + fix flaky issues | `6_Testing/` | ⬜ |
| 7 | W4-T7 | Verify: all E2E tests pass | `6_Testing/` | ⬜ |

### Exit criteria
- [ ] Scenario 1 (full flow) pass
- [ ] Scenario 2 (minimal flow) pass
- [ ] Không có flaky test
- [ ] E2E coverage: QR scan, voice note, payment (cash + transfer), real-time sync, kitchen flow, admin confirm + complete, customer confirm, OTP, loyalty, PWA, toggle scenarios

---

## 7. WAVE DEPENDENCY GRAPH

```
WAVE 0 (Toggle Infrastructure) ← BLOCKING
  │
  ├── WAVE 1 (Payment + Kitchen UI + Polling 3s)
  │     │
  │     └── WAVE 2 (Completion + Loyalty + Customer Confirm)
  │           │
  │           └── WAVE 3 (Voice Note + TTS + QR Table)
  │                 │
  │                 └── WAVE 4 (E2E Tests)
  │
  └── (WAVE 1-3 có thể chạy song song nếu đủ session, nhưng recommended sequential)
```

**Critical path:** W0 → W1 → W2 → W3 → W4

---

## 8. RISK ASSESSMENT

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| Toggle storage schema conflict | Medium | Medium | Dùng JSON column hoặc separate table — investigate trong Wave 0 |
| KhachLink Blazor WASM toggle fetch slow | Low | Low | Cache toggles trong localStorage + refresh on app start |
| Kitchen bypass logic breaks state machine | Medium | High | Validate transition trong `OrderWorkflowService` — khi toggle OFF, cho phép confirmed→completed trực tiếp |
| TTS không support trên tất cả browsers | Medium | Low | Fallback: hiển thị text ghi chú nếu TTS không available |
| E2E test flaky do polling 3s | Medium | Medium | Dùng `waitForResponse` thay vì fixed wait |
| Domain modification cho `ConfirmReceivedByCustomer` | Low | High | Ưu tiên dùng status transition existing (→delivered) thay vì thêm method mới |

---

## 9. SUCCESS CRITERIA (OVERALL)

- [ ] 6 module toggles hoạt động (bật/tắt trong Shop Settings)
- [ ] Full flow chạy end-to-end khi tất cả toggle ON
- [ ] Minimal flow chạy khi kitchen + loyalty + accounting OFF
- [ ] Payment: Cash (Processing Bar) + Transfer (VietQR + dual status bars)
- [ ] Kitchen: Nhận đơn → Đang chế biến → Sẵn sàng giao (Order-level buttons)
- [ ] Voice note: STT (client) + TTS (kitchen), toggle ON/OFF
- [ ] QR: số bàn tùy chọn (toggle)
- [ ] Completion: Admin "Hoàn tất" + Customer "Xác nhận đã nhận hàng"
- [ ] Loyalty: OTP + tích điểm + PWA (toggle ON/OFF)
- [ ] Accounting sync: toggle ON/OFF
- [ ] Polling 3s
- [ ] E2E tests: 2 scenarios pass
- [ ] Build: 0 errors
- [ ] guard-check.ps1 pass
- [ ] KhachLinkStartupTests pass

---

## 10. POST-COMPLETION

Sau khi tất cả 5 waves complete:
1. Update `docs/AI/project_state.md` — move objective to history, add completed items
2. Commit final: `[KL FLOW] All 5 waves complete — full business flow operational`
3. Tag: `khachlink-flow-v1.0`
4. Remaining Tech Debt: TD-KL-01 (EInvoice auto-trigger — chờ sandbox Viettel/MISA)
