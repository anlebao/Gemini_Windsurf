# TASK CARD: [CATEGORY] - [PHASE] - [TASK NAME]

## 1. GOAL & CONTEXT
- **Mục tiêu cốt lõi:** [CORE_GOAL]
- **Nghiệp vụ áp dụng:** [BUSINESS_CONTEXT]

## 2. ACTIVE WORKFLOW ROUTING
- **Target Workflow:** `[WORKFLOW_FILE]`
- **Execution Mode:** [EXECUTION_MODE]

## 3. RELEVANT FILES (CONTEXT BOUNDARY)
- **Files được phép đọc/sửa:**
  - `docs/AI/project_state.md` (Bắt buộc đọc đầu phiên)
  - [FILE_1]
  - [FILE_2]
  - [FILE_3]
  - [FILE_4]
  - [FILE_5]
- **Boundary Rules (Nghiêm cấm):**
  - [BOUNDARY_RULE_1]
  - [BOUNDARY_RULE_2]
  - [BOUNDARY_RULE_3]
  - [BOUNDARY_RULE_4]

## 4. TECHNICAL & REGULATORY CONSTRAINTS (HARDENING GATES)
- [ ] **[CONSTRAINT_1_TITLE]:** [CONSTRAINT_1_DESCRIPTION]
- [ ] **[CONSTRAINT_2_TITLE]:** [CONSTRAINT_2_DESCRIPTION]
- [ ] **[CONSTRAINT_3_TITLE]:** [CONSTRAINT_3_DESCRIPTION]
- [ ] **[CONSTRAINT_4_TITLE]:** [CONSTRAINT_4_DESCRIPTION]
- [ ] **[CONSTRAINT_5_TITLE]:** [CONSTRAINT_5_DESCRIPTION]

## 5. SUCCESS CRITERIA (ĐO LƯỜNG ĐƯỢC)
- [ ] **[SC_1]:** [SC_1_DESCRIPTION]
- [ ] **[SC_2]:** [SC_2_DESCRIPTION]
- [ ] **[SC_3]:** [SC_3_DESCRIPTION]
- [ ] **[SC_4]:** [SC_4_DESCRIPTION]
- [ ] **[SC_5]:** [SC_5_DESCRIPTION]
- [ ] **[SC_6]:** [SC_6_DESCRIPTION]
- [ ] **[SC_7]:** [SC_7_DESCRIPTION]
- [ ] **[SC_8]:** [SC_8_DESCRIPTION]
- [ ] **[SC_9]:** [SC_9_DESCRIPTION]
- [ ] **[SC_10]:** [SC_10_DESCRIPTION]
- [ ] **[SC_11]:** [SC_11_DESCRIPTION]
- [ ] **[SC_12]:** [SC_12_DESCRIPTION]

**Implementation Date:** [DATE]
**Branch:** [BRANCH_NAME]

## 6. ACTIVE SKILLS (MAX 3)
- `[SKILL_1]` — [SKILL_1_PURPOSE]
- `[SKILL_2]` — [SKILL_2_PURPOSE]
- `[SKILL_3]` — [SKILL_3_PURPOSE]

## 7. AI HEALTH CHECK MATRIX (INITIAL)
- **Evidence Count:** [EVIDENCE_COUNT]
- **Verified Facts:**
  - Fact 1: [FACT_1]
  - Fact 2: [FACT_2]
  - Fact 3: [FACT_3]
  - Fact 4: [FACT_4]
  - Fact 5: [FACT_5]
- **Assumptions:**
  - [ASSUMPTION_1]
  - [ASSUMPTION_2]
- **Open Questions:**
  - Q1: [QUESTION_1]
  - Q2: [QUESTION_2]
  - Q3: [QUESTION_3]
- **Recommended Action:** [RECOMMENDED_ACTION]

## 8. REVERSE IMPACT ANALYSIS
| File thay đổi | Reverse impact | Mitigation |
|---|---|---|
| [FILE_1] | [IMPACT_1] | [MITIGATION_1] |
| [FILE_2] | [IMPACT_2] | [MITIGATION_2] |
| [FILE_3] | [IMPACT_3] | [MITIGATION_3] |
| [FILE_4] | [IMPACT_4] | [MITIGATION_4] |
| [FILE_5] | [IMPACT_5] | [MITIGATION_5] |

## 9. TDD & E2E TESTING STRATEGY
- **[TEST_STRATEGY_1_TITLE]:**
  - [TEST_STRATEGY_1_DETAIL_1]
  - [TEST_STRATEGY_1_DETAIL_2]
  - [TEST_STRATEGY_1_DETAIL_3]
- **[TEST_STRATEGY_2_TITLE]:**
  - [TEST_STRATEGY_2_DETAIL_1]
  - [TEST_STRATEGY_2_DETAIL_2]
  - [TEST_STRATEGY_2_DETAIL_3]
- **Test boundary:**
  - Unit tests: [UNIT_TEST_SCOPE]
  - Integration tests: [INTEGRATION_TEST_SCOPE]
  - E2E tests: [E2E_TEST_SCOPE]

## 10. JIT PLANNING + PURE EXECUTION (MICRO-PHASES)

### Chiến lược thực thi: JIT Planning + Pure Execution

[EXECUTION_STRATEGY_DESCRIPTION]

### Micro-phase breakdown cho [PHASE_NAME]

| Session | JIT Planning (chốt gì) | Pure Execution (viết gì) |
|---|---|---|
| **[S1]** | [S1_PLANNING] | [S1_EXECUTION] |
| **[S2]** | [S2_PLANNING] | [S2_EXECUTION] |
| **[S3]** | [S3_PLANNING] | [S3_EXECUTION] |
| **[S4]** | [S4_PLANNING] | [S4_EXECUTION] |

### Rules
- [RULE_1]
- [RULE_2]
- [RULE_3]
## 11. COMPLETION SUMMARY

**[PHASE_NAME] COMPLETE** — commit `<HASH>` on `main`.

### Files created
| File | Purpose |
|------|---------|
| _TBD_ | _TBD_ |

### Files modified
| File | Change |
|------|--------|
| _TBD_ | _TBD_ |

### Issues fixed during implementation
- _TBD_

### Verification

#### Static Verification (compile-time)
- **Build:** _TBD_
- **Unit tests:** _TBD_
- **guard-check.ps1:** _TBD_

#### Live Runtime Verification (boot + HTTP + UI)
> **Lesson learned (Wave 0):** Build + Architecture Tests + guard-check PASS ≠ runtime works.
> Live runtime verification is MANDATORY for all phases.

| # | Test | Status | Evidence |
|---|------|--------|----------|
| RV1 | _TBD_ | _TBD_ | _TBD_ |

## 12. ESTIMATED EFFORT
- [EFFORT_ESTIMATE]
- [SESSION_COUNT] sessions theo JIT Planning
- **BLOCKER:** [BLOCKER_DESCRIPTION]