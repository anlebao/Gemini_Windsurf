# ShopERP AI Agents

> **Agent definitions cho AI-assisted development**  
> Mỗi agent có mode, skills, workflow, và constraints riêng.

## Agent Overview

| Agent | Mode | Trigger | Priority |
|-------|------|---------|----------|
| Build Fixer | FIX_ONLY | Build/Test errors | High |
| Feature Developer | ANALYZE → IMPLEMENT | Feature request | High |
| Playwright Guardian | TRIAGE/VALIDATE/FIX_PLAYWRIGHT | E2E failures | Medium |
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

## 2. Feature Developer Agent

### Mode
**ANALYZE → IMPLEMENT** - Full feature development workflow

### Trigger
- New feature request
- User story implementation

### Skills
Feature-specific từ table:

| Feature Type | Active Skills |
|--------------|---------------|
| Accounting UI | accounting-ui-implementation, ui-platform-migration, domain-integrity-validation |
| UI Platform | ui-platform-migration, ui-platform-compliance-review |
| Outbox/NATS | outbox-pattern-implementation, nats-sqlite-deployment-validation |
| E-Invoice | einvoice-integration, ui-platform-compliance-review |
| Period Closing | period-closing-audit-trail, domain-integrity-validation |

### Workflow
`newfeaturebuild.md` - 7 step workflow:
1. ANALYZE (no browser)
2. DESIGN (no browser)
3. DOMAIN (no browser)
4. APP (no browser)
5. INFRA (no browser)
6. UI (no browser)
7. VALIDATE (Playwright enabled)

### Constraints

| Constraint | Rule |
|------------|------|
| Phase isolation | Domain → App → Infra → UI |
| Playwright | Disabled during Steps 1-6 |
| Max files | 10 files per phase |
| Domain | Read-only in IMPLEMENT mode |

### Stop Conditions
- Domain modification required
- Unclear requirements
- Architecture conflict with ADRs

---

## 3. Playwright Guardian Agent

### Mode
**TRIAGE_ONLY / VALIDATE_ONLY / FIX_PLAYWRIGHT**

### Trigger
- E2E test failures
- Validation required

### Skills
- `playwright_guard`
- `playwright_cost_optimizer`

### Workflows
- `playwright_triage.md` → Classify failures
- `playwright_validation.md` → Post-implementation validation
- `playwright_fix.md` → Fix classified failures

### Constraints

| Constraint | Rule |
|------------|------|
| Mode | Never during IMPLEMENT mode |
| Retry | Max 1 rerun per spec |
| Cost | Cost tiers enforced |
| Ownership | User owns .spec.ts files |

### Stop Conditions
- Repeated failure (same spec, same cause, 2+ times)
- Full suite requested without approval
- >5 specs in single run

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

## Hand-off Protocols

### Between Agents

```
Build Fixer → Feature Developer
    [When] Build errors in feature development
    [Hand-off] Error report + attempted fixes
    
Feature Developer → Playwright Guardian
    [When] Implementation complete
    [Hand-off] Build confirmation + test specs
    
Playwright Guardian → Build Fixer
    [When] Fix needed for test
    [Hand-off] Classified failure + root cause
    
Domain Guardian → Feature Developer
    [When] Domain review complete
    [Hand-off] Approval + constraints
```

### To User

```
All Agents → User
    [When] 
    - Stop condition triggered
    - Approval required
    - Architectural decision needed
    - Unclear requirements
```

---

## Skills to Agents Mapping

| Feature Type | Primary Agent | Skills |
|--------------|---------------|--------|
| Accounting UI | Feature Developer | accounting-ui-implementation, ui-platform-migration, domain-integrity-validation |
| UI Platform | Feature Developer | ui-platform-migration, ui-platform-compliance-review |
| Outbox/NATS | Feature Developer | outbox-pattern-implementation, nats-sqlite-deployment-validation |
| E-Invoice | Feature Developer | einvoice-integration, ui-platform-compliance-review |
| Period Closing | Feature Developer | period-closing-audit-trail, domain-integrity-validation |
| Build Errors | Build Fixer | pattern-based-fixing, build-error-analysis, domain-integrity-validation |
| Test Failures | Build Fixer + Playwright | test-system-upgrade, pattern-based-fixing |
| Domain Changes | Domain Guardian | domain-integrity-validation |
| Refactoring | Refactoring Safety | system-refactor-safety, test-strategy-planning |

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
- `docs/decisions/ADR-005-Playwright-Isolation.md` - Playwright rules

---

*Version: 1.0*  
*Last Updated: June 1, 2026*
