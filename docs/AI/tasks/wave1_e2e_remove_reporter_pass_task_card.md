# TASK CARD: E2E Cleanup - Wave 1 - Remove Decorative `reporter.pass()` (Pattern F)

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** Xóa toàn bộ `reporter.pass(...)` calls trong 9 spec files — chúng là decorative noise, tạo ảo giác 2 lớp verify trong khi `expect()` đã verify xong
- **Nghiệp vụ áp dụng:** E2E test hygiene — giảm noise, giảm false sense of security
- **Status:** PENDING — Planning & Approval
- **Branch:** `feature/e2e-cleanup-wave1-remove-reporter-pass`
- **Estimated Sessions:** 1

---

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `newfeaturebuild.md` (cleanup — không phải feature mới)
- **Execution Mode:** IMPLEMENT (plan đã chốt trong master plan)
- **Current Phase:** Wave 1 of 8
- **Dependency:** Wave 0 (pre-flight verification)

---

## 3. RELEVANT FILES (CONTEXT BOUNDARY)

### Files được phép đọc/sửa
- `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
- `docs/AI/tasks/e2e_test_cleanup_master_plan.md` (READ)
- `6_Testing/e2e-tests/accounting-flow.spec.ts` (UPDATE — xóa reporter.pass)
- `6_Testing/e2e-tests/audit-trail-flow.spec.ts` (UPDATE)
- `6_Testing/e2e-tests/balance-dashboard-flow.spec.ts` (UPDATE)
- `6_Testing/e2e-tests/order-flow.spec.ts` (UPDATE)
- `6_Testing/e2e-tests/order-tracking.spec.ts` (UPDATE)
- `6_Testing/e2e-tests/qr-payment.spec.ts` (UPDATE)
- `6_Testing/e2e-tests/qr-payment-ui.spec.ts` (UPDATE)
- `6_Testing/e2e-tests/van-an-dashboard.spec.ts` (UPDATE)
- `6_Testing/e2e-tests/period-closing-flow.spec.ts` (UPDATE)

### Boundary Rules (Nghiêm cấm)
- KHÔNG sửa `expect()` assertions — chỉ xóa `reporter.pass(...)` line
- KHÔNG sửa `reporter.log(...)` — log là OK (không phải assertion giả)
- KHÔNG sửa `reporter.setArchitectDecision(...)` — decision tracking là OK
- KHÔNG sửa code C# — chỉ trong `6_Testing/e2e-tests/`
- KHÔNG xóa `TestReporter` import nếu file còn dùng `reporter.log` hoặc `reporter.setArchitectDecision`

---

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **Assertion Purity:** `reporter.pass()` KHÔNG phải assertion — xóa không làm test fail
- [ ] **Import Cleanup:** Nếu sau khi xóa, `TestReporter` không còn dùng → xóa import + constructor
- [ ] **Parse Check:** `npx playwright test --list` phải pass sau mỗi file
- [ ] **No Logic Change:** KHÔNG thay đổi test flow, chỉ xóa decorative line

---

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **SC1:** 0 `reporter.pass(...)` calls còn lại trong 9 files
- [ ] **SC2:** 0 unused `TestReporter` imports (nếu file không còn dùng reporter nào)
- [ ] **SC3:** 0 unused `const reporter = new TestReporter(...)` declarations
- [ ] **SC4:** `npx playwright test --list` pass (no TS parse error)
- [ ] **SC5:** `dotnet build VanAn.sln` pass (verify không break C# — sanity check)
- [ ] **SC6:** No `expect()` assertion bị xóa hoặc thay đổi
- [ ] **SC7:** No `reporter.log(...)` hoặc `reporter.setArchitectDecision(...)` bị xóa

---

## 6. ACTIVE SKILLS (MAX 3)
- `pattern-based-fixing` — Áp dụng cùng pattern xóa cho 9 files
- `build-error-analysis` — Fix TS parse error nếu có

---

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** 5
- **Verified Facts:**
  - Fact 1: `reporter.pass(...)` xuất hiện ~30 lần trong 9 files (đếm trong review)
  - Fact 2: `reporter.pass(...)` luôn xuất hiện SAU `expect()` — decorative, không phải assertion
  - Fact 3: `TestReporter` import từ `../utils/test-reporter`
  - Fact 4: Một số file dùng `reporter.log(...)` và `reporter.setArchitectDecision(...)` — KHÔNG xóa những cái này
  - Fact 5: `accounting-flow.spec.ts` có `reporter.log` trong `beforeAll` — giữ lại
- **Assumptions:**
  - Xóa `reporter.pass` không break test logic (chỉ là logging)
  - `TestReporter` class có thể vẫn cần cho `log`/`setArchitectDecision`
- **Open Questions:**
  - Q1: Có file nào dùng `reporter.pass` làm return value không? (Verify khi đọc từng file)
- **Recommended Action:** PROCEED — risk thấp, chỉ xóa decorative line

---

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| `accounting-flow.spec.ts` | Mất logging pass — không ảnh hưởng test outcome | `expect()` vẫn verify; nếu cần log, dùng `console.log` |
| `audit-trail-flow.spec.ts` | Same | Same |
| `balance-dashboard-flow.spec.ts` | Same | Same |
| `order-flow.spec.ts` | Same | Same |
| `order-tracking.spec.ts` | Same | Same |
| `qr-payment.spec.ts` | Same | Same |
| `qr-payment-ui.spec.ts` | Same | Same |
| `van-an-dashboard.spec.ts` | Same | Same |
| `period-closing-flow.spec.ts` | Same | Same |

---

## 9. TDD & TESTING STRATEGY
- **Unit tests:** N/A — đây là E2E cleanup
- **Integration tests:** N/A
- **E2E tests:** Parse check only (`npx playwright test --list`) — không chạy runtime
- **Verification:** `npx playwright test --list` pass + `dotnet build VanAn.sln` pass

---

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: Pattern-Based Batch Fix
Áp dụng cùng 1 thao tác cơ học cho 9 files:
1. Đọc file → identify tất cả `reporter.pass(...)` lines
2. Xóa từng line `reporter.pass(...);`
3. Check xem file còn dùng `reporter.log` / `reporter.setArchitectDecision` không
4. Nếu không còn dùng → xóa `import { TestReporter }` + `const reporter = new TestReporter(...)`
5. Chạy `npx playwright test --list` verify parse

### Micro-phase breakdown cho Wave 1

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **S1** | - Đọc 9 files, đếm chính xác số `reporter.pass` per file<br>- Chốt: file nào còn dùng `reporter.log`/`setArchitectDecision` → giữ import<br>- Chốt: file nào chỉ có `reporter.pass` → xóa hết + xóa import | - Xóa `reporter.pass(...)` trong 9 files<br>- Xóa unused imports/constructors<br>- Run `npx playwright test --list`<br>- Commit |

### Rules
- 1 file tại 1 thời điểm — xóa xong verify parse trước khi sang file tiếp
- KHÔNG dùng `replace_all` blind — đọc context để không xóa `reporter.log`
- Nếu file có `reporter.pass` trong `try/catch` block — cẩn thận không break block structure

---

## 11. ESTIMATED EFFORT
- 1 session (9 files, ~30 lines xóa, thao tác cơ học)
- **BLOCKER:** None — risk thấp nhất trong 8 waves
