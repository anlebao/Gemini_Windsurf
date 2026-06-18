# Session Initialization Rules

> **Purpose:** Mandate context loading at the start of each session
> **Status:** Active
> **Priority:** High (applies before all other rules)

## MANDATORY SESSION INITIALIZATION

**When starting a new session, AI MUST:**

1. **Read PROJECT_CONTEXT.md**
   - Path: `docs/knowledge-base/00-core/PROJECT_CONTEXT.md`
   - Purpose: Project overview, architecture, ADRs, tech stack, code patterns, hard stops

2. **Read project_state.md**
   - Path: `docs/AI/project_state.md`
   - Purpose: Current objective, status, next actions, health check

3. **Check .devin/rules/.windsurfrules**
   - Purpose: Identify active workflow mode and constraints

4. **Identify current mode**
   - Determine: ANALYZE/IMPLEMENT/FIX_ONLY/REVIEW_ONLY based on task
   - Apply mode-specific constraints

5. **Verify git context** (if task requires)
   - Check current branch
   - Review recent commits

## EXCEPTIONS

Skip context loading only for:
- Simple read-only operations (file reading, grep, basic exploration)
- User explicitly requests to skip context loading
- Quick documentation lookups unrelated to implementation

## CONTEXT SUMMARY REPORT

After loading context, AI should report:

```
✅ Context Loaded

Current Objective: [from project_state.md]
Active Mode: [ANALYZE/IMPLEMENT/FIX_ONLY/REVIEW_ONLY]
Current Branch: [from git]
Key Constraints: [from PROJECT_CONTEXT.md and .windsurfrules]
Next Actions: [from project_state.md]
```

## INTEGRATION

This rule applies before:
- All workflows (newfeaturebuild.md, Fix_Errors.md, review.md)
- All implementation tasks
- Architecture reviews
- Feature development

## SKILL REFERENCE

For detailed initialization protocol, see:
- `.devin/skills/session-initialization.md` - Load context at session start
- `.devin/skills/session-update-state.md` - Update context after session completion

## RECOMMENDED WORKFLOW

**Start of Session:**
```
skill session-initialization
```

**During Session:**
- Do work (implement features, fix bugs, etc.)

**End of Session (important work):**
```
skill session-update-state
```

This ensures:
- Context is loaded at start
- Context is updated at end
- Continuous state tracking across sessions

## COMMAND SHORTCUTS

**/update-state** or **update-state**
- Shortcut to invoke session-update-state skill
- Use when: context is long, completed significant work, or before context overflow
- AI will: update project_state.md and report summary

## AUTO-UPDATE TRIGGERS

AI should auto-invoke session-update-state when:

**Threshold-based:**
- 5+ files modified in current session
- 30+ minutes elapsed in current session
- Context length > 50% of limit
- Completed a workflow phase

**User-triggered:**
- User types `/update-state` or `update-state`
- User requests "update state" or "save progress"

**Context overflow:**
- When context is near limit (>80%)
- Before context becomes unmanageable
- To clear non-essential context

## CONTEXT OVERFLOW PROTOCOL

When context is near limit or overflow detected:

1. **STOP** current work
2. **INVOKE** session-update-state
3. **CLEAR** non-essential context (keep only current task)
4. **CONTINUE** with fresh context
5. **REPORT** summary of what was saved

**Priority:** State update > continuing work when context is critical

---

*Version: 1.0*
*Last Updated: June 16, 2026*
