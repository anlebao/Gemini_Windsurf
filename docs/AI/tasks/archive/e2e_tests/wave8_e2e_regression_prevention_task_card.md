# TASK CARD: E2E Cleanup - Wave 8 - Regression Prevention

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Tạo helper + lint script để ngăn 7 anti-pattern tái xuất hiện trong E2E tests
- **Nghiệp vụ áp dụng:** E2E test long-term hygiene — đảm bảo chất lượng test duy trì sau cleanup
- **Status:** ✅ COMPLETE — Commit `ffe8607` on `feature/e2e-cleanup-wave8-regression-prevention`
- **Branch:** `feature/e2e-cleanup-wave8-regression-prevention`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md`
- **Execution Mode:** IMPLEMENT
- **Current Phase:** Wave 8 of 8 (final)
- **Dependency:** Wave 7 merged (all 7 anti-pattern fixed)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/e2e_test_cleanup_master_plan.md` (READ)
- `6_Testing/e2e-tests/utils/strict-assert.ts` (NEW — helper)
- `6_Testing/e2e-tests/utils/anti-pattern-lint.ts` (NEW — lint script)
- `6_Testing/package.json` (UPDATE — add `lint:e2e` script)
- `6_Testing/e2e-tests/README-OMNICHANNEL.md` (UPDATE — document anti-patterns)
- `docs/AI/project_state.md` (UPDATE — mark complete)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa code C# — chỉ trong `6_Testing/`
- KHÔNG sửa spec files — chỉ tạo helpers + lint
- KHÔNG break existing tests — helper là optional, lint là informational
- KHÔNG thêm dependency mới — dùng Node.js built-in

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Helper Purity:** `strict-assert.ts` chỉ export helper functions, không side effects
- [ ] **Lint Non-Breaking:** `anti-pattern-lint.ts` exit code 0 = pass, 1 = violations found — không crash
- [ ] **No New Dependencies:** Dùng `fs`, `path` built-in — không thêm npm package
- [ ] **Documentation:** Anti-patterns phải document rõ trong README
- [ ] **Parse Check:** `npx playwright test --list` pass

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** `strict-assert.ts` tạo với `assertOneOf(page, locators, opts)` — fail if NONE visible
- [ ] **SC2:** `strict-assert.ts` tạo với `assertVisibleOrSkip(page, locator, reason)` — explicit skip
- [ ] **SC3:** `anti-pattern-lint.ts` tạo — check 7 patterns:
  - `reporter.pass(` (Pattern F)
  - `if (await.*isVisible\(\).*\{` no else (Pattern B)
  - `expect\(.*\|\|.*\).toBeTruthy` (Pattern A)
  - `fill\('#username'|fill\('#email'|fill\('#Username'` (Pattern D)
  - `waitForURL\('/'|waitForURL\('/dashboard'` (Pattern D)
  - `tts-api.example.com` (Pattern G1)
  - `storageState: \{ cookies: \[\], origins: \[\] \}` (Pattern D)
- [ ] **SC4:** `npm run lint:e2e` added to `package.json` — chạy `anti-pattern-lint.ts`
- [ ] **SC5:** `npm run lint:e2e` exit code 0 (0 violations sau Wave 1-7)
- [ ] **SC6:** `README-OMNICHANNEL.md` updated với 7 anti-pattern + cách tránh
- [ ] **SC7:** Smoke test subset pass: `npx playwright test gateway-smoke.spec.ts` (2 test)
- [ ] **SC8:** `project_state.md` updated — E2E cleanup complete
- [ ] **SC9:** `npx playwright test --list` pass

---

## 6. ACTIVE SKILLS (MAX 3)
- `test-system-upgrade` — Tạo helper + lint infrastructure
- `pattern-based-fixing` — Pattern detection trong lint script

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 3
- **Verified Facts:**
  - Fact 1: 7 anti-pattern đã fix trong Wave 1-7
  - Fact 2: `6_Testing/package.json` tồn tại (cần đọc confirm structure)
  - Fact 3: `README-OMNICHANNEL.md` tồn tại (L8190 bytes)
- **Assumptions:**
  - Node.js built-in `fs`/`path` đủ để implement lint (không cần npm package)
  - `npx playwright test gateway-smoke.spec.ts` có thể chạy nếu services up (optional — note nếu không chạy được)
- **Open Questions:**
  - Q1: `package.json` ở `6_Testing/` hay root? (Cần confirm khi đọc)
  - Q2: Smoke test có chạy được không cần services? (Có thể skip nếu services down — note trong task)
- **Recommended Action:** PROCEED — tạo helper + lint

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `strict-assert.ts` (NEW) | File mới — không break | Positive — helper optional |
| `anti-pattern-lint.ts` (NEW) | File mới — không break | Positive — lint informational |
| `package.json` | Add 1 script | OK — không break existing |
| `README-OMNICHANNEL.md` | Add documentation | Positive — guidance |
| `project_state.md` | Update status | OK |

---

## 9. TDD & TESTING STRATEGY
- **Parse check:** `npx playwright test --list` pass
- **Lint check:** `npm run lint:e2e` exit 0
- **Smoke check:** `npx playwright test gateway-smoke.spec.ts` (optional — cần services)
- **Verification:** All 3 checks pass

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Template cho `strict-assert.ts`
```ts
import { Page, expect } from '@playwright/test';

/**
 * Assert that at least one of the locators is visible.
 * Fails if NONE visible — no silent pass.
 */
export async function assertOneOf(
  page: Page,
  selectors: string[],
  opts: { timeout?: number; message?: string } = {}
): Promise<string> {
  const timeout = opts.timeout ?? 5000;
  for (const selector of selectors) {
    const visible = await page.locator(selector).isVisible().catch(() => false);
    if (visible) return selector;
  }
  throw new Error(
    opts.message ?? `None of selectors visible: ${selectors.join(', ')}`
  );
}

/**
 * Assert locator visible, or test.skip with reason.
 */
export async function assertVisibleOrSkip(
  page: Page,
  selector: string,
  reason: string,
  opts: { timeout?: number } = {}
): Promise<boolean> {
  const timeout = opts.timeout ?? 5000;
  const visible = await page.locator(selector).isVisible().catch(() => false);
  if (!visible) {
    test.skip(true, reason);
  }
  return visible;
}
```

### Template cho `anti-pattern-lint.ts`
```ts
import * as fs from 'fs';
import * as path from 'path';

const PATTERNS = [
  { name: 'Pattern F: reporter.pass', regex: /reporter\.pass\(/g },
  { name: 'Pattern B: if(isVisible) no else', regex: /if\s*\(await.*isVisible\(\).*\)\s*\{/g },
  { name: 'Pattern A: OR-tautology', regex: /expect\(.*\|\|.*\)\.toBeTruthy/g },
  { name: 'Pattern D: form login', regex: /fill\('#username'|fill\('#email'|fill\('#Username'/g },
  { name: 'Pattern D: waitForURL root', regex: /waitForURL\('\/'\)|waitForURL\('\/dashboard'\)/g },
  { name: 'Pattern G1: fabricated URL', regex: /tts-api\.example\.com/g },
  { name: 'Pattern D: empty storageState', regex: /storageState:\s*\{\s*cookies:\s*\[\],\s*origins:\s*\[\]\s*\}/g },
];

function lintDir(dir: string): number {
  let violations = 0;
  const files = fs.readdirSync(dir).filter(f => f.endsWith('.spec.ts'));
  for (const file of files) {
    const content = fs.readFileSync(path.join(dir, file), 'utf8');
    for (const { name, regex } of PATTERNS) {
      const matches = content.match(regex);
      if (matches) {
        console.error(`❌ ${file}: ${name} (${matches.length} matches)`);
        violations += matches.length;
      }
    }
  }
  return violations;
}

const violations = lintDir(__dirname);
if (violations === 0) {
  console.log('✅ No anti-pattern violations found');
  process.exit(0);
} else {
  console.error(`❌ ${violations} anti-pattern violations found`);
  process.exit(1);
}
```

### Micro-phase breakdown cho Wave 8

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Confirm `package.json` location<br>- Chốt helper API (assertOneOf, assertVisibleOrSkip)<br>- Chốt 7 lint patterns | - Tạo `strict-assert.ts`<br>- Tạo `anti-pattern-lint.ts`<br>- Add `lint:e2e` to package.json<br>- Run `npm run lint:e2e` (expect 0 violations)<br>- Update README-OMNICHANNEL.md<br>- Update project_state.md<br>- Commit |

### Rules
- Helper là optional — không ép buộc dùng trong existing tests
- Lint exit 1 nếu có violations — nhưng không block CI (informational)
- Document 7 anti-pattern rõ ràng trong README

---

## 11. ESTIMATED EFFORT
- 1 session (2 file mới + 1 script + 1 doc update)
- **BLOCKER:** None — final wave
