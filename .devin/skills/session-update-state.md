# Session Update State Skill

> **Purpose:** Update project_state.md after completing work in a session
> **Trigger:** Manual invocation at end of important sessions
> **Status:** Active

## When to Use

Invoke this skill when:
- Completing a feature implementation
- Finishing a bug fix
- Completing a phase of work
- Before committing significant changes
- When current objective is completed

**Skip for:**
- Minor file edits
- Documentation updates
- Trivial changes

## Update Protocol

When this skill is invoked:

### 1. Read Current State
- Read `docs/AI/project_state.md`
- Identify current objective (Section 2)
- Check current status (Section 3)
- Review next actions (Section 4)

### 2. Analyze Changes
- Run `git status` to see modified files
- Run `git diff --stat` to understand scope
- Run `git log -1` to see latest commit message
- Identify what was accomplished in this session

### 3. Update Sections

#### Section 2: Current Objective
- If objective is completed: Move to Section 3 (Completed)
- Update status to: ✅ COMPLETED (date)
- If objective is still in progress: Update status with progress
- If new objective started: Update with new objective

#### Section 3: Current Status

**Completed:**
- Add newly completed items
- Format: `- [Description] ✅ (date)`
- Include key achievements from this session

**Blocked:**
- Add any new blockers discovered
- Format: `- [Description] ❌ (reason)`

#### Section 4: Next Actions
- Remove completed actions
- Add new next actions based on current state
- Keep actions actionable and specific
- Format: `- [Action description]`

#### Section 5: Architecture Decisions
- Add any new architectural decisions made
- Format: `* Decision: [decision]`
- Format: `* Reason: [reason]`
- Format: `* Consequences: [consequences]`

#### Section 11: Last Updated
- Update timestamp: `*Last Updated: [YYYY-MM-DD HH:MM]*
- Update current branch: `*Current Branch: [branch-name]*`

### 4. Maintain Format Rules

Follow maintenance rules from project_state.md Section 0:
1. **One-and-only-one:** Each section exists once, no duplicates
2. **No contradiction:** Each item has one status only
3. **Ground Truth first:** Verify paths/branches exist before writing
4. **Now over History:** Sections 2-4 describe current/future only
5. **Actionable Next Actions:** Remove outdated actions
6. **Concise & DRY:** Each fact written once, no long log copies
7. **Stamp every edit:** Update Section 11
8. **Honest Health Check:** Report assumptions vs verified facts accurately

### 5. Generate Summary Report

After updating, output:

```
✅ project_state.md Updated

Current Objective: [updated status]
Completed Items Added: [count]
Next Actions Updated: [count]
Last Updated: [timestamp]
Current Branch: [branch]

Files Modified in Session:
- [file1]
- [file2]
```

## Example Session Update

**Before:**
```markdown
## 2. Current Objective
Fix Integration Tests: Value Object Mapping
Status: 🔄 IN PROGRESS

## 3. Current Status
### Completed
- Sprint 1 ✅
- Sprint 2 ✅

## 4. Next Actions
- Create EF Core configuration files
- Run integration tests
```

**After session (completed EF Core configs):**
```markdown
## 2. Current Objective
Fix Integration Tests: Value Object Mapping
Status: ✅ COMPLETED (2026-06-16)

## 3. Current Status
### Completed
- Sprint 1 ✅
- Sprint 2 ✅
- Fix Integration Tests: Value Object Mapping ✅ (2026-06-16)
  * Created 14 EF Core configuration files
  * Fixed ProductId, IngredientId, RecipeId converters
  * All integration tests passing

## 4. Next Actions
- Run full test suite verification
- Phase 2: KhachLink Integration & Hardening
```

## Integration with Session Initialization

This skill works with `session-initialization.md`:
1. Start session: `skill session-initialization` → Load context
2. Do work
3. End session: `skill session-update-state` → Update context

## Error Handling

If errors occur:
- Verify file path: `docs/AI/project_state.md`
- Check file is not locked by another process
- Verify git is available for diff analysis
- Rollback changes if update fails

## Context Overflow Handling

When invoked due to context overflow:

1. **Quick Update Mode**
   - Skip detailed git diff analysis
   - Focus on current objective and next actions
   - Update only essential sections (2, 3, 4, 11)
   - Use minimal context to avoid further overflow

2. **Context Cleanup**
   - After update, clear non-essential context
   - Keep only: current task, immediate next actions
   - Archive completed work to project_state.md

3. **Overflow Recovery**
   - Report: "Context overflow detected, state saved, context cleared"
   - Provide summary of what was saved
   - Ready to continue with fresh context

## Trigger Detection

This skill can be triggered by:

**Manual:**
- User command: `/update-state` or `update-state`
- User request: "update state" or "save progress"

**Auto (AI decision):**
- 5+ files modified in session
- 30+ minutes elapsed in session
- Context length > 50% of limit
- Completed workflow phase
- Context overflow imminent (>80%)

**Context Overflow:**
- When context is near limit
- Before context becomes unmanageable
- To enable continuous work

## Maintenance

Update this skill if:
- project_state.md structure changes
- New sections are added
- Update protocol needs modification

---

*Version: 1.0*
*Last Updated: June 16, 2026*
