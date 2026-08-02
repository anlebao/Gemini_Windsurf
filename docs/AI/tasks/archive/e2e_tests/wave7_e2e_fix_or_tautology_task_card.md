# TASK CARD: E2E Cleanup - Wave 7 - Fix OR-Tautology Assertions (Pattern A)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix 7 test cases dùng `expect(hasX || hasY || hasZ).toBeTruthy()` — chúng pass với bất cứ state nào. Chuyển sang assert cụ thể 1 expected state
- **Nghiệp vụ áp dụng:** E2E test integrity — assertion phải verify state cụ thể, không tautology
- **Status:** ✅ COMPLETE — Commit `108cc58` on `feature/e2e-cleanup-wave7-fix-or-tautology`
- **Branch:** `feature/e2e-cleanup-wave7-fix-or-tautology`
- **Estimated Sessions:** 1-2
- **Risk:** HIGH — cần biết expected state thực tế

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 7 of 8
- **Dependency:** Wave 6 merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/e2e_test_cleanup_master_plan.md` (READ)
- `6_Testing/e2e-tests/period-closing-flow.spec.ts` (UPDATE — 1 test)
- `6_Testing/e2e-tests/audit-trail-flow.spec.ts` (UPDATE — 1 test)
- `6_Testing/e2e-tests/order-flow.spec.ts` (UPDATE — 1 test)
- `6_Testing/e2e-tests/order-tracking.spec.ts` (UPDATE — 2 test)
- `6_Testing/e2e-tests/van-an-dashboard.spec.ts` (UPDATE — 1 test)
- `6_Testing/e2e-tests/qr-payment-ui.spec.ts` (UPDATE — 1 test)
- `5_WebApps/ShopERP/Pages/Accounting/PeriodClosing*.razor` (READ — confirm expected state)
- `5_WebApps/KhachLink/Pages/OrderTracking.razor` (READ — confirm expected state)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa code C# — chỉ sửa test assertion
- KHÔNG xóa test — fix tautology, không xóa
- KHÔNG guess expected state — phải đọc UI code để confirm
- KHÔNG split thành 2 test nếu không cần thiết — ưu tiên pick 1 canonical state

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **No Tautology:** `expect(a || b || c).toBeTruthy()` → MUST replace với specific assertion
- [ ] **State Verification:** Phải đọc UI code (Razor) để biết expected state trước khi assert
- [ ] **Canonical State:** Chọn 1 state cụ thể làm expected — không OR nhiều states
- [ ] **Split If Needed:** Nếu 2 states đều valid (e.g., success OR error) → split thành 2 test riêng
- [ ] **Parse Check:** `npx playwright test --list` pass

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `period-closing-flow.spec.ts` — `validates period before closing`: assert cụ thể validation result card (success text HOẶC error list có row), không `hasSuccess || hasError`
- [ ] **SC2:** `audit-trail-flow.spec.ts` — `Non-admin cannot access`: bỏ `redirectedAway`, chỉ giữ `isLoginPage || isForbidden || hasAccessDenied`
- [ ] **SC3:** `order-flow.spec.ts` — `Customer can place order`: assert cụ thể URL `/order-tracking/{id}` hoặc success message text
- [ ] **SC4:** `order-tracking.spec.ts` — `Checkout redirects`: assert cụ thể URL hoặc tracking element
- [ ] **SC5:** `order-tracking.spec.ts` — `shows order ID in heading`: assert `text?.includes(TEST_ORDER_ID) || text?.includes('Theo dõi')`
- [ ] **SC6:** `van-an-dashboard.spec.ts` — `metrics grid renders`: chọn 1 state (metrics OR spinner), không OR 3 states
- [ ] **SC7:** `qr-payment-ui.spec.ts` — `QR modal contains .qr-image`: hard assert `.qr-image` visible
- [ ] **SC8:** 0 `expect(a || b || c).toBeTruthy()` tautology còn lại
- [ ] **SC9:** `npx playwright test --list` pass

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Áp dụng cùng pattern fix cho 7 test
- `build-error-analysis` — Fix TS parse error nếu có
- `ui-platform-compliance-review` — Verify expected state match UI thật

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `period-closing-flow.spec.ts` L56-58 `hasSuccess || hasError` — bất kỳ `.alert-*` nào trên page đều pass
  - Fact 2: `audit-trail-flow.spec.ts` L150 `redirectedAway = !url.includes('admin/audit-trail')` — true cho mọi redirect kể cả error page
  - Fact 3: `order-flow.spec.ts` L87 `isOnTrackingPage || hasTrackingOrConfirmation` — OR pattern
  - Fact 4: `order-tracking.spec.ts` L72 `expect(text).toBeTruthy()` — heading có ANY text là pass
  - Fact 5: `van-an-dashboard.spec.ts` L73 `hasMetrics || hasSpinner || hasWarning` — luôn true nếu page render
- **Assumptions:**
  - Period closing: validation result card có text cụ thể (cần đọc Razor confirm)
  - Order tracking: URL pattern `/order-tracking/{id}` là expected state
  - Dashboard: metrics grid là expected state (spinner là transient)
- **Open Questions:**
  - Q1: Period closing validation — success text cụ thể là gì? (Cần đọc PeriodClosing Razor)
  - Q2: Order tracking heading — chứa order ID hay "Theo dõi"? (Cần đọc OrderTracking.razor)
  - Q3: Dashboard metrics — có luôn render không hay spinner-first? (Cần đọc VanAnDashboard.razor)
- **Recommended Action:** INVESTIGATE FIRST — đọc Razor files để confirm expected state trước khi fix

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `period-closing-flow.spec.ts` | Test có thể fail nếu expected state sai | Đọc Razor confirm trước |
| `audit-trail-flow.spec.ts` | Test strict hơn — fail nếu redirect không phải login/403 | OK — đó là mục đích |
| `order-flow.spec.ts` | Test fail nếu không redirect đúng | OK — verify order flow thật |
| `order-tracking.spec.ts` | 2 test strict hơn | Đọc Razor confirm |
| `van-an-dashboard.spec.ts` | Test fail nếu metrics không render | Đọc Razor confirm |
| `qr-payment-ui.spec.ts` | Test fail nếu QR image không load | OK — đó là mục đích |

---

## 9. TDD & TESTING STRATEGY
- **Parse check:** `npx playwright test --list` sau mỗi fix
- **Runtime check:** Skip (cần services chạy)
- **Verification:** `npx playwright test --list` pass
- **State Research:** Đọc Razor files TRƯỚC khi fix — không guess

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược: Investigate-Then-Fix
1. Đọc Razor files để biết expected state cho mỗi test
2. Pick canonical state per test
3. Replace OR-tautology với specific assertion
4. Verify parse

### Pattern fix templates

**Template 1: Specific state assertion**
```ts
// BEFORE
expect(hasSuccess || hasError).toBeTruthy();

// AFTER — pick canonical expected state
await expect(page.locator('[class*="alert-success"]')).toBeVisible({ timeout: 5000 });
// OR if error is also valid: split into 2 tests
```

**Template 2: Remove weak condition**
```ts
// BEFORE
const redirectedAway = !currentUrl.includes('admin/audit-trail');
expect(isLoginPage || isForbidden || hasAccessDenied || redirectedAway).toBeTruthy();

// AFTER — remove redirectedAway (true cho mọi redirect)
expect(isLoginPage || isForbidden || hasAccessDenied).toBeTruthy();
```

**Template 3: Specific text assertion**
```ts
// BEFORE
const text = await heading.textContent();
expect(text).toBeTruthy();

// AFTER
const text = await heading.textContent();
expect(text).toMatch(/Theo dõi|Đơn hàng|order/i);
```

### Micro-phase breakdown cho Wave 7

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc PeriodClosing Razor → chốt expected validation state<br>- Đọc OrderTracking.razor → chốt expected heading text<br>- Đọc VanAnDashboard.razor → chốt metrics vs spinner<br>- Chốt canonical state per test | - Fix `period-closing-flow.spec.ts` (1 test)<br>- Fix `audit-trail-flow.spec.ts` (1 test)<br>- Fix `order-flow.spec.ts` (1 test) |
| **S2** | - (Nếu S1 chưa xong) continue | - Fix `order-tracking.spec.ts` (2 test)<br>- Fix `van-an-dashboard.spec.ts` (1 test)<br>- Fix `qr-payment-ui.spec.ts` (1 test)<br>- Run `npx playwright test --list`<br>- Commit |

### Rules
- ĐỌC Razor TRƯỚC khi fix — không guess expected state
- 1 test tại 1 thời điểm — verify parse trước khi sang test tiếp
- Nếu 2 states đều valid → split thành 2 test, không OR
- Hard assert phải có specific selector/text, không generic

---

## 11. ESTIMATED EFFORT
- 1-2 sessions (7 test, cần research expected state)
- **BLOCKER:** Cần đọc Razor files để confirm expected state — không guess
