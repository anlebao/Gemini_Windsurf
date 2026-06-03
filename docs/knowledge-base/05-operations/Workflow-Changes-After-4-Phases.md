# Workflow Changes After 4 Phases

> **So sánh cách làm việc TRƯỚC và SAU khi hoàn tất 4 phases**  
> Phát triển tính năng mới | Sửa lỗi | Sửa test failed

---

## 1. Phát Triển Tính Năng Mới

### ❌ TRƯỚC (Before 4 Phases)

```
User: "Thêm form nhập chi phí"
    ↓
AI: Code ngay
    ↓
[Có thể]
- Sửa Domain.cs để fix UI bug
- Thêm custom CSS
- Playwright chạy trong khi code
- Thiếu test coverage
- Code inconsistent với patterns cũ
    ↓
Manual testing
    ↓
[Có thể lỗi khi deploy]
```

**Vấn đề:**
- AI không hiểu domain đầy đủ
- Không có constraints → code bừa bãi
- Playwright drive development → fragile tests
- Domain layer bị xâm phạm
- Không có CI → lỗi phát hiện muộn

---

### ✅ SAU (After 4 Phases)

```
User: "Thêm form nhập chi phí" + Issue #42 (GitHub)
    ↓
┌─────────────────────────────────────────────┐
│ 1. AI đọc Issue #42 (MCP - GitHub)          │
│    - Requirement rõ ràng từ issue          │
│    - Acceptance criteria documented        │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 2. AI load context (MCP - Filesystem)       │
│    - PROJECT_CONTEXT.md                    │
│    - Accounting.md domain rules            │
│    - ADR-003, ADR-004 constraints          │
│    - .windsurfrules                        │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 3. Feature Developer Agent                  │
│    Mode: ANALYZE → IMPLEMENT               │
│    - Phân tích impact                      │
│    - Check domain changes needed           │
│    → [Nếu cần sửa Domain] → Yêu cầu approve│
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 4. 7-Step Workflow (Playwright DISABLED)    │
│    Step 1-2: ANALYZE, DESIGN               │
│    Step 3: DOMAIN (read-only check)        │
│    Step 4-6: APP, INFRA, UI                │
│        ✓ VanAn.UI.Platform only             │
│        ✗ NO custom CSS                      │
│    Step 7: VALIDATE (Playwright ENABLED)    │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 5. AI commit + tạo PR (MCP - GitHub)        │
│    - Branch: feature/expense-entry-#42      │
│    - Commit: "feat: add expense entry form" │
│    - PR description link đến issue #42      │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 6. CI/CD Auto-trigger (GitHub Actions)      │
│    - Build ✓                               │
│    - Unit tests ✓                          │
│    - Architecture tests ✓                  │
│    - Guard check ✓                         │
│    - Playwright E2E tests ✓                 │
└─────────────────────────────────────────────┘
    ↓
Code review → Merge
```

**Lợi ích:**
- Requirement rõ ràng từ GitHub issue
- AI hiểu domain qua documentation
- Hard stops bảo vệ Domain layer
- UI consistency với UI Platform
- Playwright chỉ chạy sau implement
- Auto CI/CD trên mọi PR

---

## 2. Sửa Lỗi (Bug Fix)

### ❌ TRƯỚC (Before 4 Phases)

```
User: "Sửa lỗi này"
    ↓
AI: Fix ngay
    ↓
[Có thể]
- Sửa 10+ files một lúc
- Đụng chạm Domain.cs
- Fix triệu chứng, không root cause
- Test fail → sửa test luôn
    ↓
[Không chắc đã hết lỗi]
```

**Vấn đề:**
- Không có pattern-based fixing
- Domain bị sửa để fix UI
- Test bị weaken để pass
- Không có guard check

---

### ✅ SAU (After 4 Phases)

```
User: "Sửa lỗi build này" + Error log
    ↓
┌─────────────────────────────────────────────┐
│ 1. Build Fixer Agent                        │
│    Mode: FIX_ONLY                          │
│    - Max 3 files per batch                 │
│    - Never modify Domain.cs                │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 2. Root Cause Analysis                     │
│    - pattern-based-fixing skill            │
│    - build-error-analysis skill             │
│    - domain-integrity-validation skill      │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 3. Pattern Match                           │
│    Error: CS1061 'Order' no 'TotalPrice'    │
│    Pattern: Property renamed/removed        │
│    Fix: Use TotalAmount (existing alias)    │
│    OR Add missing property (Domain? → NO)   │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 4. Minimal Fix                              │
│    File: Orders.razor                       │
│    Change: TotalPrice → TotalAmount         │
│    Lines: 1 line changed                    │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 5. Validation                               │
│    dotnet build ✓                          │
│    dotnet test ✓                            │
│    ./guard-check.ps1 ✓                      │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 6. Stop Conditions (Escalate to user)       │
│    - Domain layer modification required     │
│    - Same error after 3 attempts            │
│    - >3 files needed                        │
│    - Architectural change required          │
└─────────────────────────────────────────────┘
```

**Lợi ích:**
- Pattern-based fixing → minimal changes
- Domain protection → không bị sửa để fix UI
- Max 3 files → focused fixes
- Validation trước khi xem là xong
- Stop conditions → biết khi nào escalate

---

## 3. Sửa Test Failed

### ❌ TRƯỚC (Before 4 Phases)

```
User: "Test failed, sửa đi"
    ↓
AI: Rerun test → fail
    ↓
AI: Sửa code → rerun → fail
    ↓
AI: Sửa test → rerun → pass ✓
    ↓
[Test pass nhưng behavior sai]
```

**Vấn đề:**
- AI loop: fix code → fix test → fix code
- Test bị weaken để pass
- Behavior thay đổi nhưng test không catch
- Playwright drive architecture

---

### ✅ SAU (After 4 Phases)

#### A. Unit Test Failure

```
Test failed notification
    ↓
┌─────────────────────────────────────────────┐
│ Build Fixer Agent                           │
│ Mode: FIX_ONLY                             │
│ Skill: pattern-based-fixing                │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ Analysis                                    │
│ - Test expects: 100000                      │
│ - Actual: 110000 (10% VAT added)            │
│ → Behavior change intentional?              │
│    YES → Update test expectation            │
│    NO  → Fix calculation logic              │
└─────────────────────────────────────────────┘
    ↓
Fix appropriate layer
    ↓
Validation: dotnet test ✓
```

#### B. E2E (Playwright) Failure

```
Playwright test failed
    ↓
┌─────────────────────────────────────────────┐
│ Playwright Guardian Agent                   │
│ Mode: TRIAGE_ONLY (không fix ngay)          │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 1. Classify Failure (playwright_triage.md)  │
│    - Selector? Timing? UI? Backend? Domain? │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 2. Route to Appropriate Fix                 │
│    - Selector → playwright_fix.md           │
│    - UI change → Document + update selector │
│    - Backend → Fix API, không sửa test      │
│    - Domain → Domain Guardian review        │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 3. Fix Rules                                │
│    - Max 1 rerun per spec after fix         │
│    - Still fail → STOP, don't rerun        │
│    - User owns .spec.ts files               │
│    - AI: proposer only (no auto-rewrite)   │
└─────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────┐
│ 4. Execution Ledger                         │
│    - Log to playwright-ledger.md            │
│    - Track failure patterns                 │
│    - Alert if repeated failures              │
└─────────────────────────────────────────────┘
```

**Lợi ích:**
- Triage trước khi fix → đúng root cause
- Không weaken test để pass
- Max 1 rerun → tránh AI loops
- Test ownership rõ ràng (User)
- Ledger tracking → pattern recognition

---

## Summary: Key Changes

| Aspect | Trước | Sau |
|--------|-------|-----|
| **Requirement** | Chat message | GitHub Issue (MCP) |
| **Context** | None | PROJECT_CONTEXT.md + Domain docs |
| **Agent** | Generic AI | Specialized agents (5 types) |
| **Mode** | None | ANALYZE/IMPLEMENT/FIX_ONLY/... |
| **Domain Protection** | None | Hard stops, Domain Guardian |
| **UI Consistency** | Random | VanAn.UI.Platform mandatory |
| **Playwright** | During implement | After implement only |
| **CI/CD** | Manual | Automated (GitHub Actions) |
| **Commit/PR** | Manual | MCP automated |
| **Test Ownership** | Unclear | User owns tests |
| **Fix Pattern** | Random | Pattern-based, max 3 files |

---

## Activation Checklist

Để sử dụng workflow mới:

- [ ] Phase 1: Domain docs đã đọc và hiểu
- [ ] Phase 2: AGENTS.md reference sẵn
- [ ] Phase 3: CI/CD đang chạy (GitHub Actions)
- [ ] Phase 4: MCP servers configured
- [ ] GitHub token có đủ scopes
- [ ] Team trained về workflow mới

---

*Document Version: 1.0*  
*Last Updated: June 1, 2026*
