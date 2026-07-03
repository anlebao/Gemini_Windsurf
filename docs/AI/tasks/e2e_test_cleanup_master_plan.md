# MASTER IMPLEMENTATION PLAN — E2E Test Cleanup (Anti-Pattern Removal)

> **Status:** ACTIVE — Wave 0+1+2+3+4+5 COMPLETE, Wave 6 NEXT
> **Created:** 2026-07-02
> **Last Updated:** 2026-07-03 (Wave 5 complete — 9 reachability tests consolidated into 2, commit `111197e`)
> **Target Workflow:** `newfeaturebuild.md` (ANALYZE → IMPLEMENT)
> **Branch strategy:** `main` → feature branches per wave
> **Execution principle:** Pattern-Based Batch Fix (không case-by-case)
> **Prerequisite:** E2E test review report (verify Phase 7, 2026-07-02)

---

## 0. EXECUTION RULES

### Pattern-Based Batch Fix Strategy
**Nguyên tắc cốt lõi:** Nhóm ~60 test giả/không giá trị thành **7 anti-pattern** + 1 wave phòng ngừa regression. Mỗi wave = 1 loại thao tác cơ học lặp lại, áp dụng `replace_all` / script / template.

**Bước 1: INVESTIGATE & ANALYZE (Đã xong — Review v1 + Verify Phase 7)**
- Đã đọc toàn bộ 20 spec files + 3 page objects + 1 utility
- Đã verify endpoint/UI tồn tại bằng grep (Phase 7)
- Đã phân loại 7 anti-pattern + 8 wave fix

**Bước 2: IMPLEMENT (Execution Phase)**
- Mỗi wave fix 1 pattern cơ học trên nhiều files
- KHÔNG thay đổi approach khi đang implement
- Mỗi wave xong, chạy `npx playwright test --list` (verify parse) + `dotnet build VanAn.sln` (verify không break C#)
- Sau wave cuối: chạy smoke test subset để verify

### Session protocol
1. **Mỗi session chỉ làm 1 wave**
2. **Bắt đầu mỗi session:** Đọc `project_state.md` + task card wave đang làm
3. **Sau khi plan chốt:** Execution Phase
4. **Trước khi session end:** `npx playwright test --list` pass + commit
5. **Sau mỗi wave:** Commit với message format `[E2E-CLEANUP WAVE X] Pattern <name> cleanup`

### Branch protocol
```
main
  └── feature/e2e-cleanup-wave1-remove-reporter-pass
      └── feature/e2e-cleanup-wave2-fix-auth-pattern
          └── feature/e2e-cleanup-wave3-delete-anti-schema-tests
              └── feature/e2e-cleanup-wave4-delete-anti-ui-tests
                  └── feature/e2e-cleanup-wave5-consolidate-smoke-tests
                      └── feature/e2e-cleanup-wave6-fix-silent-skip
                          └── feature/e2e-cleanup-wave7-fix-or-tautology
                              └── feature/e2e-cleanup-wave8-regression-prevention
```
- Mỗi wave có branch riêng
- Merge wave vào branch trước đó
- Final merge vào `main` khi tất cả waves complete

### Hard rules
- **KHÔNG sửa code C#** — toàn bộ thay đổi chỉ trong `6_Testing/e2e-tests/`
- **KHÔNG tạo spec file mới** (trừ Wave 5 gộp smoke + Wave 8 helper)
- **KHÔNG xóa test có giá trị** — chỉ xóa test đã verify fake/anti-schema/anti-UI
- **Mỗi wave phải pass `npx playwright test --list`** (TypeScript parse OK, không syntax error)
- **Playwright runtime test DISABLED** cho Wave 1-7 — chỉ chạy smoke subset sau Wave 8
- **TDD không áp dụng** — đây là cleanup, không phải feature mới
- **Domain layer KHÔNG liên quan** — không sửa `1_Shared/Domain.cs`

### Critical context
- **20 spec files** trong `6_Testing/e2e-tests/`
- **~110 test cases** tổng cộng
- **~60 test cases (55%)** là giả/không giá trị/sẽ fail nếu chạy
- **Auth mechanism:** ShopERP dùng `/dev/login` (Cookie auth) — `global-setup.ts` đã đúng, `auth/admin.json` được apply globally qua `playwright.config.ts` (L34, L56)
- **6 spec files** override `storageState` rồi fill login form không tồn tại → chắc chắn fail
- **VoiceCommand API** return `{ Success: bool }` — test hallucinate schema `{ Command, Executed }`
- **No loyalty UI** trong KhachLink — `omnichannel SCENARIO 2+3` sẽ fail

---

## 0.5. WAVE 0 — PARALLEL: Pre-flight Verification (Non-code, start immediately)

> **Verify nhanh trước khi bắt đầu — đảm bảo môi trường sạch**

### Tasks
| # | Task | Owner | Status |
|---|---|---|---|
| 1 | Confirm `auth/admin.json` được generate bởi `global-setup.ts` (chạy `npx playwright test --list` 1 lần) | AI | ✅ DONE — `global-setup.ts` L111-112 writes `auth/admin.json` via `context.storageState({ path })` |
| 2 | Confirm `playwright.config.ts` apply `storageState: 'auth/admin.json'` globally (L34, L56) | AI | ✅ DONE — L34 (`use.storageState`) + L56 (`e2e-tests` project) confirmed |
| 3 | Confirm `isTierEnabled('e2e')` mechanism — verify env-config.ts | AI | ✅ DONE — `env-config.ts` L359-389, returns `config.ENABLE_E2E` |
| 4 | Snapshot `git status` sạch trước khi bắt đầu Wave 1 | AI | ✅ DONE — committed smoke test state (`797ce36`), tree clean (only pre-existing .vs/ + stray local files) |

### Tracking
- Update `project_state.md` Maintenance Log khi verify xong
- Nếu `auth/admin.json` không generate được → STOP, báo user

---

## 1. CURRENT ISSUES SUMMARY

### Issue 1: Decorative `reporter.pass()` — Noise (Pattern F)
**Status:** ❌ NOISE — Tạo ảo giác 2 lớp verify
**Priority:** 1 (Thấp nhất risk, làm trước để build momentum)
**Count:** ~30 calls across 8 files

**Files liên quan:**
- `accounting-flow.spec.ts`, `audit-trail-flow.spec.ts`, `balance-dashboard-flow.spec.ts`
- `order-flow.spec.ts`, `order-tracking.spec.ts`, `qr-payment.spec.ts`, `qr-payment-ui.spec.ts`
- `van-an-dashboard.spec.ts`, `period-closing-flow.spec.ts`

### Issue 2: Wrong Auth Pattern — 6 files chắc chắn fail (Pattern D)
**Status:** ❌ BROKEN — Fill login form không tồn tại
**Priority:** 2 (Critical — 6 files không chạy được)
**Count:** 6 files, ~25 test cases

**Files liên quan:**
- `expense-entry-flow.spec.ts` (`#username`, `#password`)
- `export-excel-flow.spec.ts` (`#username`, `#password`)
- `einvoice-dashboard.spec.ts` (`#email`, `#password`)
- `invoice-management.spec.ts` (`#email`, `#password`)
- `provider-management.spec.ts` (`#email`, `#password`)
- `rbac-enforcement.spec.ts` (`#Username`, `#Password`)

### Issue 3: Anti-Schema Tests — Test chống API response thật (Pattern G1)
**Status:** ❌ WILL FAIL — Schema mismatch
**Priority:** 3 (High — 6 test hallucinate schema)
**Count:** 6 test cases trong 2 files

**Files liên quan:**
- `voice-command.spec.ts` (4/5 test — `result.Command.CommandText`, `tts-api.example.com`)
- `i18n.spec.ts` (2/5 test — `viResult.Executed`, silent skip)

### Issue 4: Anti-UI Tests — Test chống UI không tồn tại (Pattern G2)
**Status:** ❌ WILL FAIL — Selectors không tồn tại
**Priority:** 4 (High — 2 scenario chống feature không có)
**Count:** 2 test cases trong 1 file

**Files liên quan:**
- `omnichannel-order-lifecycle.spec.ts` (SCENARIO 2: Loyalty, SCENARIO 3: Offline)

### Issue 5: Reachability Smoke — Low-value, gộp được (Pattern C)
**Status:** ❌ LOW VALUE — 9 test chỉ check `status !== 404`
**Priority:** 5 (Medium — gộp 9 → 2)
**Count:** 9 test cases trong 4 files

**Files liên quan:**
- `accounting-flow.spec.ts` (5 test trong Gateway section)
- `order-flow.spec.ts` (2 test)
- `order-tracking.spec.ts` (1 test)
- `qr-payment-ui.spec.ts` (1 test)

### Issue 6: Silent Skip — Pass mà không test (Pattern B)
**Status:** ❌ FAKE — `if(isVisible){...}` no else
**Priority:** 6 (Medium — 10 test pass trắng)
**Count:** ~10 test cases trong 5 files

**Files liên quan:**
- `invoice-management.spec.ts` (2 test acknowledge alert)
- `balance-dashboard-flow.spec.ts` (2 test conditional)
- `van-an-dashboard.spec.ts` (1 test OR-tautology)
- `qr-payment-ui.spec.ts` (1 test OR-tautology)
- `voice-command.spec.ts` (1 test silent skip speech)
- `i18n.spec.ts` (1 test silent skip products)

### Issue 7: OR-Tautology — Pass với bất cứ state (Pattern A)
**Status:** ❌ TAUTOLOGY — `hasX || hasY || hasZ` always true
**Priority:** 7 (Cao nhất risk — cần biết expected state)
**Count:** ~8 test cases trong 5 files

**Files liên quan:**
- `period-closing-flow.spec.ts` (1 test `hasSuccess || hasError`)
- `audit-trail-flow.spec.ts` (1 test `isLogin || isForbidden || hasAccessDenied || redirectedAway`)
- `order-flow.spec.ts` (1 test `isOnTracking || hasConfirmation`)
- `order-tracking.spec.ts` (2 test OR patterns)
- `van-an-dashboard.spec.ts` (1 test `hasMetrics || hasSpinner || hasWarning`)

---

## 2. WAVE 1 — Remove Decorative `reporter.pass()` (Pattern F)

**Branch:** `feature/e2e-cleanup-wave1-remove-reporter-pass`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 1
**Task Card:** `docs/AI/tasks/wave1_e2e_remove_reporter_pass_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W1-T1 | Xóa tất cả `reporter.pass(...)` calls trong `accounting-flow.spec.ts` | `accounting-flow.spec.ts` | ✅ DONE (14 removed) |
| 2 | W1-T2 | Xóa tất cả `reporter.pass(...)` calls trong `audit-trail-flow.spec.ts` | `audit-trail-flow.spec.ts` | ✅ DONE (6 removed) |
| 3 | W1-T3 | Xóa tất cả `reporter.pass(...)` calls trong `balance-dashboard-flow.spec.ts` | `balance-dashboard-flow.spec.ts` | ✅ DONE (4 removed) |
| 4 | W1-T4 | Xóa tất cả `reporter.pass(...)` calls trong `order-flow.spec.ts` | `order-flow.spec.ts` | ✅ DONE (7 removed) |
| 5 | W1-T5 | Xóa tất cả `reporter.pass(...)` calls trong `order-tracking.spec.ts` | `order-tracking.spec.ts` | ✅ DONE (7 removed) |
| 6 | W1-T6 | Xóa tất cả `reporter.pass(...)` calls trong `qr-payment.spec.ts` | `qr-payment.spec.ts` | ✅ DONE (4 removed) |
| 7 | W1-T7 | Xóa tất cả `reporter.pass(...)` calls trong `qr-payment-ui.spec.ts` | `qr-payment-ui.spec.ts` | ✅ DONE (7 removed) |
| 8 | W1-T8 | Xóa tất cả `reporter.pass(...)` calls trong `van-an-dashboard.spec.ts` | `van-an-dashboard.spec.ts` | ✅ DONE (5 removed) |
| 9 | W1-T9 | Xóa tất cả `reporter.pass(...)` calls trong `period-closing-flow.spec.ts` | `period-closing-flow.spec.ts` | ✅ DONE (5 removed) |
| 10 | W1-T10 | Xóa unused `TestReporter` imports + constructor calls nếu không còn dùng | All 9 files | ✅ DONE (0 removed — all 9 files still use `reporter.log`/`setArchitectDecision` in beforeAll) |
| 11 | W1-T11 | Verify `npx playwright test --list` pass (no TS parse error) | Solution-wide | ✅ DONE — 759 tests in 20 files (unchanged) |

### Entry criteria
- [x] Wave 0 complete (verify môi trường)
- [x] Git status clean
- [x] `npx playwright test --list` pass trước khi sửa

### Exit criteria
- [x] 0 `reporter.pass(...)` calls còn lại trong 9 files (4 comment refs preserved — not calls)
- [x] 0 unused `TestReporter` imports (none were unused — all 9 files still reference `reporter` for `.log`/`.setArchitectDecision`)
- [x] `npx playwright test --list` pass — 759 tests in 20 files
- [x] `dotnet build VanAn.sln` pass — 0 errors (.ts-only changes; C# unaffected)
- [x] guard-check.ps1 PASSED
- [x] Committed `0c7965e` on `feature/e2e-cleanup-wave1-remove-reporter-pass`

### Why first
- Risk thấp nhất — chỉ xóa line decorative, không thay đổi assertion logic
- Build momentum cho các wave risk cao hơn
- Giảm noise ngay lập tức

---

## 3. WAVE 2 — Fix Auth Pattern (Pattern D)

**Branch:** `feature/e2e-cleanup-wave2-fix-auth-pattern`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 2
**Task Card:** `docs/AI/tasks/wave2_e2e_fix_auth_pattern_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W2-T1 | Replace `beforeEach` trong `expense-entry-flow.spec.ts` — bỏ form login, dùng storageState global | `expense-entry-flow.spec.ts` | ✅ DONE |
| 2 | W2-T2 | Replace `beforeEach` trong `export-excel-flow.spec.ts` — bỏ form login + skip staff-role test | `export-excel-flow.spec.ts` | ✅ DONE |
| 3 | W2-T3 | Replace `beforeEach` trong `einvoice-dashboard.spec.ts` — bỏ `storageState: { cookies: [], origins: [] }` override + form login + unused imports | `einvoice-dashboard.spec.ts` | ✅ DONE |
| 4 | W2-T4 | Replace `beforeEach` trong `invoice-management.spec.ts` — cùng pattern | `invoice-management.spec.ts` | ✅ DONE |
| 5 | W2-T5 | Replace `beforeEach` trong `provider-management.spec.ts` — cùng pattern | `provider-management.spec.ts` | ✅ DONE |
| 6 | W2-T6 | Replace `beforeEach` trong `rbac-enforcement.spec.ts` — removed loginAs helper; Owner tests use global storageState; 6 multi-role tests skipped with note; unauthenticated test wrapped with empty storageState | `rbac-enforcement.spec.ts` | ✅ DONE |
| 7 | W2-T7 | Verify `npx playwright test --list` pass | Solution-wide | ✅ DONE — 759 tests in 20 files (unchanged) |

### Entry criteria
- [x] Wave 1 merged
- [x] `auth/admin.json` confirmed generate được (Wave 0)
- [x] `playwright.config.ts` L34, L56 confirmed apply storageState globally

### Exit criteria
- [x] 0 `fill('#username'...)` / `fill('#email'...)` / `fill('#Username'...)` còn lại
- [x] 0 `waitForURL('/' | '/dashboard')` sau login form
- [x] 0 `storageState: { cookies: [], origins: [] }` override (trừ rbac unauthenticated test — intentional)
- [x] `rbac-enforcement.spec.ts` multi-role tests skipped with note (Option B — need `auth/<role>.json` generation, separate task)
- [x] `npx playwright test --list` pass — 759 tests in 20 files

### Why second
- 6 files hiện không chạy được — fix auth trước thì các wave sau có thể verify thực tế
- Risk thấp — `auth/admin.json` đã có sẵn, chỉ cần bỏ override
- Special case `rbac-enforcement` cần multi-role — note riêng

---

## 4. WAVE 3 — Delete Anti-Schema Tests (Pattern G1)

**Branch:** `feature/e2e-cleanup-wave3-delete-anti-schema-tests`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 3
**Task Card:** `docs/AI/tasks/wave3_e2e_delete_anti_schema_tests_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W3-T1 | Xóa test `TC_Voice_TextCommand` trong `voice-command.spec.ts` — schema mismatch (`result.Command.CommandText` vs `{ Success }`) | `voice-command.spec.ts` | ✅ DONE |
| 2 | W3-T2 | Xóa test `TC_Voice_TTS` trong `voice-command.spec.ts` — fabricated URL `tts-api.example.com` (API trả `/audio/speech.mp3`) | `voice-command.spec.ts` | ✅ DONE |
| 3 | W3-T3 | Xóa test `TC_Voice_StatusUpdate` trong `voice-command.spec.ts` — schema mismatch (`result.Command.CommandType`) | `voice-command.spec.ts` | ✅ DONE |
| 4 | W3-T4 | Xóa test `TC_Voice_AudioStorage` trong `voice-command.spec.ts` — try/catch swallow, không verify thực | `voice-command.spec.ts` | ✅ DONE |
| 5 | W3-T5 | Xóa test `TC_i18n_VoiceLanguage` trong `i18n.spec.ts` — schema mismatch (`viResult.Executed`) | `i18n.spec.ts` | ✅ DONE |
| 6 | W3-T6 | Giữ lại `TC_Voice_Flow` (silent skip — sẽ fix Wave 6) + `TC_i18n_Switch/Vietnamese/Fallback/LocalizationService/ProductNames` (có thể pass) | — | ✅ DONE (5 i18n tests kept) |
| 7 | W3-T7 | Verify `npx playwright test --list` pass | Solution-wide | ✅ DONE — 729 tests in 20 files (was 759; -30 = 5 × 6 project instances) |

### Entry criteria
- [x] Wave 2 merged
- [x] VoiceCommandController.cs confirmed return `{ Success: bool }` (L78, L112)
- [x] `tts-api.example.com` confirmed không có trong codebase (chỉ trong test + docs)

### Exit criteria
- [x] 4 test trong `voice-command.spec.ts` đã xóa (giữ `TC_Voice_Flow`)
- [x] 1 test trong `i18n.spec.ts` đã xóa (`TC_i18n_VoiceLanguage`)
- [x] 0 reference đến `tts-api.example.com` còn lại (grep e2e-tests/ — no matches)
- [x] 0 reference đến `result.Command.CommandText` / `result.Command.CommandType` / `viResult.Executed` còn lại (grep e2e-tests/ — no matches)
- [x] `npx playwright test --list` pass — 729 tests in 20 files
- [x] `dotnet build VanAn.sln` Release — 0 errors (1000 pre-existing warnings)
- [x] windsurf-guard.js + architecture-guard.ps1 PASSED
- [x] Committed `5f57179` on `feature/e2e-cleanup-wave3-delete-anti-schema-tests`

### Why third
- Test đã verify SẼ FAIL nếu chạy — xóa ngay giảm noise
- Không cần fix (fix = implement feature thật, out of scope)
- Risk thấp — chỉ xóa, không sửa logic

---

## 5. WAVE 4 — Delete Anti-UI Tests (Pattern G2)

**Branch:** `feature/e2e-cleanup-wave4-delete-anti-ui-tests`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 4
**Task Card:** `docs/AI/tasks/wave4_e2e_delete_anti_ui_tests_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W4-T1 | Xóa `SCENARIO 2: Returning Loyalty Customer Flow` trong `omnichannel-order-lifecycle.spec.ts` — selectors `.loyalty-points`, `.points-balance` không tồn tại | `omnichannel-order-lifecycle.spec.ts` | ✅ DONE |
| 2 | W4-T2 | Xóa `SCENARIO 3: Network Interruption / Edge Offline Resiliency` — `.offline-indicator` không có (chỉ PWAInstallPrompt), `setOffline(true)` + `goto()` sẽ fail | `omnichannel-order-lifecycle.spec.ts` | ✅ DONE |
| 3 | W4-T3 | Giữ lại `SCENARIO 1: First-Time Guest Omnichannel Order Flow` (có thể pass — dùng CustomerPage real selectors) | — | ✅ DONE (SCENARIO 1 kept) |
| 4 | W4-T4 | Xóa `TestDataGenerator.generateLoyaltyCustomerData` trong `test-data-cleaner.ts` nếu không còn dùng | `utils/test-data-cleaner.ts` | ✅ DONE (only ref was SCENARIO 2) |
| 5 | W4-T5 | Verify `npx playwright test --list` pass | Solution-wide | ✅ DONE — 715 tests in 20 files (was 729; -14 = 2 × 7 project instances) |

### Entry criteria
- [x] Wave 3 merged
- [x] `loyalty-points`, `points-balance`, `applyLoyaltyPoints` confirmed 0 match trong `5_WebApps/KhachLink`
- [x] `offline-indicator`, `network-status` confirmed chỉ trong `PWAInstallPrompt.razor` (không phải offline indicator thật)

### Exit criteria
- [x] SCENARIO 2 + 3 đã xóa khỏi `omnichannel-order-lifecycle.spec.ts`
- [x] `generateLoyaltyCustomerData` đã xóa (0 reference còn lại)
- [x] `npx playwright test --list` pass — 715 tests in 20 files
- [x] windsurf-guard.js + architecture-guard.ps1 PASSED
- [x] Committed `c1e5c59` on `feature/e2e-cleanup-wave4-delete-anti-ui-tests`
- [ ] Dead code note: `CustomerPage.ts` loyalty methods (`loyaltyPointsDisplay` L44, `getLoyaltyPoints` L191, `applyLoyaltyPoints` L201) now unreferenced — out of Wave 4 scope, candidate for future page-object cleanup

### Why fourth
- 2 scenario đã verify SẼ FAIL — xóa giảm noise
- Không cần fix (fix = implement loyalty program + offline indicator, out of scope)

---

## 6. WAVE 5 — Consolidate Reachability Smoke Tests (Pattern C)

**Branch:** `feature/e2e-cleanup-wave5-consolidate-smoke-tests`
**Estimated sessions:** 1
**Conflict risk:** MEDIUM
**Priority:** 5
**Task Card:** `docs/AI/tasks/wave5_e2e_consolidate_smoke_tests_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W5-T1 | Tạo `gateway-smoke.spec.ts` mới — gộp 5 test Gateway Accounting API trong `accounting-flow.spec.ts` thành 1 test dùng `test.step()` | `gateway-smoke.spec.ts` (NEW) | ✅ DONE |
| 2 | W5-T2 | Thêm vào `gateway-smoke.spec.ts` — gộp 2 test `order-flow.spec.ts` (Order API, Inventory API) + 1 test `order-tracking.spec.ts` (Order Status API) + 1 test `qr-payment-ui.spec.ts` (VietQR Generate) | `gateway-smoke.spec.ts` | ✅ DONE |
| 3 | W5-T3 | Xóa 5 test Gateway Accounting API section trong `accounting-flow.spec.ts` (L235-303) | `accounting-flow.spec.ts` | ✅ DONE |
| 4 | W5-T4 | Xóa 2 test reachability trong `order-flow.spec.ts` (L137, L146) | `order-flow.spec.ts` | ✅ DONE |
| 5 | W5-T5 | Xóa 1 test reachability trong `order-tracking.spec.ts` (L156) | `order-tracking.spec.ts` | ✅ DONE |
| 6 | W5-T6 | Xóa 1 test reachability trong `qr-payment-ui.spec.ts` (L144) | `qr-payment-ui.spec.ts` | ✅ DONE (kept `supported-banks returns list` — validates body) |
| 7 | W5-T7 | Verify `npx playwright test --list` pass — confirm 9 test → 2 test | Solution-wide | ✅ DONE — 668 tests in 21 files (was 715 in 20; +1 new file) |

### Entry criteria
- [x] Wave 4 merged
- [x] Gateway routes confirmed tồn tại (LocalizationController, VoiceCommandController, AccountingEntriesController)

### Exit criteria
- [x] `gateway-smoke.spec.ts` tạo mới với 2 test (accounting routes, order/qr routes)
- [x] 9 test reachability cũ đã xóa khỏi 4 files
- [x] Mỗi smoke test dùng `test.step()` cho từng route, assert `status !== 404 && !== 500`
- [x] `npx playwright test --list` pass — 668 tests in 21 files
- [x] windsurf-guard.js + architecture-guard.ps1 PASSED
- [x] Committed `111197e` on `feature/e2e-cleanup-wave5-consolidate-smoke-tests`

### Why fifth
- Sau khi xóa test fake (Wave 3+4), gộp smoke để giảm trùng lặp
- Risk medium — tạo file mới, cần verify parse

---

## 7. WAVE 6 — Fix Silent Skip (Pattern B)

**Branch:** `feature/e2e-cleanup-wave6-fix-silent-skip`
**Estimated sessions:** 1
**Conflict risk:** MEDIUM
**Priority:** 6
**Task Card:** `docs/AI/tasks/wave6_e2e_fix_silent_skip_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W6-T1 | Fix `invoice-management.spec.ts` — `should acknowledge a single alert`: bỏ `if(isVisible)`, dùng `test.skip()` hoặc hard assert | `invoice-management.spec.ts` | PENDING |
| 2 | W6-T2 | Fix `invoice-management.spec.ts` — `should acknowledge all alerts`: cùng pattern | `invoice-management.spec.ts` | PENDING |
| 3 | W6-T3 | Fix `balance-dashboard-flow.spec.ts` — `should show warning when expenses exceed threshold`: hard assert warning hoặc `test.skip()` | `balance-dashboard-flow.spec.ts` | PENDING |
| 4 | W6-T4 | Fix `balance-dashboard-flow.spec.ts` — `should display balance grid with account details`: hard assert detail section | `balance-dashboard-flow.spec.ts` | PENDING |
| 5 | W6-T5 | Fix `voice-command.spec.ts` — `TC_Voice_Flow`: bỏ `if(supportsSpeech) else console.log`, dùng `test.skip(!supportsSpeech)` | `voice-command.spec.ts` | PENDING |
| 6 | W6-T6 | Fix `i18n.spec.ts` — `TC_i18n_ProductNames`: bỏ `if(products.length > 0)`, hard assert hoặc `test.skip()` | `i18n.spec.ts` | PENDING |
| 7 | W6-T7 | Verify `npx playwright test --list` pass | Solution-wide | PENDING |

### Entry criteria
- [ ] Wave 5 merged

### Exit criteria
- [ ] 0 `if(await x.isVisible()) { ... }` không có else/skip còn lại trong 6 test
- [ ] Mỗi test hoặc hard assert (`expect.toBeVisible`) hoặc `test.skip(condition, reason)`
- [ ] `npx playwright test --list` pass

### Why sixth
- Sau khi xóa fake + gộp smoke, fix silent skip để test còn lại có giá trị
- Risk medium — cần chọn giữa hard assert vs skip per test

---

## 8. WAVE 7 — Fix OR-Tautology Assertions (Pattern A)

**Branch:** `feature/e2e-cleanup-wave7-fix-or-tautology`
**Estimated sessions:** 1-2
**Conflict risk:** HIGH
**Priority:** 7
**Task Card:** `docs/AI/tasks/wave7_e2e_fix_or_tautology_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W7-T1 | Fix `period-closing-flow.spec.ts` — `validates period before closing`: bỏ `hasSuccess \|\| hasError`, assert cụ thể validation result card text | `period-closing-flow.spec.ts` | PENDING |
| 2 | W7-T2 | Fix `audit-trail-flow.spec.ts` — `Non-admin cannot access`: bỏ `redirectedAway` (true cho mọi redirect), chỉ giữ `isLoginPage \|\| isForbidden \|\| hasAccessDenied` | `audit-trail-flow.spec.ts` | PENDING |
| 3 | W7-T3 | Fix `order-flow.spec.ts` — `Customer can place order`: assert cụ thể URL `/order-tracking/{id}` hoặc success message text | `order-flow.spec.ts` | PENDING |
| 4 | W7-T4 | Fix `order-tracking.spec.ts` — `Checkout redirects`: assert cụ thể URL hoặc tracking element | `order-tracking.spec.ts` | PENDING |
| 5 | W7-T5 | Fix `order-tracking.spec.ts` — `shows order ID in heading`: assert `text?.includes(TEST_ORDER_ID) \|\| text?.includes('Theo dõi')` | `order-tracking.spec.ts` | PENDING |
| 6 | W7-T6 | Fix `van-an-dashboard.spec.ts` — `metrics grid renders`: chọn 1 state cụ thể (metrics OR spinner), không OR 3 states | `van-an-dashboard.spec.ts` | PENDING |
| 7 | W7-T7 | Fix `qr-payment-ui.spec.ts` — `QR modal contains .qr-image`: hard assert `.qr-image` visible (sau khi modal mở) | `qr-payment-ui.spec.ts` | PENDING |
| 8 | W7-T8 | Verify `npx playwright test --list` pass | Solution-wide | PENDING |

### Entry criteria
- [ ] Wave 6 merged
- [ ] Expected state cho mỗi test đã research (cần biết UI thực tế render gì)

### Exit criteria
- [ ] 0 `expect(a \|\| b \|\| c).toBeTruthy()` tautology còn lại
- [ ] Mỗi test assert cụ thể 1 expected state (hoặc split thành 2 test nếu 2 states đều valid)
- [ ] `npx playwright test --list` pass

### Why last (before regression prevention)
- Risk cao nhất — cần biết expected state thực tế
- Để sau cùng để có context đầy đủ từ các wave trước

---

## 9. WAVE 8 — Regression Prevention

**Branch:** `feature/e2e-cleanup-wave8-regression-prevention`
**Estimated sessions:** 1
**Conflict risk:** LOW
**Priority:** 8
**Task Card:** `docs/AI/tasks/wave8_e2e_regression_prevention_task_card.md`

### Tasks
| # | Task ID | Task | Files | Status |
|---|---|---|---|---|
| 1 | W8-T1 | Tạo `utils/strict-assert.ts` helper — `assertOneOf(page, locators, opts)` fail if NONE visible | `utils/strict-assert.ts` (NEW) | PENDING |
| 2 | W8-T2 | Tạo `utils/anti-pattern-lint.ts` — script check `reporter.pass`, `if(isVisible)` no else, OR-tautology | `utils/anti-pattern-lint.ts` (NEW) | PENDING |
| 3 | W8-T3 | Add lint script vào `package.json` — `npm run lint:e2e` chạy anti-pattern check | `package.json` | PENDING |
| 4 | W8-T4 | Update `README-OMNICHANNEL.md` — document 7 anti-pattern + cách tránh | `README-OMNICHANNEL.md` | PENDING |
| 5 | W8-T5 | Run smoke test subset (`npx playwright test gateway-smoke.spec.ts`) — verify 2 test pass | Solution-wide | PENDING |
| 6 | W8-T6 | Update `project_state.md` — mark E2E cleanup complete | `docs/AI/project_state.md` | PENDING |

### Entry criteria
- [ ] Wave 7 merged
- [ ] All 7 anti-pattern đã fix

### Exit criteria
- [ ] `strict-assert.ts` helper tạo + documented
- [ ] `anti-pattern-lint.ts` script chạy pass (0 violation)
- [ ] `npm run lint:e2e` added to package.json
- [ ] Smoke test subset pass (2 test trong `gateway-smoke.spec.ts`)
- [ ] `project_state.md` updated
- [ ] Ready for production E2E suite

### Why last
- Phòng ngừa regression sau khi tất cả fix xong
- Helper + lint đảm bảo pattern không tái xuất hiện

---

## 10. CROSS-WAVE CONCERNS

### Test Quality Protection
- **KHÔNG xóa test có giá trị** — chỉ xóa test đã verify fake/anti-schema/anti-UI
- **Mỗi wave phải pass `npx playwright test --list`** — TypeScript parse OK
- **Playwright runtime DISABLED** cho Wave 1-7 — chỉ chạy smoke sau Wave 8

### Auth Strategy
- `global-setup.ts` đã đúng — dùng `/dev/login`, save `auth/admin.json`
- `playwright.config.ts` apply `storageState` globally (L34, L56)
- 6 files override bằng `storageState: { cookies: [], origins: [] }` → bỏ override
- `rbac-enforcement.spec.ts` cần multi-role — note trong task card (cần generate `auth/staff.json`, `auth/owner.json`, etc. hoặc dùng `test.use({ storageState: ... })` per test)

### File Boundary
- **KHÔNG sửa code C#** — toàn bộ thay đổi chỉ trong `6_Testing/e2e-tests/`
- **KHÔNG sửa `global-setup.ts` hoặc `playwright.config.ts`** (trừ khi cần thêm auth files cho rbac)
- **KHÔNG tạo spec file mới** (trừ Wave 5 `gateway-smoke.spec.ts` + Wave 8 helpers)

### Testing Strategy
- **Parse check:** `npx playwright test --list` sau mỗi wave
- **Runtime check:** Chỉ sau Wave 8 — chạy `gateway-smoke.spec.ts` subset
- **Full E2E run:** Out of scope — cần services chạy (Gateway, KhachLink, ShopERP)

### Regression Prevention
- Wave 8 tạo helper + lint script
- `npm run lint:e2e` chạy trong CI (optional — note cho user)

---

## 11. APPROVAL CHECKLIST

- [ ] Master plan reviewed (v1 — 8 waves, 7 anti-pattern + 1 regression prevention)
- [ ] 8 task cards reviewed (Wave 1-8)
- [ ] E2E test review report reviewed (~60/110 test fake/no-value)
- [ ] Verify Phase 7 complete (VoiceCommand schema, loyalty UI, auth pattern)
- [ ] `auth/admin.json` confirmed generate được (Wave 0)
- [ ] `playwright.config.ts` storageState confirmed (L34, L56)
- [ ] Branch strategy confirmed (8 feature branches)
- [ ] Sẵn sàng implement Wave 1

---

## 12. EFFORT SUMMARY

| Wave | Description | Sessions | Risk |
|---|---|---|---|
| Wave 0 | Pre-flight verification (parallel, non-code) | 0.5 | None |
| Wave 1 | Remove `reporter.pass` (Pattern F) | 1 | Low |
| Wave 2 | Fix auth pattern (Pattern D) | 1 | Low |
| Wave 3 | Delete anti-schema tests (Pattern G1) | 1 | Low |
| Wave 4 | Delete anti-UI tests (Pattern G2) | 1 | Low |
| Wave 5 | Consolidate smoke tests (Pattern C) | 1 | Medium |
| Wave 6 | Fix silent skip (Pattern B) | 1 | Medium |
| Wave 7 | Fix OR-tautology (Pattern A) | 1-2 | High |
| Wave 8 | Regression prevention | 1 | Low |
| **Total** | | **8-9 sessions** | |

**Critical path:** Wave 0 → Wave 1 → Wave 2 → Wave 3 → Wave 4 → Wave 5 → Wave 6 → Wave 7 → Wave 8
**Parallel path:** None (sequential — mỗi wave build trên wave trước)

**Reduction target:**
- Before: ~110 test cases, ~60 fake/no-value (55%)
- After: ~50 test cases, all with real assertions (100% value)
- Noise reduction: ~55%
