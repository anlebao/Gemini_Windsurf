# WINDSURF GOVERNANCE — HƯỚNG DẪN SỬ DỤNG

**Phiên bản:** 1.0 — 2026-05-26
**Áp dụng cho:** Vạn An Ecosystem

---

## KIẾN TRÚC

```
.windsurfrules (luôn active, mọi session)
    ↓ references
rules/playwright.rules.md (active khi Playwright workflow gọi)
    ↓ triggered by
workflows/ (user chọn 1 tại 1 thời điểm)
    ↓ loads max 3
skills/ (chuyên môn, workflow chọn)
    ↓ persists across sessions
memory (Windsurf tự nhớ giữa sessions)
```

**Nguyên tắc:** Token tiêu hao = files AI phải đọc. Càng ít file active → càng rẻ.

---

## 1. RULES — Luôn active, không cần gọi

| File | Vai trò | Khi nào AI đọc |
|------|---------|----------------|
| `.windsurfrules` | Governance bất biến (domain, architecture, hard stops) | Tự động mọi session |
| `playwright.rules.md` | Playwright governance (test ownership, state mgmt, retry) | Khi Playwright workflow active |

**Cách dùng:**
- Không cần nhắc `.windsurfrules` — AI đã biết.
- Không cần nói "hãy tuân theo rules" — redundant, tốn token.
- Chỉ nhắc rule khi AI vi phạm: `"Hard stop — domain modification"`.

---

## 2. WORKFLOWS — Gọi đúng cái, đúng lúc

### Bản đồ quyết định

```
Bạn muốn gì?
│
├─ Build feature mới ──────→ dùng workflow newfeaturebuild
├─ Build error ─────────────→ dùng workflow Fix_Errors
├─ Test fail (dotnet) ──────→ dùng workflow Fix_Tests
├─ Code review ─────────────→ dùng workflow review
├─ Test refactor ───────────→ dùng workflow test-refactor-workflow
├─ Playwright fail ─────────→ dùng workflow playwright_triage
├─ Playwright validate ─────→ dùng workflow playwright_validation
└─ Playwright fix ──────────→ dùng workflow playwright_fix
```

### Cú pháp gọi

```
dùng workflow Fix_Errors
```

```
dùng workflow newfeaturebuild
```

Chỉ cần tên file, không cần path. AI tự tìm trong `.windsurf/workflows/`.

### Danh sách workflows

| Workflow | Mode | Mục đích |
|----------|------|----------|
| `newfeaturebuild.md` | ANALYZE → IMPLEMENT | Build feature mới, 7 bước |
| `Fix_Errors.md` | FIX_ONLY | Fix compile/runtime errors theo pattern |
| `Fix_Tests.md` | FIX_ONLY_TESTS | Fix dotnet test failures |
| `review.md` | REVIEW_ONLY | Review code, không sửa |
| `test-refactor-workflow.md` | ANALYZE → IMPLEMENT | Refactor test system |
| `playwright_triage.md` | TRIAGE_ONLY | Collect, classify, route Playwright failures |
| `playwright_validation.md` | VALIDATE_ONLY | Validate sau implementation |
| `playwright_fix.md` | FIX_PLAYWRIGHT | Fix classified Playwright failures |

### Playwright workflow chain (bắt buộc theo thứ tự)

```
Implement xong → build pass
    → playwright_validation (validate)
        → nếu fail → playwright_triage (classify)
            → playwright_fix (fix từng category)
```

Không bao giờ nhảy thẳng vào `playwright_fix` mà chưa triage.

---

## 3. SKILLS — AI tự chọn, bạn có thể override

### Mỗi workflow tự load max 3 skills

| Workflow | Default Skills |
|----------|---------------|
| `newfeaturebuild` | Tùy feature type (xem bảng trong workflow file) |
| `Fix_Errors` | `build-error-analysis`, `pattern-based-fixing`, `domain-integrity-validation` |
| `Fix_Tests` | `build-error-analysis`, `pattern-based-fixing`, `test-system-upgrade` |
| `review` | Tùy review mode |
| `playwright_*` | `playwright_cost_optimizer`, `playwright_guard` |

### Override khi cần

```
dùng workflow Fix_Errors, swap skill domain-integrity thành ui-platform-compliance
```

Chỉ override khi lỗi thuộc layer khác default.

### Phân loại 19 skills

**Domain & Architecture:**
- `domain-integrity-validation` — bảo vệ Domain layer
- `system-refactor-safety` — refactor an toàn
- `dynamic-hkd-book-architecture` — HKD Books
- `period-closing-audit-trail` — Period Closing

**Build & Fix:**
- `build-error-analysis` — phân tích lỗi compile
- `pattern-based-fixing` — fix theo pattern

**UI:**
- `ui-platform-compliance-review` — kiểm tra UI Platform
- `ui-platform-migration` — migrate sang UI Platform

**Testing:**
- `test-strategy-planning` — lập test strategy
- `test-refactor-cost-benefit` — đánh giá cost/benefit
- `test-system-upgrade` — upgrade test system

**Playwright:**
- `playwright_cost_optimizer` — tiết kiệm browser execution
- `playwright_guard` — chặn Playwright khi implement

**Feature-specific:**
- `accounting-ui-implementation`
- `einvoice-integration`
- `order-workflow-unified`
- `outbox-pattern-implementation`
- `nats-sqlite-deployment-validation`
- `sqlite-concurrency-analysis`

---

## 4. MEMORY — Nhớ giữa các session

### Memory lưu gì
- Kiến trúc project đã học
- Quyết định đã approved
- Pattern đã fix thành công
- Context quan trọng

### Cách dùng

**Đầu session mới (không cần brief lại):**
```
tiếp tục task accounting UI sprint 1
```
AI dùng memory để recall context. Không cần paste lại plan.

**Khi AI quên context (session dài):**
```
nhắc lại objective hiện tại
```

**Khi muốn lưu quyết định:**
```
ghi nhớ: chúng ta đã quyết định dùng NATS thay RabbitMQ
```

**Tiết kiệm:** Memory tự động, không tốn token để load. Giúp tiết kiệm hàng ngàn token so với brief lại mỗi session.

---

## 5. VÍ DỤ THỰC TẾ

### A. Build feature mới (end-to-end)

```
Session 1:
  "dùng workflow newfeaturebuild, build Revenue Entry page"
  → AI: ANALYZE (Steps 1-4) → plan
  → Bạn: "approved"
  → AI: IMPLEMENT (Steps 5-6) → code
  → playwright_guard active → KHÔNG chạy Playwright
  → Build pass → report

Session 2:
  "dùng workflow playwright_validation"
  → AI: select specs → run → report
  → Nếu fail: "dùng workflow playwright_triage"
  → AI: classify → route
  → "dùng workflow playwright_fix"
  → AI: fix → rerun 1 lần → report
```

### B. Fix build errors

```
"dùng workflow Fix_Errors"
→ AI: classify → batch fix 3 files → rebuild → report
→ Pass: done
→ Fail: next batch
```

### C. Playwright test fail

```
"dùng workflow playwright_triage"
→ AI: check ledger → collect → classify → route
→ "dùng workflow playwright_fix"
→ AI: fix selector → rerun spec → pass → update ledger
```

---

## 6. ANTI-PATTERNS — Tránh những lỗi này

| Sai | Tốn thêm | Đúng |
|-----|----------|------|
| Không chỉ workflow | AI tự đoán → scope creep | `dùng workflow X` |
| "Fix all errors" | Token explosion | `dùng workflow Fix_Errors` (tự batch) |
| Brief lại context đầu session | 2-5K tokens thừa | 1 câu trigger, trust memory |
| Mix mục đích trong 1 câu | AI confused | 1 workflow per request |
| Không review report giữa chừng | AI tiếp tục sai hướng | Đọc report → approved/redirect |
| Chạy Playwright trong lúc code | Loop vô hạn | `playwright_guard` tự chặn |
| Gọi playwright_fix không triage | Fix sai → loop | Luôn triage trước |
| Nói "tuân theo rules" | Token thừa | Rules tự động active |

---

## 7. QUICK REFERENCE

```
BUILD FEATURE:   dùng workflow newfeaturebuild
FIX ERRORS:      dùng workflow Fix_Errors
FIX TESTS:       dùng workflow Fix_Tests
REVIEW:          dùng workflow review
TEST REFACTOR:   dùng workflow test-refactor-workflow

PLAYWRIGHT:      validate → triage → fix (theo thứ tự)

MEMORY:          "ghi nhớ: [quyết định]"
                 "nhắc lại objective"

TIẾT KIỆM:
  ✓ 1 workflow per request
  ✓ max 3 files per batch
  ✓ trust memory, don't re-brief
  ✓ triage before fix
  ✓ single spec before suite
```
