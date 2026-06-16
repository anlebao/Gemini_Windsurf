# ShopERP AI Agents (Minimal Phase 8)

> **Agent definitions cho AI-assisted development - Minimal Phase 8**
> Chỉ giữ 5 agents essential: Developer + QA + 3 Support Agents.

---

## Phase 1: Abstraction Layer (Planner/Builder/Auditor)

> **Purpose:** Simplify workflow for small team, reduce token cost, maintain quality control
> **Status:** Active (Phase 1 implementation)
> **Future:** Phase 2 will convert specialized agents to skills

### Role Mapping

| Abstraction Role | Backend Implementation | Skills |
|------------------|------------------------|--------|
| **Planner** | Developer Agent (ANALYZE mode) | boundary-detection, dod-definition, execution-ordering |
| **Builder** | Developer Agent + Build Fixer + Refactoring Safety | pattern-based-fixing, build-error-analysis, system-refactor-safety |
| **Auditor** | QA Agent + Domain Guardian | playwright_guard, domain-integrity-validation |

### Workflow

```
Planner (ANALYZE)
   ↓ EXECUTION PLAN
Builder (IMPLEMENT)
   ↓ IMPLEMENTATION REPORT
Auditor (VALIDATE)
   ↓ AUDIT REPORT
```

### Role Definitions

#### Planner
**Purpose:** Analyze requirements, detect boundaries, define DoD
**Mode:** ANALYZE only (no code changes)
**Backend:** Developer Agent in ANALYZE mode
**Output:** EXECUTION PLAN (files to modify, DoD, execution order)

#### Builder
**Purpose:** Code, test, build, refactor
**Mode:** IMPLEMENT
**Backend:** Developer Agent + Build Fixer skill + Refactoring Safety skill
**Output:** IMPLEMENTATION REPORT (files modified, test results, build status)

#### Auditor
**Purpose:** Review, verify, challenge assumptions
**Mode:** VALIDATE
**Backend:** QA Agent + Domain Guardian skill
**Output:** AUDIT REPORT (validation results, approval/rejection)

### Usage

When triggering a task, specify the abstraction role:
- "Act as Planner for [task]" → ANALYZE mode
- "Act as Builder for [task]" → IMPLEMENT mode
- "Act as Auditor for [task]" → VALIDATE mode

---

## Agent Overview

| Agent | Mode | Trigger | Priority |
|-------|------|---------|----------|
| Build Fixer | FIX_ONLY | Build/Test errors | High |
| Developer Agent | ANALYZE → IMPLEMENT | Feature request | High |
| QA Agent | VALIDATE_ONLY → FIX_PLAYWRIGHT | E2E failures | Medium |
| Domain Guardian | REVIEW_ONLY | Domain layer changes | Critical |
| Refactoring Safety | ANALYZE → IMPLEMENT (with validation) | Refactoring | Medium |

---

## 1. Build Fixer Agent

### Mode
**FIX_ONLY** - Limited scope, pattern-based fixing only

### Trigger
- Build errors > 0
- Unit test failures
- Compilation errors

### Skills
- `pattern-based-fixing`
- `build-error-analysis`
- `domain-integrity-validation`

### Workflow
`Fix_Errors.md` → pattern matching → minimal fix → validation

### Constraints

| Constraint | Rule |
|------------|------|
| Max files | 3 files per batch |
| Domain | Never modify Domain.cs |
| Pattern | Pattern-based fixing only |
| Validation | Build must pass after fix |

### Stop Conditions
- Requires architectural change
- Domain layer affected
- Same error after 3 attempts

---

## 2. Developer Agent

### Mode
**ANALYZE → IMPLEMENT** - Full feature development workflow

### Trigger
- New feature request
- User story implementation
- Code changes

### Skills
Feature-specific từ table:

| Feature Type | Active Skills |
|--------------|---------------|
| Accounting UI | accounting-ui-implementation, ui-platform-migration, domain-integrity-validation |
| UI Platform | ui-platform-migration, ui-platform-compliance-review |
| Outbox/NATS | outbox-pattern-implementation, nats-sqlite-deployment-validation |
| E-Invoice | einvoice-integration, ui-platform-compliance-review |
| Period Closing | period-closing-audit-trail, domain-integrity-validation |
| Refactor | system-refactor-safety, domain-integrity-validation |
| Tests | test-system-upgrade, pattern-based-fixing |

### Workflow
`newfeaturebuild.md` - 7 step workflow:
1. Use Case & Business Design
2. Reverse Impact Analysis + TDD Plan
3. Detailed Coding Plan + Namespace Strategy
4. Review & Approval (User)
5. Pre-Implementation Validation
6. Implementation by Phase
7. Review & Approval after each Phase

### Constraints

| Constraint | Rule |
|------------|------|
| Phase isolation | Domain → App → Infra → UI |
| Playwright | Disabled during Steps 1-6 |
| Max files | 10 files per phase |
| Domain | Read-only in IMPLEMENT mode |
| JIT Planning | Phase 1 chốt design trước khi execute Phase 2 |

### Stop Conditions
- Domain modification required
- Unclear requirements
- Architecture conflict with ADRs
- Assumptions >= Verified Facts (Gate 6)

---

## 3. QA Agent

### Mode
**VALIDATE_ONLY → FIX_PLAYWRIGHT** - Validation first, fix only when classified

### Trigger
- E2E test failures
- Post-implementation validation required
- Developer Agent hand-off

### Skills
- `playwright_guard`
- `playwright_cost_optimizer`

### Workflows
- `playwright_validation.md` → Post-implementation validation (primary)
- `playwright_triage.md` → Classify failures (when >5 failures)
- `playwright_fix.md` → Fix classified failures

### Constraints

| Constraint | Rule |
|------------|------|
| Mode isolation | Never during Developer Agent IMPLEMENT mode |
| Validation first | Must validate before fix |
| Retry | Max 1 rerun per spec |
| Cost | Cost tiers enforced |
| Ownership | User owns .spec.ts files |

### Stop Conditions
- Repeated failure (same spec, same cause, 2+ times)
- Full suite requested without approval
- >5 specs in single run (triage first)
- Backend/Domain failures (escalate to Developer)

### Validation Checklist

| Check | Method | Pass Criteria |
|-------|--------|---------------|
| Build | `dotnet build` | 0 errors |
| Tests | `dotnet test` | All pass |
| No TODOs | `grep -r "TODO\|// stub"` | 0 results in scope |
| E2E | Playwright | Pass count / Fail count measurable |

---

## 4. Domain Guardian Agent

### Mode
**REVIEW_ONLY** (default), exception requires explicit approval

### Trigger
- Any Domain layer change
- `Domain.cs` modifications
- Business rule changes

### Skills
- `domain-integrity-validation`

### Hard Stops

| Violation | Action |
|-----------|--------|
| Domain fix for UI/Service | REFUSE - "Wrong layer" |
| Accounting immutability | REFUSE - "AccountingEntry is immutable" |
| Multi-tenancy bypass | REFUSE - "TenantId required" |
| EF Core in Domain | REFUSE - "Domain purity violated" |
| Property mutation | REFUSE - "Use proper pattern" |

### Validation Rules
- `AccountingEntry` append-only check
- `TenantId` presence check
- No EF Core/DataAnnotations
- Factory methods preferred
- Navigation properties configured correctly

---

## 5. Refactoring Safety Agent

### Mode
**ANALYZE → IMPLEMENT** with extra validation steps

### Trigger
- Refactoring request
- Code improvement
- Technical debt payment

### Skills
- `system-refactor-safety`
- `test-strategy-planning`
- `pattern-based-fixing`

### Workflow
1. ANALYZE impact
2. CREATE tests (Retrofit TDD)
3. REFACTOR with safety
4. VALIDATE all tests pass
5. DOCUMENT changes

### Constraints

| Constraint | Rule |
|------------|------|
| Tests first | Must have tests before refactoring |
| Public API | No change without approval |
| Scope | Max 5 files per batch |
| Rollback | Keep rollback plan ready |

### Stop Conditions
- No existing tests
- Public API change required
- >10 files affected
- Cross-domain impact

---

---

## 6. Architecture Review Agent

### Mode
**VALIDATE_ONLY** - Architecture validation only

### Trigger
- Plan contains EF Core migration
- Plan contains database schema changes
- Plan contains DbContext modifications
- Plan contains Infrastructure layer changes

### Skills
- `architecture-pattern-validation` - Validate pattern matches DDD
- `layer-boundary-check` - Verify layer ownership
- `ef-core-setup-review` - Review EnsureCreated vs Migrations

### Hard Stops

| Violation | Action | Error Code |
|-----------|--------|------------|
| Migration ở Application layer | REFUSE | VA-ARCH-001 |
| EnsureCreated vs Migrations mismatch | REFUSE | VA-ARCH-002 |
| Database schema không match Entity | REFUSE | VA-ARCH-003 |
| DbContext ở Application layer | REFUSE | VA-ARCH-004 |

### Self-Correction Mechanism (3 Perspectives)
**Domain Perspective:**
- Entity definition correct?
- Business rules preserved?
- Domain integrity maintained?
- Single Source of Truth: 1_Shared/Domain.cs

**Infrastructure Perspective:**
- DbContext location correct (Infrastructure layer only)?
- Migration strategy appropriate (EnsureCreated vs Migrations)?
- Database schema matches Entity definition?
- No orphan migrations in WebApps/APIs

**Application Perspective:**
- No migration creation in Application layer
- DbContext usage follows proper DI
- No direct EF Core in Domain layer

**Self-Correction Process:**
- If perspectives conflict → identify root cause
- If perspective unclear → ask for clarification
- If violation found → propose fix
- If `[BYPASS-DDD]` present → log bypass and allow

### Human-in-the-loop Bypass
- **Flag:** `[BYPASS-DDD]` in user prompt
- **Logging:** Log to PROJECT_MEMORY.md with format: "User bypassed DDD at [DateTime] | Reason: [Reason]"
- **Scope:** Does NOT override Domain Guardian hard stops
- **Temporary:** Bypass requires follow-up proper fix in subsequent sprint

---

## Hand-off Protocols

### Minimal Phase 8 Flow (Updated)

```
User → Planner (ANALYZE) → Architecture Review (VALIDATE) → Builder (IMPLEMENT) → QA Agent → User
```

### Between Agents

```
User → Developer Agent
    [When] Feature request, implementation task
    [Input] Task description + acceptance criteria
    [Output] Implementation + filled developer-output-template

Developer Agent → QA Agent
    [When] Implementation complete, build pass
    [Hand-off] 
        - Files modified
        - Test specs impacted
        - Developer output template filled
    [Stop] If build fail → escalate to Build Fixer

QA Agent → User
    [When] Validation complete
    [Hand-off]
        - Validation checklist results
        - Pass/Fail classification
        - QA output template filled
    [Stop] If FAIL → return to Developer Agent with classification

QA Agent → Build Fixer (escalation)
    [When] Build/test errors found during validation
    [Hand-off] Error report + scope

Build Fixer → Developer Agent
    [When] Build fix applied
    [Hand-off] Fixed files + validation results

Domain Guardian → Developer Agent
    [When] Domain review triggered
    [Hand-off] Approval or REFUSE with reason
```

### Escalation to User

```
All Agents → User
    [When] 
    - Stop condition triggered (3+ attempts, unclear requirements)
    - Domain Guardian REFUSE
    - Architectural decision needed
    - Assumptions >= Verified Facts (Gate 6)
```

---

## Skills to Agents Mapping

| Feature Type | Primary Agent | Skills |
|--------------|---------------|--------|
| Accounting UI | Developer Agent | accounting-ui-implementation, ui-platform-migration, domain-integrity-validation |
| UI Platform | Developer Agent | ui-platform-migration, ui-platform-compliance-review |
| Outbox/NATS | Developer Agent | outbox-pattern-implementation, nats-sqlite-deployment-validation |
| E-Invoice | Developer Agent | einvoice-integration, ui-platform-compliance-review |
| Period Closing | Developer Agent | period-closing-audit-trail, domain-integrity-validation |
| Build Errors | Build Fixer | pattern-based-fixing, build-error-analysis, domain-integrity-validation |
| Test Failures | Build Fixer + QA Agent | test-system-upgrade, pattern-based-fixing |
| Domain Changes | Domain Guardian | domain-integrity-validation |
| Refactoring | Refactoring Safety | system-refactor-safety, test-strategy-planning |
| E2E Validation | QA Agent | playwright_cost_optimizer, playwright_guard |

---

## Mode Enforcement

### AI Self-Check
```
Before every action:
  1. Identify current mode
  2. Check agent constraints
  3. Validate against hard stops
  4. Execute or escalate
```

### User Override
- User có thể explicit yêu cầu mode change
- Agent ghi nhận override trong response
- Override không affect hard stops (Domain rules)

---

## References

- `.windsurf/rules/.windsurfrules` - Core governance
- `.windsurf/workflows/` - All workflows
- `.windsurf/skills/` - All skills
- `docs/knowledge-base/08-ai/prompts/developer-agent.md` - Developer Agent prompt template
- `docs/knowledge-base/08-ai/prompts/qa-agent.md` - QA Agent prompt template
- `docs/knowledge-base/08-ai/hand-off-protocol.md` - Hand-off protocol
- `docs/decisions/ADR-005-Playwright-Isolation.md` - Playwright rules

---

*Version: 2.1 (Phase 1: Abstraction Layer)*
*Last Updated: June 2026 (Added Planner/Builder/Auditor abstraction)*
