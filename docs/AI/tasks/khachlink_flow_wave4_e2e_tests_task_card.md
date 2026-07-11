# TASK CARD: KhachLink Full Flow — Wave 4 — E2E Playwright Tests

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo E2E test full luồng với 2 scenarios: (1) Full flow (tất cả toggle ON), (2) Minimal flow (kitchen + loyalty + accounting OFF).
- **Nghiệp vụ áp dụng:** Section 8 của `Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` v1.2
- **Status:** ⬜ NOT STARTED
- **Branch:** `feature/khachlink-flow-wave4-e2e-tests`
- **Dependency:** Wave 3 COMPLETE (voice note + TTS + QR table number)

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
- **Execution Mode:** IMPLEMENT (E2E tests — Playwright enabled sau khi implementation complete)
- **Current Phase:** Wave 4 of 5 (FINAL)
- **Dependency:** Wave 0-3 ALL COMPLETE
- **Playwright:** ENABLED (implementation complete, build passing)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/khachlink_full_flow_master_plan.md` (READ — master plan)
- `docs/MVP_Product/Tai_lieu_yeu_cau_nghiep_vu_Khachlink.md` (READ — requirements v1.2)

### Files cần CREATE
- `6_Testing/e2e-tests/khachlink-full-order-flow.spec.ts` — Scenario 1: Full flow (tất cả toggle ON)
- `6_Testing/e2e-tests/khachlink-minimal-flow.spec.ts` — Scenario 2: Minimal flow (kitchen + loyalty + accounting OFF)
- `6_Testing/e2e-tests/pages/ShopSettingsPage.ts` — Page Object cho toggle settings
- `6_Testing/e2e-tests/pages/VoiceNotePage.ts` — Page Object cho voice note (mocked Speech API)
- `6_Testing/e2e-tests/pages/CustomerConfirmPage.ts` — Page Object cho customer confirm + OTP

### Files cần MODIFY
- `6_Testing/e2e-tests/pages/CustomerPage.ts` — thêm methods cho loyalty points verification
- `6_Testing/e2e-tests/pages/CheckoutPage.ts` — thêm payment method selection (cash/transfer)

### Files READ ONLY (investigate patterns)
- `6_Testing/e2e-tests/order-flow.spec.ts` — existing order flow test pattern
- `6_Testing/e2e-tests/omnichannel-order-lifecycle.spec.ts` — existing omnichannel test pattern
- `6_Testing/e2e-tests/qr-payment-ui.spec.ts` — existing QR payment test pattern
- `6_Testing/e2e-tests/realtime-sync-flow.spec.ts` — existing real-time sync test pattern
- `6_Testing/e2e-tests/payment-confirm-flow.spec.ts` — existing payment confirm test pattern
- `6_Testing/e2e-tests/voice-command.spec.ts` — existing voice note test (SKIPPED — check mock pattern)
- `6_Testing/e2e-tests/pages/` — existing Page Object Model pattern
- `6_Testing/playwright.config.ts` — Playwright config (tiers, timeouts, projects)
- `6_Testing/package.json` — dependencies

### Boundary Rules
- KHÔNG sửa production code — chỉ tạo test files + Page Objects
- KHÔNG tạo UI custom HTML/CSS trong tests — test existing UI
- Follow existing Page Object Model pattern trong `6_Testing/e2e-tests/pages/`
- Mock Web Speech API cho voice note tests (browser không support SpeechRecognition trong Playwright)
- Mock `speechSynthesis` cho TTS tests
- Dùng `waitForResponse` thay vì fixed wait cho polling 3s

---

## 4. TECHNICAL CONSTRAINTS
- [ ] **No production code changes:** Chỉ tạo test files + Page Objects
- [ ] **Playwright config:** Follow existing `playwright.config.ts` tiers (smoke, golden, full)
- [ ] **Page Object Model:** Follow existing pattern trong `6_Testing/e2e-tests/pages/`
- [ ] **Mock Speech API:** Web Speech API không support trong Playwright headless — mock `SpeechRecognition` + `speechSynthesis`
- [ ] **Polling 3s:** Dùng `waitForResponse` hoặc `expect.toHaveText` với timeout 5s (cho phép 3s polling + buffer)
- [ ] **Toggle scenarios:** Test phải set toggles qua API trước khi chạy (GET/PUT `/api/shop/settings/features`)
- [ ] **Data cleanup:** Test phải clean up order data sau khi chạy (hoặc dùng unique test data)
- [ ] **Tagging:** Tag tests `@golden` cho full flow, `@smoke` cho minimal flow

---

## 5. SUCCESS CRITERIA
- [ ] **SC1:** `khachlink-full-order-flow.spec.ts` tồn tại, cover Scenario 1 (full flow, tất cả toggle ON)
- [ ] **SC2:** `khachlink-minimal-flow.spec.ts` tồn tại, cover Scenario 2 (minimal flow, kitchen + loyalty + accounting OFF)
- [ ] **SC3:** Scenario 1 cover: QR scan (có số bàn) → cart → voice note (mocked) → payment (cash + transfer) → real-time sync 3s → kitchen (nhận đơn + TTS + chế biến + sẵn sàng) → admin confirm + hoàn tất → customer confirm → OTP → loyalty → PWA
- [ ] **SC4:** Scenario 2 cover: QR scan (không số bàn) → cart → payment (cash only) → confirmed→completed (bypass kitchen) → "Cảm ơn" (no OTP/loyalty/PWA) → no accounting sync
- [ ] **SC5:** Page Objects: ShopSettingsPage, VoiceNotePage, CustomerConfirmPage tồn tại
- [ ] **SC6:** All E2E tests pass (0 flaky)
- [ ] **SC7:** Build: 0 errors (production code không thay đổi)
- [ ] **SC8:** Live Runtime Verification PASS (RV1-RV10 trong §9 Post-IMPLEMENT) — boot full stack + run Playwright E2E thực tế

---

## 6. DETAILED IMPLEMENTATION

### 6.1. ANALYZE Phase (trước khi code)

**Cần investigate:**
1. **Existing test patterns:** Đọc `omnichannel-order-lifecycle.spec.ts` — hiểu scenario structure, setup/teardown, Page Object usage.
2. **Page Object pattern:** Đọc 2-3 files trong `6_Testing/e2e-tests/pages/` — hiểu class structure, selectors, methods.
3. **Playwright config:** Đọc `playwright.config.ts` — hiểu tiers (smoke, golden, full), timeouts, projects, base URL.
4. **Mock pattern:** Đọc `voice-command.spec.ts` — hiểu cách mock Web Speech API (line 51: `test.skip(!supportsSpeech...)`).
5. **Payment test:** Đọc `qr-payment-ui.spec.ts` + `payment-confirm-flow.spec.ts` — hiểu QR modal test, payment confirm test.
6. **Real-time sync test:** Đọc `realtime-sync-flow.spec.ts` — hiểu cách test polling/SignalR.
7. **Toggle API:** Confirm `GET/PUT /api/shop/settings/features?tenantId={id}` hoạt động qua Gateway.
8. **DevLogin:** Check cách login trong E2E tests (DevLoginController, `POST /dev/login`).

### 6.2. Page Objects (W4-T5)

**ShopSettingsPage.ts:**
```typescript
export class ShopSettingsPage {
  constructor(private page: Page) {}
  
  async setToggles(toggles: {
    qrTableNumber?: boolean;
    kitchenWorkflow?: boolean;
    voiceNote?: boolean;
    loyaltyProgram?: boolean;
    accountingSync?: boolean;
    einvoiceAutoExport?: boolean;
  }) {
    // Call API: PUT /api/shop/settings/features
  }
  
  async getToggles() {
    // Call API: GET /api/shop/settings/features
  }
  
  async enableAll() { /* Set all toggles ON */ }
  async disableKitchenLoyaltyAccounting() { /* Set kitchen, loyalty, accounting OFF */ }
}
```

**VoiceNotePage.ts:**
```typescript
export class VoiceNotePage {
  constructor(private page: Page) {}
  
  async mockSpeechRecognition() {
    // Mock window.SpeechRecognition
    // Return predefined transcript
  }
  
  async recordVoiceNote(transcript: string) {
    // Click "Ghi chú" button → mock STT → verify text saved
  }
}
```

**CustomerConfirmPage.ts:**
```typescript
export class CustomerConfirmPage {
  constructor(private page: Page) {}
  
  async clickConfirmReceived() {
    // Click "Xác nhận đã nhận hàng" button
  }
  
  async enterOtp(otp: string) {
    // Enter OTP code
  }
  
  async verifyLoyaltyPoints(expectedPoints: number) {
    // Check loyalty points display
  }
  
  async verifyThankYouMessage() {
    // Check "Cảm ơn quý khách" message (when loyalty OFF)
  }
}
```

### 6.3. Scenario 1: Full Flow (W4-T1, W4-T2)

**File:** `6_Testing/e2e-tests/khachlink-full-order-flow.spec.ts`

```typescript
test.describe('KhachLink Full Order Flow — All Toggles ON @golden', () => {
  test.beforeAll(async () => {
    // Set all toggles ON via API
    const settingsPage = new ShopSettingsPage(request);
    await settingsPage.enableAll();
  });

  test('Complete flow: QR scan → cart → voice note → payment → kitchen → confirm → loyalty', async ({ page }) => {
    // 1. QR scan (có số bàn)
    // 2. Add to cart
    // 3. Voice note (mocked Speech API)
    // 4. Payment: cash flow (ProcessingBar) OR transfer (VietQR + dual status bars)
    // 5. Real-time sync 3s: pending → confirmed → preparing → ready
    // 6. Kitchen: admin nhận đơn → TTS đọc ghi chú → chế biến → sẵn sàng
    // 7. Admin confirm payment + complete order
    // 8. Customer confirm received
    // 9. OTP verification
    // 10. Loyalty points verified
    // 11. PWA install prompt shown
  });
});
```

### 6.4. Scenario 2: Minimal Flow (W4-T3, W4-T4)

**File:** `6_Testing/e2e-tests/khachlink-minimal-flow.spec.ts`

```typescript
test.describe('KhachLink Minimal Flow — Kitchen + Loyalty + Accounting OFF @smoke', () => {
  test.beforeAll(async () => {
    // Set toggles: kitchen=OFF, loyalty=OFF, accounting=OFF, QR_table=OFF, voice=OFF
    const settingsPage = new ShopSettingsPage(request);
    await settingsPage.disableKitchenLoyaltyAccounting();
  });

  test('Minimal flow: QR scan → cart → cash payment → confirmed→completed → thank you', async ({ page }) => {
    // 1. QR scan (không số bàn)
    // 2. Add to cart
    // 3. Payment: cash only (ProcessingBar)
    // 4. Order goes directly confirmed → completed (bypass kitchen)
    // 5. No kitchen statuses in tracking
    // 6. "Cảm ơn quý khách" message (no OTP, no loyalty, no PWA)
    // 7. No accounting sync (verify via API or DB check)
  });
});
```

### 6.5. Mock Web Speech API

```typescript
// In test setup:
await page.addInitScript(() => {
  // Mock SpeechRecognition
  window.SpeechRecognition = class MockSpeechRecognition {
    start() { 
      setTimeout(() => {
        this.onresult?.({ results: [{ 0: { transcript: 'Không đá, ít đường' }, isFinal: true }] });
      }, 100);
    }
    stop() {}
  } as any;
  
  // Mock speechSynthesis
  window.speechSynthesis = {
    speak: () => {},
    cancel: () => {},
  } as any;
});
```

---

## 7. AI HEALTH CHECK MATRIX

### Pre-ANALYZE
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: 8 existing E2E test files, tất cả PARTIAL — không có test full flow (subagent C)
  - Fact 2: `voice-command.spec.ts` SKIPPED do SpeechRecognition API không support (line 51) (subagent C)
  - Fact 3: Page Object Model tồn tại trong `6_Testing/e2e-tests/pages/` — `CustomerPage.ts` có loyalty methods nhưng không test nào gọi (subagent C)
  - Fact 4: `playwright.config.ts` có tiered testing (smoke, golden, full) (subagent C)
  - Fact 5: `omnichannel-order-lifecycle.spec.ts` cover guest checkout → admin → kitchen → tracking → QR payment (subagent C)
- **Assumptions:**
  - Assumption 1: DevLogin pattern hoạt động cho E2E tests (high confidence — existing tests dùng)
  - Assumption 2: Toggle API accessible từ test context (Cần verify auth)
- **Open Questions:**
  - Q1: Toggle API cần auth gì? (Admin token? Customer token?)
  - Q2: Làm sao cleanup order data sau test? (Existing pattern?)
- **Gate check:** Assumptions (2) < Verified Facts (5) → ✅ OK để proceed IMPLEMENT sau khi verify Q1-Q2

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `khachlink-full-order-flow.spec.ts` (new) | No impact — new test | None |
| `khachlink-minimal-flow.spec.ts` (new) | No impact — new test | None |
| `ShopSettingsPage.ts` (new) | No impact — new Page Object | None |
| `VoiceNotePage.ts` (new) | No impact — new Page Object | None |
| `CustomerConfirmPage.ts` (new) | No impact — new Page Object | None |
| `CustomerPage.ts` (modify) | Low — additive methods | None |
| `CheckoutPage.ts` (modify) | Low — additive methods | None |

---

## 9. EXECUTION CHECKLIST

### ANALYZE Phase
- [ ] Read `omnichannel-order-lifecycle.spec.ts` — scenario structure
- [ ] Read 2-3 Page Objects trong `pages/` — class pattern
- [ ] Read `playwright.config.ts` — tiers, timeouts
- [ ] Read `voice-command.spec.ts` — mock pattern
- [ ] Read `qr-payment-ui.spec.ts` + `payment-confirm-flow.spec.ts` — payment test
- [ ] Read `realtime-sync-flow.spec.ts` — polling test
- [ ] Verify toggle API auth + cleanup pattern
- [ ] Update Health Check Matrix

### IMPLEMENT Phase
- [ ] W4-T1: Create `khachlink-full-order-flow.spec.ts` — Scenario 1 skeleton
- [ ] W4-T2: Implement Scenario 1 full steps
- [ ] W4-T3: Create `khachlink-minimal-flow.spec.ts` — Scenario 2 skeleton
- [ ] W4-T4: Implement Scenario 2 minimal steps
- [ ] W4-T5: Create Page Objects (ShopSettingsPage, VoiceNotePage, CustomerConfirmPage)
- [ ] W4-T6: Run E2E tests + fix flaky issues
- [ ] W4-T7: Verify all E2E tests pass

### Post-IMPLEMENT
- [ ] Commit: `[KL WAVE 4] E2E tests — full flow + minimal flow scenarios`
- [ ] Update `project_state.md` — mark KhachLink Full Flow COMPLETE
- [ ] Tag: `khachlink-flow-v1.0`

### Live Runtime Verification (MANDATORY — see Wave 0 lesson)
> E2E tests tự thân là live runtime verification, nhưng cần đảm bảo môi trường boot đúng.
> Playwright tests KHÔNG pass nếu app không boot được — đây là tầng verification cuối cùng.

**Prerequisites:**
- [ ] Docker Desktop running (PostgreSQL 5432 + NATS 4222)
- [ ] ShopERP started on http://localhost:5003 (watch logs: migration + seed OK)
- [ ] KhachLink started on http://localhost:5002 (PWA loads)
- [ ] Gateway started on http://localhost:5001 (YARP forwarding)
- [ ] Playwright browsers installed (`npx playwright install chromium`)
- [ ] All Wave 0-3 features deployed + toggles accessible

**RV tests (all MUST pass):**
- [ ] **RV1 — Full flow E2E (all toggles ON):** `npx playwright test khachlink-full-order-flow.spec.ts` → PASS
  - Guest checkout → payment (cash) → ShopERP kitchen (preparing→ready) → KhachLink OrderTracking polling 3s → customer confirm (delivered) → loyalty modal → accounting entry created
- [ ] **RV2 — Minimal flow E2E (kitchen/loyalty/accounting OFF):** `npx playwright test khachlink-minimal-flow.spec.ts` → PASS
  - Guest checkout → payment (transfer) → kitchen bypass (no preparing/ready) → customer confirm (delivered) → no loyalty modal → no accounting entry
- [ ] **RV3 — Toggle API accessible from test:** Page Object `ShopSettingsPage.setToggle(name, value)` → PUT API trả 200 → verify persist qua GET
- [ ] **RV4 — Speech API mock:** `voice-command.spec.ts` không SKIP → mock `speechSynthesis` + `SpeechRecognition` → test PASS
- [ ] **RV5 — No flaky tests:** Run 3 lần liên tiếp → tất cả PASS (no timeout, no selector flake)
- [ ] **RV6 — Cleanup:** Sau mỗi test → order data cleanup (no leftover rows) → verify `SELECT COUNT(*) FROM Orders WHERE TestId='...'` = 0
- [ ] **RV7 — Playwright config tiers:** `npx playwright test --grep @smoke` → smoke tests PASS (< 30s) → `--grep @golden` → golden tests PASS (< 5min)
- [ ] **RV8 — Page Objects reusable:** `ShopSettingsPage`, `VoiceNotePage`, `CustomerConfirmPage` import OK trong cả 2 spec files (no circular dependency)
- [ ] **RV9 — CI integration:** `scripts/ci-full.ps1` Step 2c (Playwright) → PASS trong CI environment
- [ ] **RV10 — HTML report:** `playwright-report/index.html` generated → all tests green → screenshots attached

**Final sign-off (all MUST be ✅):**
- [ ] Static: Build 0 errors + Architecture Tests PASS + guard-check PASS
- [ ] Live: RV1-RV10 all PASS
- [ ] Cross-wave: Wave 0-3 features still work (no regression)
- [ ] Documentation: `project_state.md` updated + master plan marked COMPLETE
- [ ] Tag: `khachlink-flow-v1.0` pushed
