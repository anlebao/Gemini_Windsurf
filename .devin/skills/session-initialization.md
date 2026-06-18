# Session Initialization Skill

> **Purpose:** Auto-load project context at the start of each session
> **Trigger:** Invoked automatically when starting a new session (if configured)
> **Status:** Active

## When to Use

This skill should be invoked:
- At the start of a new session for implementation tasks
- Before starting feature development or bug fixing
- When context about current project state is needed

**Skip for:**
- Simple read-only operations (file reading, grep, basic exploration)
- User explicitly requests to skip context loading
- Quick documentation lookups unrelated to implementation

## Context Files to Load

### 1. PROJECT_CONTEXT.md
**Path:** `docs/knowledge-base/00-core/PROJECT_CONTEXT.md`

**Provides:**
- Project overview (ShopERP/VanAn Ecosystem)
- Repository structure
- Architecture Decisions (ADRs)
- Core Business Rules (Multi-tenancy, Accounting, Order Processing)
- Technology Stack (.NET 8, Blazor, MAUI, SQLite, NATS)
- Code Patterns (Domain Entity, Factory Method, Value Object)
- Hard Stops for AI
- Common Commands (build, test, guard-check)
- MCP Integration details

### 2. project_state.md
**Path:** `docs/AI/project_state.md`

**Provides:**
- Current Objective (what we're working on now)
- Current Status (completed, blocked items)
- Next Actions (immediate next steps)
- Architecture Decisions (recent decisions and consequences)
- Health Check (assumptions, open questions, verified facts)
- History Log (what was done in previous sessions)

## Initialization Protocol

When this skill is invoked:

1. **Read PROJECT_CONTEXT.md**
   - Understand project structure and architecture
   - Note ADRs and hard stops
   - Identify tech stack and patterns

2. **Read project_state.md**
   - Identify current objective
   - Check current status (completed/blocked)
   - Review next actions
   - Note health check status (assumptions vs verified facts)

3. **Check .devin/rules/.windsurfrules**
   - Identify active workflow mode
   - Note hard stop rules
   - Check UI Platform requirements

4. **Verify git context** (if task requires)
   - Check current branch
   - Review recent commits
   - Identify any merge conflicts or issues

5. **Report context summary**
   - Current objective
   - Active mode
   - Key constraints
   - Next steps

## Example Output

After loading context, the AI should report:

```
✅ Context Loaded

Current Objective: [from project_state.md]
Active Mode: [ANALYZE/IMPLEMENT/FIX_ONLY/REVIEW_ONLY]
Current Branch: [from git]
Key Constraints: [from PROJECT_CONTEXT.md and .windsurfrules]
Next Actions: [from project_state.md]
```

## Integration with Workflows

This skill should be invoked before:
- `newfeaturebuild.md` workflow
- `Fix_Errors.md` workflow
- Any implementation task
- Architecture review

## Maintenance

Update this skill if:
- New context files are added
- Context file paths change
- Initialization protocol needs modification

## Related Skills

- **session-update-state**: Update project_state.md after completing work
  - Use at end of session to save progress
  - Ensures continuous state tracking
  - Auto-triggers on context overflow

## Recommended Workflow

```
Start Session → skill session-initialization
                ↓
            Do Work
                ↓
[Check: Context Overflow?]
  Yes → skill session-update-state → Clear context → Continue
   No → Continue work
                ↓
End Session → skill session-update-state
```

## Context Overflow Detection

During session, monitor for:
- 5+ files modified
- 30+ minutes elapsed
- Context length > 50% limit
- Context overflow imminent (>80%)

**When detected:**
1. Auto-invoke session-update-state
2. Save progress to project_state.md
3. Clear non-essential context
4. Continue with fresh context

**User can also trigger:**
- Command: `/update-state` or `update-state`
- Request: "update state" or "save progress"

---

*Version: 1.0*
*Last Updated: June 16, 2026*
