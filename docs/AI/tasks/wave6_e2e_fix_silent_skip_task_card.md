# TASK CARD: E2E Cleanup - Wave 6 - Fix Silent Skip (Pattern B)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix 6 test cases dùng `if(isVisible){...}` không có else — chúng pass trắng khi feature missing. Chuyển sang hard assert hoặc `test.skip(condition, reason)`
- **Nghiệp vụ áp dụng:** E2E test integrity — test phải fail hoặc skip explicitly, không pass silently
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/e2e-cleanup-wave6-fix-silent-skip`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 6 of 8
- **Dependency:** Wave 5 merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/e2e_test_cleanup_master_plan.md` (READ)
- `6_Testing/e2e-tests/invoice-management.spec.ts` (UPDATE — 2 test acknowledge alert)
- `6_Testing/e2e-tests/balance-dashboard-flow.spec.ts` (UPDATE — 2 test conditional)
- `6_Testing/e2e-tests/voice-command.spec.ts` (UPDATE — 1 test speech skip)
- `6_Testing/e2e-tests/i18n.spec.ts` (UPDATE — 1 test products skip)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa code C# — chỉ sửa test logic
- KHÔNG xóa test — fix pattern, không xóa
- KHÔNG thay đổi test name hoặc test.describe structure
- KHÔNG thêm feature mới — chỉ fix assertion pattern

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **No Silent Pass:** `if(isVisible){...}` không có else → MUST convert thành hard assert hoặc `test.skip()`
- [ ] **test.skip() Pattern:** `test.skip(condition, 'reason')` — explicit skip với reason
- [ ] **Hard Assert Pattern:** `await expect(locator).toBeVisible()` — fail nếu missing
- [ ] **Decision Per Test:** Chọn hard assert (fail) hay skip (explicit) dựa trên feature có nên có data không
- [ ] **Parse Check:** `npx playwright test --list` pass

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `invoice-management.spec.ts` — `should acknowledge a single alert`: hard assert `acknowledgeBtn.toBeVisible` hoặc `test.skip`
- [ ] **SC2:** `invoice-management.spec.ts` — `should acknowledge all alerts`: same pattern
- [ ] **SC3:** `balance-dashboard-flow.spec.ts` — `should show warning when expenses exceed threshold`: hard assert warning hoặc `test.skip`
- [ ] **SC4:** `balance-dashboard-flow.spec.ts` — `should display balance grid with account details`: hard assert detail section
- [ ] **SC5:** `voice-command.spec.ts` — `TC_Voice_Flow`: `test.skip(!supportsSpeech, 'Browser does not support speech recognition')`
- [ ] **SC6:** `i18n.spec.ts` — `TC_i18n_ProductNames`: hard assert products hoặc `test.skip`
- [ ] **SC7:** 0 `if(await x.isVisible()) { ... }` không có else/skip còn lại trong 6 test
- [ ] **SC8:** `npx playwright test --list` pass

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Áp dụng cùng pattern fix cho 6 test
- `build-error-analysis` — Fix TS parse error nếu có

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: `invoice-management.spec.ts` L118 `if (await acknowledgeBtn.isVisible()) { ... }` — no else
  - Fact 2: `balance-dashboard-flow.spec.ts` L54 `const isWarningVisible = await warning.isVisible().catch(() => false)` — pass trắng
  - Fact 3: `voice-command.spec.ts` L52 `if (supportsSpeech) {...} else { console.log('skipping') }` — report PASS trên browser không hỗ trợ
  - Fact 4: `i18n.spec.ts` L188 `if (viProducts.length > 0) {...}` — silently pass nếu không có product
- **Assumptions:**
  - Alert acknowledge tests: feature có nên có alerts không? (Nếu có → hard assert; nếu tùy data → skip)
  - Balance warning: chỉ xuất hiện khi expense > 150% revenue → skip hợp lý (data-dependent)
- **Open Questions:**
  - Q1: Alert acknowledge — hard assert hay skip? (Recommend: skip — alerts là data-dependent, có thể không có alert nào)
  - Q2: Balance warning — hard assert hay skip? (Recommend: skip — warning chỉ xuất hiện với data cụ thể)
  - Q3: Voice speech — skip (đã rõ) hay hard assert? (Recommend: skip — browser-dependent)
  - Q4: i18n products — hard assert hay skip? (Recommend: hard assert — products nên có trong test DB)
- **Recommended Action:** PROCEED — fix per Q1-Q4 recommendations

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `invoice-management.spec.ts` | Test có thể skip thay vì pass trắng | Positive — explicit skip > silent pass |
| `balance-dashboard-flow.spec.ts` | Same | Same |
| `voice-command.spec.ts` | Test skip explicit trên browser không hỗ trợ | Positive |
| `i18n.spec.ts` | Test fail nếu không có products (hard assert) | OK — products nên có trong test DB |

---

## 9. TDD & TESTING STRATEGY
- **Parse check:** `npx playwright test --list` sau mỗi fix
- **Runtime check:** Skip
- **Verification:** `npx playwright test --list` pass

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Pattern fix templates

**Template 1: test.skip() (cho data-dependent features)**
```ts
// BEFORE
test('should acknowledge a single alert', async ({ page }) => {
  const acknowledgeBtn = page.locator('[data-testid="btn-acknowledge"]').first();
  if (await acknowledgeBtn.isVisible()) {
    await acknowledgeBtn.click();
    await expect(page.locator('.badge-success').first()).toBeVisible();
  }
});

// AFTER
test('should acknowledge a single alert', async ({ page }) => {
  const acknowledgeBtn = page.locator('[data-testid="btn-acknowledge"]').first();
  const hasAlerts = await acknowledgeBtn.isVisible().catch(() => false);
  test.skip(!hasAlerts, 'No alerts to acknowledge — feature not testable in this state');
  await acknowledgeBtn.click();
  await expect(page.locator('.badge-success').first()).toBeVisible();
});
```

**Template 2: Hard assert (cho features nên có)**
```ts
// BEFORE
if (viProducts.length > 0) {
  const firstProduct = viProducts[0];
  expect(firstProduct).toHaveProperty('Name');
}

// AFTER
expect(viProducts.length, 'Products should exist in test DB').toBeGreaterThan(0);
const firstProduct = viProducts[0];
expect(firstProduct).toHaveProperty('Name');
expect(firstProduct.Name).toBeTruthy();
```

### Micro-phase breakdown cho Wave 6

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Chốt per test: hard assert vs skip (Q1-Q4)<br>- Chốt template pattern | - Fix 2 test trong invoice-management (skip)<br>- Fix 2 test trong balance-dashboard (skip)<br>- Fix 1 test trong voice-command (skip)<br>- Fix 1 test trong i18n (hard assert)<br>- Run `npx playwright test --list`<br>- Commit |

### Rules
- 1 test tại 1 thời điểm — verify parse trước khi sang test tiếp
- `test.skip(condition, reason)` — reason phải rõ ràng
- Hard assert phải có message: `expect(value, 'description').toBe...`

---

## 11. ESTIMATED EFFORT
- 1 session (6 test, thay if/else pattern)
- **BLOCKER:** None
