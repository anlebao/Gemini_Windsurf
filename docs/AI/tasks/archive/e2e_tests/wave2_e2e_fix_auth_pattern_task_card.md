# TASK CARD: E2E Cleanup - Wave 2 - Fix Auth Pattern (Pattern D)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Fix 6 spec files dùng login form không tồn tại (`#username`/`#email`/`#Username` + `waitForURL`) — chuyển sang dùng `storageState` global từ `auth/admin.json`
- **Nghiệp vụ áp dụng:** E2E test auth — ShopERP dùng `/dev/login` (Cookie auth), không có form login trực tiếp
- **Status:** ✅ COMPLETE — Commit `b40b640` on `feature/e2e-cleanup-wave2-fix-auth-pattern`
- **Branch:** `feature/e2e-cleanup-wave2-fix-auth-pattern`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 2 of 8
- **Dependency:** Wave 1 merged; Wave 0 (`auth/admin.json` confirmed)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/e2e_test_cleanup_master_plan.md` (READ)
- `6_Testing/global-setup.ts` (READ — confirm auth/admin.json generation)
- `6_Testing/playwright.config.ts` (READ — confirm storageState L34, L56)
- `6_Testing/e2e-tests/expense-entry-flow.spec.ts` (UPDATE — beforeEach)
- `6_Testing/e2e-tests/export-excel-flow.spec.ts` (UPDATE — beforeEach)
- `6_Testing/e2e-tests/einvoice-dashboard.spec.ts` (UPDATE — beforeEach + test.use)
- `6_Testing/e2e-tests/invoice-management.spec.ts` (UPDATE — beforeEach + test.use)
- `6_Testing/e2e-tests/provider-management.spec.ts` (UPDATE — beforeEach + test.use)
- `6_Testing/e2e-tests/rbac-enforcement.spec.ts` (UPDATE — special: multi-role)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `global-setup.ts` — đã đúng, dùng `/dev/login`
- KHÔNG sửa `playwright.config.ts` — đã apply storageState globally
- KHÔNG tạo auth file mới (trừ rbac multi-role — note trong task)
- KHÔNG sửa test logic bên trong `test(...)` blocks — chỉ sửa `beforeEach` + `test.use`

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Auth Mechanism:** ShopERP dùng Cookie auth qua `/dev/login` — KHÔNG có form login
- [ ] **storageState Global:** `playwright.config.ts` L34, L56 apply `auth/admin.json` — KHÔNG cần override
- [ ] **No Hardcoded Passwords:** Xóa `'VanAn@2026'` hardcoded trong 6 files
- [ ] **Multi-Role (rbac):** Cần `test.use({ storageState: 'auth/<role>.json' })` per test — cần generate thêm auth files
- [ ] **Parse Check:** `npx playwright test --list` pass sau mỗi file

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** 0 `fill('#username'...)` / `fill('#email'...)` / `fill('#Username'...)` còn lại trong 6 files
- [ ] **SC2:** 0 `fill('#password'...)` còn lại
- [ ] **SC3:** 0 `waitForURL('/' | '/dashboard')` sau login form
- [ ] **SC4:** 0 `storageState: { cookies: [], origins: [] }` override (trừ rbac multi-role)
- [ ] **SC5:** 0 hardcoded `'VanAn@2026'` password
- [ ] **SC6:** `rbac-enforcement.spec.ts` dùng `test.use({ storageState: 'auth/<role>.json' })` cho multi-role
- [ ] **SC7:** `npx playwright test --list` pass
- [ ] **SC8:** Test logic bên trong `test(...)` không thay đổi

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Áp dụng cùng pattern fix cho 6 files
- `build-error-analysis` — Fix TS parse error nếu có

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 6
- **Verified Facts:**
  - Fact 1: `global-setup.ts` L94-101 gọi `POST /dev/login`, save `auth/admin.json`
  - Fact 2: `playwright.config.ts` L34 `storageState: 'auth/admin.json'` (global use)
  - Fact 3: `playwright.config.ts` L56 `storageState: 'auth/admin.json'` (e2e-tests project)
  - Fact 4: `#username`, `#email`, `#Username`, `#password` — 0 match trong mọi `.razor` file KhachLink + ShopERP
  - Fact 5: `DevLoginController.cs` tồn tại (L24, L35, L85, L92, L103)
  - Fact 6: `balance-dashboard-flow.spec.ts` comment (L5-9) đã ghi rõ pattern sai
- **Assumptions:**
  - `auth/admin.json` generate được khi services chạy (Gateway, KhachLink, ShopERP healthy)
  - 5 files (trừ rbac) chỉ cần 1 role (admin/owner) → dùng `auth/admin.json` global
- **Open Questions:**
  - Q1: `rbac-enforcement.spec.ts` cần 4 roles (Owner, Staff, StoreKeeper, Guard) — làm sao generate 4 auth files?
    - Option A: Mở rộng `global-setup.ts` để generate 4 files (OUT OF SCOPE — không sửa global-setup)
    - Option B: Dùng `test.skip()` cho rbac tests, note cần generate auth files riêng (RECOMMEND — không break)
    - Option C: Tạo `auth-setup-multirole.ts` riêng (complex)
- **Recommended Action:** PROCEED với Option B cho rbac — skip tests + note, fix 5 files kia trước

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `expense-entry-flow.spec.ts` | Test có thể chạy được (trước đó chắc chắn fail) | Positive impact |
| `export-excel-flow.spec.ts` | Same | Same |
| `einvoice-dashboard.spec.ts` | Same | Same |
| `invoice-management.spec.ts` | Same | Same |
| `provider-management.spec.ts` | Same | Same |
| `rbac-enforcement.spec.ts` | Tests skip nếu không có multi-role auth files | Note trong task — cần generate auth/staff.json etc. riêng |

---

## 9. TDD & TESTING STRATEGY
- **Parse check:** `npx playwright test --list` sau mỗi file
- **Runtime check:** Skip (cần services chạy)
- **Verification:** `npx playwright test --list` pass

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Pattern fix template (5 files đơn giản)
```ts
// BEFORE
test.beforeEach(async ({ page }) => {
  await page.goto('/login');
  await page.fill('#username', 'admin@vanan.vn');  // hoặc #email
  await page.fill('#password', 'VanAn@2026');
  await page.click('button[type="submit"]');
  await page.waitForURL('/');  // hoặc /dashboard
});

// AFTER — xóa toàn bộ beforeEach (storageState global tự apply)
// Nếu file có test.use({ storageState: { cookies: [], origins: [] } }) → xóa luôn
```

### Pattern fix template (rbac-enforcement — special)
```ts
// BEFORE — loginAs helper fill form
async function loginAs(page, username, password = 'VanAn@2026') {
  await page.goto(`${config.SHOPERP_URL}/Login`);
  await page.fill('#Username', username);
  ...
}

// AFTER — Option B: skip tests, note cần multi-role auth
test.skip('Staff login → redirected to KDS', async ({ page }) => {
  test.skip(true, 'Requires auth/staff.json — not generated by global-setup');
});
// Hoặc: dùng test.use({ storageState: 'auth/staff.json' }) per test + note cần generate
```

### Micro-phase breakdown cho Wave 2

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc 6 files, confirm pattern beforeEach<br>- Chốt rbac strategy (Option B: skip + note)<br>- Chốt: xóa `test.use({ storageState: { cookies: [], origins: [] } })` | - Fix 5 files đơn giản (xóa beforeEach + test.use override)<br>- Fix rbac (skip + note)<br>- Run `npx playwright test --list`<br>- Commit |

### Rules
- 1 file tại 1 thời điểm — verify parse trước khi sang file tiếp
- KHÔNG thay đổi test logic bên trong `test(...)` blocks
- rbac: ghi rõ note trong comment về cần generate auth files

---

## 11. ESTIMATED EFFORT
- 1 session (6 files, thay beforeEach + test.use)
- **BLOCKER:** rbac multi-role auth files — cần task riêng để generate (out of scope wave này)
