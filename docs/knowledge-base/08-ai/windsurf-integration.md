# Windsurf Integration Guide

> **How ShopERP integrates with Windsurf IDE**  
> Link between AI Knowledge Base và Windsurf configuration.

## Overview

ShopERP sử dụng Windsurf IDE với custom Knowledge Base để:
- Đảm bảo code consistency
- Enforce architectural decisions
- Automate common workflows
- Giảm cognitive load cho developers

## Knowledge Base Structure

```
.windsurf/                          # Windsurf-specific config
├── rules/
│   ├── .windsurfrules              # Core governance v7.0
│   └── playwright.rules.md         # E2E testing rules
├── workflows/                       # AI workflows (12 files)
│   ├── Fix_Errors.md
│   ├── Fix_Tests.md
│   ├── newfeaturebuild.md
│   ├── playwright_triage.md
│   ├── playwright_fix.md
│   ├── playwright_validation.md
│   └── review.md
└── skills/                          # Domain skills (21 files)
    ├── domain-integrity-validation.md
    ├── pattern-based-fixing.md
    ├── accounting-ui-implementation.md
    ├── ui-platform-migration.md
    ├── nats-sqlite-deployment-validation.md
    └── ...

docs/knowledge-base/                 # Project knowledge
├── 00-core/
│   ├── Vision.md
│   └── PROJECT_CONTEXT.md
├── 02-domains/
│   ├── Order.md
│   ├── Inventory.md
│   ├── Payment.md
│   └── Accounting.md
└── 08-ai/
    ├── AGENTS.md
    ├── windsurf-integration.md (this file)
    └── prompts/
        ├── feature-development.md
        ├── bug-fix.md
        └── code-review.md
```

## How It Works

### 1. Session Initialization

Khi Windsurf AI bắt đầu session:

1. Read `docs/knowledge-base/00-core/PROJECT_CONTEXT.md`
2. Load `.windsurf/rules/.windsurfrules`
3. Identify active agent từ AGENTS.md
4. Determine mode (ANALYZE, IMPLEMENT, FIX_ONLY, ...)

### 2. Mode Enforcement

```
User Request
     ↓
AI identifies mode
     ↓
Check .windsurfrules for constraints
     ↓
Execute or escalate
```

### 3. Skill Activation

| Task Type | Auto-loaded Skills |
|-----------|-------------------|
| Domain changes | domain-integrity-validation |
| Error fixing | pattern-based-fixing, build-error-analysis |
| Accounting UI | accounting-ui-implementation, ui-platform-compliance |
| Deployment | nats-sqlite-deployment-validation |
| E2E issues | playwright_guard, playwright_cost_optimizer |

## Usage Patterns

### Pattern 1: Feature Development

**User**: "Thêm form nhập chi phí"

**AI Flow**:
1. Load `AGENTS.md` → Feature Developer agent
2. Load `newfeaturebuild.md` workflow
3. Read `Accounting.md` domain context
4. Check `ADR-004` UI constraints
5. Execute 7-step workflow

### Pattern 2: Bug Fix

**User**: "Fix lỗi build này"

**AI Flow**:
1. Load `AGENTS.md` → Build Fixer agent
2. Load `Fix_Errors.md` workflow
3. Apply `pattern-based-fixing` skill
4. Validate with `domain-integrity-validation`

### Pattern 3: Code Review

**User**: "Review PR này"

**AI Flow**:
1. Load `AGENTS.md` → Domain Guardian
2. Load `review.md` workflow
3. Check against ADRs
4. Flag violations

## Configuration

### .windsurfrules (Core)

Located at: `.windsurf/rules/.windsurfrules`

Contains:
- Architecture boundaries
- Naming conventions
- Mode definitions
- Hard stops
- Code style rules

### Custom Rules

Extend by creating files in `.windsurf/rules/`:

```markdown
# .windsurf/rules/custom.rules.md

## Custom Rule

Description of rule.

### Example

```csharp
// ✅ Good
public class Order : BaseEntity { }

// ❌ Bad
public class OrderDto : BaseEntity { }
```
```

## Best Practices

### For Users

1. **Start with context**: Reference relevant domain docs
2. **Specify mode**: "Fix this build error" vs "Implement feature"
3. **Provide constraints**: Budget, timeline, priority
4. **Review AI plan**: Confirm trước khi AI implement

### For AI

1. **Read context first**: PROJECT_CONTEXT.md + ADRs
2. **Identify mode**: Check constraints
3. **Propose, don't assume**: Ask for approval on significant changes
4. **Respect hard stops**: Never bypass Domain rules
5. **Link to docs**: Cite ADRs và domain docs trong responses

## Troubleshooting

### AI không follow rules
- Check `.windsurfrules` syntax
- Verify file paths
- Reference ADRs explicitly trong prompt

### Domain violations slip through
- Enable Domain Guardian mode
- Add architecture tests
- Review `domain-integrity-validation.md`

### Inconsistent code style
- Reference `CodingStandards.md`
- Use EditorConfig
- Enable format-on-save

## Migration Guide

### Adding New Skill

1. Create `.windsurf/skills/{skill-name}.md`
2. Follow skill template
3. Reference trong AGENTS.md
4. Test với real task

### Adding New Workflow

1. Create `.windsurf/workflows/{workflow-name}.md`
2. Define steps, inputs, outputs
3. Map to agent trong AGENTS.md
4. Document trong integration guide

### Adding New ADR

1. Create `docs/decisions/ADR-XXX-{title}.md`
2. Update `docs/decisions/README.md` index
3. Reference trong PROJECT_CONTEXT.md
4. Add to `.windsurfrules` if affects AI behavior

## References

- `docs/WINDSURF_USAGE_GUIDE.md` - User guide
- `docs/decisions/ADR-001` through `ADR-005` - Architecture decisions
- `.windsurf/rules/.windsurfrules` - Core governance
- `.windsurf/workflows/*.md` - All workflows

## Maintenance

| Component | Review Cycle | Owner |
|-----------|--------------|-------|
| .windsurfrules | Monthly | Tech Lead |
| ADRs | Quarterly | Architect |
| Skills | As needed | AI Lead |
| Domain docs | Per feature | Domain Owner |
| AGENTS.md | Per new agent | AI Lead |

---

*Version: 1.0*  
*Last Updated: June 1, 2026*
