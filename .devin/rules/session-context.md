---
description: "Mandatory session context loading for ShopERP/VanAn project"
trigger: always_on
---

# LIGHTWEIGHT CONTEXT LOADING (LAZY-LOADING)

At the START of every session, you MUST ONLY read:
1. `docs/AI/project_state.md` (To synchronize the current objective & next actions)
2. `.devin/rules/governance.md` (To align with core constraints)

CRITICAL RULES:
- DO NOT read `docs/knowledge-base/00-core/PROJECT_CONTEXT.md` or any architectural knowledge-base files by default for micro-tasks, system configuration changes, environment updates, or bug-fixing batches.
- `PROJECT_CONTEXT.md` is strictly LAZY-LOADED. Only read it when explicitly triggered by the user or during Phase 1-3 of a major Feature Build (`newfeaturebuild.md`).
```
Context Loaded
- Current Objective: <from project_state.md section 2>
- Active Mode: ANALYZE | IMPLEMENT | FIX_ONLY | REVIEW_ONLY
- Current Branch: <git branch>
- Next Actions: <from project_state.md section 4>
```

## EXCEPTIONS (skip loading)
- Trivial read-only questions (single file read, grep, "what does X do")
- User explicitly says "skip context"
- Pure documentation/CLI lookups unrelated to the codebase

## UPDATING STATE
When the user says "update state" / "save progress", or when a milestone/objective is completed, update `docs/AI/project_state.md`:
- Section 2 (Current Objective): update status; move finished objective into history
- Section 3 (Current Status): add completed items
- Section 4 (Next Actions): remove done, add new
- Section 11 (Maintenance Log): update Last Updated + current branch

Follow the Maintenance Rules in Section 0 of project_state.md (no duplicate sections, no contradictions, verify paths/branches against the real repo).

## FULL GOVERNANCE
The complete VanAn governance (Domain rules, workflow modes, UI Platform, hard stops) is in `.devin/rules/governance.md` and is always-on.