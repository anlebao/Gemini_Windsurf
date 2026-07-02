# TASK CARD: E2E Cleanup - Wave 5 - Consolidate Reachability Smoke Tests (Pattern C)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Gộp 9 reachability smoke tests (chỉ check `status !== 404`) thành 2 test tập trung dùng `test.step()` — giảm trùng lặp, giữ giá trị smoke
- **Nghiệp vụ áp dụng:** E2E test hygiene — smoke test nên gọn, 1 test cho 1 nhóm routes
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/e2e-cleanup-wave5-consolidate-smoke-tests`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 5 of 8
- **Dependency:** Wave 4 merged

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/e2e_test_cleanup_master_plan.md` (READ)
- `6_Testing/e2e-tests/gateway-smoke.spec.ts` (NEW — tạo file gộp)
- `6_Testing/e2e-tests/accounting-flow.spec.ts` (UPDATE — xóa 5 test Gateway section L235-303)
- `6_Testing/e2e-tests/order-flow.spec.ts` (UPDATE — xóa 2 test L137, L146)
- `6_Testing/e2e-tests/order-tracking.spec.ts` (UPDATE — xóa 1 test L156)
- `6_Testing/e2e-tests/qr-payment-ui.spec.ts` (UPDATE — xóa 1 test L144)
- `6_Testing/utils/env-config.ts` (READ — confirm `loadEnvConfig` export)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa code C# — chỉ tạo/sửa spec files
- KHÔNG xóa test có giá trị (chỉ xóa reachability smoke)
- KHÔNG tạo smoke test cho routes không tồn tại (đã verify Wave 3+4)
- KHÔNG thay đổi `playwright.config.ts` — file mới tự được pick up bởi `testMatch: '**/*.spec.ts'`

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Route Existence:** Tất cả routes trong smoke test đã verify tồn tại (LocalizationController, VoiceCommandController, AccountingEntriesController)
- [ ] **Status Acceptance:** Smoke test accept `200 | 401 | 403` (auth required) — KHÔNG accept `404` (route missing) hoặc `500` (crash)
- [ ] **test.step() Pattern:** Mỗi route là 1 step trong test — fail 1 route không skip routes khác
- [ ] **Parse Check:** `npx playwright test --list` pass

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `gateway-smoke.spec.ts` tạo mới với 2 test:
  - Test 1: Accounting routes (`/api/accounting-entries`, `/api/accounting`, `/api/accounting/revenue`, `/api/accounting/expense`, `/api/accounting/revenue/summary`)
  - Test 2: Order/QR routes (`/api/orders`, `/api/inventory/check`, `/api/orders/{id}`, `/api/v1/vietqr/generate`)
- [ ] **SC2:** 5 test Gateway Accounting API section đã xóa khỏi `accounting-flow.spec.ts` (L235-303)
- [ ] **SC3:** 2 test reachability đã xóa khỏi `order-flow.spec.ts` (L137, L146)
- [ ] **SC4:** 1 test reachability đã xóa khỏi `order-tracking.spec.ts` (L156)
- [ ] **SC5:** 1 test reachability đã xóa khỏi `qr-payment-ui.spec.ts` (L144)
- [ ] **SC6:** Mỗi step trong smoke test assert `status !== 404 && status !== 500`
- [ ] **SC7:** `npx playwright test --list` pass — confirm 9 test → 2 test

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Gộp test theo route group
- `build-error-analysis` — Fix TS parse error nếu có

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 4
- **Verified Facts:**
  - Fact 1: 5 test trong `accounting-flow.spec.ts` L238-302 — Gateway Accounting API alias
  - Fact 2: 2 test trong `order-flow.spec.ts` L137, L146 — Order + Inventory API
  - Fact 3: 1 test trong `order-tracking.spec.ts` L156 — Order Status API
  - Fact 4: 1 test trong `qr-payment-ui.spec.ts` L144 — VietQR Generate API
- **Assumptions:**
  - Tất cả routes tồn tại (verified Wave 3+4 — controllers confirmed)
  - `loadEnvConfig` export `GATEWAY_URL` (cần confirm khi đọc env-config.ts)
- **Open Questions:**
  - Q1: `gateway-smoke.spec.ts` có cần auth không? (Recommend: dùng storageState global — auth/admin.json)
- **Recommended Action:** PROCEED — gộp 9 test thành 2

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `gateway-smoke.spec.ts` (NEW) | File mới — không break gì | Positive — smoke tập trung |
| `accounting-flow.spec.ts` | Mất 5 test Gateway section | OK — đã gộp vào gateway-smoke |
| `order-flow.spec.ts` | Mất 2 test reachability | OK — đã gộp |
| `order-tracking.spec.ts` | Mất 1 test reachability | OK — đã gộp |
| `qr-payment-ui.spec.ts` | Mất 1 test reachability | OK — đã gộp |

---

## 9. TDD & TESTING STRATEGY
- **Parse check:** `npx playwright test --list` sau tạo file + sau xóa
- **Runtime check:** Skip (cần services chạy)
- **Verification:** `npx playwright test --list` pass + confirm 9 → 2 test

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Template cho `gateway-smoke.spec.ts`
```ts
import { test, expect } from '@playwright/test';
import { loadEnvConfig } from '../utils/env-config';

const config = loadEnvConfig();

test.describe('Gateway Routes Smoke Tests', () => {
  test('Accounting API routes are registered', async ({ request }) => {
    const routes = [
      '/api/accounting-entries',
      '/api/accounting',
      '/api/accounting/revenue/summary?year=2026&month=7',
    ];
    for (const route of routes) {
      await test.step(`GET ${route}`, async () => {
        const r = await request.get(`${config.GATEWAY_URL}${route}`);
        expect(r.status(), `${route} should not 404`).not.toBe(404);
        expect(r.status(), `${route} should not 500`).not.toBe(500);
      });
    }
    // POST routes
    await test.step('POST /api/accounting/revenue', async () => {
      const r = await request.post(`${config.GATEWAY_URL}/api/accounting/revenue`, {
        data: { year: 2026, month: 7, amount: 100000, description: 'smoke' },
      });
      expect(r.status()).not.toBe(404);
      expect(r.status()).not.toBe(500);
    });
  });

  test('Order/QR API routes are registered', async ({ request }) => {
    await test.step('GET /api/orders', async () => {
      const r = await request.get(`${config.GATEWAY_URL}/api/orders`);
      expect(r.status()).not.toBe(404);
      expect(r.status()).not.toBe(500);
    });
    // ... other routes
  });
});
```

### Micro-phase breakdown cho Wave 5

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Confirm `loadEnvConfig` export `GATEWAY_URL`<br>- Chốt 2 test groups (accounting, order/qr)<br>- Chốt route list per group | - Tạo `gateway-smoke.spec.ts`<br>- Xóa 5 test trong accounting-flow.spec.ts<br>- Xóa 2 test trong order-flow.spec.ts<br>- Xóa 1 test trong order-tracking.spec.ts<br>- Xóa 1 test trong qr-payment-ui.spec.ts<br>- Run `npx playwright test --list`<br>- Commit |

### Rules
- Tạo file mới trước, xóa test cũ sau — tránh trạng thái intermediate không có smoke
- Mỗi route là 1 `test.step()` — fail 1 route không skip routes khác
- Verify parse sau mỗi thao tác

---

## 11. ESTIMATED EFFORT
- 1 session (1 file mới + xóa 9 test trong 4 files)
- **BLOCKER:** None
